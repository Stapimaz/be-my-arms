# Development Environment

**Project:** `C:\Users\stapi\GameDev\be-my-arms`
**Last updated:** 2026-09-19

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
