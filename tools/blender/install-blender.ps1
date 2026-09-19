# Installs the pinned Blender build (portable zip) into a user-local tools cache.
# Reproducible and elevation-free; the repo keeps only this script and the pinned metadata.
param(
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$meta = Get-Content (Join-Path $here 'BlenderVersion.json') -Raw | ConvertFrom-Json

$cache = Join-Path $env:LOCALAPPDATA 'BeMyArms\cache'
$installRoot = Join-Path $env:LOCALAPPDATA 'BeMyArms\tools\blender'
$target = Join-Path $installRoot $meta.version
$exe = Join-Path $target 'blender.exe'

if ((Test-Path $exe) -and -not $Force) {
    Write-Host "[blender] $($meta.name) $($meta.version) already installed at $target"
    & $exe --version | Select-Object -First 1
    exit 0
}

New-Item -ItemType Directory -Force -Path $cache, $installRoot | Out-Null
$zip = Join-Path $cache "blender-$($meta.version)-windows-x64.zip"

if (-not (Test-Path $zip)) {
    Write-Host "[blender] downloading $($meta.url)"
    & curl.exe -L --fail --retry 3 -o $zip $meta.url
    if ($LASTEXITCODE -ne 0) { throw "download failed" }
}

$hash = (Get-FileHash $zip -Algorithm SHA256).Hash
if ($meta.sha256) {
    if ($hash -ne $meta.sha256.ToUpperInvariant()) {
        throw "[blender] checksum mismatch for $zip`n expected $($meta.sha256)`n actual   $hash"
    }
    Write-Host "[blender] checksum OK ($hash)"
} else {
    Write-Host "[blender] WARNING: no pinned sha256; computed $hash (pin this in BlenderVersion.json)"
}

if (Test-Path $target) { Remove-Item -Recurse -Force $target }
Write-Host "[blender] extracting to $target"
Expand-Archive -Path $zip -DestinationPath $installRoot -Force

$extracted = Join-Path $installRoot "blender-$($meta.version)-windows-x64"
if (Test-Path $extracted) { Move-Item -Force $extracted $target }

if (-not (Test-Path $exe)) { throw "[blender] extraction did not produce $exe" }
Write-Host "[blender] installed:"
& $exe --version | Select-Object -First 1
