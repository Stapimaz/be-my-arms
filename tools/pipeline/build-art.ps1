# Regenerates the Be My Arms production art with the pinned Blender build.
# Writes editable .blend sources to art/blender/blend and FBX exports to Assets/Art.
$ErrorActionPreference = 'Stop'

$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$blenderPathScript = Join-Path $PSScriptRoot '..\blender\BlenderPath.ps1'
$blender = & powershell -ExecutionPolicy Bypass -File $blenderPathScript
Write-Host "[pipeline] blender: $blender"

$scripts = @('make_characters.py', 'make_weapons.py', 'make_env.py', 'make_mapkit.py')
foreach ($script in $scripts) {
    $path = Join-Path $repo "art\blender\scripts\$script"
    Write-Host "[pipeline] running $script"
    & $blender --background --factory-startup --python $path
    if ($LASTEXITCODE -ne 0) { throw "[pipeline] Blender failed on $script (exit $LASTEXITCODE)" }
}

Write-Host "[pipeline] art regeneration complete"
