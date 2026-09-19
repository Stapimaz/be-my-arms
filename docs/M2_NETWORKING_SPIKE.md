# M2 — Networked Shared-Body Spike (NGO)

**Status:** **In progress — not complete.** The real three-process run (dedicated server + separate
P1 client + separate P2 client) now runs and verifies role authorization, conditioned
latency/loss, prediction/reconciliation, fire/sector validation and lag-compensation rewind.
Still missing: a conclusive reconnect/role-restore verification, a bandwidth measurement, and a
quantified prediction-error metric.
**Scene:** `Assets/Scenes/M2NetworkingSpike.unity` · **Player:** `Builds/M2/M2.exe`
**Module:** `Assets/Scripts/M2` (`BeMyArms.M2`).

---

## 1. What is implemented

- **Pure, testable core:** `M2BodySim` (deterministic shared-body sim: decoupled look, neck limit,
  body follow, explicit align; movement and sector use `BodyYaw`), `M2Reconciler` (client
  prediction + reconciliation with input-sequence acks and replay), `M2WeaponState` (cadence,
  magazine, reload), `M2LagCompensation` (historical body yaw + target position history, clamped
  rewind), `M2DelayQueue` (delay + loss conditioner).
- **NET glue:** `M2NetworkBody` — server-authoritative body; role-tagged `SubmitP1ServerRpc` /
  `SubmitP2ServerRpc`; **connection-to-role authorization** (`RegisterServerRpc`, token-based
  binding, unauthorized-counter); fire validation (cadence/ammo/reload/sector); lag-compensated
  fire against the rewound target; **application-level latency/loss conditioner**; server metrics.
  `M2ClientPredictor` — role-aware client (P1 predicts/reconciles with presentation smoothing;
  P2 keeps aim local), snapshot delay/loss conditioning. `M2Bootstrap` — roles + conditioning from
  command-line args so a dedicated server and two role clients run as independent processes.
- `M2SceneBuilder` (scene/prefab) and `M2Build` (Windows player).

**Approach kept:** server-authoritative snapshots + client prediction. Not deterministic lockstep.

---

## 2. Verified by a real three-process run

Run: dedicated server + P1 client + P2 client as separate processes, each launched with
`-m2-delay 50 -m2-loss 2` (≈100 ms RTT: 50 ms server-side input delay + 50 ms client-side snapshot
delay, plus 2% drop; the UTP debug simulator is deprecated and the Multiplayer Tools simulator is
editor-only, hence the application conditioner).

| Item | Evidence |
|---|---|
| Role assignment | server log: `assigned client 1 -> P1`, `assigned client 2 -> P2` |
| Connection-to-role authorization | `unauthorized 0` for the whole run (a P1 connection's P2 input, and vice versa, would increment it) |
| Prediction/reconciliation under latency+loss | P1 client predicts every tick and reconciles on delayed snapshots; loop runs with 0 errors |
| Fire cadence / ammo / reload | rejected fires dominated by cadence (client fires every tick, weapon cadence 8/s); reload plumbed |
| Sector validation | `[M2-validation] rejected fire: aim … outside historical sector around …` |
| Lag compensation | `[M2-lagcomp] validated hit: aim … vs historical yaw …; target rewound to (…)` — validated hits accumulate (≈116+ in ~25 s) |
| Server CPU (tick) | `[M2-metrics] avg server tick 0.01 ms` |
| Disconnect | `client 1 disconnected from role P1 (role freed)` |

EditMode tests: **33/33** (M2 adds sim determinism, look/follow/align, sector clamp, reconcile
no-drift, reconcile correction, weapon cadence/ammo/reload, lag-comp rewind + clamp, delay/loss).

---

## 3. Still to do before M2 is complete

- **Reconnect + role restoration:** token-based restore is implemented, but the automated test was
  inconclusive — after a process kill, disconnect detection lagged and the role-restore path did
  not produce a clean `reconnect … restored P1`. Also a duplicate-role edge exists when a client
  reconnects before the old connection's timeout; this needs a dedicated fix + test.
- **Bandwidth measurement:** only server tick CPU is measured. Per-client bandwidth is not yet
  instrumented.
- **Prediction-error metric:** reconciliation runs, but the client-vs-server position/yaw error
  under latency is not quantified, and the P2 camera no-snap threshold is not measured.
- **Two-role topology per connection:** locally the host sent both role streams; the three-process
  run now uses separate role clients, but the authorization check should also be exercised by a
  malicious-client test (send the wrong role deliberately).

## 4. Notes

- The pure core is engine-free so it can be replayed in tests and driven by the prediction loop
  without UnityEngine dependencies.
- The conditioner is application-level; it exercises prediction/reconciliation behaviour, not
  transport-level packet dynamics.
