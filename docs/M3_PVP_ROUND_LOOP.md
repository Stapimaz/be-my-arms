# M3 — PvP Round Loop (Duel)

**Status:** **In progress.** The authoritative round-loop core (round state machine, buy economy,
closing zone, role queue, telemetry) is implemented and unit-tested. In-scene playable integration,
utility effects, and the NGO authority wiring remain.
**Module:** `Assets/Scripts/M3` (`BeMyArms.M3`).

---

## 1. Implemented and tested (pure, engine-free)

- `M3MatchState` — Duel match/round state machine: `Buy -> Live -> RoundEnd -> (next round | MatchEnd)`,
  **first to 3 round wins with a 5-round cap**, immediate round end on team elimination, draw on
  live-timeout, and lifecycle events (`RoundStarted`, `LiveStarted`, `RoundEnded`, `MatchEnded`).
- `M3BuyPhase` — **DRAFT economy**: fixed per-round budget, no carry-over; catalog with primary /
  secondary / utility; enforces one primary and one secondary and never overspends. Prices are
  **TUNING**.
- `M3ClosingZone` — anti-stall zone: holds a start radius, then shrinks to an end radius; bodies
  outside take damage per second. Timings/radii/damage are **TUNING**.
- `M3RoleQueue` — players queue as **P1 / P2 / Either**; exact preferences are filled first, then
  Either fills the complementary role; solo queue is incomplete until a partner exists.
- `M3Telemetry` — round length and time-to-first-contact capture (concept §31 subset).

## 2. Tests

- EditMode **51/51**, including 11 M3 tests: match phase transitions, elimination ends a round,
  first-to-3 ends the match, the 5-round cap decides when it is reachable, live-timeout draw, buy
  budget + slot limits + per-round reset, closing-zone hold/shrink/damage-outside-only, role-queue
  formation from exact and Either preferences, telemetry averaging.

## 3. Pending (M3 is not complete)

- **In-scene playable integration:** drive the existing shared body (M1) and an opposing body
  through the round loop in a scene (buy screen, live round, elimination, round/match end UI).
- **Utility:** grenade throw (damage) plus smoke/flash effect placeholders. `M3BuyPhase` already
  prices and slots utility; the throw/effect behaviour is not wired yet.
- **Closing-zone damage** applied to live bodies each tick, and the zone visualised.
- **Role queue** wired to the matchmaker/pre-match flow (assignment + labels).
- **NGO authority wiring:** the round loop is authoritative by design (pure state machine) but is not
  yet hosted by the networked body; the M2 transport path is the intended host.
- **Telemetry** collected from live rounds rather than unit inputs.

## 4. Notes

- Round-loop logic is intentionally engine-free so it can be unit-tested and later driven
  authoritatively by the server (and only mirrored on clients).
- The force-kill cross-disconnect is a separate, classified package-level hardening item
  (`docs/NETWORKING_PROBE.md`) and does not block this milestone.

## 5. Cleanup this pass

- Disconnect wording corrected: a disconnected role is **temporarily bot-controlled** (never
  "contributes no input" and never handed to the other human) — `ROADMAP.md` M2 criterion 8.
- Removed the unused, design-inconsistent `Armor` placeholder from `M3BuyPhase` (concept §13.3:
  no armor in the initial version).

## 6. Remaining gate to close M3

M3 is complete only when the Duel works **end-to-end in a real networked run** (not unit tests):
two shared bodies, pre-match **role assignment**, server-authoritative **buy phase**, **live**
combat, elimination/timeout, round end, next round, match end, with **utility**, **closing-zone
damage/visualisation**, live **telemetry**, and the loop hosted authoritatively over the M2
transport. That integration (director component + two networked bodies + scene) is designed in
§1–§3 but not yet built/run.

