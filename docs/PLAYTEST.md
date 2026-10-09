# Current playtest — Boatyard Duel blockout

**First layout review; no art-direction decision · 2026-10-09**

Build: `Builds/Windows/BeMyArms.exe`. **PLAY → Duel → P2 → Start Match** tests the P1 movement
partner; repeat as **P1** to test the P2 weapon partner. Try Easy first, then Hard. F6 resets
an encounter; F7 exchanges practice roles. Use matching client/server builds.

## What to review now

Menu Duel and duo practice open **Boatyard**: a small coastal engine/boat repair business,
currently neutral blockout geometry. Workshop/loading platform faces a repair yard and lower
quay; an eastern maintenance passage offers an alternate approach with a low service pipe.
Floor levels are 0 / 1.2 / 2.4 m. Starting sides alternate each round, not teams or P1/P2 roles.
F6 restarts the current round on the same side. F7 exchanges practice roles and starts a fresh
match at round one, as before; an ordinary next round exchanges geographic starts.
The old arena remains available via `-client-arena DuelArena` on both server and clients.

First feedback: the continuous rectangular floor/perimeter feels too much like a boxed arena.
A coast-following, bent arrangement of separate workshop/yard/quay spaces is proposed, **not
implemented yet**. This follow-up fixes only ramp collision: try approaching high ramp sides
and backs, then normal ascent/descent and stepping onto the low side; the ramp interior must
not swallow the body. No art-direction change or layout acceptance is implied.
Ramp fix verification: 13 map tests, 15 movement tests and 5 isolated built-server ramp checks
passed. Updated Windows build succeeded with 0 errors (45 reported warnings); no full-suite or
menu/lifecycle rerun was needed. Use the updated binary on both sides when testing this fix.

1. Try both P1 and P2, ideally then two humans sharing a body. Can P1 offer useful, stable firing
   positions while P2 requests a different angle? Does movement have a purpose beyond rushing?
2. Compare the main yard crossing against the maintenance approach. Is the alternative useful,
   findable and contestable, or simply slow and pointless? Try crouch/slide under the service pipe.
3. Is the loading platform strong but answerable from other angles? Flag positions that lock
   every exit, unavoidable spawn pressure, dead ends and routes you never want to use.
4. Judge distances, contact time, openness, workshop close fighting and cover proportions.
   Flag where the P1 bot sticks, cannot leave a start or repeatedly ruins an otherwise good shot.
5. Near lips, racks and doorway edges, report where camera aim says clear but the actual rifle
   shot is obstructed. This known shot-origin/presentation limitation is not solved by a new map.

**Do not judge final visuals yet.** Map/character art direction is explicitly postponed.
This checkpoint needs layout feedback before more geometry, additional maps or an art pass.

## Retained accuracy and crosshair behavior

- Rifle accuracy now depends on P1's actual body movement. A recovered, grounded stationary
  first shot follows P2's aim exactly; crouch-walk adds less spread than walk, sprint adds more,
  and airborne/slide/dodge/kicks have stronger penalties. Humans and bots follow the same rule.
- Stopping removes movement error, not ongoing spray bloom. Pause firing for over **0.30 s**
  to recover first-round bloom; pausing while running still leaves movement error. Recoil remains.
- The rifle HUD shows `BODY STABLE` or `BODY MOTION: +…° SPREAD` for both roles. This is only
  the body penalty, not total spread or a guarantee that the rifle burst has recovered.
- P2's dynamic crosshair now shows total **next-round movement + burst spread**: a fixed center
  dot marks aim; four ticks open and a thin ring indicates the angular error boundary. Its size
  follows the actual camera FOV/resolution, not arbitrary pixels. At perfect accuracy, the ring
  disappears and only the dot/small readable ticks remain. No cosmetic "shot kick" inflates it.
- Sliding error shrinks with actual body speed (**0.75°–3°** over 0–9 m/s); an active slide still
  has a modest disturbance floor. Server accepted-shot history corrects immediate local firing
  previews. The crosshair is hidden while paused/dead, outside Live, or in the P1 role.
- Expansion/contraction now has light, frame-independent smoothing: roughly 95% of a change
  appears within **0.12 s**. It follows total spread from any cause, including stationary long
  sprays. This is a smoothed preview, not a change to real bullets or an instantaneous boundary.
  A fixed-dot render mode is ready for a future settings choice; that choice is not in the menu yet.

## Specifically evaluate accuracy first

1. With a rifle and P1 stopped, allow the burst to recover, then tap a visible target. The first
   shot should have no random deviation (correct aim is still required). Repeat crouched.
2. Compare taps while walking, crouch-walking and sprinting. Can you clearly feel why P1 should
   stop for P2's precision shot? Does sprinting punish precision enough without feeling unusable?
3. Run → stop → tap; then run → spray → stop without releasing fire. The latter must not reset
   the continuing burst. Try jump/slide/dodge/kick: P2 can still fire but loses accuracy.
4. Solo P2: does the accepted P1 bot's positioning give enough stable firing opportunities?
   Solo P1: can you feel your positioning affect the P2 weapon partner's effectiveness?
