param([string]$BuildDirectory='Builds/Windows')
$ErrorActionPreference='Stop'
$root=(Resolve-Path $BuildDirectory).Path
$qa=Join-Path $root 'QA/CameraMotion'
New-Item -ItemType Directory -Force $qa | Out-Null
$processes=[System.Collections.Generic.List[System.Diagnostics.Process]]::new()
$udp=[System.Net.Sockets.UdpClient]::new(0);$port=$udp.Client.LocalEndPoint.Port;$udp.Close()
Add-Type 'using System; using System.Runtime.InteropServices; public static class CameraMotionWindow { [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr handle); }'
function Start-Isolated([string]$name,[string]$arguments){
    $log=Join-Path $qa "$name.log"
    $process=Start-Process (Join-Path $root 'BeMyArms.exe') -ArgumentList "-logFile `"$log`" $arguments" -PassThru
    $processes.Add($process)
    $runtime=Join-Path $qa "$name-runtime"
    New-Item -ItemType Directory -Force $runtime | Out-Null
    $deadline=[DateTime]::UtcNow.AddSeconds(40)
    do {
        if($process.HasExited){throw "$name exited: $log"}
        try {
            $text=Get-Content (Join-Path $root '.unity-pipeline-runtime-port') -Raw
            if(($text | ConvertFrom-Json).pid -eq $process.Id){[IO.File]::WriteAllText((Join-Path $runtime '.unity-pipeline-runtime-port'),$text);return @{Runtime=$runtime;Process=$process}}
        }catch{}
        Start-Sleep -Milliseconds 100
    }while([DateTime]::UtcNow -lt $deadline)
    throw "No endpoint for this $name process"
}
function Qa($player,[string]$command,[string[]]$parameters=@()){
    $text=& unity command $command --runtime-path $player.Runtime --timeout 30 --format json @parameters
    if($LASTEXITCODE -ne 0){throw "$command failed: $text"}
    $r=$text | ConvertFrom-Json;if(!$r.success){throw "$command failed: $text"}
    $result=$r.data.result;if($result -is [string]){$result=$result | ConvertFrom-Json}
    if($result.PSObject.Properties.Name -contains 'Success' -and !$result.Success){throw "$command failed: $text"}
    return $result
}
function Await($player,[scriptblock]$condition,[string]$message){
    $deadline=[DateTime]::UtcNow.AddSeconds(25)
    do {$s=Qa $player 'qa_player_state';if(&$condition $s){return $s};Start-Sleep -Milliseconds 100}while([DateTime]::UtcNow -lt $deadline)
    throw "$message : $($s | ConvertTo-Json -Depth 3)"
}
try {
    # Capture the server endpoint before launching/commanding the client; no menu-host watchdog
    # or race with the shared discovery file. Only these owned disposable processes are changed.
    $server=Start-Isolated 'server' "-batchmode -nographics -match-role server -client-arena Boatyard -queue-mode duel -queue-matchmaker 0 -match-port $port -match-required-players 1 -match-start-delay 1 -match-practice 1 -match-delay 0 -match-loss 0 -match-buy 1"
    $p2=Start-Isolated 'p2' "-screen-fullscreen 0 -screen-width 1280 -screen-height 720 -client-join 127.0.0.1 -client-port $port -client-join-role p2"
    $state=Await $p2 {param($s)$s.OwnBodyResolved -and $s.LocalRole -eq 1 -and $s.P1Bot -and $s.MatchLive} 'Real P2 + P1 bot assignment'
    $p2.Process.Refresh();[CameraMotionWindow]::SetForegroundWindow($p2.Process.MainWindowHandle) | Out-Null
    $state=Await $p2 {param($s)$s.InputGameplayActive -and $s.ApplicationFocused} 'Rendered P2 focus'
    $trace=Join-Path $qa 'p2-motion.json';if(Test-Path $trace){Remove-Item $trace}
    $probe=Join-Path $qa 'start-probe.cs'
    [IO.File]::WriteAllText($probe,'var t=System.Type.GetType("BeMyArms.QA.P2MotionProbe, BeMyArms.QA");var g=new UnityEngine.GameObject("DisposableP2MotionProbe");t.GetMethod("Begin").Invoke(g.AddComponent(t),new object[]{@"'+$trace.Replace('"','""')+'",8f});return true;')
    Qa $p2 'eval_file' @($probe) | Out-Null
    # Compile the opt-in probe first, THEN reset: a bot may already be holding cover by the time
    # eval returns. The actual runtime trace must include travel, not score stationary frames.
    Qa $p2 'qa_practice_action' @('--action','0') | Out-Null
    Qa $p2 'qa_inject_input' @('--yaw_rate','8') | Out-Null
    $deadline=[DateTime]::UtcNow.AddSeconds(15)
    do {if(Test-Path $trace){break};Start-Sleep -Milliseconds 100}while([DateTime]::UtcNow -lt $deadline)
    if(!(Test-Path $trace)){throw 'No completed frame trace'}
    Qa $p2 'qa_inject_input' | Out-Null
    $frames=@((Get-Content $trace -Raw | ConvertFrom-Json).Frames)
    $between=0
    for($i=1;$i -lt $frames.Count;$i++){
        $a=$frames[$i-1];$f=$frames[$i]
        $d=[Math]::Abs($f.X-$a.X)+[Math]::Abs($f.Y-$a.Y)+[Math]::Abs($f.Z-$a.Z)
        if($f.Tick -eq $a.Tick -and $d -gt .00001){$between++}
    }
    $anchor=($frames|Measure-Object AnchorError -Maximum).Maximum
    $aim=($frames|Measure-Object AimError -Maximum).Maximum
    if($frames.Count -le 60 -or $between -le 10 -or $anchor -ge .001 -or $aim -ge .1){throw "Frame trace failed: frames=$($frames.Count), travel=$between, anchor=$anchor, aim=$aim"}
    Qa $p2 'qa_practice_action' @('--action','2') | Out-Null
    $state=Await $p2 {param($s)$s.OwnBodyResolved -and $s.LocalRole -eq 0} 'Role switch to P1'
    $camera=Join-Path $qa 'check-p1-camera.cs'
    [IO.File]::WriteAllText($camera,'var t=System.Type.GetType("BeMyArms.Client.LocalPlayer, BeMyArms.Client");var p=UnityEngine.Object.FindObjectsByType(t).Single();var c=(UnityEngine.Component)t.GetField("_p1Cam",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(p);var f=System.Type.GetType("Unity.Cinemachine.CinemachineThirdPersonFollow, Unity.Cinemachine");return new {Distance=f.GetField("CameraDistance").GetValue(c.GetComponent(f))};')
    $close=(Qa $p2 'eval_file' @($camera)).result
    if([Math]::Abs($close.Distance-2.8) -gt .001){throw 'P1 rig distance mismatch'}
    $errors=@(Get-ChildItem $qa -Filter '*.log' | Select-String 'NullReferenceException|MissingReferenceException|TypeLoadException|Exception:|error CS\d')
    if($errors.Count){throw 'Runtime reference/type errors in camera fixture logs'}
    $result=@{Success=$true;Frames=$frames.Count;TravelBetweenSnapshots=$between;MaxAnchorError=$anchor;MaxAimError=$aim;P1Distance=$close.Distance}
    $result|ConvertTo-Json|Set-Content (Join-Path $qa 'results.json') -Encoding utf8
    $result|ConvertTo-Json
}finally {
    foreach($p in $processes){if(!$p.HasExited){$p.Kill();$p.WaitForExit(5000)|Out-Null}}
    foreach($name in @('server','p2')){$path=Join-Path (Join-Path $qa "$name-runtime") '.unity-pipeline-runtime-port';if(Test-Path $path){Remove-Item $path -Force}}
}
