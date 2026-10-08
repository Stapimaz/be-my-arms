param([string]$BuildDirectory='Builds/Pass2')
$ErrorActionPreference='Stop'
$root=(Resolve-Path $BuildDirectory).Path
$exe=Join-Path $root 'BeMyArms.exe'
$qa=Join-Path $root 'QA'
New-Item -ItemType Directory -Force $qa | Out-Null
$processes=[System.Collections.Generic.List[System.Diagnostics.Process]]::new()
$checks=[System.Collections.Generic.List[string]]::new()
$udp=[System.Net.Sockets.UdpClient]::new(0); $port=$udp.Client.LocalEndPoint.Port; $udp.Close()
Add-Type @'
using System; using System.Runtime.InteropServices;
public static class Pass2Window { [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr handle); }
'@
function Check([bool]$ok,[string]$message) { if(!$ok){throw $message}; $checks.Add($message) }
function Start-Player([string]$name,[string]$arguments) {
    $log=Join-Path $qa "$name.log"
    $p=Start-Process $exe -ArgumentList "-logFile `"$log`" $arguments" -PassThru
    $processes.Add($p)
    $runtime=Join-Path $qa "$name-runtime"
    New-Item -ItemType Directory -Force $runtime | Out-Null
    $deadline=[DateTime]::UtcNow.AddSeconds(45)
    do {
        if($p.HasExited){throw "$name exited. See $log"}
        try {
            $text=Get-Content (Join-Path $root '.unity-pipeline-runtime-port') -Raw
            $entry=$text | ConvertFrom-Json
            if($entry.pid -eq $p.Id){ [IO.File]::WriteAllText((Join-Path $runtime '.unity-pipeline-runtime-port'),$text); return @{Process=$p; Runtime=$runtime} }
        } catch {}
        Start-Sleep -Milliseconds 200
    } while([DateTime]::UtcNow -lt $deadline)
    throw "No endpoint for $name"
}
function Qa($player,[string]$command,[string[]]$parameters=@()) {
    $text=& unity command $command --runtime-path $player.Runtime --timeout 30 --format json @parameters
    $r=$text | ConvertFrom-Json
    if(!$r.success){throw "$command failed: $text"}
    $result=$r.data.result
    if($result -is [string]){$result=$result | ConvertFrom-Json}
    if($result.PSObject.Properties.Name -contains 'Success' -and !$result.Success){throw "$command failed: $text"}
    return $result
}
function Await($player,[string]$command,[scriptblock]$condition,[string]$message) {
    $deadline=[DateTime]::UtcNow.AddSeconds(20)
    do {
        $state=Qa $player $command
        if(&$condition $state){return $state}
        Start-Sleep -Milliseconds 100
    } while([DateTime]::UtcNow -lt $deadline)
    $detail=Qa $player 'qa_player_state'
    $detail | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $qa 'render-failure-state.json') -Encoding utf8
    throw "$message : $($state | ConvertTo-Json -Depth 5) player=$($detail | ConvertTo-Json -Depth 3)"
}
function Queue-Key($player,[string]$key) {
    $file=Join-Path $qa 'queued-key.cs'
    $code='var input=System.Type.GetType("UnityEngine.InputSystem.InputSystem, Unity.InputSystem"); var keyboard=System.Type.GetType("UnityEngine.InputSystem.Keyboard, Unity.InputSystem"); var key=System.Type.GetType("UnityEngine.InputSystem.Key, Unity.InputSystem"); var state=System.Type.GetType("UnityEngine.InputSystem.LowLevel.KeyboardState, Unity.InputSystem"); var keys=System.Array.CreateInstance(key,1); keys.SetValue(System.Enum.Parse(key,"KEY_NAME"),0); var value=System.Activator.CreateInstance(state,new object[]{keys}); var kb=keyboard.GetProperty("current").GetValue(null); var queue=input.GetMethods().First(m=>m.Name=="QueueStateEvent" && m.IsGenericMethod); queue.MakeGenericMethod(state).Invoke(null,new object[]{kb,value,-1d}); return true;'
    [IO.File]::WriteAllText($file,$code.Replace('KEY_NAME',$key))
    Qa $player 'eval_file' @($file) | Out-Null
}
try {
    $server=Start-Player 'render-server' "-batchmode -nographics -m3-role server -m7-arena M7DuelArena -m4-mode duel -m4-matchmaker 0 -m3-port $port -m3-required-players 1 -m3-start-delay 0 -m3-practice 1 -m3-strict-slots 1 -m3-delay 0 -m3-loss 0 -m3-buy 3"
    $client=Start-Player 'render-client' "-screen-fullscreen 0 -screen-width 1280 -screen-height 720 -m7-join 127.0.0.1 -m7-port $port -m7-join-role p2 -m3-token pass2-render"
    $state=Await $client 'qa_player_state' {param($s) $s.MatchLive -and $s.OwnBodyResolved -and $s.ViewmodelCount -eq 1} 'rendered P2 assignment'
    $client.Process.Refresh(); [Pass2Window]::SetForegroundWindow($client.Process.MainWindowHandle) | Out-Null
    # Protect the fixture from deaths while retaining actual solo bot movement, aim and firing.
    $file=Join-Path $qa 'quiet-render-bots.cs'
    [IO.File]::WriteAllText($file,'var t=System.Type.GetType("BeMyArms.M3.M3DuelBody, BeMyArms.M3"); foreach(var body in UnityEngine.Object.FindObjectsByType(t,UnityEngine.FindObjectsSortMode.None)) { t.GetField("SpawnGraceSeconds").SetValue(body,120f); t.GetField("_spawnGraceRemaining",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(body,120f); } return true;')
    Qa $server 'eval_file' @($file) | Out-Null
    $state=Await $client 'qa_player_state' {param($s) $s.InputGameplayActive -and $s.ApplicationFocused} 'focused gameplay'
    Check ($state.DuplicateSummary -eq 'none') "Rendered P2 has one local camera/viewmodel/body owner ($($state.DuplicateSummary))"
    $p=Qa $client 'qa_presentation_state'
    Check ($p.Listeners -eq 1 -and $p.ViewmodelLayer -eq 9) 'One active audio listener and dedicated FPS render layer'
    Check ($p.ViewLeftGripError -lt .035 -and $p.ViewRightGripError -lt .035) 'Live FPS hands reach both authored grips'
    Check ($p.VisibleViewArms -eq 1 -and !$p.ViewBreathingAnimator) 'Rifle ready POV shows one steady weapon-mounted support arm'
    Qa $client 'qa_solo_probe' @('--reset','true') | Out-Null
    Qa $server 'qa_solo_probe' @('--reset','true') | Out-Null
    # Force a substantial enemy reacquisition through the real bot-P1 steering path.
    # Protecting the fixture retains the bot's ordinary decision/turn code throughout.
    $file=Join-Path $qa 'solo-retarget.cs'
    [IO.File]::WriteAllText($file,'var t=System.Type.GetType("BeMyArms.M3.M3DuelBody, BeMyArms.M3"); var bodies=UnityEngine.Object.FindObjectsByType(t,UnityEngine.FindObjectsSortMode.None); var own=bodies.First(b=>(int)t.GetProperty("TeamIndex").GetValue(b)==0); var enemy=bodies.First(b=>(int)t.GetProperty("TeamIndex").GetValue(b)==1); var simField=t.GetField("_sim",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic); var ownSim=simField.GetValue(own); var stateField=ownSim.GetType().GetField("State"); var ownState=stateField.GetValue(ownSim); var st=ownState.GetType(); var angle=((float)st.GetField("BodyYaw").GetValue(ownState)+130)*System.Math.PI/180; var sim=simField.GetValue(enemy); var state=stateField.GetValue(sim); st.GetField("PosX").SetValue(state,(float)st.GetField("PosX").GetValue(ownState)+(float)System.Math.Sin(angle)*10); st.GetField("PosZ").SetValue(state,(float)st.GetField("PosZ").GetValue(ownState)+(float)System.Math.Cos(angle)*10); stateField.SetValue(sim,state); return true;')
    Qa $server 'eval_file' @($file) | Out-Null
    Qa $client 'qa_capture_frame' @('--output',(Join-Path $qa 'runtime-p2.png')) | Out-Null
    $ammo=$state.Ammo
    $pitch=$state.AimPitch
    Qa $client 'qa_inject_input' @('--fire','true') | Out-Null
    $state=Await $client 'qa_player_state' {param($s) $s.Ammo -lt $ammo -and $s.LocalShots -gt 0} 'live shot feedback'
    $p=Await $client 'qa_presentation_state' {param($s) $s.AudioVoicesPlaying -gt 0} 'shot audio voice'
    Check ($state.ShotsFired -gt 0 -and $p.AudioVoicesPlaying -gt 0) 'Local firing presents audio alongside confirmed ammunition use'
    $state=Await $client 'qa_player_state' {param($s) $s.Ammo -le $ammo-12} 'sustained rifle spray'
    Check ($state.AimPitch -lt $pitch-4) 'Uncontrolled sustained rifle fire moves actual P2 aim upward'
    $ballistics=Qa $server 'qa_solo_probe'
    Check ($ballistics.MaxBurst -ge 8 -and $ballistics.MaxSpread -gt .8 -and $ballistics.MaxShotDeviation -gt .4) 'Authoritative sustained fire widens the actual hit/impact rays'
    $probe=Qa $client 'qa_solo_probe'
    @{ClientProbe=$probe;ServerProbe=$ballistics;StartPitch=$pitch;SprayPitch=$state.AimPitch} | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $qa 'solo-handling-metrics.json') -Encoding utf8
    Check ($probe.TurnSamples -gt 5 -and $probe.MaxBotTurnRate -gt 60 -and $probe.MaxBotTurnRate -le 90.5) 'Solo bot P1 reacquires a large direction change smoothly at the bounded rate'
    Check ($probe.MaxWristBend -lt 40) 'Live ready FPS wrists continue naturally along their forearms'
    Qa $client 'qa_capture_frame' @('--output',(Join-Path $qa 'runtime-firing.png')) | Out-Null
    Qa $client 'qa_inject_input' | Out-Null
    # Queue the key on the normal dynamic input update. Pipeline's simulate_key forces
    # an extra mid-frame update, whose edge can be cleared before the game's input polling.
    $client.Process.Refresh(); [Pass2Window]::SetForegroundWindow($client.Process.MainWindowHandle) | Out-Null
    Await $client 'qa_player_state' {param($s) $s.InputGameplayActive -and $s.ApplicationFocused} 'focused reload input' | Out-Null
    Queue-Key $client 'R'
    $p=Await $client 'qa_presentation_state' {param($s) $s.ReloadProgress -gt .02 -and $s.ReloadProgress -lt 1} 'reload choreography'
    Qa $client 'simulate_key' @('--key','R','--action','up') | Out-Null
    Check ($p.ViewLeftGripError -lt .04 -and $p.ViewRightGripError -lt .035) 'Live magazine reload keeps both hand targets reachable'
    Check ($p.VisibleViewArms -eq 2) 'Reload brings the second rifle POV hand into view'
    Qa $client 'qa_capture_frame' @('--output',(Join-Path $qa 'runtime-reload.png')) | Out-Null
    $state=Await $client 'qa_player_state' {param($s) !$s.Reloading -and $s.Ammo -eq 30} 'completed reload'
    Check ($state.Ammo -eq 30) 'Reload returns to a ready full rifle'
    $p=Qa $client 'qa_presentation_state'
    Check ($p.VisibleViewArms -eq 1) 'Completed reload returns to the single-hand rifle hold'
    Qa $client 'qa_practice_action' @('--action','2') | Out-Null
    $state=Await $client 'qa_player_state' {param($s) $s.MatchLive -and $s.LocalRole -eq 0 -and $s.ViewmodelCount -eq 0} 'role exchange'
    $client.Process.Refresh(); [Pass2Window]::SetForegroundWindow($client.Process.MainWindowHandle) | Out-Null
    $yaw=$state.BodyYaw
    Qa $client 'qa_inject_look' @('--yaw','20') | Out-Null
    $state=Await $client 'qa_player_state' {param($s) [Math]::Abs($s.LookYaw-$s.BodyYaw) -gt 15} 'independent P1 look'
    Check ([Math]::Abs($state.BodyYaw-$yaw) -lt 1) 'Small P1 look keeps independent-look behavior'
    Qa $client 'qa_inject_look' @('--yaw','100') | Out-Null
    $state=Await $client 'qa_player_state' {param($s) [Math]::Abs($s.BodyYaw-$yaw) -gt 5} 'earlier body follow'
    $offset=(($state.LookYaw-$state.BodyYaw+540)%360)-180
    Check ([Math]::Abs($offset) -le 45.01) 'Fast P1 look follows the body early and stays within the natural neck limit'
    Queue-Key $client 'E'
    $fixture=Join-Path $qa 'removed-vault.cs'
    [IO.File]::WriteAllText($fixture,'var t=System.Type.GetType("BeMyArms.M3.M3DuelClient, BeMyArms.M3"); var client=UnityEngine.Object.FindObjectsByType(t,UnityEngine.FindObjectsSortMode.None).First(c=>(bool)t.GetProperty("IsLocalOwnBody").GetValue(c)); var state=t.GetProperty("ViewState").GetValue(client); var type=state.GetType(); return new { ActionTime=(float)type.GetField("ActionTimeLeft").GetValue(state), FeetY=(float)type.GetField("PosY").GetValue(state), Movement=(byte)type.GetField("MovementState").GetValue(state) };')
    $inactive=Qa $client 'eval_file' @($fixture)
    $inactive=$inactive.result
    Check ($inactive.ActionTime -eq 0 -and $inactive.FeetY -eq 0 -and $inactive.Movement -eq 0) 'E has no standalone movement action'
    Qa $client 'simulate_key' @('--key','E','--action','up') | Out-Null
    Qa $client 'qa_solo_probe' @('--reset','true') | Out-Null
    Queue-Key $client 'V'
    $probe=Await $client 'qa_solo_probe' {param($s) $s.StrikeSamples -gt 0} 'live P1 forward kick'
    Check ($probe.MinKickReach -gt .75 -and $probe.MinKickAlignment -gt .99) 'Live kick visibly extends toward the captured P1 attack direction'
    $probe | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $qa 'solo-kick-metrics.json') -Encoding utf8
    Qa $client 'simulate_key' @('--key','V','--action','up') | Out-Null
    Qa $client 'qa_inject_input' @('--movex','1') | Out-Null
    $p=Await $client 'qa_presentation_state' {param($s) $s.MoveX -gt .25} 'rightward animation blend'
    Check ($p.MoveX -gt .25 -and $p.Listeners -eq 1) 'Live P1 strafe drives the directional blend after role exchange'
    Qa $client 'qa_capture_frame' @('--output',(Join-Path $qa 'runtime-p1.png')) | Out-Null
    Qa $client 'qa_inject_input' | Out-Null
    $checks | ConvertTo-Json | Set-Content (Join-Path $qa 'rendered-results.json') -Encoding utf8
    Write-Output "$($checks.Count)/$($checks.Count) rendered presentation checks passed"
} finally {
    foreach($p in $processes){ if(!$p.HasExited){Stop-Process -Id $p.Id -Force} }
}
