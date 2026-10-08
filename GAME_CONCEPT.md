# Be My Arms — Game Concept

**Current design source of truth · Updated 2026-10-09**

> Two humans physically combine into one fighter: P1 controls the body and positioning;
> P2 controls the arms, weapons and aim. Coordination is part of the control scheme.

This document describes the product and intended design, not a claim that every feature
already ships. [ROADMAP.md](ROADMAP.md) owns sequencing and checkpoint status;
[TECHNICAL_PLAN.md](TECHNICAL_PLAN.md) describes the implementation that exists.

## 1. Product identity and priorities

- Competitive, stylized multiplayer shooter, initially for **PC / Steam**.
- The shared-body relationship is the product, not a particular genre imitation. Compact
  arena rounds are the current format; larger/objective/party modes are future possibilities.
- Both roles need meaningful agency and a comparable mastery ceiling. P1 must not become
  merely the WASD passenger while P2 plays the entire game.
- Readability, responsive controls and competitive fairness take precedence over spectacle.
- Communication matters: novices are chaotic; practiced duos coordinate like one organism.
- The working title is not a legally cleared final product name.

**Decision vocabulary:** core principles are guardrails; current directions can change with
human evidence; tuning values are not permanent rules; future goals are not implemented features.

## 2. Two roles, one combat body

### P1 — body, head and legs

P1's separate character has a head, torso, pelvis and legs, but no arms. P1 owns locomotion,
routes, spacing, body orientation and leg-based close combat, viewed in third person.

The current kit includes walking/strafing, crouching, unlimited sprint, jumping, directional
dodge, slide, and light/heavy kicks. Dodge moves the actual body: **no invincibility frames**.
There is no generic sprint stamina bar. Slide and traversal should create useful routes,
not just animation flavor. The current build has **no standalone vault action**; do not
restore an obsolete vault binding as part of cleanup. Any future traversal needs design approval.

P1 can look independently within a bounded neck offset. Beyond the follow threshold the body
turns smoothly; explicit alignment also exists. The accepted playable control path uses
**look-relative movement** while `BodyYaw` remains the shared body's facing and sector center.
The older body-relative-movement specification is superseded. Current follow/neck tuning is
25° / 45°, with follow at 180°/s; these remain tuning, not product law.

### P2 — upper chest, shoulders and arms

P2 is a distinct physical identity, not simply P1's arm skin. Its headless upper-body form
may use a cosmetic lens, eye, crystal or other sensor. Sensor appearance never moves the
gameplay camera or changes hitboxes. Separate-form hand-walking and mounting rituals are
product presentation goals, not a completed lobby animation system.

P2 owns first-person aim, shooting, reloads, weapon/utility selection and hand-based interactions.
Knife and smoke/flash/grenade utility belong to P2; the current implementations are limited
foundations, not a final launch arsenal. P2 uses its own first-person view, not P1's eyes.

The rifle currently uses a simple, stable POV: one support hand under the handguard and a
second hand during reload. Its present composition is **human-accepted for now**, not a
final visual-quality certification or a universal rule for pistols and future weapons.

### Combination and cosmetic compatibility

- Two distinct role identities combine into **one authoritative combat entity**.
- Standard competitive rounds keep the pair combined until the round ends.
- Every valid P1 skin must mount with every valid P2 skin using standardized sockets,
  camera anchors, hitboxes and animation interfaces.
- Cosmetics do not change stats, hitbox geometry, camera placement or role capabilities.
- Separate-form lobby/intro presentation and dramatic mounting are future presentation work.
  Mid-round separation is outside the initial competitive core.

## 3. Aim coupling and mutual dependency

P1 owns `BodyYaw`. P2's firing sector is centered on it, **not on P1's camera/head look**.
The body never automatically chases P2's aim.

The accepted baseline is world-stabilized P2 aim: P1 turning within the available sector
does not drag the crosshair. A sector boundary moving past the aim pushes it with the body.
Discard overflow rather than accumulating hidden mouse displacement; inward input must
respond immediately after boundary contact.

Current **Elastic Soft** tuning has a ±70° resting sector and ±85° outer bound. Outward pressure
allows bounded overtravel; release returns toward the resting sector. P2's mouse aim stays
local and immediate. These values and the Hard/Soft/Free development controls are tuning
surfaces, not authorization to silently replace the accepted coupling behavior.