5. Watch the crosshair during movement and a long spray: does opening/closing communicate when
   precision is available? Does the thin boundary stay readable without obscuring small targets?
   Compare window sizes/FOV if changing them. Stopping during spray must leave burst expansion;
   a recovered stationary first round must collapse the error boundary.
   Does the quick opening/closing feel smooth and responsive rather than snapping or lagging?
6. Slide and let it slow: both spread and the boundary should shrink rather than stay fixed-wide.
   F6/F7, reload and pause/resume should not leave a stale spray indicator.

Four-second spawn protection still suppresses damage; check **PROTECTED** before interpreting
early misses. This is an angular-spread preview, not a hit guarantee: latency can correct the
estimate and near-cover camera/shot-origin/profile agreement is still open below.

## Retained bot iteration

- P1 bots choose reachable firing positions using the actual map collision geometry: useful
  cover, exposure, comfortable range and zone safety—not continual forward charging.
- Damage/enemy fire make protective repositioning valuable. Plans are held long enough to
  execute; blocked travel replans instead of random jump/strafe spam.
- Low cover can justify crouching while threatened or reloading, then a deliberate peek to
  restore the partner's shot. Low ceilings require crouching for passage.
- Sliding is uncommon and purposeful: an exposed-to-cover crossing with a fully clear stopping
  corridor, enough existing movement speed and a reuse delay. No random slides for QA.
- P2 bots turn their actual aim with bounded speed/acceleration, including target changes and
  burst error. Easy selects its next aim destination during the firing pause, not after a shot.
  Fire waits for clear sight, legal sector and acquisition. Hard also tracks smoothly, with
  faster acquisition and tighter drift; expect its combat difficulty to need human tuning.
- Bots retain briefly remembered poses, not live enemy positions through walls. Solids, smoke
  and blindness restrict observation; resets and role replacement clear old bot decisions.

Human movement, immediate local P2 mouse aim, Elastic Soft, accepted POV, rifle recoil/sustained
bloom, damage rules, prediction and session authority are retained; first-round/motion spread
changes are the focused follow-up above.

## Optional further bot feedback

1. **Solo P2 / P1 partner:** does it establish useful firing positions and give you time to aim?
   Under fire, does its repositioning make sense, or does it unnecessarily destroy a good angle?
   Flag indecision, wall sticking, excessive camping or running toward the enemy.
2. **Purposeful stance:** near low cover, does ducking feel protective and do peeks give useful
   firing opportunities? Are stance changes too frequent/disruptive? Sliding need not occur
   every encounter; judge whether it is sensible when it does.
3. **Solo P1 / P2 partner:** turn, crouch and change elevation while facing an enemy. Does the
   partner's rifle aim travel continuously before/during bursts, rather than flick on each shot?
   Does acquisition feel human-like but still useful? Moving the sector wall can still push
   aim, as it does for a human P2; bots do not bypass that coupling.
4. **Enemy behavior:** do enemies use positions/cover believably and stop firing at an occluded
   target? Compare Easy/Hard pressure; report bots becoming either harmless or oppressive.
5. **Reset/roles:** F6/F7 should clear old routes/aim goals without either human gaining both roles.

## Rifle review still applies

Rifle damage remains **18 body / 45 P1-head** (six body or three head hits from full 100 HP).
Head-hit, elimination and partner-kick cues remain server-confirmed. Four-second spawn
protection still suppresses damage; check the **PROTECTED** HUD before interpreting early misses.
Continue flagging visible hit-profile mismatches, especially in crouch/slide/action poses.
Animated limb coverage and near-cover camera/shot-origin agreement are not declared solved.

## Scope and evidence

Boatyard: **9 focused map tests** and **12 menu/shared-body smoke checks** passed. The latter
exercise the built map with both human roles and actual server round changes, preserving team
scores and P1/P2 owners/slots. One Windows Development build succeeded (**0 errors, 42 existing
warnings**). Full combat/lifecycle suites were not rerun; gun and session foundations are unchanged.
The smoke's shared-endpoint discovery and Vector3-result serialization needed tooling-only fixes;
those did not require another player build. Layout and bot usefulness still need human review.

No map-specific coordinates or hand-authored bot routes were added. Navigation consumes the
same bounds/boxes/surfaces as physical movement, so new maps need valid collision data. The
current bounded local, single-floor-per-cell search is not final global/stacked-floor navigation
or deep duo tactics; human findings will determine the next improvement.

Previous smoothing follow-up: **10 focused crosshair tests** and **9 Duel/HUD smoke checks** passed;
one Windows build succeeded (**0 errors, 33 existing deprecated-API/unused-field warnings**).
Real shot simulation/networking is unchanged; the full suite and combat/lifecycle checks were
not rerun. Human review decides whether the quick smoothing feels right.

Previous crosshair iteration: **239/239 EditMode tests**, **52/52 focused network rifle checks**,
**9/9 Duel startup/control/HUD checks** (including projected radius and real firing expansion);
That iteration's final Windows build succeeded (**0 errors, 0 reported warnings**); its initial full compile
reported the existing deprecated Unity/NGO API/unused-field warnings.
The prior bot slice passed Easy/Hard authoritative bot-controller checks and **22 session/role/
lifecycle checks** and received positive human feedback. Technical checks do not accept accuracy
balance, visual alignment or final AI quality. No screenshot exercise is required.
