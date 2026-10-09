# Development, Run and Build

**Practical current workflow · Updated 2026-10-09**

Start with [README.md](../README.md). Product design, sequence and architecture are the three
canonical root documents; historical reports are not setup instructions.

## Setup

- Unity **6000.4.3f1**, Windows build support, URP project. `ProjectSettings/ProjectVersion.txt`
  and `Packages/{manifest,packages-lock}.json` pin the project.
- Git + **Git LFS**. Fetch LFS content before opening/building; pointer files are not usable meshes.
- Unity CLI (`unity`) and the project's **com.unity.pipeline 0.7.0-exp.1** for live Editor commands.
- Python 3 for maintenance scripts. Blender **4.5.13 LTS** for DCC work, pinned with SHA-256 in
  `tools/blender/BlenderVersion.json`. No Blender regeneration is needed for an ordinary checkout/build.

```powershell
git lfs install
git lfs pull
unity open . --format json
unity status --format json
unity command --format json  # discover this Editor's commands
```

The repository is `https://github.com/Stapimaz/be-my-arms.git`. Unity service project identifiers
are preserved as existing project configuration; they are not proof of deployed accounts/services.
Generated solutions, Library, Logs, UserSettings, builds and QA output are ignored.

## Preferred Editor workflow

Use the connected Editor instead of editing prefab/scene/asset YAML. If commands cannot connect,
check `unity pipeline list` and compile/Safe Mode before assuming the Editor is closed.
With multiple Editors, add `--project-path <project>`.

```powershell
unity command recompile --format json
unity command recompile_status --format json
unity command console_status --format json
unity command run_tests --mode editor --timeout 180 --format json
```

After source edits, ensure AssetDatabase has refreshed before trusting compilation status.
The Editor's compile result, not a CLI acknowledgement that compilation was queued, is the verdict.
PlayMode tests are optional when the changed risk requires them; use command discovery and
`test_status` for asynchronous results rather than treating a queued 0/0 result as a pass.

## Build the playable Windows player

Canonical entry: `BeMyArms.Client.EditorTools.GameBuild.BuildWindowsPlayer`, also available under
**Be My Arms → Client → Build Playable Game**.

```powershell
unity command eval_file tools/build/build-player.cs 3600000 --timeout 3600 --format json
```

Output: **`Builds/Windows/BeMyArms.exe`**, with its entire accompanying folder. Scene zero is
`MainMenu`, followed by `Boatyard`, `DuelArena` and `TwoVsTwoArena`. Duel from the menu opens
Boatyard; the old arena remains available by explicit launch flags. The same binary acts as client and local
headless dedicated server. The entry builds Development and preserves the runtime Pipeline
development flag correctly. Never enable the runtime command endpoint in public release builds.

The eval positional budget is **milliseconds**; the CLI `--timeout` is **seconds**. A queued
asynchronous build requires a final `build_status` result before it is called successful.

## Human playtest

```powershell
Start-Process 'Builds/Windows/BeMyArms.exe' -WorkingDirectory 'Builds/Windows'
```

**PLAY → Duel → P1 or P2 → Start Match** for solo with a bot in the other role.
The lobby also supports a two-human shared-body practice session. Do not open a scene in
Editor and assume it covers the built menu's dedicated-server allocation behavior.

Current bindings are development bindings:

| Role / context | Inputs |
|---|---|
| P1 | WASD, mouse look, sprint Shift, crouch Ctrl, jump Space, dodge Q, slide C, light/heavy kick F/V, align Left Alt |
| P2 | Mouse aim, LMB fire, R reload, grenade/smoke/flash G/T/Y, Tab turn request |
| Practice | F6 encounter reset, F7 role exchange/reset, F8 fresh match |
| UI | Esc pause/unlock; gameplay edges resume through explicit focus/input mode |

There is no standalone vault binding. Exact bindings are read by `NetworkBodyClient` through
the `LocalInput` state and may be
revisited in an approved input/onboarding phase.

## Command-line development sessions

Current flags use domain names, not development-phase numbers:

```powershell
# Direct dedicated Duel (choose a free port)
Start-Process 'Builds/Windows/BeMyArms.exe' -ArgumentList '-batchmode -nographics -match-role server -client-arena Boatyard -queue-mode duel -queue-matchmaker 0 -match-port 7790 -match-required-players 2 -match-start-delay 0 -match-practice 1 -match-strict-slots 1 -match-delay 0 -match-loss 0'
# Human role client, using the same build
Start-Process 'Builds/Windows/BeMyArms.exe' -ArgumentList '-client-arena Boatyard -client-join 127.0.0.1 -client-port 7790 -client-join-role p1'
```

The menu is the normal human entry; flags are for repeatable development sessions. The normalized
build must be used on both sides. Old flags/scene names are historical launch interfaces, not the
canonical workflow; use the current scripts rather than copying commands from archives.
To revisit the old checkpoint, use `-client-arena DuelArena` on **both** server and clients;
there is no runtime map selector or scene auto-synchronization in this iteration.