P2 can request a turn, but cannot directly steal body authority. P1 exposes opportunities;
P2 converts them to damage. Good play includes coordinating reload cover, dodges, angles,
crossings, kicks and utility timing. Do not create arbitrary extra tasks just to keep a role busy.

## 4. Combat direction

### Gunplay

Responsive, aim-driven and headshot-sensitive, generally less instantly lethal than
Counter-Strike and not a bullet-sponge shooter. CS is a readability and rifle-POV reference,
not a demand to duplicate its movement, economy or exact weapon handling.

- A recovered first rifle round while grounded and stationary follows P2's aim exactly (zero
  random spread). P1's actual movement adds error: crouch-walk is lighter than walk, sprint is
  stronger, and jump/slide/dodge/heavy kick have strong penalties. This applies to bots too.
- P2 remains able to fire during P1 movement and kicks. Do not add a hard weapon lockout.
- Sustained automatic fire must require control. The current rifle has real local-aim
  recoil and server-enforced bloom: first rounds are tighter, a long spray spreads, and
  a brief pause (currently over 0.30 s) recovers burst accuracy. Stopping removes movement error,
  not ongoing spray bloom; pausing while running does not remove movement error. Cosmetic
  viewmodel kick is separate from actual aim. Zero spread is not automatic target acquisition.
- P2 must see that dependency directly: dynamic crosshair ticks and a thin spread boundary show
  the next round's movement + burst error, projected with the world-camera FOV. The center dot
  stays on aim. Slide spread decreases with actual sliding speed; no arbitrary cosmetic expansion
  disconnected from the shot model. Network observations/predictions can be corrected by the server.
- Server results—not client effects—decide ammo legality, hits, damage and kills.
- Detailed TTK, damage, recoil, spread, ammunition and recovery are playtest tuning.

**Current implementation:** rifle shots use a standardized body capsule and exposed P1-head
sphere, with target position/height/stance rewound together. Body hits remain **18 damage**;
head hits are provisionally **45 damage (2.5×)** against shared 100 HP—six body hits or three
head hits from full health, not an instant kill. These are playtest values, not locked balance.
Both roles receive server-confirmed hit/elimination information, distinguishing P2's weapon
from P1's kick contribution. A protected target does not produce a damage-confirmation marker.

**Current limitation:** these are competitive gameplay volumes, not animated skeletal/limb
colliders. Cosmetic sensors are never critical. Limb edges/action poses, camera-to-shot-origin
agreement near cover, movement accuracy and remaining shot presentation need further review;
Phase 3 is underway, not declared complete.

### P1 melee

Light kick: quick, lower commitment. Heavy kick: more reach/commitment and more punishable
on a miss. Capture the attack's intended look direction at initiation and keep hits and
presentation aligned with it through the strike. The accepted pose visibly extends the leg
before recovery. Long hard stuns and competitive ragdoll lockouts are not intended.

### Health and loadouts

- One shared health pool; body elimination eliminates both humans for that round.
- No passive in-round regeneration, separate role HP bars, or initial armor/helmet layer.
- Intended critical region: P1's exposed head. P2's sensor is not a critical hitbox.
- Intended inventory: primary, pistol, knife, utility; final launch roster is open.
- No ground weapon scavenging in the standard initial design.
- Utility and hand interactions follow physical ownership; body/traversal interactions belong to P1.

## 5. Modes, rounds and arenas

**Mode counts refer to bodies, not individual humans.**

| Mode | Shared bodies | Full human capacity |
|---|---|---|
| Duel | 1 vs 1 | 4 |
| 2v2 | 2 vs 2 | 8 |

The current development path includes solo with a bot partner, two humans sharing a body
against bots, and direct multi-client sessions. A human **never controls both roles**.
Solo testing in either role is an essential playtest path, not an expendable diagnostic.

Current competitive direction:

- Round elimination, no in-round respawn; a team loses when all its bodies are eliminated.
- First to three round wins, at most five rounds.
- Compact readable arenas: cover, meaningful lanes, some verticality, purposeful flanks.
- Desired contact roughly 10–20 seconds after start and rounds around 2–3 minutes; tuning targets.
- Closing-zone pressure prevents indefinite stalling; timing/radius/damage are tuning.
- A map family can close routes for Duel and open extra lanes for 2v2.
- Current economy draft: fixed per-round budget, no carryover or win/loss snowball; P2 buys,
  P1 can see the loadout. A persistent CS-style economy is not currently required.

