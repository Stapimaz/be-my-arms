# Development Environment

**Project:** `C:\Users\stapi\GameDev\be-my-arms`
**Last updated:** 2026-09-20

This documents the tooling required to develop and verify Be My Arms, and the preferred
workflow: drive the **open Unity Editor through the Unity Pipeline package** rather than
spawning batch-mode editors.

---

## 1. Installed tooling

| Tool | Version | Notes |
|---|---|---|
| Unity Editor | `6000.4.3f1` (URP) | Installed via Unity Hub |
| Unity CLI (`unity`) | `1.0.0-beta.10` | `C:\Users\stapi\AppData\Local\Unity\bin\unity.exe` |
| Unity Pipeline package | `0.7.0-exp.1` | `com.unity.pipeline`, registry source |
| Input System | `1.19.0` | New Input System only |
| Test Framework | `1.6.0` | EditMode + PlayMode |
| .NET SDK | `8.0` | Independent verification of pure C# logic |
| Git | present | GitHub remote configured |
| Blender | `4.5.13 LTS` | Production DCC (M6). Pinned (version + SHA-256) in `tools/blender/BlenderVersion.json`; installed to `%LOCALAPPDATA%\BeMyArms\tools\blender` by `tools/blender/install-blender.ps1` (portable zip, no elevation) |

### Unity CLI agent skill for OpenCode

Installed at `.opencode/skills/unity-cli` (OpenCode discovers `.opencode/skills/<id>/SKILL.md`,
and also `.claude/skills` / `.agents/skills`). Unity's installer has no OpenCode client, so the
skill is materialized from `unity skill show`:

```powershell
unity skill show --list            # SKILL.md, CHANGELOG.md, SECURITY.md, references/*.md
unity skill show --path <file>     # print one file
```

**Encoding trap:** Windows PowerShell 5.1 decodes native-command stdout with the console code
page (here `ibm857`), which corrupts Unity's UTF-8 output into mojibake. Capture with an explicit
UTF-8 encoding instead — e.g. a .NET `ProcessStartInfo` with `StandardOutputEncoding = UTF8` (or
`[Console]::OutputEncoding = [Text.Encoding]::UTF8` before running) — and write each file as
UTF-8 without BOM. Verify that `SKILL.md` contains a real em dash (U+2014) and no `Ô`.

Verify OpenCode discovery by checking that `unity-cli` appears in the available-skills list and
that loading it reports a base directory of `.opencode/skills/unity-cli`.

---

## 2. One-time setup (already done)

```
unity pipeline install --project-path C:\Users\stapi\GameDev\be-my-arms
```

The editor must be **restarted** after the package is added so it resolves the package and
starts the server. If Unreal Package Manager reports `EPERM ... rename` while resolving,
close the editor, delete any `Library/PackageCache/.tmp-*` folders, and re-run a batch pass
(`Unity.exe -batchmode -quit -projectPath <project>`) before reopening.

---

## 3. Verify the environment

```powershell
unity pipeline list
```

Expected: `Pipeline = true`, a `Server Port` (e.g. `7800`), and `Server Reachable = true`.

```powershell
unity command console_status
```

Expected: `compilationFailed: false`.

---

## 4. Preferred workflow (no batch-mode editors)

All commands target the **running editor** via the Pipeline server on port 7800.

```powershell
# Discover tests without running them
unity command list_tests --mode EditMode
unity command list_tests --mode PlayMode

# Run tests
unity command run_tests --mode EditMode

# PlayMode runs asynchronously: the call returns immediately, then poll status.
unity command run_tests --mode PlayMode --async_tests true
unity command test_status

# Read editor console output / clear it
unity command console --tail 40
unity command clear_console
```

**Quirk:** `run_tests --mode PlayMode` returns `0/0 passed` immediately even when it
actually ran; the real result is reported by `test_status`
(e.g. `completed: 2/2 passed`). EditMode `run_tests` returns its result synchronously.

---

## 5. Package policy

Gameplay and networking packages are added **only when the current milestone requires
them**. M0 needed none. `com.unity.pipeline` is development tooling, not a gameplay
dependency.

Milestone-gated additions (see `ROADMAP.md`):

- **Chosen netcode (M0.5):** `com.unity.netcode.gameobjects` 2.13.2 and
  `com.unity.multiplayer.tools` 2.2.12. Netcode for Entities was rejected and removed from
  `main`; it remains isolated on branch `m0.5/nfe`. Decision record:
  `docs/M05_NETCODE_BAKEOFF.md`.
- Unity Transport — pulled in as a dependency of NGO.
- Multiplayer Services SDK (sessions/lobby/matchmaking) — **M4**.

**Bake-off isolation:** Netcode for GameObjects and Netcode for Entities define colliding
assembly names (`Unity.Netcode.Runtime`, `Unity.Netcode.Editor`), so they cannot be installed in
the same project. This is why the rejected candidate lives on a separate branch.

---

## 6. Production art pipeline (M6)

The DCC is **Blender 4.5 LTS**, installed reproducibly with a pinned version and checksum.

```powershell
# once per machine (portable zip, user-local, no elevation)
powershell -ExecutionPolicy Bypass -File tools/blender/install-blender.ps1

# regenerate all .blend sources (art/blender/blend) and FBX exports (Assets/Art)
powershell -ExecutionPolicy Bypass -File tools/pipeline/build-art.ps1
```

Then, in the editor: **Be My Arms > M6 > Regenerate Production Assets**, followed by
**Be My Arms > M6 > Validate Production Assets** (CLI: `M6PipelineCommands.Validate()`).

