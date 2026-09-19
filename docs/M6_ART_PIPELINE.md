# M6 — Production Art and Content Pipeline

**Status:** **Complete at the M6 acceptance level.** Blender is installed/configured reproducibly,
the Blender → Unity pipeline and asset conventions are defined and enforced, and the first real
production assets (P1 body, P2 upper-body/arms, a weapon, a utility and a small environment kit)
travel the pipeline into Unity and satisfy the M5 P1/P2 rig contract.
**Modules:** `Assets/Scripts/M6` (`BeMyArms.M6` + `BeMyArms.M6.Editor`), `tools/blender`,
`tools/pipeline`, `art/blender`.

---

## 1. DCC decision — Blender 4.5 LTS

**Chosen: Blender 4.5.13 LTS.** No concrete technical reason was found against Blender; it is the
default DCC for this project because it is free, fully scriptable through `bpy`/`bmesh` for headless
CLI automation, supported as an LTS until July 2027, and exports FBX that Unity imports natively
(no extra package). Blender's FBX exporter handles the axis/scale conversion to Unity's Y-up
convention.

Pinned metadata: `tools/blender/BlenderVersion.json` (version, URL, SHA-256). The build is the
portable Windows zip, extracted to a **user-local tools cache**
(`%LOCALAPPDATA%\BeMyArms\tools\blender\4.5.13`), so the repo stays free of binaries and the setup
needs no elevation.

```powershell
powershell -ExecutionPolicy Bypass -File tools/blender/install-blender.ps1   # downloads + verifies + extracts
powershell -ExecutionPolicy Bypass -File tools/pipeline/build-art.ps1        # regenerates .blend + FBX
```

Verified in this environment: `Blender 4.5.13 LTS`, bundled Python 3.11.15, `bpy` + `bmesh`
headless execution, and checksum `B5FDF800CE65FA2F209E8F68D02667E4D720FA1C42F247C72D1882AB04DECBA6`.

## 2. Coordinate, scale and orientation convention

- **Authoring:** Blender is Z-up; the character faces **-Y**; **1 Blender unit = 1 metre**.
- **Export:** FBX with `axis_forward='-Z'`, `axis_up='Y'`, `global_scale=1.0`,
  `apply_scale_options='FBX_SCALE_NONE'`.
- **Import:** Unity `globalScale=1`, `useFileScale=on`, `bakeAxisConversion=off`, no animation/
  cameras/lights, standard materials. Imported at **1:1 metres**.
- Measured after import: P1 body height **1.868 m**, P2 arms layer **0.570–0.660 m**, rifle length
  **0.975 m** along +Z — exactly as authored.

## 3. Folder and naming conventions

| Kind | Location | Name |
|---|---|---|
| Blender sources (artist-editable) | `art/blender/blend/**` (outside `Assets/`) | `BMA_P1_*.blend` etc. |
| Blender scripts | `art/blender/scripts/*.py` | `bma_common.py`, `make_*.py` |
| Exported meshes | `Assets/Art/{Characters/P1,Characters/P2,Weapons,Environment}` | `BMA_P1_*`, `BMA_P2_*`, `BMA_Weapon_*`, `BMA_Utility_*`, `BMA_Env_*` |
| Built prefabs | `Assets/Art/<kind>/Prefabs` | same base name |
| URP materials | `Assets/Art/Materials` | palette names (`BMA_*`) |
| Shared-body rig | `Assets/Art/Rigs/SharedBody.prefab` | root GameObject must be `SharedBody` |

Blender sources live **outside** `Assets/` on purpose: only exported runtime assets enter the
Unity import pipeline, and artists keep editing the `.blend` files normally.

## 4. Rig, hierarchy and transform conventions

The production rig prefab is the single standardized shared body and is the same for every
combination. Anchors (Unity space, metres):

| Anchor | World position | Parent |
|---|---|---|
| `Hips` | (0, 0.95, 0) | root |
| `Chest` | (0, 1.35, 0) | Hips |
| `Neck` | (0, 1.55, 0) | Chest |
| `Head` | (0, 1.68, 0) | Neck |
| `ShoulderAnchor` | (0, 1.48, 0.06) | Chest |
| `WeaponAnchor` | (0, 1.35, 0.35) | Chest |
| `UtilityAnchor` | (0, 1.15, 0.22) | Chest |
| `P2CameraAnchor` | (0, 1.50, 0.16) | Chest |
| `P1CameraAnchor` | (0, 1.68, 0.06) | Neck |
| `Cosmetic_P1` | (0, 0, 0) | root |
| `Cosmetic_P2` | (0, 1.48, 0.06) | Chest |
| `Hitbox_Head` (critical) | Head | Head |
| `Hitbox_Body` | Chest | Chest |

