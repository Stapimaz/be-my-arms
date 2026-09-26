# Quaternius Low Poly Guns Pack

CC0 first/third-person weapon models used by Be My Arms.

- **Author:** Quaternius (https://quaternius.com)
- **Licence:** CC0 1.0 Universal (public domain dedication)
  https://creativecommons.org/publicdomain/zero/1.0/
- **Source:** *Low Poly Guns Pack* — https://opengameart.org/content/low-poly-guns-pack
  (direct: https://opengameart.org/sites/default/files/ultimate_gun_pack_by_quaternius.zip)

## What is vendored

`AssaultRifle_1.fbx` — the stylized assault rifle used by the shared-body P2 weapon mount
(world presentation) and by the first-person arms viewmodel. The source pack contains 40 models
(FBX/OBJ/Blend); only the rifle actually used by the vertical slice is kept in the project so the
repo stays small. It is imported at its native scale and normalized to a fixed length and +Z muzzle
by `M7WeaponBuilder`, which also derives the hand-grip markers from the model bounds.

Regenerate the weapon prefab from the Unity editor menu **Be My Arms > M7 > Build Weapon** (or via
`M7PipelineCommands.Regenerate`).
