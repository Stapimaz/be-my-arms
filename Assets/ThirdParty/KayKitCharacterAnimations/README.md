# KayKit Character Animations (CC0)

Genuine authored humanoid directional-locomotion clips used by Be My Arms for P1 strafing and
backward movement.

- **Author:** Kay Lousberg (KayKit) — https://kaylousberg.com / https://kaylousberg.itch.io
- **Licence:** CC0 1.0 Universal (public domain dedication), no attribution required
  https://creativecommons.org/publicdomain/zero/1.0/
- **Source pack:** *KayKit – Character Animations* (161 humanoid animations, `Rig_Medium`)
  https://kaylousberg.itch.io/kaykit-character-animations
  (CC0 mirror: https://github.com/GeorgeQLe/assets-kaykit-3d-characters)

## What is vendored

`Rig_Medium_MovementAdvanced.fbx` — the Rig_Medium movement-advanced animation set. Only the clips
Be My Arms actually uses are referenced:

- `Walking_Backwards` — S / backward locomotion
- `Running_Strafe_Left`, `Running_Strafe_Right` — A/D strafing

They are authored humanoid clips; Be My Arms retargets them onto its own humanoid rig via Unity's
Mecanim Humanoid avatars (see `M7CharacterBodyBuilder`), rather than forging directional motion from
the forward cycles.