Coordinates, scale, naming, materials, LOD budgets and the rig/mount conventions are documented in
`docs/M6_ART_PIPELINE.md`. Blender sources live outside `Assets/`; only exported FBX and built
prefabs/materials enter the Unity project.

### M7 content (audio, VFX, maps)

```powershell
# procedural production-test SFX/music (uses Blender's bundled Python)
powershell -ExecutionPolicy Bypass -File tools/pipeline/build-audio.ps1
```

Then, in the editor: **Be My Arms > M7 > Regenerate Content** builds the map prefabs and arenas,
the audio library and the VFX prefabs/libraries; **Be My Arms > M7 > Validate Content** runs the map
and shippable-match-set checks (CLI: `M7PipelineCommands.Validate()`). See `docs/M7_CONTENT.md` and
`docs/M7_ART_DIRECTION.md`.

---

## 7. Runtime visual QA (the real Player build)

For player-facing UI and gameplay work, verify against the **actual built player**, not the editor.
A small set of `RuntimeOnly` Pipeline commands (in `Assets/Scripts/QA`, assembly `BeMyArms.QA`)
drives the production UI and its real callbacks and captures what the player actually rendered —
including **screen-space (overlay) UI**, which an editor camera capture misses.

| Command | Does |
|---|---|
| `qa_ui_state` | Active scene, screen resolution, canvases, and every active `Button` with its label, interactability and on-screen visibility. |
| `qa_capture_frame` | Renders the current player frame (overlay UI included) to a PNG and returns its absolute path. `--output` is absolute or relative to the player root; `--include_inline true` also returns base64. |
| `qa_click_button --name <GameObject name>` | Invokes the button's real `Button.onClick` callback (case-insensitive name). |

These are `RuntimeOnly`, so they are hidden from the running Editor's command listing and are
reached with `--runtime` / `--runtime-path`. They act on the shipped UI, so navigating through
them exercises the exact flow a player uses.

### Enable the runtime server in the dev build (one-time, per project)

`ProjectSettings/Packages/com.unity.pipeline/RuntimePipelineConfig.json` sets `enableInBuilds`:

```powershell
unity command set_runtime_pipeline_settings --settings '{"enableInBuilds":true}' --confirm true
```

**Security:** this starts an HTTP command server inside the Player. It is for **development/QA
builds only** and is never enabled in a shipping build (`M7GameBuild` builds `BuildOptions.Development`;
the build processor bakes the config only for development builds / `ENABLE_RUNTIME_PIPELINE`).
`M7GameBuild.BuildWindowsPlayer` mirrors the Development flag into
`EditorUserBuildSettings.development` for the duration of the build, because the Pipeline build
processor only bakes the config for a scripted build when it can tell the build is a development
build.

### Build, launch, connect

```powershell
# Build the normal playable game (canonical entry point; also Be My Arms > M7 > Build Playable Game).
unity command eval_file Temp/m7_build_eval.cs 3600000 --timeout 3600   # calls M7GameBuild.BuildWindowsPlayer()

# Launch the client and wait for its runtime descriptor.
Start-Process Builds\M7\BeMyArms.exe -WorkingDirectory Builds\M7
# descriptor: Builds\M7\.unity-pipeline-runtime-port  (pid, port, evalToken)

# Connect. --runtime-path takes the DIRECTORY that contains the descriptor, not the file.
unity command qa_ui_state --runtime-path Builds\M7
```

`unity command ... --runtime-path <build dir>` targets the Player, not the Editor.

**Dedicated-server collision:** a private match launches a second process of the same build, and
both write the *same* `.unity-pipeline-runtime-port` next to the exe. Before starting a match,
snapshot the client's descriptor into its own directory and drive the client through that copy:

```powershell
New-Item -ItemType Directory -Force Builds\M7\.qa-client | Out-Null
Copy-Item Builds\M7\.unity-pipeline-runtime-port Builds\M7\.qa-client\.unity-pipeline-runtime-port
# then use:  --runtime-path Builds\M7\.qa-client
```

### Worked flow (main menu → private lobby → 2v2 match)

```powershell
$R = "Builds\M7\.qa-client"
unity command qa_capture_frame --output "QA/01_main_menu.png" --runtime-path $R
unity command qa_click_button  --name PLAY        --runtime-path $R
unity command qa_capture_frame --output "QA/03_lobby.png"     --runtime-path $R
unity command qa_click_button  --name TwoVsTwo    --runtime-path $R
unity command qa_click_button  --name P2          --runtime-path $R
unity command qa_capture_frame --output "QA/04_lobby_2v2_p2.png" --runtime-path $R
unity command qa_click_button  --name Start       --runtime-path $R
unity command qa_ui_state      --runtime-path $R          # → M7TwoVsTwoArena + M7MatchHud canvas
unity command qa_capture_frame --output "QA/05_match_hud.png" --runtime-path $R
```

Captures default to `<player root>/QA/` (`Builds/M7/QA`, gitignored). **Read the PNGs back with
the agent's image-capable tools** and treat them as the acceptance evidence — code inspection and
green tests do not prove the pixels. `qa_ui_state`'s per-button `onScreen` flag is the quick
numeric check (an off-screen button reports `onScreen:false` and a screen centre far outside
`Resolution`).

### Diagnosing runtime UI without a rebuild

`eval` / `eval_file` work in a desktop development Player, so a live layout can be probed or
temporarily mutated (then rebuilt by navigating) to reproduce a suspected defect before changing
source. Example: `unity command eval_file Temp/qa_break_menu.cs --runtime-path Builds\M7\.qa-client`.
