# M3 — PvP Round Loop (Duel)

**Status:** **Complete at the M3 acceptance level.** The authoritative round-loop core and the
end-to-end networked Duel are implemented, built and verified in a real
**dedicated-server + 4-client** run.
**Module:** `Assets/Scripts/M3` (`BeMyArms.M3`, runtime) and `Assets/Scripts/M3/Editor`
(`BeMyArms.M3.Editor`, tooling).
**Scene / build:** `Assets/Scenes/M3Duel.unity`, player `Builds/M3/M3.exe`.

---

## 1. What M3 is

A Duel is **one shared body versus one shared body = four humans** (concept §14.1). Each team's
body is controlled by a P1 (legs/body) and a P2 (arms/aim), per the shared-body model proven in
M1/M2. M3 turns the M2 single-body spike into a complete, server-authoritative competitive round
loop: pre-match role assignment, buy draft, live combat, elimination/timeout, closing zone,
utility, round transitions and match end, with telemetry.

The defining property is that **all match and combat outcomes are server-authoritative**; clients
only send inputs and mirror state. The clients never declare a result.

## 2. Pure, engine-free core (unit-tested)

- `M3MatchState` — Duel match/round state machine: `Buy -> Live -> RoundEnd -> (next round |
  MatchEnd)`, **first to 3 round wins with a 5-round cap**, immediate round end on team
  elimination, draw on live-timeout, lifecycle events.
- `M3BuyPhase` — **DRAFT economy**: fixed per-round budget, no carry-over; catalog with
  primary / secondary / utility; enforces one primary and one secondary and never overspends.
  Prices are **TUNING**.
- `M3ClosingZone` — anti-stall zone: holds a start radius, then shrinks to an end radius; bodies
  outside take damage per second. Timings/radii/damage are **TUNING**.
- `M3RoleQueue` — players queue as **P1 / P2 / Either**; exact preferences first, then Either fills
  the complementary role.
- `M3Telemetry` — round length and time-to-first-contact capture (concept §31 subset).
- `M3DuelRoster` — the **four role slots** (A P1/P2, B P1/P2) with token-based reconnect and
  temporary bot substitution, extended from the two-role M2 registry; plus `FormTeams`, which wires
  the role queue into a two-body pre-match assignment.
- `M3UtilitySystem` — server-authoritative placeholder utility: a thrown grenade does radial damage
  on a fuse, smoke blocks the hitscan line, and a flash blinds nearby bodies. Pure and testable.
- `M3Loadouts` — server weapon stats per buy-catalog id (rifle / smg / shotgun / pistol / knife);
  resolves the active weapon from a round's purchases.

## 3. Networking layer

- `M3DuelDirector` (`NetworkBehaviour`) — hosts the round-loop core on the server, spawns the two
  bodies, drives pre-match slot assignment and replicates phase/round/score/timer/zone state to
  clients. Owns buy validation, closing-zone application, utility dispatch and telemetry.
- `M3DuelBody` (`NetworkBehaviour`) — one server-authoritative shared body per team. Reuses the
  pure `M2BodySim` (decoupled look / neck limit / Model-C sector clamp), `M2WeaponState`,
  `M2LagCompensation` and `M2DelayQueue`. Role-tagged P1/P2 input RPCs are authorized against the
  connection's slot; buy and utility RPCs are validated against phase and the P2 slot. Enemy
  hitscan is lag-compensated (historical sector + rewound target) and smoke-blocked.
- `M3DuelClient` — role-aware client: P1 predicts/reconciles the body, P2 keeps aim local (Model C),
  both present every body from replicated state. Supports real device input and an
  auto-drive mode used for headless multi-process verification.
- `M3DuelRoleService` — connection-to-slot authority at NGO connection approval (token-based
  reconnect; a disconnected slot becomes a temporary bot, never handed to the other human).
- `M3DuelBootstrap` — command-line configuration for dedicated server / clients.
- `M3DuelHud`, `M3ZoneVisual` — greybox presentation (phase, score, timer, health/ammo, buy list;
  the closing-zone ground ring).
- `M3DuelSceneBuilder`, `M3DuelBuild` — build the scene/prefabs and a development Windows player.

### Vertical slice ordering

`P1 input -> look/body model -> motor` and `P2 input -> sector-clamped aim -> weapon`, with the
authoritative simulation separated from presentation. P2 can fire in every P1 posture; movement
changes accuracy, never a lockout.

## 4. Real networked verification

The scene was built to a development player and run as **one dedicated server plus four separate
client processes** (`-batchmode -nographics`, distinct team/role tokens) over UTP with a 50 ms
one-way input delay and 2 % conditioned loss.

