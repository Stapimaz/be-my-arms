# M0.5 — Netcode Bake-off Record

**Status:** Complete. Decision: **Netcode for GameObjects (NGO)**, with an explicitly budgeted
custom prediction/lag-compensation layer.
**Date:** 2026-09-19
**Candidates:** NGO 2.13.2 (+ Multiplayer Tools 2.2.12) on `main`; Netcode for Entities (NfE)
1.14.3 on branch `m0.5/nfe`.

---

## 1. Method

Both candidates were given the same minimum loop and the same role split:

- one authoritative shared body;
- two role-tagged input domains on that one entity — **P1** (move + `BodyYaw`) and **P2**
  (sector-clamped world aim + fire);
- server-authoritative hitscan against a dummy;
- 60 Hz;
- auto-driven synthetic input so the loop runs without a human.

**Candidate isolation is mandatory.** NGO and NfE define colliding assembly names
(`Unity.Netcode.Runtime`, `Unity.Netcode.Editor`); installing both produced four
`Assembly with name ... already exists` errors. They were therefore evaluated in separate
projects/branches, and the final NGO/`main` state contains only the chosen stack.

---

## 2. Candidate A — Netcode for GameObjects

**Delivered:** `Assets/Scripts/M05/Ngo/*`, generated `Assets/Scenes/M05Ngo.unity` and
`NgoBakeoffBody.prefab`. Role via `-bakeoff-role server|client|host`.

**Observed (Editor, Host mode, driven through the Unity Pipeline):**

- The two role domains are bound to one entity with a single `BakeoffInput` packet sent over
  `SubmitInputServerRpc` (`RequireOwnership = false`). The server applies P1 `BodyYaw` + motion
  and P2 world aim (clamped to the sector) + pitch, then fires. It works.
- **Server-authoritative hitscan verified:** the dummy's health went `100 → 82 → 64 → 46 → 28 →
  10 → 0`, after which shots reported "hit geometry" as the aim left the dead target.
- `bootstrap role=Host port=7777 tickRate=60`, body spawned, no compile or runtime errors.
- **No built-in prediction, reconciliation, or lag compensation.** The client sends input and
  observes the server result; there is no input redundancy/slack for packet loss, no rollback,
  no hitbox history.
- **Integration cost with the existing project: negligible.** Reused the existing
  `CharacterController`, the M0 `AimSector` math, and the M0 damageable/hitbox types. One
  prefab and one scene, both generated from code.
- **Dedicated server:** standard headless build; the prototype already switches role by
  command-line argument.

---

## 3. Candidate B — Netcode for Entities

**Environment:** installed on `m0.5/nfe`; resolved and compiled (`compilationFailed: false`).

**Confirmed provided infrastructure (from the package API surface):**

- automatic client/server world creation (`ClientServerBootstrap`, `ClientServerWorld`);
- a command/input pipeline (`IInputComponentData`, `GhostInputSystemGroup`,
  `CommandSendSystemGroup`) with **input redundancy + latency slack** explicitly designed for
  packet loss;
- ghost snapshots, ghost prediction/rollback, and `GhostPrefabCreation` for runtime prefab
  conversion;
- native dedicated-server worlds.

**Observed cost:**

- Requires a DOTS/ECS gameplay architecture with SubScene authoring/baking, replacing the
  GameObject/Mecanim pipeline — including the two-layer rig, IK, and P1×P2 skin-mounting system
  that is a core product differentiator — with a custom ECS pose/hitbox pipeline.
- Adds a significant DOTS learning/ramp cost.
- **Not completed:** a running NfE ghost shared-body loop. Authoring/baking a SubScene ghost was
  outside what a bounded bake-off could responsibly finish in one pass, so the NfE candidate is
  recorded as **environment + infrastructure evidence only**, not a running prototype. This is a
  limitation of this record.

---

## 4. Comparison against the decision criteria

| Criterion | NGO | NfE |
|---|---|---|
| Two roles / connections control one body | Works (single input packet, server-applied) | Supported (input + command pipeline), not run |
| P1 prediction/reconciliation at 100 ms / 2% loss | **Not provided** — must be built | Provided (ghost prediction/rollback) |
| P2 aim local and responsive | Manual (client-side presentation); no rollback | Provided (predicted ghosts) |
| Server-side sector/fire validation | Manual, straightforward | Manual systems, but with prediction compare |
| Custom engineering to shooter-grade lag comp | **High** (input buffer, replay, hitbox history, rewind) | **Low for networking**, high for the ECS pose/hitbox rebuild |
| Dedicated-server workflow | Standard headless build, low friction | Native server worlds, higher setup |
| Integration with GameObject / animation / IK / skin pipeline | **Native, zero migration** | **Full DOTS migration required** |
| Cannot coexist with the other candidate | Confirmed | Confirmed |

---

## 5. Decision

**Use Netcode for GameObjects, and budget a custom prediction / reconciliation /
lag-compensation layer for the M2 networking spike.**

Rationale, tied to the criteria above:

1. **The product differentiator is the body/animation/skin system, not netcode plumbing.**
   NfE's networking advantages are real, but they come bundled with a full DOTS migration and a
   custom ECS animation/IK/hitbox pipeline that directly fights the two-layer rig and P1×P2 skin
   compatibility this game is built around. NGO keeps that system native.
2. **Scale is tiny (4–8 players).** NGO's scale limits are irrelevant here, so NfE's scaling
   strength buys nothing this project needs.
3. **The remaining engineering is bounded and well understood.** Prediction/reconciliation,
   input redundancy/slack, and lag-compensated hit validation with body-yaw history are a known
   body of work, and proving exactly that is the purpose of M2. NfE only removes that work while
   adding a larger, riskier migration.
4. **Integration cost is measured, not assumed:** the NGO candidate ran in the existing project
   in one pass with zero migration; NfE could not even share the project and requires a SubScene
   rewrite.
5. **Dedicated servers** are straightforward for both; NGO's friction is lower.

**Caveat:** the NfE candidate was not run as a live ghost loop (§3). The decision rests on the
measured NGO result, the measured candidate incompatibility, and NfE's documented infrastructure
plus its architectural cost. If a live NfE ghost run is ever required to revisit this, resume
branch `m0.5/nfe`.

---

## 6. Remaining engineering handed to M2

- P1 client-side prediction + reconciliation (input/state ring buffer, replay, correction
  clamping) with a presentation smoothing layer that never snaps P2's camera.
- Input redundancy/slack so 2% packet loss does not stall P1 movement.
- Lag-compensated, server-authoritative hit validation that rewinds targets, validates the aim
  sector against the **historical body orientation**, and clamps maximum rewind.
- Server-authoritative fire cadence, ammo, weapon state, and sector/turn-rate validation.
- Headless dedicated-server build plus reconnect grace handling.

---

## 7. Artifacts

- `main`: chosen NGO stack, NGO bake-off prototype (`Assets/Scripts/M05/Ngo`), M0.5 packages.
- `m0.5/nfe`: isolated NfE environment setup only (unmerged, by design).
