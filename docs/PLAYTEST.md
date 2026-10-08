# Current playtest — shared-body movement and rifle accuracy

**Rifle-accuracy follow-up after positive bot feedback · 2026-10-09**

Build: `Builds/Windows/BeMyArms.exe`. **PLAY → Duel → P2 → Start Match** tests the P1 movement
partner; repeat as **P1** to test the P2 weapon partner. Try Easy first, then Hard. F6 resets
an encounter; F7 exchanges practice roles. Use matching client/server builds.

## What changed

- Rifle accuracy now depends on P1's actual body movement. A recovered, grounded stationary
  first shot follows P2's aim exactly; crouch-walk adds less spread than walk, sprint adds more,
  and airborne/slide/dodge/kicks have stronger penalties. Humans and bots follow the same rule.
- Stopping removes movement error, not ongoing spray bloom. Pause firing for over **0.30 s**
  to recover first-round bloom; pausing while running still leaves movement error. Recoil remains.
- The rifle HUD shows `BODY STABLE` or `BODY MOTION: +…° SPREAD` for both roles. This is only
  the body penalty, not total spread or a guarantee that the rifle burst has recovered.

## Specifically evaluate accuracy first

1. With a rifle and P1 stopped, allow the burst to recover, then tap a visible target. The first
   shot should have no random deviation (correct aim is still required). Repeat crouched.
2. Compare taps while walking, crouch-walking and sprinting. Can you clearly feel why P1 should
   stop for P2's precision shot? Does sprinting punish precision enough without feeling unusable?
3. Run → stop → tap; then run → spray → stop without releasing fire. The latter must not reset
   the continuing burst. Try jump/slide/dodge/kick: P2 can still fire but loses accuracy.
4. Solo P2: does the accepted P1 bot's positioning give enough stable firing opportunities?
   Solo P1: can you feel your positioning affect the P2 weapon partner's effectiveness?

Four-second spawn protection still suppresses damage; check **PROTECTED** before interpreting
early misses. Zero spread does not fix the still-open near-cover origin/profile questions below.

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

No map-specific coordinates or hand-authored bot routes were added. Navigation consumes the
same bounds/boxes/surfaces as physical movement, so new maps need valid collision data. The
current bounded local, single-floor-per-cell search is not final global/stacked-floor navigation
or deep duo tactics; human findings will determine the next improvement.

Current accuracy follow-up: **227/227 EditMode tests**, **41/41 focused network rifle checks**,
**7/7 Duel startup/control smoke checks**;
Windows build succeeded (**0 errors, 42 existing deprecated Unity/NGO API/unused-field warnings**).
The prior bot slice passed Easy/Hard authoritative bot-controller checks and **22 session/role/
lifecycle checks** and received positive human feedback. Technical checks do not accept accuracy
balance, visual alignment or final AI quality. No screenshot exercise is required.
