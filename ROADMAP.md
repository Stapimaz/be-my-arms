# Be My Arms — Roadmap

**Current status: accepted Boatyard layout; refined PBR surfaces/workshop lighting for review · Updated 2026-10-09**

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

## Current iteration — workshop/loading/yard look sample

### Follow-up to human review of `641063b`

The user liked the exterior light but reported surfaces still looked textureless, the floating
workshop text was far too large, and the interior was dark. The first sample is **not visually
accepted**. They authorized a correction in the same slice with quality, matched texture maps,
not a cheap flat image overlay; no whole-map or character expansion.

- Five major materials now use 2K albedo/normal/packed metallic-smoothness sets. Concrete/surface
  normals and visible relative albedo variation replace the excessive initial flattening.
  Dedicated CC0 powder-coating/bare-metal sources supply paint/steel surface response. Preserve
  roughness-map variation instead of clipping most values to a small constant range; coatings
  remain dielectric. Metric UVs also fix stretched texture scale on thin paint/trim/drawer pieces.
- Remove only the rejected `WorkshopServiceSign`. Add three local ceiling fixtures/downward
  **baked area lights**, with four-bounce GI and denser workshop charts. Current bake has **3
  lightmaps**, 72 probes and two local reflection captures. Exterior sun/sky/grading remain the
  same; no global ambient lift or fake new cover. Shared collision entries were asserted identical.
- Two eye-height URP previews reviewed before building for visible surface detail and room light;
  technical rendering review is not human art acceptance. A disposable preview initially cleared
  global lightmaps via Editor `NewScene`; preserve/reload those maps when previewing instead of
  mistaking that tooling artifact for missing delivered GI. No gameplay scene was altered by preview.
- Verification scope: compilation, the focused `LookSample` cases and one updated build, with
  the existing representative rendered-P2 smoke checking the five PBR map sets, baked room light
  and removed sign. No full movement/combat/lifecycle rerun; geometry and network code are unchanged.

Focused verification: **8 look-sample cases** passed across the initial run and one affected
rerun. URP material validation disabled panel emission when no `AnyEmissive` flag was set;
the flag is now correct and the fixtures remain non-GI contributors, so the area lights (not
decorative panels) supply the bake. That material-only fix needed no second light bake.
One updated Windows Development build succeeded (**0 errors, 0 reported warnings**). Runtime
verification uses the disposable rendered P2, with an optional actual game-camera frame for
technical review; that frame is not a requirement or an automated visual acceptance gate.
All **13 menu/shared-body/presentation smoke checks** passed with the five 2K PBR sets and
baked room lights loaded in the actual player. Its real P2 frame was reviewed for wall/floor
detail and lit room surfaces. A QA-only use of the Editor-only `Light.lightmapBakeType` API
was corrected to runtime `bakingOutput`; no second player build was needed.

Stop for human feedback on perceptible material quality and workshop readability. Do not declare
the art successful just because map references/bake data exist, and do not expand into other areas.

### First sample (historical)

The user accepted the bent coastal layout (`e551b23`: “layout baya beğendim”) and authorized
the proposed **small environment art slice**, not a full map or character pass. Direction to
test: bright/welcoming, readable, lightly stylized realism, somewhat more realistic than
Fortnite; avoid grimy photorealism, crushed shadows and excessive rainbow saturation.

- Local sample: warm, low-contrast textured plaster/concrete, teal painted machinery/trim,
  restrained ochre safety accents, steel/rubber, inward edge bevels and workshop surface detail.
  Adjacent areas/backdrop remain blockout; sun/sky/sea-context color and grading affect the
  whole scene. Characters, rig, P2 POV/immediate aim and combat remain unchanged.
- Ramp presentation bug fixed on all four wedges: hard geometric-face normals, metric texture
  UVs/tangents and UV2, without changing the slope/bounds/solid volume. Sample architecture
  keeps its renderer envelopes and original box camera colliders; the author asserted identical
  shared collision entries before saving. `LookSample` details have no independent colliders.
- Mixed shadowed sun, CPU-baked indirect light (**2 lightmaps**), **72 light probes** and two
  baked reflection captures; existing restrained SSAO retained, no stacked baked AO. Neutral
  tonemapping/light grading; no bloom, motion blur, DOF or vignette. World camera opts into
  map Volume grading; the separate overlay viewmodel and volume-free legacy maps do not.
- Free ambientCG CC0 texture sources and adaptations are recorded in
  `Assets/Art/Maps/BoatyardLookSample/SOURCES.md`. No paid assets/services, AI models, new
  content pipeline, layout revision or additional map. Live authoring refuses regeneration.

Verification: compilation completed; all **20 focused Boatyard map/presentation cases** passed.
The grading check first caught an empty persisted Volume profile; component sub-assets were
explicitly saved and that one affected test passed on rerun. No light rebake was invalidated.
One Windows Development build succeeded (**0 errors, 12 reported warnings**): 11 existing
deprecated-API/unused-field warnings and one Unity native-symbol upload 403 warning, not a
player/shader failure. **13 disposable menu/shared-body smoke checks** passed, including actual
rendered P2 loading of grading components, baked light/probes/reflections and ramp coordinates,
with the overlay viewmodel ungraded. No full EditMode/combat/lifecycle rerun; no automated visual acceptance.
Stop for human feedback on this slice before expanding or choosing a final cross-map style.

