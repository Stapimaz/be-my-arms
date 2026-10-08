# Be My Arms — Roadmap

**Current status: dynamic P2 spread crosshair / speed-dependent slide accuracy ready for review · Updated 2026-10-09**

Design: [GAME_CONCEPT.md](GAME_CONCEPT.md). Architecture: [TECHNICAL_PLAN.md](TECHNICAL_PLAN.md).
Run/build: [docs/DEV_ENVIRONMENT.md](docs/DEV_ENVIRONMENT.md).

## Naming and authority

Use **Phase N** for current development sequencing. Do not introduce new numbered milestone
script folders, class prefixes or phase-named asset directories. Code/content are named by
responsibility. Older labels survive only in archives, migration compatibility metadata and Git.

This roadmap guides sequence, not artificial completion. Continued development is now
authorized from the accepted normalized baseline. Use focused, concept-serving iterations;
ask before genuine ambiguous product decisions, and preserve working foundations rather than
rewriting them to satisfy a theoretical architecture or a phase label.

## Accepted foundation

The user has accepted the current P2 POV **for now**. No further POV iteration is scheduled.
The accepted playable rollback tag is **`checkpoint/pre-phase-3-playable`**; the accepted
domain-normalized rollback tag is **`checkpoint/pre-phase-3-normalized`**. The user confirmed
that the game still feels the same after cleanup. Neither tag is moved by later development.

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

Known gaps at that checkpoint: simplified network hit regions/flat damage, incomplete combat-feedback truthfulness,
arena/bot quality, generated audio/VFX, onboarding and round presentation, production services,
durable identities/security and final release readiness. Human visual/feel acceptance is not
inferred from screenshots or green tests.

## Current iteration — visible P2 spread / speed-dependent slide error

The user requested a dynamic crosshair that communicates the area a bullet can deviate into,
not just body-motion text. Implemented as a follow-up to movement-dependent rifle accuracy:

- P2's center dot retains the aim direction; four ticks and a thin circular envelope expand with
  next-round movement + burst spread. World-camera FOV, viewport height and canvas scale determine
  the projected size; no arbitrary motion animation or smoothing lag understates the area.
- Accepted server burst/shot-tick/epoch/count metadata reconciles immediate local shot previews.
  Late confirmations age from the shot tick, not arrival; round/role resets clear old previews.
  The display remains a network-observed estimate, not a new hit/weapon authority.
- Slide spread now scales from **0.75° at zero speed to 3° at 9 m/s**, decreasing as actual
  collision-resolved speed falls. Active sliding retains a modest disturbance floor. Human/bot
  shot simulation and HUD use the same shared accuracy model; movement mechanics are unchanged.
- Crosshair is P2/live/alive/gameplay only; pause, role exchange and missing body/camera hide it.

Technical checks: **239/239 EditMode tests**, **52/52 focused network rifle checks**, **9/9 Duel
checks**, final Windows Development build succeeded (**0 errors, 0 reported warnings**). Rendered
HUD checks cover projection and real firing expansion, not visual/feel acceptance.
Stop for human readability/tuning review and next-step discussion. Near-cover camera/shot-origin
agreement and hit-profile readability remain recommended follow-ups, not already-started work.

## Previous iteration — P1 movement / P2 rifle accuracy

After positive human feedback on the bots, the user requested CS-like movement-dependent
accuracy: P1 movement should disrupt P2's shots, but a stationary first round should be exact.
Implemented as a focused rifle slice; stop for review/discussion rather than starting another phase.

- Recovered, grounded stationary first rifle round has zero ballistic spread. Actual P1 speed
  adds a lighter crouch-walk/walk penalty and a stronger sprint penalty; airborne/actions have
  explicit penalties. Both human and bot P2 shots use the same authoritative spread ray.
- Stopping removes only movement spread; existing spray bloom and recoil remain. A >0.30 s
  shot pause restores first-round bloom but does not remove a moving-body penalty.
- HUD communicates body stability/motion spread. Human input/POV, sector-history legality,
  prediction, authority, ammo cadence and damage are retained; no new movement lockout/inertia.

Technical checks: **227/227 EditMode tests**, **41/41 focused network rifle checks**, **7/7 Duel
startup/control smoke checks**, Windows
Development build succeeded (**0 errors, 42 existing deprecated-API/unused-field warnings**).
Human playtest still decides penalty strength and whether firing opportunities feel useful.
Next priority to discuss: near-cover camera/shot-origin agreement and hit-profile readability,
then purposeful arena work or role-intent communication—not a silent new iteration.

## Previous iteration — purposeful positioning and smooth bot aim

Human solo-P2 review exposed that the P1 teammate did not meaningfully use stance, obstructing
combat playtesting. The user chose useful firing positions and threat-driven repositioning over
aggressive pushing, and requested human-like P2 aim rather than at-shot snaps. This brings a
focused bot slice of Phase 4 forward; it does not declare rifle combat or arena design finished.

