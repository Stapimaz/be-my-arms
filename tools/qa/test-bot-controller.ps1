param([string]$BuildDirectory='Builds/Windows')
$ErrorActionPreference='Stop'
$root=(Resolve-Path $BuildDirectory).Path
$qa=Join-Path $root 'QA/BotController'
New-Item -ItemType Directory -Force $qa | Out-Null
$runtime=Join-Path $qa 'server-runtime'
New-Item -ItemType Directory -Force $runtime | Out-Null
$udp=[System.Net.Sockets.UdpClient]::new(0);$port=$udp.Client.LocalEndPoint.Port;$udp.Close()
$log=Join-Path $qa 'server.log'
$process=Start-Process (Join-Path $root 'BeMyArms.exe') -ArgumentList "-batchmode -nographics -logFile `"$log`" -match-role server -client-arena DuelArena -queue-mode duel -queue-matchmaker 0 -match-port $port -match-required-players 1 -match-start-delay 1 -match-practice 1 -match-delay 0 -match-loss 0 -match-buy 1" -PassThru
function Qa([string]$command,[string[]]$parameters=@()){
    $text=& unity command $command --runtime-path $runtime --timeout 60 --format json @parameters
    if($LASTEXITCODE -ne 0){throw "$command failed: $text"}
    $r=$text | ConvertFrom-Json
    if(!$r.success){throw "$command failed: $text"}
    $result=$r.data.result;if($result -is [string]){$result=$result | ConvertFrom-Json}
    if($result.PSObject.Properties.Name -contains 'Success' -and !$result.Success){throw "$command failed: $text"}
    return $result
}
try {
    $deadline=[DateTime]::UtcNow.AddSeconds(40)
    $connected=$false
    do {
        if($process.HasExited){throw "Server exited. See $log"}
        try {
            $text=Get-Content (Join-Path $root '.unity-pipeline-runtime-port') -Raw
            if(($text | ConvertFrom-Json).pid -eq $process.Id){[IO.File]::WriteAllText((Join-Path $runtime '.unity-pipeline-runtime-port'),$text);$connected=$true;break}
        }catch{}
        Start-Sleep -Milliseconds 200
    }while([DateTime]::UtcNow -lt $deadline)
    if(!$connected){throw 'No isolated server runtime endpoint'}
    $deadline=[DateTime]::UtcNow.AddSeconds(20)
    do {$state=Qa 'qa_player_state';if($state.MatchLive){break};Start-Sleep -Milliseconds 200}while([DateTime]::UtcNow -lt $deadline)
    if(!$state.MatchLive){throw 'Bot-filled Duel did not reach Live'}
    $result=(Qa 'eval_file' @((Join-Path $PSScriptRoot 'check-bot-controller.cs'),'60000')).result
    if(!$result.Success){throw 'No bot-controller verdict'}
    $errors=@(Select-String -Path $log -Pattern 'NullReferenceException|MissingReferenceException|TypeLoadException|Exception:|error CS\d')
    if($errors.Count){throw "Runtime errors: $errors"}
    $result | ConvertTo-Json -Depth 7 | Set-Content (Join-Path $qa 'results.json') -Encoding utf8
    $result.Reports | Format-Table -AutoSize
    Write-Output 'Easy/Hard authoritative bot motion, firing, occlusion and reset checks passed (not human feel acceptance)'
}finally {
    if(!$process.HasExited){$process.Kill();$process.WaitForExit(5000) | Out-Null}
    $descriptor=Join-Path $runtime '.unity-pipeline-runtime-port';if(Test-Path $descriptor){Remove-Item $descriptor -Force}
    if(Test-Path $runtime){Remove-Item $runtime}
}
