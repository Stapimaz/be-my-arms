# Smooth fighter source and licensing

The mesh/rig is adapted from Quaternius **Universal Base Characters [Standard]**,
`Superhero_Male_FullBody.fbx`. Models, weights, face and eyes are artist-authored source
content. The separately evaluated buzzed hairstyle was omitted because its bind orientation
did not fit this base. The active character has an exposed, smooth head.

Animations are from Quaternius **Universal Animation Library [Standard] v3.0**, free
**Universal Animation Library 2 [Standard]**, and Kay Lousberg **KayKit Character
Animations 1.1**, Rig_Medium MovementBasic, MovementAdvanced and CombatRanged.
All selected downloads are **CC0**, permitting modification and commercial use.

Official sources:
- https://quaternius.itch.io/universal-base-characters
- https://quaternius.itch.io/universal-animation-library
- https://quaternius.itch.io/universal-animation-library-2
- https://kaylousberg.itch.io/kaykit-character-animations
- https://creativecommons.org/publicdomain/zero/1.0/

Retained modified `.blend` files: `art/blender/blend/Pass2Redo/`.
Mesh adaptation: `tools/pipeline/build-smooth-fighter.py`.
Animation retarget/bake/controller/prefab authoring: `M7SmoothFighterBuilder.cs`.

The shared rig's P1 and P2 mesh surfaces partition the same original triangles. Skin
weights and bindposes remain coherent at the role seam. FPS arms/hands are a trimmed
version of that source, with proximal geometry continuing below the camera frame.
Each role uses one consistent matte material across its complete surface: blue P1
(including head/eyes/feet), orange P2 (including hands and the camera-local arms).
Role skin/color selection is deferred.

Directional crouch side/back clips are project adaptations of the free KayKit motion;
they are not claimed to be original Quaternius directional crouch clips. The rifle's
finger poses, grip sockets, choreography and geometry remain project-authored derivatives.

Full asset-research findings and the free-only decision are in `docs/PASS2_FOUNDATION_REDO.md`.

The final cleanup removes the standalone vault action. Climb source media retained from
the earlier redo is archival and is not connected to the active controller or controls.
