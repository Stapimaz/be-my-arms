# M2 — Networked Shared-Body Spike (NGO)

**Status:** **In progress — not complete.** All four remaining items were implemented and most were
verified in a real three-process run; two gaps remain before M2 can be closed (see §4).

**Scene:** `Assets/Scenes/M2NetworkingSpike.unity` · **Player:** `Builds/M2/M2.exe`
**Module:** `Assets/Scripts/M2` (`BeMyArms.M2`).

---

## 1. Implemented

- **Pure core:** `M2BodySim` (deterministic shared-body sim), `M2Reconciler` (client
  prediction/reconciliation), `M2WeaponState`, `M2LagCompensation`, `M2DelayQueue`,
  `M2RoleRegistry` (connection-to-role binding + token reconnect + double-role reclaim).
- **NET glue:** `M2NetworkBody` (server-authoritative, role-tagged RPCs, authorization, fire
  validation, lag-compensated hit test, latency/loss conditioner, metrics), `M2ClientPredictor`
  (role-aware client; prediction/reconciliation; local P2 aim; correction/no-snap measurement),
  `M2Bootstrap` (roles, conditioned network, **transport-level simulator**), `M2SceneBuilder`,
  `M2Build`.
- **Engine tick:** the server and client run on a **fixed 60 Hz step** (batch-mode players run
  uncapped fps otherwise, which desynced the sim and skewed metrics).

## 2. Verified — real three-process run

Dedicated server + separate P1 client + separate P2 client, as independent processes.

| Item | Evidence |
|---|---|
| Role assignment | `assigned client 1 -> P1 (token 'p1')`, `assigned client 2 -> P2 (token 'p2')` |
| **Authorization (wrong-role client)** | P1 client deliberately sent P2 input: server `unauth 820`, and those inputs never reached the sim |
| **Bandwidth** | `fromClients 1.8 KB/s toClients 2.1 KB/s (approx)` |
| **Prediction error** | app conditioner: `avg 0.47 / max 0.68`; transport sim: `avg 0.71 / max 1.58` (position m + yaw term) over 120 snapshots/s |
| **P2 camera correction** | in-sector correction `0.0°`; at the sector boundary a per-frame snap up to `9.0°` was observed (**exceeds the 5° target**) |
| **Transport-level simulation** | `[M2] transport simulator enabled: delay 50ms loss 2%` via the Multiplayer Tools runtime `NetworkSimulator`; server metrics show `delay 0ms loss 0%` (app conditioner off) yet correct behaviour — so UTP-level latency/loss works |
| Sector validation | `[M2-validation] rejected fire: aim … outside historical sector around …` |
| Lag compensation | `[M2-lagcomp] validated hit: … vs historical yaw …; target rewound to (…)` |
| Fire cadence / ammo / reload | rejected fires dominated by cadence; reload plumbed |
| Server CPU | `tick 0.01–0.05 ms` |
| Disconnect | `client N disconnected from role P1 (role freed)` |

EditMode tests: **38/38** (sim, reconcile, weapon, lag-comp, delay/loss, role registry incl.
double-role reclaim).

## 3. Transport-level vs application-level conditioning

Transport-level simulation is **feasible and preferred**, and was verified: the Multiplayer Tools
runtime `NetworkSimulator` component drives UTP's simulator in a built player (`packet delay/loss`
on the driver). The application conditioner (`M2DelayQueue`) is retained as an offline/testable
fallback and because it is deterministic in EditMode tests. Its limitations: it conditions only
the M2 messages (inputs/snapshots), not transport packets; it does not model jitter, reordering,
MTU or congestion; and it cannot exercise transport-level behaviours (e.g. UTP reliability,
fragmentation). The transport simulator removes those limitations for the runtime runs.

## 4. Remaining gaps (M2 cannot be closed until these pass)

1. **Reconnect / role restore at runtime is not verified.** The token/reclaim logic is implemented
   and unit-tested (including the double-role edge), and disconnect frees the role. But in the run,
   a killed P1 client relaunched with the same token **never connected** (the body was not present
   for it to register), so the `reclaim … dropping stale client` path did not execute. Late-join /
   reconnect needs a fix and a runtime test.
2. **P2 camera snap threshold is exceeded at the sector boundary** (max 9°/frame vs the 5° target).
   In-sector the aim is perfectly world-stable (0° correction), so this is a boundary-smoothing
   problem: the clamp moves with the body's (server-corrected) orientation. It needs either
   boundary smoothing or a reviewed threshold.

## 5. Notes

- The pure core is engine-free so it can be replayed in EditMode tests.
- Double-role edge: `M2RoleRegistry.Assign` reports the displaced connection so the server drops the
  stale owner before reclaiming the role.