Implemented; **positive human feedback received** ("bot olayı çok iyi olmuş"):

- Both difficulties choose reachable firing/cover positions from the same boxes, surfaces and
  bounds as movement simulation. No map-specific waypoints, scene edits or new movement rules.
- Standing/crouched exposure, range, incoming damage, enemy fire, reload cover and the closing
  zone influence stable decisions. Blocked routes replan; no idle random strafing/jumping.
- Useful low cover gives conditional duck/peek behavior; low passages require crouching. Slide
  requires a straight, fully clear stopping corridor that actually reduces exposure, with a
  bot-local reuse delay. It remains intentionally uncommon, not a QA animation schedule.
- Perception respects solids/smoke/blindness. Brief last-seen memory retains an observed pose,
  not a live target position through walls. Lost targets lead to local exploration.
- P2's actual server aim has bounded angular speed and acceleration. Easy aim goals change
  during burst pauses; shooting requires sight, legal sector and acquisition. Hard also turns
  smoothly and now has tighter drift. Human local aim/POV and Elastic Soft are untouched.
- Epoch/round/ownership changes clear bot routes, perception and aim velocity. Existing role
  authority, hit regions/damage, prediction, reconnect and transport are retained.

Validation: **214/214 EditMode tests**, authoritative Easy/Hard bot-controller checks and the
existing **22 session/role/lifecycle checks** passed; Windows build succeeded. These establish
technical behavior, not that the teammate is pleasant or tactically optimal in human play.

Further tuning can use firing opportunities, repositioning, stance transitions and aim continuity.
Navigation is a bounded local, single-floor-per-cell
search, not final layered navigation/global tactics. See [current playtest](docs/PLAYTEST.md).

## Previous iteration — rifle precision and shared confirmation

Implemented; human review is ongoing:

- Replace oversized sampled-line hits with exact surface intersection against standardized
  body/head volumes. Actual target Y, stance, facing and life/control epoch rewind with X/Z.
- Resolve the closest unobstructed enemy surface and report that actual impact point.
- Keep rifle body damage at 18; provisionally reward P1-head precision with 45 damage.
  No armor, regeneration, role HP split, movement invulnerability or weapon lockout was added.
- Give both roles authoritative confirmation. P1 sees partner weapon hits; P2 distinguishes
  partner kicks. Head hits have distinct text/color/sound; elimination remains server-confirmed.
- Preserve cameras/POV, local P2 aim/recoil/bloom, Elastic Soft, P1 movement/kicks, bot decisions,
  session ownership and prediction. Easy bot miss offsets now use the new body radius so the
  existing forgiving accuracy intent is not accidentally replaced by the smaller profile.

Technical checks: **196 EditMode tests**, focused built-player network combat checks, a Windows
build and normal rendered Duel startup/control smoke. This is not visual or balance acceptance.
See [current playtest](docs/PLAYTEST.md).

Rifle review still needs hit-profile/readability and near-cover camera/shot-origin evidence.
Human feedback brought the focused bot iteration above forward to make those playtests more
useful. Do not mark rifle combat finished merely because headshots work.

## Development directions

| Phase | Purpose | Exit gate |
|---|---|---|
| **3 — Truthful rifle combat** | Make rifle aim, shot obstruction, hit/damage regions and player feedback agree with authoritative outcomes. Historical region hits, confirmed feedback and motion-dependent rifle accuracy are implemented; near-cover origin/presentation and profile readability still need evidence. | Relevant authority/hit tests plus human rifle-combat playtest; visible feedback must not claim an unconfirmed result. **In progress; accuracy follow-up ready for review.** |
| **4 — Purposeful arena and bots** | Bot slice brought forward: geometry-aware positions, threat cover, conditional stance and smooth P2 tracking. Arena content and deeper duo/global tactics remain open. | Human solo and duo matches demonstrate purposeful positioning, sightlines and partner behavior. **Positive feedback on focused bot iteration; broader arena work open.** |
| **5 — Presentation, onboarding and round rhythm** | Explain the two-role dependency clearly, improve buy/live/end transitions and communication of teammate intent, and address approved presentation/content needs. | Fresh-player onboarding and full-round human review; avoid reopening accepted POV without new direction. |

Later directions—not the current iteration—are:

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
- Keep each iteration focused; phase names are not acceptance criteria. A cleanup alone does
  not justify gameplay tuning, and technical passes never substitute for human acceptance.

Historical sequence and evidence are intentionally outside this active roadmap in
[docs/archive](docs/archive/README.md). Current checkpoint details and migration exceptions are
recorded in [docs/REPOSITORY_STRUCTURE.md](docs/REPOSITORY_STRUCTURE.md).
