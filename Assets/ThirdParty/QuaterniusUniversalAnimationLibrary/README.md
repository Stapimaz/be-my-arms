# Quaternius Universal Base Characters / Universal Animation Library

Third-party CC0 character art and animation used as the **development presentation baseline** for
Be My Arms.

- **Author:** Quaternius (https://quaternius.com)
- **Licence:** CC0 1.0 Universal (public domain dedication) — see `LICENSE-CC0.txt`
- **Animation source:** *Universal Animation Library (Standard)*
  https://quaternius.com/packs/universalanimationlibrary.html
  (Unity FBX download mirrored on OpenGameArt: https://opengameart.org/content/universal-animation-library)
- **Rig source:** the library's own universal humanoid "Mannequin" rig (53 bones, Blender `DEF-*`
  naming). The library is compatible with *Universal Base Characters*
  (https://quaternius.com/packs/universalbasecharacters.html), which is where future higher-detail
  body meshes would come from.

## What is vendored here

`Bodies/` contains three derived FBX files produced from the library's Unity FBX by
`tools/pipeline/build-quaternius-bodies.py`. They share the original 53-bone rig, skin weights,
materials and the animation clips, but each has a different, anatomy-locked mesh:

| File | Anatomy | Used for |
|---|---|---|
| `Q_P1_Body.fbx` | head + torso + pelvis + legs, **no arms** | P1 third-person body |
| `Q_P2_Body.fbx` | upper chest / shoulders + two arms, **no head/legs** | P2 third-person arms+torso |
| `Q_P2_Arms.fbx` | shoulders + arms only | P2 first-person viewmodel |

The Be My Arms locked anatomy (P1 = body without arms, P2 = arms without head) is enforced by
deleting the vertices whose dominant skin weight belongs to the hidden bones and capping the
resulting boundary loops. Nothing about the skeleton, weights or animation data is re-authored, so
all three remain compatible with one another and with the source library.

## Regenerating

```powershell
# 1. Download the Universal Animation Library (Standard) Unity FBX from the URL above.
# 2. Run the pinned Blender (see tools/blender) headless:
blender -b --factory-startup --python tools/pipeline/build-quaternius-bodies.py -- `
    "<UniversalAnimationLibrary.fbx>" "Assets/ThirdParty/QuaterniusUniversalAnimationLibrary/Bodies"
```

The derived FBX are committed so a normal Unity import does not require the source download or
Blender. The generator never needs to run in CI.