| Scenario | Configuration | Observed result |
|---|---|---|
| **Full Duel** | 4 clients auto-driven; buy 3 s, live 60 s, round end 2 s | 4 connections assigned the four distinct slots (A P1/P2, B P1/P2); all four bought each round (rifle + grenade + smoke + flash, 1200/1200); live rounds resolved by elimination; A won 3–0 across 3 rounds; `MATCH END winner=A score A=3 B=0 avgRound=0.72s avgFirstContact=0.05s rounds=3` |
| **Full Duel (competitive)** | same, another run | match went the distance: A 3–2 B across the 5-round cap, alternating round winners |
| **Utility + closing zone** | auto-fire off; zone start/end 4 m, close at 1 s, 30 dps | both teams threw and detonated grenades every round (`grenade detonated`), threw smoke and flash; the zone shrank and dealt continuous damage; eliminations resolved the rounds; `MATCH END winner=B score A=2 B=3 avgRound=2.95s rounds=5` |
| **Live-timeout draw** | auto-fire and auto-utility off; 100 m zone; live 5 s | all five rounds timed out as draws; `MATCH END winner=draw score A=0 B=0 avgRound=5.00s rounds=5` |
| **Disconnect policy** | clients gracefully exit during/after the match | each slot reverted to a **bot** on disconnect (single owner), as designed; a reconnect with the same token reclaims its slot |

All runs also exercised the application-level 50 ms / 2 % conditioner, the lag-compensated fire
path, buy-slot/budget enforcement and per-round resets.

## 5. Tests

- EditMode **62/62** (Duel tests). The M3 Duel tests cover:
  - four-slot roster assignment, free-slot fallback, token reclaim + displacement, bot single
    ownership and human-clears-bot, the pre-match **queue → slot** path, and two-body team
    formation from the role queue;
  - smoke line-of-sight blocking (including expiry), grenade fuse/detonation/radial falloff, flash
    blind falloff;
  - loadout resolution (primary wins, else secondary, else pistol) and server stat validity.

## 6. How to run

In the Editor: **Be My Arms > M3 > Build M3 Scene**, then **Be My Arms > M3 > Build M3 Player**.

```powershell
# dedicated server
Builds/M3/M3.exe -batchmode -nographics -logFile server.log -m3-role server `
  -m3-required-players 4 -m3-buy 3 -m3-live 60 -m3-roundend 2 -m3-exit-after 40

# four role clients (tokens are stable identities)
Builds/M3/M3.exe -m3-role client -m3-team a -m3-position p1 -m3-token a1 -m3-auto 1 -m3-exit-after 35
Builds/M3/M3.exe -m3-role client -m3-team a -m3-position p2 -m3-token a2 -m3-auto 1 -m3-exit-after 35
Builds/M3/M3.exe -m3-role client -m3-team b -m3-position p1 -m3-token b1 -m3-auto 1 -m3-exit-after 35
Builds/M3/M3.exe -m3-role client -m3-team b -m3-position p2 -m3-token b2 -m3-auto 1 -m3-exit-after 35
```

Useful flags: `-m3-buy/-m3-live/-m3-roundend`, `-m3-zone-start/-end/-close/-duration/-dps`,
`-m3-auto/-m3-auto-buy/-m3-auto-fire/-m3-auto-utility`, `-m3-delay/-m3-loss/-m3-rewind`,
`-m3-start-delay/-m3-required-players/-m3-port`.

## 7. Notes and scope boundaries

- **Generalised for 2v2 (M4).** The match layer was later parameterised by **bodies per team**, so
  the Duel is the `BodiesPerTeam = 1` case of the same server-authoritative director/body/roster used
  by 2v2 (`BodiesPerTeam = 2`). Slot encoding is `(team × bodiesPerTeam + body) × 2 + role`; a team
  loses a round only when all of its bodies are eliminated. The verified 4-client Duel above still
  passes on the generalised layer. See `docs/M4_2V2_MATCHMAKING.md`.
- The two bodies are capsule placeholders and the arena is greybox; this is temporary development
  geometry, not final presentation.
- Hitscan uses a capsule/radius hit test against the enemy body. The M1 critical head region is not
  yet ported to the networked Duel; damage is flat per weapon. This is a placeholder, not a design
  decision.
- Utility projectiles are resolved immediately to their landing point on the server; there is no
  networked projectile travel or visual/audio presentation yet. The authoritative effect path is
  real (damage, smoke occlusion, flash blind).
- The force-kill cross-disconnect limitation classified in `docs/NETWORKING_PROBE.md` is a
  package-level UTP/NGO item and remains non-blocking here; disconnect → bot → token reclaim works
  on graceful disconnect.
- Bot input is trivial (turn toward the enemy, aim, fire). Real bot gameplay is a later milestone.
