param([string]$BuildDirectory='Builds/Windows', [switch]$CheckLookSample)
$ErrorActionPreference='Stop'
$root=(Resolve-Path $BuildDirectory).Path
$exe=Join-Path $root 'BeMyArms.exe'
$qa=Join-Path $root 'QA/DuelSmoke'
New-Item -ItemType Directory -Force $qa | Out-Null
$processes=[System.Collections.Generic.List[System.Diagnostics.Process]]::new()
$checks=[System.Collections.Generic.List[string]]::new()
Add-Type 'using System; using System.Runtime.InteropServices; public static class DuelSmokeWindow { [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr handle); }'
function Check([bool]$ok,[string]$message){if(!$ok){throw $message};$checks.Add($message)}
function Start-Player([string]$name,[string]$arguments){
    $log=Join-Path $qa "$name.log"
    $process=Start-Process $exe -ArgumentList "-logFile `"$log`" $arguments" -PassThru
    $processes.Add($process)
    $runtime=Join-Path $qa "$name-runtime"
    New-Item -ItemType Directory -Force $runtime | Out-Null
    $deadline=[DateTime]::UtcNow.AddSeconds(45)
    do {
        if($process.HasExited){throw "$name exited. See $log"}
        try {
            $text=Get-Content (Join-Path $root '.unity-pipeline-runtime-port') -Raw
            $entry=$text | ConvertFrom-Json
            if($entry.pid -eq $process.Id){[IO.File]::WriteAllText((Join-Path $runtime '.unity-pipeline-runtime-port'),$text);return @{Process=$process;Runtime=$runtime}}
        }catch{}
        Start-Sleep -Milliseconds 200
    }while([DateTime]::UtcNow -lt $deadline)
    throw "No runtime endpoint for $name"
}
function Qa($player,[string]$command,[string[]]$parameters=@()){
    $text=& unity command $command --runtime-path $player.Runtime --timeout 30 --format json @parameters
    if($LASTEXITCODE -ne 0){throw "$command failed: $text"}
    $r=$text | ConvertFrom-Json
    if(!$r.success){throw "$command failed: $text"}
    $result=$r.data.result
    if($result -is [string]){$result=$result | ConvertFrom-Json}
    if($result.PSObject.Properties.Name -contains 'Success' -and !$result.Success){throw "$command failed: $text"}
    return $result
}
function Await($player,[scriptblock]$condition,[string]$message){
    $deadline=[DateTime]::UtcNow.AddSeconds(25)
    do {$state=Qa $player 'qa_player_state';if(&$condition $state){return $state};Start-Sleep -Milliseconds 100}while([DateTime]::UtcNow -lt $deadline)
    throw "$message : $($state | ConvertTo-Json -Depth 5)"
}
function Read-Crosshair($player){
    $file=Join-Path $qa 'crosshair.cs'
    [IO.File]::WriteAllText($file,@'
var type=System.Type.GetType("BeMyArms.Client.DynamicCrosshair, BeMyArms.Client");
var graphic=(UnityEngine.Component)UnityEngine.Object.FindObjectsByType(type,UnityEngine.FindObjectsInactive.Include).Single();
var camera=UnityEngine.Object.FindObjectsByType(System.Type.GetType("BeMyArms.Client.LocalPlayer, BeMyArms.Client")).Single();
var world=(UnityEngine.Camera)camera.GetType().GetProperty("LocalCamera").GetValue(camera);
var canvas=graphic.GetComponentInParent<UnityEngine.Canvas>();
float spread=(float)type.GetProperty("SpreadDegrees").GetValue(graphic);
float radius=(float)type.GetProperty("RadiusCanvasUnits").GetValue(graphic);
float expected=UnityEngine.Mathf.Tan(spread*UnityEngine.Mathf.Deg2Rad)*world.pixelHeight*.5f/(UnityEngine.Mathf.Tan(world.fieldOfView*.5f*UnityEngine.Mathf.Deg2Rad)*canvas.scaleFactor);
return new {Active=graphic.gameObject.activeInHierarchy,Spread=spread,Radius=radius,ProjectedRadius=expected,Raycast=type.GetProperty("raycastTarget").GetValue(graphic)};
'@)
    return (Qa $player 'eval_file' @($file)).result
}
try {
    # Headless menu callbacks exercise the real entry/session path, not a custom test scene.
    $p1=Start-Player 'menu-p1' '-batchmode -nographics'
    $ui=Qa $p1 'qa_ui_state'
    Check ($ui.Scene -eq 'MainMenu' -and @($ui.Buttons | Where-Object Name -eq 'DUO PRACTICE').Count -eq 1) 'Normal player starts at the usable main-menu entry'
    Qa $p1 'qa_click_button' @('--name','DUO PRACTICE') | Out-Null
    Qa $p1 'qa_click_button' @('--name','Host') | Out-Null
    $file=Join-Path $qa 'allocated-server.cs'
    [IO.File]::WriteAllText($file,'var t=System.Type.GetType("BeMyArms.Client.PrivateMatch, BeMyArms.Client");var current=t.GetField("Current").GetValue(null);var allocator=t.GetField("Allocator").GetValue(null);var process=allocator.GetType().GetField("_process",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(allocator);return new { Port=current.GetType().GetField("Port").GetValue(current),Pid=process.GetType().GetProperty("Id").GetValue(process) };')
    $allocated=(Qa $p1 'eval_file' @($file)).result
    $processes.Add([System.Diagnostics.Process]::GetProcessById([int]$allocated.Pid))
    $serverRuntime=Join-Path $qa 'allocated-server-runtime'
    New-Item -ItemType Directory -Force $serverRuntime | Out-Null
    # Snapshot server startup BEFORE polling the client: every client command republishes
    # the shared discovery file, hiding a server that has already finished startup.
    $deadline=[DateTime]::UtcNow.AddSeconds(25)
    $descriptor=$null
    do {
        try {
            $candidate=Get-Content (Join-Path $root '.unity-pipeline-runtime-port') -Raw
            if(($candidate | ConvertFrom-Json).pid -eq $allocated.Pid){$descriptor=$candidate;break}
        }catch{}
        Start-Sleep -Milliseconds 100
    }while([DateTime]::UtcNow -lt $deadline)
    Check ($null -ne $descriptor) 'Disposable allocated server endpoint belongs to this smoke session'
    [IO.File]::WriteAllText((Join-Path $serverRuntime '.unity-pipeline-runtime-port'),$descriptor)
    $server=@{Runtime=$serverRuntime}
    $a=Await $p1 {param($s)$s.OwnBodyResolved -and $s.MatchPhase -eq 'Warmup'} 'P1 host assignment'
    Check ($a.LocalRole -eq 0 -and $allocated.Port -gt 0) 'Menu allocates a separate dedicated Duel server and binds P1'
    # A rendered P2 checks the actual Resources viewmodel load without judging its pixels.
    $p2=Start-Player 'duel-p2' "-screen-fullscreen 0 -screen-width 1280 -screen-height 720 -client-join 127.0.0.1 -client-port $($allocated.Port) -client-join-role p2"
    $b=Await $p2 {param($s)$s.MatchLive -and $s.OwnBodyResolved -and $s.ViewmodelCount -eq 1} 'P2 live assignment/viewmodel import'
    $a=Qa $p1 'qa_player_state'
    Check ($a.Scene -eq 'Boatyard' -and $b.Scene -eq 'Boatyard') 'Menu and joining partner load the same new Boatyard Duel map'
    Check ($b.LocalRole -eq 1 -and $a.LocalTeam -eq $b.LocalTeam -and $a.LocalBodyIndex -eq $b.LocalBodyIndex -and $b.BodyCount -eq 2 -and !$b.P1Bot -and !$b.P2Bot) 'Both authorized human roles own the same shared body, without duplicate bots'
    Check ($b.DuplicateSummary -eq 'none' -and $b.ViewmodelCount -eq 1) 'Normalized P2 presentation resources resolve to one viewmodel'
    if($CheckLookSample){
        $look=(Qa $p2 'eval_file' @((Join-Path $PSScriptRoot 'check-boatyard-look.cs'))).result
        Check ($look.Success) 'Rendered P2 loads the map grading, baked lighting/reflections and valid ramp visuals without grading the viewmodel'
    }
    Qa $p1 'qa_headless_controls' | Out-Null
    $before=$b.AuthoritativePosition
    Qa $p1 'qa_inject_input' @('--movex','1') | Out-Null
    Start-Sleep -Milliseconds 600
    Qa $p1 'qa_inject_input' | Out-Null
    $b=Qa $p2 'qa_player_state'
    $distance=[Math]::Sqrt([Math]::Pow($b.AuthoritativePosition[0]-$before[0],2)+[Math]::Pow($b.AuthoritativePosition[2]-$before[2],2))
    Check ($distance -gt .1) 'P1 input moves the server-owned body observed by P2'
    $p2.Process.Refresh();[DuelSmokeWindow]::SetForegroundWindow($p2.Process.MainWindowHandle) | Out-Null
    $b=Await $p2 {param($s)$s.InputGameplayActive -and $s.ApplicationFocused} 'P2 focus/input ownership'
    $cross=Read-Crosshair $p2
    Check ($cross.Active -and !$cross.Raycast -and [Math]::Abs($cross.Radius-$cross.ProjectedRadius) -lt .01) 'Live P2 crosshair is input-transparent and projects spread with the actual world-camera FOV/canvas scale'
    $ammo=$b.Ammo
    Qa $p2 'qa_inject_input' @('--fire','true') | Out-Null
    $b=Await $p2 {param($s)$s.Ammo -lt $ammo -and $s.ShotsFired -gt 0} 'P2 authoritative shot'
    $cross=Read-Crosshair $p2
    Check ($cross.Active -and $cross.Spread -gt 0 -and $cross.Radius -gt 0) 'Real local rifle firing opens the rendered P2 crosshair for next-round bloom'
    Qa $p2 'qa_inject_input' | Out-Null
    Check ($b.Ammo -lt $ammo) 'P2 fire reaches authoritative ammo/shot state'
    $rounds=(Qa $server 'eval_file' @((Join-Path $PSScriptRoot 'check-boatyard-rounds.cs'))).result
    Check ($rounds.Success -and $rounds.OwnersAndScoresPreserved) 'Actual server round starts alternate geographic sides while preserving team scores and P1/P2 owners'
    $logs=Get-ChildItem $qa -Filter '*.log'
    $errors=@($logs | Select-String -Pattern 'NullReferenceException|MissingReferenceException|TypeLoadException|Exception:|Not allowed|\[Error\]|error CS\d')
    Check ($errors.Count -eq 0) 'Smoke logs contain no runtime reference/type errors'
    @{Success=$true;Checks=$checks.ToArray();P1=$a;P2=$b} | ConvertTo-Json -Depth 7 | Set-Content (Join-Path $qa 'smoke-results.json') -Encoding utf8
    Write-Output "$($checks.Count)/$($checks.Count) startup/core-Duel smoke checks passed (not visual or feel acceptance)"
}finally {
    foreach($process in $processes){if(!$process.HasExited){$process.Kill();$process.WaitForExit(5000) | Out-Null}}
    # Remove only endpoint copies created by this run, not arbitrary runtime/user files.
    foreach($name in @('menu-p1','duel-p2','allocated-server')){
        $runtime=Join-Path $qa "$name-runtime"
        $descriptor=Join-Path $runtime '.unity-pipeline-runtime-port'
        if(Test-Path $descriptor){Remove-Item $descriptor -Force}
        if(Test-Path $runtime){Remove-Item $runtime}
    }
}