## Previous iteration — coast-shaped Boatyard blockout

The user authorized the first purpose-built Duel map: a small coastal boat/engine repair
business, with workshop/service and lower-quay starts, a loading platform, central repair
yard and side maintenance passage. At that stage **visual/art direction, including characters, was deferred**;
neutral blockout materials do not establish a style. No 2v2 variant or additional map is built.

Human review rejected the first broad rectangular floor/perimeter as too much like a boxed
arena. The user confirmed a natural-feeling place inside a larger scenic world, with unreachable
background structures if useful. Implemented that direction; the user subsequently accepted its layout:

- Replace the full square ground with three connected quay pads bending around a land spur;
  their combined footprint is 225 m². Narrow the loading yard; retain the workshop and ramps.
- A pump-house mass separates the yard from the maintenance route and interrupts the old
  diagonal arena crossing. Main approach rounds the quay bend; maintenance still reaches the
  workshop side entrance, with its low service pipe. No forced single L-shaped corridor.
- Different cliff depths/heights and service buildings form land-side limits. Open seaward
  guards retain horizon views; individual posts/bar geometry, not an invisible solid panel,
  blocks bodies while leaving visible gaps. Guard height follows the quay ramp.
- Visual-only `Backdrop`: sea context, faceted coastal/island masses, neighboring workshops,
  service-road continuation and a distant berth/boat. These are neutral blockout silhouettes,
  not final art or playable space. They have no colliders and are excluded from map collision.
- Map/record GUIDs, start poses, side alternation, match wiring and existing rig/controls are
  preserved. The old full rectangle is recoverable from `96edf68` / `d003674`; existing legacy
  arenas and rollback tags are unchanged. The live-Editor revision script refuses dirty or
  already-revised scenes rather than overwriting later authoring.

Verification: **17 focused Boatyard tests** passed: both approaches, ramps, current bot exits,
spawn occlusion/clearance, backdrop separation and standing/crouch jumping at the raised guard.
One Windows Development build succeeded (**0 errors, 0 reported warnings**), and **12 normal
menu/shared-body smoke checks** passed, including both roles on the revised map and round-side
reset. No full movement/combat/lifecycle suite rerun: simulation and network code are unchanged
this revision. Stop for human layout/world-continuity feedback, not an automatic art pass.

Previous bug follow-up: ramps had walkable tops but no solid volume in the shared numeric
collision model. Filled, slope-clipped ramp volumes now block high-side/back entry and bullets,
while retaining low-edge steps and ordinary ascent/descent. Bots use that same volume. Scene
layout, human controls, prediction protocol and rifle handling are unchanged.
Verification for this fix: **13 Boatyard tests + 15 movement tests**, one isolated built-server
check covering **5 ramp movement/ray cases**, and one Windows build (**0 errors, 45 reported
warnings**). No full suite, menu smoke or lifecycle rerun; the map layout has not been revised.

- Menu Duel and duo practice now open `Boatyard`. The previous `DuelArena` remains unchanged
  and included in the build for explicit development launch; `TwoVsTwoArena` is unchanged.
- Approximately 40 × 40 m defensive simulation bounds (not a rectangular playable floor),
  three natural floor levels (0 / 1.2 / 2.4 m), four ramps,
  wide workshop space and functional crane/engine-rack/hull cover masses. The maintenance pipe
  needs crouch/slide. Existing geometry-based bots use the same deterministic collision.
- Starts alternate each round; team identities, scores and human P1/P2 ownership do not swap.
  No attack/defend objective, new traversal or network foundation was introduced.
- Stop here for human review of approaches, scale, firing opportunities, dominant platform
  angles and near-cover camera/shot-origin disagreement. That layout review now permits a small art sample.
  Release content target is 5+ maps per mode, not this iteration's delivery scope.

First blockout verification: **9 focused map tests** passed, covering spawn clearance/occlusion, side schedule,
all four ramps, crouch clearance and existing bot reachability. **12 menu/shared-body smoke checks**
passed, including Boatyard entry and real server round-side resets preserving owners/slots/score.
One Windows Development build succeeded (**0 errors, 42 existing warnings**). No full combat or
lifecycle suite rerun: this iteration changes geometry, menu map selection and round poses.

## Previous follow-up — responsive crosshair smoothing

Human feedback requested quick, lightly smoothed expansion/contraction rather than snapping.
The display continues to track total next-round spread from any cause, including stationary
sustained-fire bloom. A 0.04 s exponential response smooths presentation only; gun accuracy,
networking and burst recovery are unchanged. Epoch/re-enable resets drop stale animation.
The renderer supports a fixed-center-dot mode for a future settings choice, but this follow-up
does not build a settings menu or persistence layer. Review responsiveness/readability, then stop.

Verification: the **10 focused crosshair tests** passed (including 30/60/144 FPS smoothing,
stationary spray and dot-only rendering), plus **9 Duel/HUD smoke checks**. One Windows build
succeeded (**0 errors, 33 existing warnings**). No full EditMode or combat/lifecycle rerun:
this change affects presentation only.

## Previous iteration — visible P2 spread / speed-dependent slide error

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
