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

The Unity CLI agent skill has **no OpenCode target** (supported clients: claude-code,
claude-desktop, grok, cursor, windsurf, vscode, cline, codex). The CLI is usable directly;
run `unity skill show` for its task guide.

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
them**. At M0 that means: no Netcode for GameObjects, no Netcode for Entities, no transport
packages. `com.unity.pipeline` is development tooling, not a gameplay dependency.

Milestone-gated additions (see `ROADMAP.md`):

- Netcode for GameObjects / Netcode for Entities — **M0.5** bake-off.
- Unity Transport — **M0.5/M2** with the chosen stack.
- Multiplayer Services SDK (sessions/lobby/matchmaking) — **M4**.
