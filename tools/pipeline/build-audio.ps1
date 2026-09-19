# Regenerates the Be My Arms production-test audio with Blender's bundled Python.
$ErrorActionPreference = 'Stop'

$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$meta = Get-Content (Join-Path $PSScriptRoot '..\blender\BlenderVersion.json') -Raw | ConvertFrom-Json
$python = Join-Path $env:LOCALAPPDATA "BeMyArms\tools\blender\$($meta.version)\$($meta.version.Substring(0,3))\python\bin\python.exe"
if (-not (Test-Path $python)) { throw "[audio] Blender bundled python not found at $python; run tools/blender/install-blender.ps1" }

Write-Host "[audio] python: $python"
& $python (Join-Path $repo 'art\audio\make_sfx.py')
if ($LASTEXITCODE -ne 0) { throw "[audio] synthesis failed (exit $LASTEXITCODE)" }