Current arenas and bots are playable foundations, not final proof of competitive map quality
or bot competence. Rifle combat is the current focus; human evidence may bring a targeted
arena/bot improvement forward when positioning or partner behavior obstructs useful playtests.

**Current bot direction, confirmed in human review:** P1 teammates establish useful firing
positions and reposition under threat; they do not continuously charge the enemy. Crouching
uses real cover/headroom and preserves deliberate peek opportunities for the partner. Sliding
is a committed exposed-to-cover crossing only when its full stopping path is safe, never a
random action added for QA. P2 bots turn their actual aim with bounded speed/acceleration,
including target and burst changes, rather than snapping onto a target when firing. This applies
to enemy and replacement bots too. Easy remains forgiving; actual accuracy is not guaranteed
by a dice roll. The same role limits and physical movement rules apply to bots and humans.

Bot reasoning should consume map geometry/navigation contracts, not hardcoded arena coordinates.
The current local routing supports the existing collision model; complex multi-floor/global
navigation and high-level duo tactics remain development work, not a claim of final AI.

## 6. Sessions, ranked and communication

### Authority and disconnection

Ranked intends authoritative dedicated servers, never a player's listen-host authority.
The development menu currently launches a local **separate server process**. Production
allocation, authenticated identities and hosting operations are future integrations.

A disconnected role receives temporary bot control. The other human retains only their
own role. Valid reconnection atomically reclaims the original slot; there must never be
two owners. Intentional leave and replacement are distinct from reconnect. Exact grace,
AFK, surrender/remake and penalty policies remain product work.

### Matchmaking and progression goals

- Queue as P1, P2 or Either, solo or premade; pair complementary roles.
- Separate P1 and P2 ratings; account progression is separate from role skill.
- Match outcome and expected opponent strength drive rating, not selfish kill/damage metrics.
- Derived body/team rating and any skill-gap penalty require evidence, not an immutable formula.
- Voice, pings and intent/state feedback should support strangers coordinating quickly.
- Future mute/report/moderation must be practical; provider and production policy remain open.

The repository has local matchmaking, rating and in-memory product seams. It does **not**
have production online matchmaking, durable accounts, voice, commerce or entitlement security.

## 7. Art, cosmetics and platforms

The working presentation is stylized, grounded and readable: smooth shared-rig characters,
distinct uniform role surfaces, exposed P1 head, and independently composed rifle POV.
Final art style, skin range, animation quality, music, SFX and VFX still need human review
and production work. Generated content is not automatically final production art.

Accounts are intended to own both role identities. Skins, weapons, mounting/lobby animations,
poses and profile cosmetics may express identity; **no hero stat kits or pay-to-win**.
Progression/store specifics and any battle pass are future decisions, not a shipped economy.
Content sourcing remains **free-only with recorded licenses** unless explicitly authorized otherwise.

PC/Steam comes first. Keep identity, authority, input domains and service adapters portable
for possible mobile/console/cross-progression later. Do not weaken PC mechanics to pre-solve
a mobile port. Future cross-play should be input-aware; platform pools, assistance and
account linking must be designed and tested rather than assumed equivalent.

## 8. Open decisions and guardrails

Open: combat balance/TTK, final weapon roster, utility/traversal detail, arena tuning,
hosting/backend/voice/anti-cheat vendors, final art/title/store/progression, cross-play pools,
input assistance and ranked policy. **NGO versus Entities is not an open stack decision**:
the current project uses NGO with its existing custom prediction and lag compensation.

Before a feature or change, ask:

1. Does it strengthen two humans controlling one body?
2. Does each role retain agency and meaningful skill?
3. Is anatomy/control ownership logical and competitive information readable?
4. Does it preserve server authority and cosmetic-only fairness?
5. Is this an approved design decision, or tuning that needs human evidence?

Development history is optional reading in [docs/archive](docs/archive/README.md), not an
alternative source of current decisions. Automated checks establish correctness where
applicable; **humans accept visual readability, comfort and game feel**.
