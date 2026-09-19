# M4 — 2v2 and Matchmaking

**Status:** **Complete at the M4 acceptance level.** Duel and 2v2 are the same server-authoritative
match layer parameterised by bodies per team; a real dedicated-server + **8-client** 2v2 run is
verified end-to-end, including matchmaking, provider-neutral allocation, parties and post-match
role-specific rating updates. Production hosting and live Multiplayer Services integration remain a
later, explicitly non-blocking integration concern.
**Modules:** `Assets/Scripts/M4` (pure matchmaking/ratings + the server match host,
`BeMyArms.M4`) on top of the generalized `Assets/Scripts/M3` match layer (`BeMyArms.M3`).

---

## 1. Scope from the roadmap

> Two bodies per team, derived body MMR, role-specific matchmaking, parties, dedicated server
> allocation. **Acceptance:** ranked-ready structure with provider-neutral server allocation.

A **2v2 is two shared bodies per team = eight humans** (concept §14.1). “2v2” counts shared combat
bodies, never individual players; that distinction is explicit in the data model, the HUD and the
match logs.

## 2. What is implemented

### Pure matchmaking and rating (`BeMyArms.M4`, engine-free)

- `M4Rating` — **role-specific Elo**: a result updates only the rating of the role that was played
  (concept §18.2).
- `M4BodyMmr` — derived **body rating** `average(P1, P2) - k × |P1 - P2|` with a configurable gap
  penalty (§18.3), plus the 2v2 **team rating** from two body ratings (§18.4).
- `M4Matchmaker` — pure Duel/2v2 matcher: forms complete bodies from solo and premade-duo players,
  honors P1/P2/Either preferences, keeps pre-made parties on one body, splits bodies into balanced
  teams, and relaxes constraints with queue time. Returns a `M4MatchProposal` of explicit
  `(team, body, role)` slot assignments plus a quality score.
- `IM4ServerAllocator` / `M4LocalAllocator` — **provider-neutral allocation**; the game layer depends
  only on the interface and the local stub hands out loopback endpoints for dev/test.
- `M4MatchBridge` — maps a proposal onto the shipped M3 slot encoding and applies a match result to
  each player's played role (2v2-aware opponent averaging).

### Server host (`M4MatchHost`)

On the server, and only in matchmaker mode, it:
1. observes the M3 connection queue and builds `M4QueueEntry` values (preference, party, MMR,
   wait time, region), filling unfilled slots with Either bots so a match can always start;
2. runs `M4Matchmaker.TryMatch` and `IM4ServerAllocator.Allocate` (logging the allocated ticket);
3. hands the slot assignments to the M3 director, which assigns humans/bots and starts the match;
4. subscribes to the director's match result and applies `M4MatchBridge.ApplyResult`, logging each
   player's role-rating delta.

### Generalized match layer (`BeMyArms.M3`)

The M3 director/body were generalised from a fixed Duel to **N bodies per team** with the slot
encoding `(team × bodiesPerTeam + body) × 2 + role`:

- `M3DuelDirector` spawns and binds 1 or 2 bodies per team, runs the round loop, applies the closing
  zone to every body, and ends a round only when **all** of a team's bodies are eliminated.
- `M3DuelBody` keeps its own buy/utility economy, does lag-compensated hitscan against every enemy
  body, and is authorized per role slot.
- `M3DuelRoster` holds either direct/dev slots or the pre-match **queue** used by the matchmaker,
  with token reconnect and temporary bot substitution.
- The Duel is simply `BodiesPerTeam = 1`; the previously verified 4-client Duel still passes.

## 3. Verified — real dedicated-server + 8-client 2v2

One dedicated server and eight client processes (`-batchmode -nographics`, 50 ms one-way delay and
2 % conditioned loss), matchmaker mode, seeded MMRs:

```
server:  -m4-mode 2v2 -m4-matchmaker 1 -m4-required 8
         -m4-mmr t1=1400,t2=1400,t3=900,t4=900,t5=1200,t6=1200,t7=700,t8=700
clients: -m4-mode 2v2 -m4-matchmaker 1 -m3-token tN -m3-position p1|p2|either
```

| Check | Observed |
|---|---|
| Matchmaking | `matchmaking (TwoVsTwo) quality=0.0 teamA=1050 teamB=1050` — four bodies split into two teams with a zero rating gap |
| Provider-neutral allocation | `-> allocated match 'local-0001' at 127.0.0.1:7780 (local)` through `IM4ServerAllocator` |
| Role/body/team assignment | all eight queued connections assigned distinct slots: `t1→A0P1, t2→A0P2, t3→B0P1, t4→B0P2, t5/t6→B1P1/P2, t7/t8→A1P1/P2`; `humans=8` |
| Client confirmation | each client logged its own slot and the P2 clients auto-bought (e.g. `B1P2 auto-buy round 1`) |
| Full match loop | `bodies spawned: 2 per team (4 total)`; rounds ended only after **both** enemy bodies fell (`team B body eliminated (1 left)` → `(0 left)`); `MATCH END winner=A score A=3 B=0 … rounds=3` |
| Post-match role ratings | only the played role moved, winners gained and losers lost, and the underdog gained more: `t1 P1 1400→1404 P2 1400→1400`, `t3 P1 900→891 P2 900→900`, `t7 P1 700→728 P2 700→700` |

A second 2v2 run with two premade duos (`-m4-party t1=duoA,t2=duoA,t3=duoB,t4=duoB`) kept each duo
on one body (`t1,t2→A0`; `t3,t4→B0`) while still balancing the teams at 1050 each.

The 4-client Duel regression was re-run after the generalisation and still completes
(`mode=duel matchmaker=False`, `bodies spawned: 1 per team (2 total)`, A 3–0).

## 4. Tests

EditMode **76/76**. Coverage includes role-specific rating, derived body/team MMR, Duel and 2v2
matching, role preferences, parties, balanced team splits, queue-time relaxation, provider-neutral
allocation, the proposal→slot/rating bridge, the four-slot and queued roster paths, and the M3 round
loop, utility and closing zone.

## 5. Deliberately deferred (production integration, not structural)

These do not block the M4 acceptance and are kept as later integration concerns:

1. **Live matchmaking/session services** (Unity Multiplayer Services: sessions, lobby, relay) — the
   pure matchmaker and the `M4MatchHost` seam are where they plug in.
2. **A real dedicated-server allocation provider** — replace `M4LocalAllocator` behind
   `IM4ServerAllocator` (provider-neutral by design; the Multiplay deprecation means a current
   provider must be chosen).
3. **Rating persistence** — `M4PlayerProfile` is in-memory; account/back-end persistence is M5.
4. **Input-aware weighting** — entries already carry region/input profile; weighting is deferred
   until cross-platform play exists (concept §18.5).
5. **Parties larger than a duo** — currently rejected rather than split across bodies; a natural
   follow-up once large-party 2v2 is needed.

## 6. Notes

- Placeholder capsule bodies and greybox arena remain temporary development assets.
- Networked hitscan against multiple enemies uses per-enemy rewound history; the shot hits the
  nearest enemy along the ray, with smoke occlusion.
- The matchmaker is deterministic and side-effect free, so it can be shared by a server, a test
  harness or a future backend unchanged.
