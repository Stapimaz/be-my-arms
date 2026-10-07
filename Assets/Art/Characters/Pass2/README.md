# Pass 2 fighter source and rights

The combined fighter is project-authored tactical armour and a split role mesh on a native
Quaternius rig. The exposed face comes from Quaternius Universal Base Characters. Authored
eight-direction jog/crouch and sprint cycles use explicit foot targets, swing arcs and knee poles.
Idle/jump/landing/death use Universal Animation Library 1; slide/climb use Library 2, bone-name
mapped offline onto the matching native mannequin rest rig. Unity uses baked Generic clips.

## Third-party inputs (downloaded 2026-10-07)

All three packs are published by **Quaternius** under **CC0 1.0 Universal**, allowing use,
adaptation and commercial redistribution. See `Assets/ThirdParty/QuaterniusUniversalAnimationLibrary/LICENSE-CC0.txt`.
Only free Standard content is used. No paid pack, external service account or runtime dependency is required.

| Input | Source | SHA-256 of Unity FBX used |
| --- | --- | --- |
| Universal Animation Library Standard | https://opengameart.org/content/universal-animation-library | `42F16A4ED61D1E9F5FC7C2526894C5487148AC6BA82B64281CEB7943570F3C37` |
| Universal Base Characters Standard, Superhero Male | https://quaternius.itch.io/universal-base-characters | `79344418D754A59730B79D1874752E9592143DB34ABE8ADF138FA9A92A4768E9` |
| Universal Animation Library 2 Standard | https://quaternius.itch.io/universal-animation-library-2 | `D26D0E9F4A202D473194C056045143095A605A53BA1D823EF24055BE4B86851D` |

The armour, rifle geometry/component layout, directional gaits, grip/reload choreography,
particle texture/effects and procedural SFX additions are authored for this project.

## Rebuild

`tools/pipeline/build-pass2-fighter.py` accepts the UAL1 Unity FBX, Base Characters
`Superhero_Male_FullBody.fbx`, output directory, and UAL2 Unity FBX (in that order).
Run it with the pinned Blender 4.5.13. The output `.blend` authoring files are retained in
`art/blender/blend/Pass2`; copy its four FBX files into this directory's `Models` subdirectory.
The blend files contain the complete baked actions and meshes, so they also support further DCC editing.

Use `unity command eval_file tools/pipeline/qa/prepare-pass2.cs` to rebuild controllers,
materials, role skins, viewmodel, network body prefab and audio/VFX libraries in the connected Editor.
`art/audio/make_sfx.py` regenerates the WAVs using Blender's Python. The prefab/controller
builder preserves existing asset GUIDs. `tools/pipeline/qa/pass2-preview.cs` renders static
world/FPS pose fixtures into ignored QA output.
