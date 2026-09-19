# Resolves the pinned Blender executable path. Run tools/blender/install-blender.ps1 first.
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$meta = Get-Content (Join-Path $here 'BlenderVersion.json') -Raw | ConvertFrom-Json
$exe = Join-Path $env:LOCALAPPDATA "BeMyArms\tools\blender\$($meta.version)\blender.exe"
if (-not (Test-Path $exe)) { throw "Blender $($meta.version) not installed. Run tools/blender/install-blender.ps1" }
Write-Output $exe
