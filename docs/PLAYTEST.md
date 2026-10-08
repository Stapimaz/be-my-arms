# Current playtest — purposeful bot positions and smooth aim

**Focused bot iteration after the first Phase 3 rifle slice · 2026-10-09**

Build: `Builds/Windows/BeMyArms.exe`. **PLAY → Duel → P2 → Start Match** tests the P1 movement
partner; repeat as **P1** to test the P2 weapon partner. Try Easy first, then Hard. F6 resets
an encounter; F7 exchanges practice roles. Use matching client/server builds.

## What changed

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

Human movement, immediate local P2 mouse aim, Elastic Soft, accepted POV, rifle recoil/bloom,
damage rules, prediction and session authority are unchanged.

## Specifically evaluate

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

**214/214 EditMode tests**, Easy/Hard authoritative bot-controller checks and **22 session/role/
lifecycle checks** passed. The Windows build succeeded with zero errors; reviewed warnings are
existing deprecated Unity/NGO APIs and unused fields. These prove technical behavior—not that
the AI is pleasant, human-like or tactically optimal. No screenshot exercise is required.
