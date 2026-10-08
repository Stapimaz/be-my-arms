# Repository structure and normalization checkpoint

**Pre-Phase-3 · 2026-10-08**

## Current layout

```text
Assets/
  Scripts/
    Core/             aim/input/health primitives and local-control support
    Gameplay/         local movement/combat helpers and accuracy model
    Networking/       simulation, prediction, history/ownership and prediction sample
    Match/            NGO integration, combat, bots, sessions and round rules
    Matchmaking/      pairing, roles, ratings and allocator seam
    Product/          account/cosmetic/social seams and rig contract
    Content/          asset descriptors, conventions and authoring tools
    Client/           app/session, cameras/animation, UI, maps, audio and VFX
    Diagnostics/      isolated netcode comparison and Transport probe
    QA/               development-only runtime commands
  Tests/{EditMode,PlayMode}/  matching domain names
  Scenes/             MainMenu, DuelArena, TwoVsTwoArena
    Samples/          older control/combat/prediction/match/asset/probe scenes
  Art/Characters/
    SharedRig/        accepted shared skeleton, role meshes, clips, POV and provenance
    Samples/          SegmentRig and SkeletalStudies, not the active character
    P1/, P2/          original modular content/rig-contract test assets
art/blender/blend/
  SharedRig/          current editable character sources
  Samples/SegmentRig/ older editable study; other kit sources remain by domain
tools/
  build/              canonical playable build entry
  qa/                 minimal Duel smoke and session/peer-loss regression
  maintenance/        objective reference/import audit
  pipeline/, blender/ supported DCC/content tooling
  archive/            one-time migrations and old investigation/generation scripts
docs/
  DEV_ENVIRONMENT.md  current practical run/build guide
  REPOSITORY_STRUCTURE.md
  archive/            historical evidence, not current design or acceptance
```

The original runtime assembly dependency graph is retained under domain names. A domain folder
is not an instruction to rewrite its subsystem; see `TECHNICAL_PLAN.md` for boundaries and the
distinction between the shipping match path and local/diagnostic samples.

## Intentional migration

| Former source group | Current responsibility |
|---|---|
| M0 / M1 | Core / Gameplay |
| M2 / M3 | Networking / Match |
| M4 / M5 | Matchmaking / Product |
| M6 / M7 | Content / Client |
| M05 NGO / NetProbe | Diagnostics/NetcodeComparison / Diagnostics/NetworkingProbe |
| Pass2Redo / Pass2 character assets | SharedRig / Samples/SegmentRig |

Numbered prefixes were removed from script/type/assembly names. Important names now include
`BodySim`, `NetworkBody`, `NetworkBodyClient`, `MatchDirector`, `MatchConfig`, `RifleHandling`,
`LocalPlayer`, `CharacterAnimator`, `RiflePose`, `SmoothFighterBuilder` and `GameBuild`.
Serialized field names and packet layouts are retained; type-renaming collisions were resolved
with domain-specific names such as `WeaponType`, `AssetWeaponKind` and `MatchmakingSlot`.

Unity asset moves retain `.meta` GUIDs. Renamed MonoBehaviour/ScriptableObject types carry
`MovedFrom` attributes naming their original assemblies/classes. These old strings are
**intentional migration compatibility**, not active development naming. Scriptable scene names,
resource keys, editor constants, reflection strings, asmdefs and build settings were migrated
together; original prefab/scene identities remain stable.

The current launch flags are `-match-*`, `-queue-*` and `-client-*`. Old command snippets and
old player binaries are not the supported normalized interface. Build and run both server/client
from the same normalized source; RPC/type identity changes are not a backwards network protocol
compatibility guarantee.

## What was archived or retained

- Former milestone/pass reports and probe source copies moved to `docs/archive/milestones`.
  Superseded concept/roadmap/technical-plan snapshots are in `docs/archive/planning`.
- One-off pose, animation, screenshot and rebuild snippets moved to `tools/archive/investigations`.
  Earlier character generators moved to `tools/archive/character-studies`.
- The obsolete **Netcode for Entities** project-settings file was preserved in
  `docs/archive/configuration`, outside active `ProjectSettings`. NGO is the chosen stack.
- Prototype scenes and study assets remain available, with their GUIDs, outside the playable
  scene set. No broad deletion of historical code/art or downloaded licensed sources was used.
- Existing URP imports, Input System preload, service identifiers, IDE setup and the already
  self-deleted temporary Hub resolver were checkpointed rather than reverted.
- Pipeline's temporary build-injected Resources assets are ignored, not all Resources content.
  Existing folder metas are preserved; authored Resources content remains in SharedRig.
  Runtime Pipeline settings remain tracked and development-only.
- Original modular kit assets and rig-contract tests remain; they still exercise useful
  compatibility/content responsibilities despite not being the current character.

## Reference-audit exceptions and metadata repair

The pre-move audit found four environment prefabs (and their showcase instances) with an
already-null **environment descriptor script**. Two component types had shared a source
filename. `WeaponDescriptor` retains the original script GUID; `EnvironmentDescriptor` now
has its own matching file, and only the four missing, non-gameplay metadata roots were repaired.
Mesh, transforms, colliders and authoritative gameplay were not changed by that repair.

The raw serialization audit also found **11 pre-existing obsolete URP references**: four
orphaned test/removed effect script GUIDs in `DefaultVolumeProfile.asset`, and seven legacy
probe-debug resource GUIDs in `PC_Renderer.asset`. They remain recorded as warnings rather
than being hidden. Unity reserialization did not remove them; this pass does not replace
the renderer/profile or alter their active effects merely to obtain a zero-warning report.
There are no missing scripts in the current scene/prefab paths after normalization.

Unity reserialization also removed eight legacy SSAO shader/noise dependency entries from
`PC_Renderer.asset`. URP 17.4's SSAO no longer serializes those private fields; it loads these
resources from GraphicsSettings. The feature/settings remain unchanged. The preservation
check explicitly records this reviewed schema-upgrade exception and rejects other losses.

## Checkpoints and validation

- `c514829`: accepted P2 rifle POV committed before restructuring.
- `checkpoint/pre-phase-3-playable`: accepted playable source plus existing environment/import state.
- `checkpoint/pre-phase-3-normalized`: final domain-organized baseline after validation.

Completed objective checks:

- Editor compilation succeeded. All **557 original GUIDs** remain in tracked `.meta` files;
  no new reference errors or unexplained dependency loss across **132 serialized assets**.
- **179/179** existing EditMode tests passed after normalization.
- Windows Development build succeeded with **0 errors, 1 warning**.
- **7/7** minimal menu/core Duel smoke checks passed, including rendered P2 viewmodel loading,
  exact shared-body roles, server-observed P1 movement and authoritative P2 ammo/shot state.
- **22/22** existing headless session regression checks passed: sector bounds/return, firing,
  crouch/movement, reset/role exchange, peer-loss reconnect, bot removal/replacement, fresh
  menu-owned sessions, server shutdown and owner-crash watchdog.

The build was refreshed after final hierarchy-name cleanup. No screenshot/subjective visual
pass was used. Reports live in ignored `Builds/Normalization` and the build's `QA` directory;
the compact reference-verification record is retained in `docs/archive/normalization`.
These are correctness evidence, **not subjective visual or feel acceptance**. Phase 3 gameplay
changes are not part of this checkpoint.
