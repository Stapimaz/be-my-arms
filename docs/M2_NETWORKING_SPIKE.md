# M2 — Networked Shared-Body Spike (NGO)

**Status:** In progress. Core role-tagged input, deterministic shared-body simulation, client
prediction/reconciliation and server sector validation are implemented and automatically
verified; the host-mode loop runs clean. The two-process run, lag-compensated hit rewind,
reconnect and bandwidth/CPU measurements remain.
**Scene:** `Assets/Scenes/M2NetworkingSpike.unity` (built by `Be My Arms > M2 > Build M2 Scene`)
**Module:** `Assets/Scripts/M2` (`BeMyArms.M2`), NGO on the chosen stack.

---

## 1. Implemented

**Pure, testable core (no UnityEngine types):**
- `M2BodySim` — deterministic shared-body simulation. Same orientation model as M1: decoupled
  look with a neck limit, smooth body follow, explicit align; movement and P2's sector use
  `BodyYaw` only. Also clamps P2's desired world aim to the sector.
- `M2Reconciler` — client prediction/reconciliation: predict locally from inputs, and on each
  authoritative snapshot reset to the server state at the last acknowledged input sequence and
  replay the not-yet-acknowledged inputs. This is the custom layer NGO does not provide.

**NGO glue:**
- `M2NetworkBody` — server-authoritative body. Two **role-tagged** ServerRpcs
  (`SubmitP1ServerRpc`, `SubmitP2ServerRpc`) merge into one entity. Replicates `M2BodyState` plus
  `LastAckedP1Sequence` / `LastAckedP2Sequence`. Rejects a P2 fire whose aim is outside the
  sector around the current body orientation.
- `M2ClientPredictor` — client-side prediction and reconciliation for the P1-owned portion; local
  P2 aim; presentation transform from the predicted state.
- `M2Bootstrap` — host / dedicated-server / client role via `-m2-role server|client|host`;
  60 Hz.
- `M2SceneBuilder` — generates the body prefab and scene from code.

---

## 2. Verified

- **EditMode 30/30**, including six M2 tests: simulation determinism; looking inside the
  threshold does not turn the body while movement stays body-relative; explicit align; P2 aim
  sector clamp; reconciliation replay has no drift; reconciliation corrects toward a divergent
  server state.
- **PlayMode 2/2**.
- **Host-mode runtime smoke:** `bootstrap role=Host port=7778 tickRate=60`, body spawned,
  `consoleErrors: 0`.

---

## 3. Pending (not yet done — do not mark M2 complete)

- A real **two-client + dedicated headless server** run.
- **Network Simulator at 100 ms RTT + 2% packet loss**, and the reconciliation / visual-correction
  thresholds.
- **Lag compensation:** server history of target hitboxes and body orientation, rewind on fire,
  and a max-rewind clamp. Currently only current-orientation sector legality is validated; no
  hitbox rewind exists yet.
- **Fire validation beyond the sector:** cadence, ammo and weapon-state checks.
- **Reconnect** restoring the role, and the disconnected role contributing no input.
- **Bandwidth / CPU measurement** via the Multiplayer Tools profiler.
- **P2 camera no-snap** threshold verification.

---

## 4. Notes

- For local runs, the single host client sends both role streams. The RPCs and the server-side
  merge are role-tagged, so two real connections map onto the same code path; a two-process run is
  still required to exercise prediction under real latency.
- The pure core is intentionally engine-free so it can be replayed in tests and later be driven by
  a rollback/prediction loop without UnityEngine dependencies.
