param([string]$BuildDirectory='Builds/Windows')
$ErrorActionPreference='Stop'
$root=(Resolve-Path $BuildDirectory).Path
$exe=Join-Path $root 'BeMyArms.exe'
$qa=Join-Path $root 'QA/RifleCombat'
New-Item -ItemType Directory -Force $qa | Out-Null
$processes=[System.Collections.Generic.List[System.Diagnostics.Process]]::new()
$runtimes=[System.Collections.Generic.List[string]]::new()
$checks=[System.Collections.Generic.List[string]]::new()
$udp=[System.Net.Sockets.UdpClient]::new(0);$port=$udp.Client.LocalEndPoint.Port;$udp.Close()
function Check([bool]$ok,[string]$message){if(!$ok){throw $message};$checks.Add($message)}
function Start-Player([string]$name,[string]$arguments){
    $log=Join-Path $qa "$name.log"
    $process=Start-Process $exe -ArgumentList "-batchmode -nographics -logFile `"$log`" $arguments" -PassThru
    $processes.Add($process)
    $runtime=Join-Path $qa "$name-runtime";$runtimes.Add($runtime)
    New-Item -ItemType Directory -Force $runtime | Out-Null
    $deadline=[DateTime]::UtcNow.AddSeconds(40)
    do {
        if($process.HasExited){throw "$name exited. See $log"}
        try {
            $text=Get-Content (Join-Path $root '.unity-pipeline-runtime-port') -Raw
            if(($text | ConvertFrom-Json).pid -eq $process.Id){[IO.File]::WriteAllText((Join-Path $runtime '.unity-pipeline-runtime-port'),$text);return $runtime}
        }catch{}
        Start-Sleep -Milliseconds 200
    }while([DateTime]::UtcNow -lt $deadline)
    throw "No runtime endpoint for $name"
}
function Qa([string]$runtime,[string]$command,[string[]]$parameters=@()){
    $text=& unity command $command --runtime-path $runtime --timeout 30 --format json @parameters
    if($LASTEXITCODE -ne 0){throw "$command failed: $text"}
    $r=$text | ConvertFrom-Json
    if(!$r.success){throw "$command failed: $text"}
    $result=$r.data.result;if($result -is [string]){$result=$result | ConvertFrom-Json}
    if($result.PSObject.Properties.Name -contains 'Success' -and !$result.Success){throw "$command failed: $text"}
    return $result
}
function Eval([string]$runtime,[string]$code){
    $file=Join-Path $qa 'check.cs';[IO.File]::WriteAllText($file,$code)
    return (Qa $runtime 'eval_file' @($file)).result
}
function Read-Combat([string]$runtime){
    return Eval $runtime @'
var type=System.Type.GetType("BeMyArms.Match.NetworkBody, BeMyArms.Match");
var bodies=UnityEngine.Object.FindObjectsByType(type,UnityEngine.FindObjectsSortMode.None);
var own=bodies.Single(b=>(int)type.GetProperty("TeamIndex").GetValue(b)==0);
var target=bodies.Single(b=>(int)type.GetProperty("TeamIndex").GetValue(b)==1);
object Field(object obj,string name)=>obj.GetType().GetField(name).GetValue(obj);
object Value(object obj,string name)=>Field(obj,name).GetType().GetProperty("Value").GetValue(Field(obj,name));
var events=(System.Collections.Generic.List<object>)System.AppDomain.CurrentDomain.GetData("CombatEvents");
var impacts=(System.Collections.Generic.List<UnityEngine.Vector3>)System.AppDomain.CurrentDomain.GetData("CombatImpacts");
return new { Health=Field(Value(target,"State"),"Health"),Alive=Value(target,"Alive"),Shots=Value(own,"ShotsFired"),Ammo=Value(own,"Ammo"),Kills=Value(own,"Kills"),
    Accuracy=Value(own,"RifleAccuracy"),
    Events=events.Select(e=>new {Amount=Field(e,"Amount"),Killed=Field(e,"Killed"),AttackerTeam=Field(e,"AttackerTeam"),VictimTeam=Field(e,"VictimTeam"),AttackerEpoch=Field(e,"AttackerEpoch"),Region=Field(e,"Region").ToString(),Kind=Field(e,"Kind").ToString(),Point=new[]{((UnityEngine.Vector3)Field(e,"Point")).x,((UnityEngine.Vector3)Field(e,"Point")).y,((UnityEngine.Vector3)Field(e,"Point")).z}}).ToArray(),
    Impacts=impacts.Select(p=>new[]{p.x,p.y,p.z}).ToArray() };
'@
}
function Wait-Combat([string]$runtime,[scriptblock]$condition){
    $deadline=[DateTime]::UtcNow.AddSeconds(10)
    do {$s=Read-Combat $runtime;if(&$condition $s){return $s};Start-Sleep -Milliseconds 100}while([DateTime]::UtcNow -lt $deadline)
    throw "Combat state timeout: $($s | ConvertTo-Json -Depth 7)"
}
function Clear-Events([string]$runtime){Eval $runtime '((System.Collections.Generic.List<object>)System.AppDomain.CurrentDomain.GetData("CombatEvents")).Clear();((System.Collections.Generic.List<UnityEngine.Vector3>)System.AppDomain.CurrentDomain.GetData("CombatImpacts")).Clear();return true;' | Out-Null}
function Set-Case([string]$mode){
    $fixture=Get-Content (Join-Path $PSScriptRoot 'rifle-combat-fixture.cs') -Raw
    $result=Eval $server ('System.AppDomain.CurrentDomain.SetData("CombatCase","'+$mode+'");'+$fixture)
    Wait-Combat $p2 {param($s)$s.Health -eq 100 -and $s.Shots -eq 0 -and $s.Ammo -eq 30} | Out-Null
    Clear-Events $p1;Clear-Events $p2
    return $result
}
function Trigger([string]$runtime,[double]$pitch,[float]$yaw=0,[bool]$stale=$false){
    $pitchText=$pitch.ToString('R',[Globalization.CultureInfo]::InvariantCulture)
    $yawText=$yaw.ToString('R',[Globalization.CultureInfo]::InvariantCulture)
    $epoch=if($stale){'(uint)state.GetType().GetField("ControlEpoch").GetValue(state)-1u'}else{'(uint)state.GetType().GetField("ControlEpoch").GetValue(state)'}
    Eval $runtime @"
var type=System.Type.GetType("BeMyArms.Match.NetworkBodyClient, BeMyArms.Match");
var client=UnityEngine.Object.FindObjectsByType(type,UnityEngine.FindObjectsSortMode.None).Single(c=>(bool)type.GetProperty("IsLocalOwnBody").GetValue(c));
var body=type.GetProperty("Body").GetValue(client);var field=type.GetField("_p2Sequence",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
uint sequence=(uint)field.GetValue(client);
var variable=body.GetType().GetField("State").GetValue(body);var state=variable.GetType().GetProperty("Value").GetValue(variable);
var method=body.GetType().GetMethod("SubmitP2ServerRpc");var inputType=method.GetParameters()[0].ParameterType;var input=System.Activator.CreateInstance(inputType);
void Set(string name,object value)=>inputType.GetField(name).SetValue(input,value);
Set("Sequence",++sequence);Set("ControlEpoch",$epoch);Set("BodyTick",state.GetType().GetField("SimulationTick").GetValue(state));Set("AimYaw",${yawText}f);Set("AimPitch",${pitchText}f);Set("Fire",true);
var rpc=System.Activator.CreateInstance(method.GetParameters()[1].ParameterType);
method.Invoke(body,new[]{input,rpc});Set("Sequence",++sequence);Set("Fire",false);method.Invoke(body,new[]{input,rpc});field.SetValue(client,sequence);return true;
"@ | Out-Null
    # Wait for BOTH reliable RPCs to be received before advancing the normal server tick.
    # The eval round trip targets a different process, so receipt normally precedes this call.
    Start-Sleep -Milliseconds 150
    Eval $server 'var type=System.Type.GetType("BeMyArms.Match.NetworkBody, BeMyArms.Match");var body=UnityEngine.Object.FindObjectsByType(type,UnityEngine.FindObjectsSortMode.None).Single(b=>(int)type.GetProperty("TeamIndex").GetValue(b)==0);type.GetMethod("ServerTick",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(body,new object[]{1f/60f});return true;' | Out-Null
}
function Move-Body([string]$mode){
    Eval $p1 @"
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
var type=System.Type.GetType("BeMyArms.Match.NetworkBodyClient, BeMyArms.Match");
var client=UnityEngine.Object.FindObjectsByType(type).Single(c=>(bool)type.GetProperty("IsLocalOwnBody").GetValue(c));
var body=type.GetProperty("Body").GetValue(client);var sequence=type.GetField("_p1Sequence",flags);uint next=(uint)sequence.GetValue(client)+1;
var variable=body.GetType().GetField("State").GetValue(body);var state=variable.GetType().GetProperty("Value").GetValue(variable);
var method=body.GetType().GetMethod("SubmitP1ServerRpc");var inputType=method.GetParameters()[0].ParameterType;var input=System.Activator.CreateInstance(inputType);
void Set(string name,object value)=>inputType.GetField(name).SetValue(input,value);
string mode="$mode";
Set("Sequence",next);Set("ControlEpoch",state.GetType().GetField("ControlEpoch").GetValue(state));
Set("MoveX",new[]{"walk","sprint","crouch-walk"}.Contains(mode) ? 1f : 0f);
Set("Sprint",mode=="sprint");Set("Crouch",mode=="crouch" || mode=="crouch-walk");
Set("Jump",mode=="jump");Set("Dodge",mode=="dodge");Set("Slide",mode=="slide");Set("HeavyKick",mode=="heavy-kick");
method.Invoke(body,new[]{input,System.Activator.CreateInstance(method.GetParameters()[1].ParameterType)});sequence.SetValue(client,next);return true;
"@ | Out-Null
}
function Read-Shot {
    return Eval $server @'
var type=System.Type.GetType("BeMyArms.Match.NetworkBody, BeMyArms.Match");
var body=UnityEngine.Object.FindObjectsByType(type).Single(b=>(int)type.GetProperty("TeamIndex").GetValue(b)==0);
var variable=type.GetField("State").GetValue(body);var state=variable.GetType().GetProperty("Value").GetValue(variable);
object F(string name)=>state.GetType().GetField(name).GetValue(state);
var aim=UnityEngine.Quaternion.Euler((float)F("AimPitch"),(float)F("AimYaw"),0)*UnityEngine.Vector3.forward;
var ray=(UnityEngine.Vector3)type.GetProperty("LastShotDirection").GetValue(body);
return new {Spread=type.GetProperty("LastShotSpread").GetValue(body),DirectionError=(ray-aim).magnitude,Speed=F("PlanarSpeed"),Stance=F("MovementState")};
'@
}
function Advance-Ticks([int]$count){
    Eval $server "var type=System.Type.GetType(`"BeMyArms.Match.NetworkBody, BeMyArms.Match`");var body=UnityEngine.Object.FindObjectsByType(type).Single(b=>(int)type.GetProperty(`"TeamIndex`").GetValue(b)==0);var tick=type.GetMethod(`"ServerTick`",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);for(int i=0;i<$count;i++)tick.Invoke(body,new object[]{1f/60f});return true;" | Out-Null
}
try {
    $server=Start-Player 'server' "-match-role server -client-arena DuelArena -queue-mode duel -queue-matchmaker 0 -match-port $port -match-required-players 2 -match-start-delay 0 -match-practice 1 -match-strict-slots 1 -match-delay 0 -match-loss 0 -match-buy 1"
    $p1=Start-Player 'p1' "-client-arena DuelArena -client-join 127.0.0.1 -client-port $port -client-join-role p1"
    $p2=Start-Player 'p2' "-client-arena DuelArena -client-join 127.0.0.1 -client-port $port -client-join-role p2"
    $deadline=[DateTime]::UtcNow.AddSeconds(25)
    do {$state=Qa $p2 'qa_player_state';if($state.MatchLive -and $state.OwnBodyResolved){break};Start-Sleep -Milliseconds 200}while([DateTime]::UtcNow -lt $deadline)
    Check ($state.MatchLive -and $state.LocalRole -eq 1) 'Real dedicated Duel assigns P1/P2 before the combat fixture'
    foreach($runtime in @($p1,$p2)){
        Eval $runtime @'
var clientType=System.Type.GetType("BeMyArms.Match.NetworkBodyClient, BeMyArms.Match");
foreach(var client in UnityEngine.Object.FindObjectsByType(clientType,UnityEngine.FindObjectsSortMode.None))((UnityEngine.Behaviour)client).enabled=false;
var events=new System.Collections.Generic.List<object>();var impacts=new System.Collections.Generic.List<UnityEngine.Vector3>();
var bus=System.Type.GetType("BeMyArms.Match.CombatEvents, BeMyArms.Match");
var damage=bus.GetEvent("Damage");var parameter=System.Linq.Expressions.Expression.Parameter(damage.EventHandlerType.GetGenericArguments()[0]);
var add=System.Linq.Expressions.Expression.Call(System.Linq.Expressions.Expression.Constant(events),events.GetType().GetMethod("Add"),System.Linq.Expressions.Expression.Convert(parameter,typeof(object)));
damage.AddEventHandler(null,System.Linq.Expressions.Expression.Lambda(damage.EventHandlerType,add,parameter).Compile());
bus.GetEvent("WorldImpact").AddEventHandler(null,(System.Action<UnityEngine.Vector3>)(p=>impacts.Add(p)));
System.AppDomain.CurrentDomain.SetData("CombatEvents",events);System.AppDomain.CurrentDomain.SetData("CombatImpacts",impacts);return true;
'@ | Out-Null
    }
    foreach($mode in @('body','head','wall','behind-surface','head-cover','crouch-cover','crouch','elevated','historical','protected')){
        $setup=Set-Case $mode
        Trigger $p2 $setup.Pitch
        $s=Wait-Combat $p2 {param($s)$s.Shots -eq 1}
        $damage=if($mode -in @('head','head-cover','crouch','historical')){45}elseif($mode -in @('body','behind-surface')){18}else{0}
        $s=Wait-Combat $p2 {param($s)$s.Health -eq 100-$damage -and @($s.Events).Count -eq [int]($damage -gt 0)}
        Check ($s.Health -eq 100-$damage -and $s.Ammo -eq 29) "$mode : one accepted rifle shot applies only the resolved damage ($damage)"
        if($damage -gt 0){
            $partner=Wait-Combat $p1 {param($s)@($s.Events).Count -eq 1}
            $region=if($damage -eq 45){'Head'}else{'Body'}
            Check ($s.Events[0].Region -eq $region -and $s.Events[0].Amount -eq $damage -and $partner.Events[0].Region -eq $region -and $s.Events[0].AttackerEpoch -eq $setup.Epoch) "$mode : both roles receive the same server-confirmed region, damage and current epoch"
            if($mode -eq 'body'){Check ([Math]::Abs($s.Events[0].Point[2]-9.65) -lt .01) 'Flesh impact is the hit surface, not a generic chest center'}
        }else{Check (@($s.Events).Count -eq 0) "$mode : no false hit confirmation"}
        if($mode -eq 'wall'){Check (@($s.Impacts).Count -eq 1 -and [Math]::Abs($s.Impacts[0][2]-5) -lt .01) 'Cover receives the authoritative impact instead of the hidden target'}
    }
    $setup=Set-Case 'body'
    Trigger $p2 $setup.Pitch 90
    $s=Read-Combat $p2
    Check ($s.Shots -eq 0 -and $s.Ammo -eq 30 -and @($s.Events).Count -eq 0) 'Out-of-sector aim cannot spend ammo or confirm a hit'
    Trigger $p2 $setup.Pitch 0 $true
    $s=Read-Combat $p2
    Check ($s.Shots -eq 0 -and $s.Health -eq 100) 'Stale control-epoch fire remains rejected'
    Trigger $p1 $setup.Pitch
    $s=Read-Combat $p2
    Check ($s.Shots -eq 0 -and $s.Health -eq 100) 'P1 cannot submit weapon input on P2 behalf'
    foreach($case in @(
        @{Mode='idle';Spread=0},@{Mode='crouch';Spread=0},@{Mode='crouch-walk';Spread=.35},
        @{Mode='walk';Spread=.75},@{Mode='sprint';Spread=2.5},@{Mode='jump';Spread=3.5},
        @{Mode='dodge';Spread=3.5},@{Mode='slide';Spread=.75},@{Mode='heavy-kick';Spread=4}
    )){
        $setup=Set-Case 'body'
        Move-Body $case.Mode
        Trigger $p2 $setup.Pitch
        Wait-Combat $p2 {param($s)$s.Shots -eq 1} | Out-Null
        $shot=Read-Shot
        Check ([Math]::Abs($shot.Spread-$case.Spread) -lt .001) "$($case.Mode) : real P1 input determines P2 authoritative first-shot spread ($($case.Spread) degrees)"
        $remote=Read-Combat $p2
        Check ($remote.Accuracy.Burst -eq 1 -and $remote.Accuracy.Shots -eq 1 -and $remote.Accuracy.ControlEpoch -eq $setup.Epoch) "$($case.Mode) : P2 receives accepted rifle burst/tick/epoch for crosshair recovery"
        if($case.Spread -eq 0){Check ($shot.DirectionError -lt .000001) "$($case.Mode) : stationary first bullet is exactly the submitted aim ray"}
    }
    $setup=Set-Case 'body';Move-Body 'sprint';Trigger $p2 $setup.Pitch
    Advance-Ticks 9
    Move-Body 'idle';Trigger $p2 $setup.Pitch
    $shot=Read-Shot
    Check ($shot.Speed -lt .001 -and [Math]::Abs($shot.Spread-.16) -lt .001) 'Stopping removes movement error but does not erase the second-round burst bloom'
    Advance-Ticks 21
    Trigger $p2 $setup.Pitch
    $shot=Read-Shot
    Check ($shot.Spread -eq 0 -and $shot.DirectionError -lt .000001) 'A stopped and recovered rifle returns to an exact first-shot ray'
    $setup=Set-Case 'body';Move-Body 'slide';Advance-Ticks 1
    Advance-Ticks 1;Trigger $p2 $setup.Pitch
    $fast=Read-Shot
    Advance-Ticks 19;Trigger $p2 $setup.Pitch
    $slow=Read-Shot
    Check ($fast.Speed -gt $slow.Speed -and $fast.Spread -gt $slow.Spread) 'Slide first-round body penalty decreases with real collision-resolved speed'
    $remote=Read-Combat $p2
    Check ($remote.Accuracy.Burst -eq 1 -and $remote.Accuracy.Shots -eq 2) 'Server shot-pause recovery is replicated instead of treating late receipt as a new spray'
    $setup=Set-Case 'head'
    for($i=0;$i -lt 3;$i++){
        # Advance the actual weapon clock between rounds without leaving the deterministic fixture.
        Eval $server 'var type=System.Type.GetType("BeMyArms.Match.NetworkBody, BeMyArms.Match");var body=UnityEngine.Object.FindObjectsByType(type,UnityEngine.FindObjectsSortMode.None).Single(b=>(int)type.GetProperty("TeamIndex").GetValue(b)==0);var tick=type.GetMethod("ServerTick",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);for(int i=0;i<9;i++)tick.Invoke(body,new object[]{1f/60f});return true;' | Out-Null
        Trigger $p2 $setup.Pitch
        $s=Wait-Combat $p2 {param($s)@($s.Events).Count -eq $i+1}
    }
    Check ($s.Health -eq 0 -and !$s.Alive -and $s.Kills -eq 1 -and $s.Events[2].Killed -and $s.Events[2].Amount -eq 10) 'Three confirmed rifle head hits eliminate the shared body once, with actual remaining damage reported'
    $errors=@(Get-ChildItem $qa -Filter '*.log' | Select-String -Pattern 'NullReferenceException|MissingReferenceException|TypeLoadException|Exception:|error CS\d')
    Check ($errors.Count -eq 0) 'Network combat/feedback logs contain no runtime exceptions'
    @{Success=$true;Checks=$checks.ToArray();Final=$s} | ConvertTo-Json -Depth 7 | Set-Content (Join-Path $qa 'results.json') -Encoding utf8
    Write-Output "$($checks.Count)/$($checks.Count) focused rifle-combat checks passed (not visual or balance acceptance)"
}finally {
    foreach($process in $processes){if(!$process.HasExited){$process.Kill();$process.WaitForExit(5000) | Out-Null}}
    foreach($runtime in $runtimes){$descriptor=Join-Path $runtime '.unity-pipeline-runtime-port';if(Test-Path $descriptor){Remove-Item $descriptor -Force};if(Test-Path $runtime){Remove-Item $runtime}}
}