## Risk-directed verification

```powershell
# Imports, missing scripts and unresolved GUIDs; no Play mode or asset regeneration
unity command eval_file tools/maintenance/audit-unity-references.cs 30000 --timeout 120 --format json
# Menu/startup/shared-body P1/P2 Duel + rendered crosshair projection/firing smoke (no screenshots)
powershell -NoProfile -ExecutionPolicy Bypass -File tools/qa/smoke-duel.ps1
# Current Boatyard look sample: same disposable smoke plus rendered lighting/grading import checks
powershell -NoProfile -ExecutionPolicy Bypass -File tools/qa/smoke-duel.ps1 -CheckLookSample
# Optional: save this disposable P2's real rendered frame in Builds/Windows/QA/DuelSmoke;
# not a desktop/human-session screenshot or an automated art acceptance requirement
powershell -NoProfile -ExecutionPolicy Bypass -File tools/qa/smoke-duel.ps1 -CheckLookSample -CaptureLookReview
# Camera follow-up only: isolated real P2 + P1-bot frame trace and role-switched 2.8 m P1 rig;
# no full smoke repetition or human-session mutation (not human smoothness/framing acceptance)
powershell -NoProfile -ExecutionPolicy Bypass -File tools/qa/check-camera-motion.ps1
# Broader session/peer-loss regression: use when ownership/lifecycle/Transport changes
powershell -NoProfile -ExecutionPolicy Bypass -File tools/qa/test-session-lifecycle.ps1 -BuildDirectory Builds/Windows
# Rifle geometry/damage/feedback + P1-motion spread: real role RPCs and server ticks
powershell -NoProfile -ExecutionPolicy Bypass -File tools/qa/test-rifle-combat.ps1
# Isolated Easy/Hard bot server ticks: bounded aim, real shots, occlusion and reset
powershell -NoProfile -ExecutionPolicy Bypass -File tools/qa/test-bot-controller.ps1
```

Run runtime scripts **sequentially**: multiple processes share the build directory's Pipeline
descriptor. `--runtime-path` takes the directory containing `.unity-pipeline-runtime-port`, not
the descriptor file. Each script snapshots its own process's descriptor, checks results and
cleans up its processes. Runtime QA needs a Development build.

For small Boatyard art/mesh edits, compile and run the focused `BoatyardTests` filter instead of
the whole EditMode suite. The accepted layout is not regenerated. When changed lighting needs
rebaking, open **only** the saved Boatyard scene (no Play mode/additive scenes), then run
`unity command eval_file tools/maps/bake-boatyard-look.cs 3600000 --timeout 3600 --format json`.
Inspect its actual lightmap/probe/capture result before one final playable build. Bake artifacts
live under `Assets/Scenes/Boatyard`; texture provenance is in
`Assets/Art/Maps/BoatyardLookSample/SOURCES.md`. Visual direction is accepted by human feedback,
not this runtime import check. `BoatyardLookSample.Author()` is a guarded one-time operation,
not a routine bake/build prerequisite.

The rifle test creates isolated headless processes, freezes its disposable combat/round/zone clocks,
then submits real role-authenticated input and advances the ordinary server tick. It checks
history/cover/region damage, P1-motion/slide spread/stop/recovery, accepted crosshair burst metadata
and the events received by both roles;
it does not evaluate pixels
or balance. Never run `rifle-combat-fixture.cs` against a human playtest session.

The bot-controller check likewise launches its own disposable bot-filled server. Its controlled
fixture advances ordinary server ticks, crosses target directions and then occludes a moving
target. It checks actual bot aim/firing and reset state, not cosmetic camera pixels. Never run
`check-bot-controller.cs` against a human session. Reachability/stance/slide cases live in
`BotDecisionTests`, including translated layouts, ceilings, ramps, corners and the closing zone.

Prefer `qa_player_state`, `qa_ui_state` and focused input commands for objective debugging.
No screenshot is required to prove startup/ownership. Capture images only to answer an actual
visual question; **human review, not automated image inspection, accepts visual/feel quality**.

## Content work

- `tools/blender/install-blender.ps1` installs the pinned user-local Blender build.
- `tools/pipeline/build-art.ps1` generates the original modular art/kit samples; it is **not**
  a command to replace the accepted SharedRig character/POV.
- `tools/pipeline/build-smooth-fighter.py` adapts licensed source meshes; importing/rebaking
  SharedRig animations is explicit content work, not ordinary setup.
- `SmoothFighterBuilder.BuildArms()` authors the rifle POV only. World grips use their own
  path; don't re-run a historical character generator to adjust a first-person composition.
- `AssetPipeline` and `ContentPipeline` provide import/build/validation menus under Content/Client.
  Full regeneration overwrites authored data: inspect scope before invoking it.
- Source/provenance stays with art and third-party packages. Keep sourcing free-only.

The embedded Transport patch is essential to the existing disconnect path. Its note describes
the upstream ownership defect and when it is safe to remove; normal setup must not replace it.
