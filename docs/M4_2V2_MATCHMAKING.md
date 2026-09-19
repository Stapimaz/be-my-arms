# M4 — 2v2 and Matchmaking

**Status:** **In progress.** The ranked-ready **structure** (role ratings, derived body MMR, party
handling, Duel/2v2 match proposals, provider-neutral server allocation) is implemented and
unit-tested. Live services integration, networked 2v2 and a real hosting provider remain.
**Module:** `Assets/Scripts/M4` (`BeMyArms.M4`, pure C#; references `BeMyArms.M3` for role semantics).

---

## 1. Scope from the roadmap

> Two bodies per team, derived body MMR, role-specific matchmaking, parties, dedicated server
> allocation. **Acceptance:** ranked-ready structure with provider-neutral server allocation.

M4 is the first time **2v2** exists as a first-class mode: two shared bodies per team, **eight
humans total** (concept §14.1). The notation matters — “2v2” counts shared combat bodies, never
individual players, and that distinction is kept explicit in the data model.

## 2. What is implemented and tested (pure)

- `M4Rating` — **role-specific Elo**. A match result updates only the rating of the role that was
  played: P1 and P2 MMR are independent (concept §18.2). `ExpectedScore` and `Update` are pure;
  K-factor and scale are **TUNING**.
- `M4BodyMmr` — derived **body rating** `BodyMMR = average(P1, P2) - k × |P1 - P2|`, with a
  draft gap penalty that can be tuned to zero (concept §18.3), plus the 2v2 **team rating** from two
  body ratings (§18.4).
- `M4Matchmaker` — a pure, provider-neutral matchmaker:
  - forms **complete shared bodies** from solo and premade-duo players, honoring P1 / P2 / Either
    preferences (reusing `M3RolePreference`);
  - **keeps premade parties on one body**;
  - splits bodies into **balanced teams**, choosing the split with the smallest rating gap;
  - **relaxes tolerances with queue time** (body role-gap and team rating-gap widen by
    `RelaxPerSecond × longest wait`, capped);
  - supports both `Duel` (1 body/team = 4 players) and `TwoVsTwo` (2 bodies/team = 8 players);
  - returns a `M4MatchProposal` of explicit `(team, body, role)` slot assignments plus a quality
    score — no networking, no vendor types.
- `IM4ServerAllocator` / `M4LocalAllocator` — **provider-neutral allocation**. The game layer depends
  on the interface only; the local stub returns loopback endpoints and bounded match ids for
  development and tests. A real adapter (Unity Multiplayer Services, an external cloud, or a local
  fleet manager) implements the same interface without touching gameplay.
- `M4MatchBridge` — connects the pure proposal to the shipped game: maps a Duel proposal onto the
  four M3 role slots the networked director uses, and applies a match result to every player's
  played role (2v2-aware opponent averaging).

## 3. Tests

EditMode **75/75** (was 61). The 14 new M4 tests cover:

- role-specific rating: only the played role updates, expected score is fair/monotonic, upsets gain
  more;
- derived body MMR gap penalty (including `k = 0` = plain average) and team rating;
- Duel: two complete bodies with role preferences honored, unfillable bodies rejected, Either
  filling the complementary role;
- party: a premade duo stays on the same body/team;
- 2v2: four bodies, two per team, with strong and weak bodies split across teams to a zero rating
  gap;
- constraint relaxation with queue time;
- provider-neutral allocation: tickets are unique, carry an endpoint/token, and release correctly;
- the bridge: a Duel proposal maps to the four unique M3 slots and a result updates only each
  player's played role.

## 4. Remaining M4 work (not claimed)

1. **Networked 2v2 run.** M3 proved a dedicated server plus four clients for a Duel. 2v2 needs
   four bodies / eight clients, a team-level director and the corresponding scene; the M3 body,
   director and roster are structured so this is a scale-out, but it has not been run.
2. **Live matchmaking/session services.** Install and integrate the Unity Multiplayer Services SDK
   (sessions, lobby, matchmaking, relay for prototypes) behind the existing pure matchmaker.
3. **Real dedicated-server allocation.** Replace `M4LocalAllocator` with a provider adapter
   (provider-neutral, per `TECHNICAL_PLAN.md` §7); the Multiplay deprecation means the adapter must
   target a current provider.
4. **Input-aware weighting.** Each queue entry already carries `InputProfile` and `Region`; the
   weighting/scoring is deferred until cross-platform play exists (concept §18.5).
5. **Rating persistence.** `M4PlayerProfile` is in-memory only; account/back-end persistence is a
   product-system (M5) concern.

## 5. Notes

- Parties larger than a duo are currently rejected by `M4Matchmaker` rather than split across
  bodies; supporting 3–4 player parties is a follow-up once 2v2 is networked.
- The matchmaker is deterministic and side-effect free, so it can be shared by a server, a test
  harness or a future backend without change.
