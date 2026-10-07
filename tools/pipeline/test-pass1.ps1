param([string]$BuildDirectory = 'Builds/Pass1', [switch]$CleanDisconnect)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path $BuildDirectory).Path
$exe = Join-Path $root 'BeMyArms.exe'
$qa = Join-Path $root 'QA'
New-Item -ItemType Directory -Force $qa | Out-Null
$processes = [System.Collections.Generic.List[System.Diagnostics.Process]]::new()
$checks = [System.Collections.Generic.List[string]]::new()
$udp = [System.Net.Sockets.UdpClient]::new(0)
$port = $udp.Client.LocalEndPoint.Port
$udp.Close()

function Assert-Check([bool]$condition, [string]$description) {
    if (!$condition) { throw "Failed: $description" }
    $checks.Add($description)
}

function Start-Headless([string]$name, [string]$arguments) {
    $log = Join-Path $qa "$name.log"
    $process = Start-Process -FilePath $exe -ArgumentList "-batchmode -nographics -logFile `"$log`" $arguments" -PassThru
    $processes.Add($process)
    $descriptor = Join-Path $root '.unity-pipeline-runtime-port'
    $runtimeDirectory = Join-Path $qa "$name-runtime"
    New-Item -ItemType Directory -Force $runtimeDirectory | Out-Null
    $copy = Join-Path $runtimeDirectory '.unity-pipeline-runtime-port'
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    do {
        if ($process.HasExited) { throw "$name exited before its QA endpoint was ready. See $log" }
        if (Test-Path $descriptor) {
            try {
                $text = Get-Content $descriptor -Raw
                $instance = $text | ConvertFrom-Json
                if ($instance.pid -eq $process.Id) {
                    [IO.File]::WriteAllText($copy, $text)
                    return $runtimeDirectory
                }
            } catch { } # A heartbeat may be rewriting the discovery file.
        }
        Start-Sleep -Milliseconds 200
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "No runtime endpoint for $name. See $log"
}

function Invoke-Qa([string]$runtime, [string]$command, [string[]]$parameters = @()) {
    $text = & unity command $command --runtime-path $runtime --timeout 30 --format json @parameters
    if ($LASTEXITCODE -ne 0) { throw "QA command failed: $command`n$text" }
    $response = $text | ConvertFrom-Json
    if (!$response.success) { throw "QA command failed: $command`n$text" }
    $result = $response.data.result
    if ($result -is [string]) { $result = $result | ConvertFrom-Json }
    if ($result.PSObject.Properties.Name -contains 'Success' -and !$result.Success) { throw "QA result failed: $command`n$text" }
    return $result
}

