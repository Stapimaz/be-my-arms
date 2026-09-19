# M2 — Networked Shared-Body Spike (NGO)

**Status:** **In progress — not complete.** Every item except a verified runtime reconnect passes.
**Scene:** `Assets/Scenes/M2NetworkingSpike.unity` · **Player:** `Builds/M2/M2.exe`

---

## 1. Implemented

- **Pure core:** `M2BodySim`, `M2Reconciler`, `M2WeaponState`, `M2LagCompensation`, `M2DelayQueue`,
  `M2RoleRegistry` (binding + token restore + double-role reclaim).
- **NET glue:** `M2RoleService` (role assigned during **NGO connection approval** — independent of
  the replicated body, which fixes the earlier "late joiner cannot register" problem),
  `M2NetworkBody` (server-authoritative, role-tagged RPCs, authorization, fire validation,
  lag-compensated hit test, conditioner, metrics), `M2ClientPredictor` (role-aware prediction /
  reconciliation, local P2 aim, camera-correction measurement, boundary smoothing),
  `M2Bootstrap`, `M2SceneBuilder`, `M2Build`.
- **Fixed 60 Hz step** on server and client (batch players otherwise run uncapped).

## 2. Verified — real three-process run

| Item | Result |
|---|---|
| Role assignment via approval | `approved client 1 -> P1 (token 'p1')`, `approved client 2 -> P2 (token 'p2')` |
| **Authorization (wrong-role client)** | P1 client deliberately sent P2 input → `unauth 820`, never reached the sim |
| **Bandwidth** | `fromClients 1.8 KB/s toClients 2.1 KB/s (approx)` |
| **Prediction error** | app conditioner `avg 0.47 / max 0.68`; transport sim `avg 0.71 / max 1.58` |
| **P2 camera correction / no-snap** | in-sector correction `0.0°`; **per-frame boundary snap max 0.3–0.6°** (target 5°) — **fixed** by smoothing the body-yaw reference + a 1° inner margin |
| **Transport-level simulation** | Multiplayer Tools runtime `NetworkSimulator`: `transport simulator enabled: delay 50ms loss 2%`, with the app conditioner off (`delay 0ms loss 0%`) |
| Sector validation | `[M2-validation] rejected fire … outside historical sector …` |
| Lag compensation | `[M2-lagcomp] validated hit … vs historical yaw …; target rewound to (…)` |
| Fire cadence / ammo / reload | rejected fires dominated by cadence; reload plumbed |
| Server CPU | `tick 0.01–0.25 ms` |
| Disconnect | `client N disconnected from role P1 (role freed)` |

EditMode tests: **38/38**.

## 3. Conditioning: transport-level vs application-level

Transport-level simulation is feasible and verified (preferred). The application conditioner
(`M2DelayQueue`) remains for EditMode tests and as a fallback; its limitations: it conditions only
M2 messages, not transport packets, and models no jitter/reordering/MTU/congestion. It is
deterministic and engine-free.

## 4. Remaining blocker (why M2 is not closed)

**Runtime reconnect is not verified.** The token-restore and double-role reclaim logic is
implemented in `M2RoleService` and unit-tested (role registry), and the initial approval flow
works. But in the runtime test, after a client is killed, a relaunched client with the same token
is **not approved**: the server never logs `approved`/`reclaim` for it, and existing connections
are only later reported disconnected. So the server does not accept the reconnecting connection in
this setup. This needs a focused fix (and is likely independent of the token logic — it looks like
connection acceptance after a disconnect, possibly transport/approval interaction) before M2 can be
closed.

The reconnect **logic** itself is not in doubt; the runtime acceptance of a second connection is.
