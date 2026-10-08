# Be My Arms — Roadmap

**Current status: clean pre-Phase-3 baseline · Updated 2026-10-08**

Design: [GAME_CONCEPT.md](GAME_CONCEPT.md). Architecture: [TECHNICAL_PLAN.md](TECHNICAL_PLAN.md).
Run/build: [docs/DEV_ENVIRONMENT.md](docs/DEV_ENVIRONMENT.md).

## Naming and authority

Use **Phase N** for current development sequencing. Do not introduce new numbered milestone
script folders, class prefixes or phase-named asset directories. Code/content are named by
responsibility. Older labels survive only in archives, migration compatibility metadata and Git.

This roadmap owns sequence, not game-design changes. Each new phase requires explicit
authorization. Repository normalization does **not** authorize Phase 3 gameplay work.

## Current checkpoint

The user has accepted the current P2 POV **for now**. No further POV iteration is scheduled.
The accepted playable rollback tag is **`checkpoint/pre-phase-3-playable`**; repository
normalization produces **`checkpoint/pre-phase-3-normalized`** without rewriting gameplay.

The checkpoint contains:

- Playable menu → Duel → chosen P1/P2 role → match, with solo bot teammate support and a
  dedicated two-human shared-body practice path. The 2v2 foundation is also present.
- NGO/Unity Transport authority, fixed-tick body simulation, P1 prediction/reconciliation,
  immediate local P2 aim, control epochs, historical firing-sector validation and reconnect.
- Independent P1 look and smooth body follow; Elastic Soft P2 sector; no standalone vault.
- Smooth shared-rig world presentation, uniform role colors and accepted third-person grips.
- Sustained-rifle bloom and controllable real-aim recoil; bounded bot P1 turns; look-directed
  extending kicks; simple one-support-hand rifle POV with a reload-only second hand.
- Existing round/buy/utility/closing-zone loop, map family, local matchmaking/rating and
  in-memory product/rig-contract foundations. These are not final production services/content.
- Domain-organized source, GUID-preserving asset migration, canonical docs, and historical
  studies separated from current run/build entry points.

Known gaps: simplified network hit regions/flat damage, incomplete combat-feedback truthfulness,
arena/bot quality, generated audio/VFX, onboarding and round presentation, production services,
durable identities/security and final release readiness. Human visual/feel acceptance is not
inferred from screenshots or green tests.

## Next phases

| Phase | Purpose | Exit gate |
|---|---|---|
| **3 — Truthful rifle combat** | Make rifle aim, shot obstruction, hit/damage regions and player feedback agree with authoritative outcomes. Preserve accepted control/camera/role foundations. Resolve remaining simplifications deliberately, not as a wholesale combat rewrite. | Relevant authority/hit tests plus human rifle-combat playtest; visible feedback must not claim an unconfirmed result. **Not started.** |
| **4 — Purposeful arena and bots** | Build readable, useful Duel routes/cover/engagements; improve complementary-role and enemy bot decisions so both solo roles are useful playtest paths. | Human solo and duo matches demonstrate purposeful positioning, sightlines and partner behavior. |
| **5 — Presentation, onboarding and round rhythm** | Explain the two-role dependency clearly, improve buy/live/end transitions and communication of teammate intent, and address approved presentation/content needs. | Fresh-player onboarding and full-round human review; avoid reopening accepted POV without new direction. |

After Phase 5, proposed—not yet approved—work is:

| Phase | Direction |
|---|---|
| **6 — Product and online integration** | Production identity/session/hosting adapters, persistence, social/communication, role-ranked systems and cosmetic entitlements; extend UI/accessibility/settings and measure performance. |
| **7 — Hardening and competitive integrity** | Targeted regressions/soaks, backend and server security, abuse/anti-cheat policy, deployment/observability and stability against agreed budgets. |
| **8 — PC/Steam release preparation** | Platform integration, publishing/legal/store work, alpha/beta human gates and launch operations. |

Future platforms, larger/objective modes, mid-round separation and a persistent economy remain
outside the current Duel sequence. Production content quality and performance budgets must be
defined explicitly; a structural foundation is not a release-quality claim.

## Validation policy

- Match checks to the changed risk. For refactors: compile/import/reference checks, relevant
  existing tests, one player build and minimal startup/core-Duel smoke testing.
- For network changes: exercise the affected authority/session/transport path, not just mocks.
- For visuals/feel: human review is the acceptance gate. Images can support diagnosis, not
  substitute for it. Do not build screenshot-heavy QA merely to make reports look comprehensive.
- Do not silently expand a cleanup into gameplay tuning or the next phase.

Historical sequence and evidence are intentionally outside this active roadmap in
[docs/archive](docs/archive/README.md). Current checkpoint details and migration exceptions are
recorded in [docs/REPOSITORY_STRUCTURE.md](docs/REPOSITORY_STRUCTURE.md).