function State([string]$runtime) { return Invoke-Qa $runtime 'qa_player_state' }
function Invoke-QaEval([string]$runtime, [string]$code) {
    $file = Join-Path $qa 'network-check.cs'
    [IO.File]::WriteAllText($file, $code)
    return Invoke-Qa $runtime 'eval_file' @($file)
}
function Await-State([string]$runtime, [scriptblock]$predicate, [string]$description, [int]$timeoutSeconds = 15) {
    $deadline = [DateTime]::UtcNow.AddSeconds($timeoutSeconds)
    do {
        $state = State $runtime
        if (& $predicate $state) { return $state }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Timeout: $description`n$($state | ConvertTo-Json -Depth 6)"
}

try {
    $server = Start-Headless 'server' "-m3-role server -m7-arena M7DuelArena -m4-mode duel -m4-matchmaker 0 -m3-port $port -m3-required-players 2 -m3-start-delay 0 -m3-practice 1 -m3-strict-slots 1 -m3-delay 0 -m3-loss 0 -m3-buy 3"
    $p1 = Start-Headless 'p1' "-m7-join 127.0.0.1 -m7-port $port -m7-join-role p1 -m3-token pass1-body"
    $a = Await-State $p1 { param($s) $s.OwnBodyResolved } 'first client assignment'
    Assert-Check ($a.LocalSlot -eq 0 -and $a.MatchPhase -eq 'Warmup') 'Duo waits for its second human'
    $p2 = Start-Headless 'p2' "-m7-join 127.0.0.1 -m7-port $port -m7-join-role p2 -m3-token pass1-arms"
    $b = Await-State $p2 { param($s) $s.MatchLive -and $s.OwnBodyResolved } 'duo live'
    $a = State $p1
    Assert-Check ($b.LocalSlot -eq 1 -and $a.LocalTeam -eq $b.LocalTeam -and $a.LocalBodyIndex -eq $b.LocalBodyIndex -and $b.BodyCount -eq 2) 'Both human roles resolve to one shared Team A body'
    Assert-Check (!$b.P1Bot -and !$b.P2Bot) 'Human roles have no simultaneous bot owner'
    Invoke-Qa $p1 'qa_headless_controls' | Out-Null
    Invoke-Qa $p2 'qa_headless_controls' | Out-Null

    Invoke-Qa $p1 'qa_inject_input' @('--crouch', 'true') | Out-Null
    $b = Await-State $p2 { param($s) $s.AuthoritativeCrouching } 'authoritative crouch on partner'
    Assert-Check ($b.HitHeight -lt 1.2) 'Crouch reaches the server and the partner low stance'
    $before = $b.AuthoritativePosition
    Invoke-Qa $p1 'qa_inject_input' @('--movex', '1', '--crouch', 'true') | Out-Null
    Start-Sleep -Milliseconds 600
    Invoke-Qa $p1 'qa_inject_input' | Out-Null
    $b = Await-State $p2 { param($s) !$s.AuthoritativeCrouching } 'released crouch'
    $distance = [Math]::Sqrt([Math]::Pow($b.AuthoritativePosition[0] - $before[0], 2) + [Math]::Pow($b.AuthoritativePosition[2] - $before[2], 2))
    Assert-Check ($distance -gt 0.1) 'Manual P1 movement advances the authoritative shared body'

    Invoke-Qa $p2 'qa_inject_look' @('--yaw', '100') | Out-Null
    $b = Await-State $p2 { param($s) [Math]::Abs($s.AimYawOffset) -ge 69.9 } 'sector edge'
    Assert-Check ([Math]::Abs($b.AimYawOffset) -le 85.01) 'Displayed P2 aim obeys the elastic outer limit'
    $ammo = $b.Ammo
    Invoke-Qa $p2 'qa_inject_input' @('--fire', 'true') | Out-Null
    $b = Await-State $p2 { param($s) $s.Ammo -lt $ammo } 'legal edge fire'
    Invoke-Qa $p2 'qa_inject_input' | Out-Null
    Assert-Check ($b.ShotsFired -gt 0) 'Authoritative firing accepts the displayed edge aim'

    Invoke-Qa $p2 'qa_inject_input' @('--yaw_rate', '400') | Out-Null
    $b = Await-State $p2 { param($s) $s.AimYawOffset -gt 80 -and $s.AimYawOffset -le 85.01 } 'slow elastic overtravel'
    Assert-Check ($b.AimYawOffset -gt 70) 'Soft pressure visibly travels beyond the resting boundary'
    $ammo = $b.Ammo
    Invoke-Qa $p2 'qa_inject_input' @('--yaw_rate', '400', '--fire', 'true') | Out-Null
    $b = Await-State $p2 { param($s) $s.Ammo -lt $ammo -and $s.AimYawOffset -gt 70 } 'fire during elastic overtravel'
    Assert-Check ($b.Ammo -lt $ammo) 'Server accepts firing in bounded Soft overtravel'
    Invoke-Qa $p2 'qa_inject_input' | Out-Null
    $b = Await-State $p2 { param($s) [Math]::Abs($s.AimYawOffset - 70) -lt 0.01 } 'elastic release returns to resting boundary'
    Assert-Check ([Math]::Abs($b.AimYawOffset - 70) -lt 0.01) 'Releasing outward pressure springs back to seventy degrees'
    Invoke-Qa $p2 'qa_inject_look' @('--yaw', '-5') | Out-Null
    $b = Await-State $p2 { param($s) [Math]::Abs($s.AimYawOffset - 65) -lt 0.01 } 'inward response after spring return'
    Assert-Check ([Math]::Abs($b.AimYawOffset - 65) -lt 0.01) 'Inward mouse aim is immediately one-to-one after return'

    $epoch = $b.ControlEpoch
    Invoke-Qa $p2 'qa_practice_action' @('--action', '0') | Out-Null
    $b = Await-State $p2 { param($s) $s.ControlEpoch -gt $epoch -and $s.Ammo -eq 30 } 'encounter reset'
    Assert-Check (!$b.AuthoritativeCrouching -and [Math]::Abs($b.AimYawOffset) -lt 0.01) 'Encounter reset restores ammo, stance and aim'
    Start-Sleep -Milliseconds 600
    Invoke-Qa $p1 'qa_practice_action' @('--action', '2') | Out-Null
    $a = Await-State $p1 { param($s) $s.LocalSlot -eq 1 } 'P1 becomes P2'
    $b = Await-State $p2 { param($s) $s.LocalSlot -eq 0 } 'P2 becomes P1'
    Assert-Check ($a.LocalRole -eq 1 -and $b.LocalRole -eq 0 -and !$b.P1Bot -and !$b.P2Bot) 'Server role exchange updates both client authorities'
    Await-State $p2 { param($s) $s.MatchLive } 'swapped live' | Out-Null
    Invoke-Qa $p2 'qa_inject_input' @('--crouch', 'true') | Out-Null
    Await-State $p1 { param($s) $s.AuthoritativeCrouching } 'new P1 owns crouch' | Out-Null
    Invoke-Qa $p2 'qa_inject_input' | Out-Null
    Assert-Check $true 'New P1 controls the same body after role exchange'

    # Unexpected loss retains the original token's exchanged role; a fresh connection reclaims it.
    if ($CleanDisconnect) {
        Invoke-QaEval $p2 'System.Type.GetType("BeMyArms.M3.M3Config, BeMyArms.M3").GetField("ExitAfterSeconds").SetValue(null, 3600f); var t = System.Type.GetType("Unity.Netcode.NetworkManager, Unity.Netcode.Runtime"); t.GetMethod("Shutdown").Invoke(t.GetProperty("Singleton").GetValue(null), new object[] { false }); return true;' | Out-Null
    } else { $processes[2].Kill() }
    $a = Await-State $p1 { param($s) $s.P1Bot } 'temporary bot takeover' 45
    Assert-Check ($a.OwnBodyResolved -and $a.LocalSlot -eq 1) 'Unaffected partner stays connected through peer loss'
    $p2 = Start-Headless 'p2-reconnect' "-m7-join 127.0.0.1 -m7-port $port -m7-join-role p2 -m3-token pass1-arms"
    $b = Await-State $p2 { param($s) $s.OwnBodyResolved -and $s.LocalSlot -eq 0 -and !$s.P1Bot } 'token reconnect to swapped slot'
    Assert-Check ($b.LocalRole -eq 0 -and !$b.P2Bot) 'Reconnect restores exchanged role and removes temporary bot'

    Invoke-QaEval $p2 'System.Type.GetType("BeMyArms.M7.M7PrivateMatch, BeMyArms.M7").GetMethod("ReturnToMenu").Invoke(null, null); return true;' | Out-Null
    Await-State $p1 { param($s) $s.P1Bot } 'explicit guest leave' | Out-Null
    $guest = Start-Headless 'fresh-guest' "-m7-join 127.0.0.1 -m7-port $port -m7-join-role p1 -m3-token pass1-new-guest"
    $b = Await-State $guest { param($s) $s.OwnBodyResolved -and $s.LocalSlot -eq 0 -and !$s.P1Bot } 'fresh guest after intentional leave'
    Assert-Check ($b.BodyCount -eq 2) 'Intentional guest leave frees the role for a fresh human without restarting the server'

    # Exercise the production menu callbacks and owned allocator, not only explicit server CLI entry.
    $menuHost = Start-Headless 'menu-host' ''
    Invoke-Qa $menuHost 'qa_click_button' @('--name', 'DUO PRACTICE') | Out-Null
    Invoke-Qa $menuHost 'qa_click_button' @('--name', 'Host') | Out-Null
    $h = Await-State $menuHost { param($s) $s.OwnBodyResolved -and $s.MatchPhase -eq 'Warmup' } 'menu host connected to allocated server'
    $connection = Invoke-QaEval $menuHost 'var t = System.Type.GetType("BeMyArms.M7.M7PrivateMatch, BeMyArms.M7"); return t.GetField("Current").GetValue(null).GetType().GetField("Port").GetValue(t.GetField("Current").GetValue(null));'
    $allocatedPort = $connection.result
    Assert-Check ($allocatedPort -gt 0 -and $allocatedPort -ne $port) 'Menu host allocates a fresh dedicated server port'
    $partner = Start-Headless 'menu-partner' "-m7-join 127.0.0.1 -m7-port $allocatedPort -m7-join-role p2"
    $h = Await-State $partner { param($s) $s.MatchLive -and $s.OwnBodyResolved } 'partner joins menu-allocated server'
    Assert-Check ($h.LocalSlot -eq 1 -and !$h.P1Bot -and !$h.P2Bot) 'Menu-hosted duo reaches live with both exact human roles'
    $serverPidResult = Invoke-QaEval $menuHost 'var t = System.Type.GetType("BeMyArms.M7.M7PrivateMatch, BeMyArms.M7"); var a = t.GetField("Allocator").GetValue(null); var p = a.GetType().GetField("_process", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(a); return p.GetType().GetProperty("Id").GetValue(p);'
    $allocatedServerPid = [int]$serverPidResult.result
    Invoke-QaEval $menuHost 'System.Type.GetType("BeMyArms.M7.M7PrivateMatch, BeMyArms.M7").GetMethod("ReturnToMenu").Invoke(null, null); return true;' | Out-Null
    $h = Await-State $menuHost { param($s) $s.Scene -eq 'M7MainMenu' -and !$s.OwnBodyResolved -and $s.BodyCount -eq 0 } 'clean host menu return'
    Assert-Check (!(Get-Process -Id $allocatedServerPid -ErrorAction SilentlyContinue)) 'Leaving menu host shuts down its owned dedicated server'
    Invoke-Qa $menuHost 'qa_click_button' @('--name', 'PLAY') | Out-Null
    Invoke-Qa $menuHost 'qa_click_button' @('--name', 'Start') | Out-Null
    $h = Await-State $menuHost { param($s) $s.MatchLive -and $s.OwnBodyResolved } 'fresh solo session after duo'
    Assert-Check ($h.LocalSlot -eq 0 -and $h.BodyCount -eq 2 -and !$h.P1Bot -and $h.P2Bot) 'Host can return to menu and start a clean solo session'
    # Owner crash is covered by the production watchdog, which must not orphan this final server.
    $serverPidResult = Invoke-QaEval $menuHost 'var t = System.Type.GetType("BeMyArms.M7.M7PrivateMatch, BeMyArms.M7"); var a = t.GetField("Allocator").GetValue(null); var p = a.GetType().GetField("_process", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(a); return p.GetType().GetProperty("Id").GetValue(p);'
    $allocatedServerPid = [int]$serverPidResult.result
    $processes[$processes.Count - 2].Kill()
    $watchdogDeadline = [DateTime]::UtcNow.AddSeconds(10)
    while ((Get-Process -Id $allocatedServerPid -ErrorAction SilentlyContinue) -and [DateTime]::UtcNow -lt $watchdogDeadline) { Start-Sleep -Milliseconds 200 }
    Assert-Check (!(Get-Process -Id $allocatedServerPid -ErrorAction SilentlyContinue)) 'Owner crash watchdog closes the allocated server'

    $report = @{ success = $true; checks = $checks.ToArray(); port = $port; headless = $true }
    $report | ConvertTo-Json -Depth 5 | Tee-Object -FilePath (Join-Path $qa 'integration-results.json')
} finally {
    foreach ($process in $processes) {
        if (!$process.HasExited) { $process.Kill(); $process.WaitForExit(5000) | Out-Null }
    }
    # Runtime discovery copies contain per-process tokens and are only needed while those processes live.
    Get-ChildItem $qa -Directory -Filter '*-runtime' | Remove-Item -Recurse
    Get-ChildItem $qa -Filter '*.runtime' | Remove-Item
}