- **P1 skins** are authored in **root space** (feet at 0) and mount at `Cosmetic_P1`.
- **P2 skins** are authored in **mount space** (origin = shoulder anchor, arms reaching the weapon
  anchor) and mount at `Cosmetic_P2`.
- The rig exposes `IM5RigAnimation` and `M5GameplayRigStats`; hitboxes carry `HitboxRegion`.
  Cosmetics are presentation-only and must carry **no collider, no hitbox and no gameplay stats** —
  enforced by `M5RigValidator`.

The single source of truth is mirrored in `art/blender/scripts/bma_common.py` (`RIG`) and
`Assets/Scripts/M6/Data/M6RigLayout.cs`.

## 5. Material conventions

Blender authors per-part materials and exports their **names**; Unity owns the look. The
`M6MaterialPalette` maps each name to a URP Lit material asset, so the visual look can be tuned
without re-exporting geometry. Palette: `BMA_Armor`, `BMA_Suit_Alpha`, `BMA_Suit_Beta`,
`BMA_Accent_Cool`, `BMA_Accent_Warm`, `BMA_Visor`, `BMA_Weapon_Metal`, `BMA_Grip`, `BMA_Concrete`,
`BMA_Env_Metal`, `BMA_Env_Accent`.

## 6. LOD expectations

This first pass is single-mesh **LOD0**; LOD1/LOD2 are authored in M7 when content scale begins.
The pipeline still enforces an LOD0 triangle budget so assets cannot silently balloon:
P1 ≤ 30k, P2 ≤ 20k, weapon ≤ 10k, environment piece ≤ 8k. Actual counts (all well under):
P1 2,876–6,612 · P2 7,892–8,108 · rifle 952 · grenade 1,940 · environment 368–756.

## 7. Rigging / skinning conventions (first pass)

The current production test is **segment/proxy rigged**: P1 and P2 are rigid cosmetic layers
mounted at fixed sockets, driven through `IM5RigAnimation`. No skinned mesh is authored yet.
When skinned characters are introduced, they must keep the same skeleton/socket names and the same
mount spaces, and continue to pass `M5RigValidator` and the combination test. Cosmetics are never
allowed to influence hitboxes or gameplay stats at any stage.

## 8. What the pipeline generates

Blender (`tools/pipeline/build-art.ps1`, headless `bpy`):

- P1 body skins: `BMA_P1_Ranger`, `BMA_P1_Brute`.
- P2 upper-body/arms skins: `BMA_P2_Scout`, `BMA_P2_Heavy`.
- Weapon/utility: `BMA_Weapon_Rifle`, `BMA_Utility_Grenade`.
- Environment kit: `BMA_Env_Wall`, `BMA_Env_Crate`, `BMA_Env_Platform`, `BMA_Env_Pillar`.

Unity (`Be My Arms > M6 > Regenerate Production Assets`) applies the import conventions, rebuilds
the material palette, the shared-body rig prefab, skin prefabs, weapon/utility prefabs and
environment prefabs. `Be My Arms > M6 > Validate Production Assets` runs the conventions and
contract checks; `M6PipelineCommands.Validate()` is the CLI form.
`Be My Arms > M6 > Build Production Showcase Scene` composes `Assets/Scenes/M6ProductionShowcase.unity`
with a mounted P1+P2 body, the rifle at the weapon anchor and the environment kit around it.

## 9. Verification / evidence

`M6AssetValidator` (also `Assets/Tests/EditMode/M6/M6PipelineTests.cs`) checks:

- every imported model obeys the naming, 1:1 scale, orientation and LOD0 budget conventions;
- the rig prefab satisfies `M5RigContract.Default()`, including all anchors;
- **every P1 skin combines with every P2 skin** (2 × 2 = 4) with valid contract, unchanged
  authoritative hitbox and gameplay-stat signatures, and independent P1/P2 identities;
- the rifle mounts at `WeaponAnchor` and the grenade at `UtilityAnchor` in each combination;
- cosmetic layers contain no hitbox and no gameplay-stat component.

Result: **validation PASSED**, `combinations valid: 4/4`, **EditMode 93/93**, **PlayMode 3/3**
(the existing M5 runtime mount proof still passes).

## 10. Open decision to surface (not silently locked)

The assets use a **first-pass stylized/readable direction** (teal/orange suit sets, dark armour,
cool/warm accents, glowing visors) purely to prove the pipeline. **The final art direction is still
an open product decision** (concept §20.1) and is not locked by M6. A product/art-director pass
should settle the final look before content scales in M7.

## 11. M7 handoff

M7 owns audio, VFX and the production map set. It should reuse this pipeline/conventions, add
LOD1/LOD2 authoring, and keep every new character/weapon passing the M6 validator and the M5
combination test. No DCC or asset-standard work should be redone from scratch.
