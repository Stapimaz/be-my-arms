# BE MY ARMS — GAME CONCEPT

**Document status:** Product and game-design concept  
**Working title:** Be My Arms (working title; not legally cleared and not final)  
**Engine:** Unity 6  
**Current production project:** `C:\Users\stapi\GameDev\be-my-arms` (`be-my-arms`)  
**Primary launch target:** PC / Steam  
**Future platform intent:** Mobile; consoles are a possible later target  
**Last updated:** 2026-09-19

---

## 0. How to Read This Document

This document separates decisions by confidence level so future developers and AI agents do not accidentally turn a prototype choice into a permanent product rule.

- **LOCKED** — core product identity or a design direction that should not be casually changed.
- **CURRENT DIRECTION** — intended first serious implementation; can change if playtesting disproves it.
- **DRAFT** — a practical starting point chosen so development can proceed.
- **TUNING** — numeric values and detailed balance that must be determined through playtests.
- **FUTURE / EXPERIMENTAL** — deliberately outside the initial competitive core.

The project is built toward the **full multiplayer product**. Development order, milestone
status and acceptance criteria are owned by `ROADMAP.md`; implementation architecture is owned
by `TECHNICAL_PLAN.md`. This document does not maintain a parallel roadmap or architecture
plan.

---

# 1. Product Vision

## 1.1 Core Identity — LOCKED

The defining product idea is:

> **One combat body is controlled by two human players.**

This is not simply a normal shooter with two control schemes. Two separate players, each represented by a distinct avatar/identity, physically combine into a single playable combat body.

The game should be immediately understandable at a social level — “two people control one fighter” — while producing a high coordination skill ceiling in competitive play.

The product should not be permanently defined by a single genre or mode. Battle royale, hero shooter, Counter-Strike clone and arena shooter may describe individual modes or inspirations; the persistent identity is the shared-body multiplayer mechanic.

## 1.2 Product Ambition

The long-term target is a multiplayer game that can support:

- casual friend-group play,
- funny and highly shareable moments,
- streamer/content-creator appeal,
- serious ranked competition,
- a meaningful mastery curve,
- long-term cosmetic identity and progression,
- multiple game modes over time.

The game should be accessible enough that younger players and friend groups understand the premise quickly, but deep enough that high-ranked duos can spend years improving.

The intended high-level fantasy is:

- new duos are chaotic and communicate constantly,
- intermediate duos develop short calls and habits,
- expert duos begin to look like a single organism.

Communication is effectively part of the control scheme.

---

# 2. The Shared Body

## 2.1 Account-Level Avatar Structure — LOCKED

Every player account owns two cosmetic identities:

1. a **P1 avatar**, and
2. a **P2 avatar**.

A player can queue as either role, so both avatar types belong to the same account.

Character differences are cosmetic only. P1 and P2 are **roles**, not hero classes.

There are no gameplay kits where one P1 is faster, another has more health, or one P2 has special weapons. If P1 gameplay changes in an update, the change applies to every P1. The same rule applies to P2.

This protects competitive integrity and makes cosmetics safe to monetize without creating character meta or pay-to-win pressure.

---

# 3. P1 Physical Identity

## 3.1 Separate Form — LOCKED

P1 is the primary body:

- normal head,
- main torso,
- pelvis,
- legs,
- no arms while separate.

P1 still needs to look like a deliberate, complete character design rather than an unfinished model.

## 3.2 Combat Responsibility — LOCKED

P1 controls:

- locomotion,
- body orientation,
- route through the map,
- spacing and positioning,
- survival through movement,
- creation of firing angles for P2,
- denial of enemy firing angles,
- close-range leg/body attacks.

P1 uses a **third-person camera**.

P1 must never feel like “the WASD player while P2 gets the real game.” P1's skill ceiling should be comparable to P2's, but expressed through different skills.

---

## 3.3 Look and Body Model — CURRENT DIRECTION

P1's camera/head look direction is **decoupled from the body's facing (`BodyYaw`)**:

- mouse / right-stick controls the camera look direction;
- the camera can turn independently up to a **neck-offset limit** around `BodyYaw`;
- when the look offset grows past a threshold, the **body smoothly turns to follow** the look;
- P1 has an explicit **"align body to look"** action that turns `BodyYaw` to the look quickly but
  smoothly;
- WASD movement stays relative to **`BodyYaw`**, so looking around does not change movement axes.

P2's firing sector is tied to **`BodyYaw`**, never to P1's camera/head direction. Looking around
inside the neck limit therefore does not move P2's sector; the sector rotates only when the body
turns — either by naturally following the look or via the align action.

Neck-offset limit, follow threshold and follow/align speeds are **TUNING**. The align action's
input binding is temporary/configurable and is **not** a design decision.

---

# 4. P2 Physical Identity

## 4.1 Separate Form — LOCKED

P2 is a separate physical character composed approximately of:

- two arms,
- shoulders / clavicle region,
- upper chest / upper torso structure,
- no normal human-style head.

P2 has an independent visual sensor because P2 has an independent point of view. The visual sensor can vary dramatically by skin:

- robotic lens,
- fantasy eye,
- crystal,
- biological organ,
- camera module,
- other theme-specific solutions.

In menus/lobbies, P2 can move independently using its arms/hands, for example by crawling or hand-walking.

This should become part of the game's recognizable visual personality.

## 4.2 Combat Responsibility — LOCKED

P2 controls:

- first-person aiming,
- shooting,
- weapon switching,
- reloads,
- knife/melee weapon use,
- grenades/utility,
- hand-based world interactions,
- weapon and utility purchasing.

P2 uses its **own first-person shooter camera**. P2 does not see through P1's eyes.

The gameplay camera anchor should be standardized around P2's shoulder/upper-chest region and should not move based on cosmetic sensor placement.

The FPS presentation should be readable and comfortable rather than a deliberately shaky body-cam gimmick.

---

# 5. Combination / Mounting

## 5.1 Match Start — LOCKED

At match start, P1 and P2 physically combine into one combat body.

All P1 skins must be compatible with all P2 skins. This requires standardized:

- attachment geometry,
- gameplay skeleton,
- camera anchors,
- hitboxes,
- weapon anchors,
- animation interfaces.

These are enforced as the **rig contract** defined in M5 (`docs/M5_PRODUCT_SYSTEMS.md`); every
cosmetic skin is authored against that contract, and the contract check rejects any skin that would
move authoritative hitboxes or gameplay stats. Production skins are created against it from M6
onward.

The combined result should still visibly contain **both players' cosmetic identities**.

P2 must not merely look like “P1's arm skin.” It should remain obvious that a second character has attached to the body.

Unusual combinations are a feature:

- fantasy P1 + military P2,
- toy P1 + undead P2,
- knight P1 + chrome robot P2,
- etc.

The mounting/combine animation can become an important brand ritual in lobbies, round intros, trailers, and cosmetics.

## 5.2 Separation Rule — LOCKED

During a standard competitive round:

- P1 and P2 remain combined until the round ends.
- There is no normal mid-round separation mechanic.

They may appear separately again in:

- lobby,
- pre-match,
- post-round,
- post-match presentation.

### Future / Experimental

A later mode or mechanic could explore emergency separation, where:

- P1 is armless and highly vulnerable,
- P2 crawls using the arms,
- both halves have limited capabilities.

This is not part of the initial competitive core.

---

# 6. Split Input and Interdependence

## 6.1 Aim Sector — CURRENT DIRECTION

P2 cannot rotate infinitely around the body. P2's horizontal aim must stay within an allowed
sector centered on P1's body-forward direction (`BodyYaw`, not P1's camera/head direction — see
§3.3); P2 cannot aim outside that sector.

Draft prototype target:

- approximately ±70°,
- approximately 140° total horizontal sector.

The exact value is **TUNING**, not sacred.

Near the sector boundary, aiming may:

1. slow near the limit,
2. then clamp.

This creates the game's most important mechanical dependency:

- P2 can see or want to shoot an enemy,
- but P1 must orient the shared body correctly to expose that target.

P1 therefore controls opportunities; P2 converts them into damage.

**The sector bound is the design rule; the coupling behavior inside the sector is not locked.**
How P2's aim responds while P1 rotates — staying world-stable, moving rigidly with the body, or
otherwise — is still open. The draft boundary behavior above (slow near the limit, then clamp)
is likewise provisional. The currently prototyped direction is **Model C**: world-stabilized aim
that the sector boundary pushes with the body. Model C is the current prototyped/preferred
direction, not a locked decision. Its alternatives are described in `TECHNICAL_PLAN.md` §2; the
model remains provisional until the M1 playtest, and choosing it is a decision gate in
`ROADMAP.md`.

## 6.2 High-Skill Coordination Goal — LOCKED

P1 movement and P2 gunplay should constantly affect each other.

Examples:

- P1 rotates to expose an enemy before P2 can shoot.
- P1 ducks behind cover while P2 reloads.
- P2 suppresses an angle so P1 can cross open space.
- P1 performs a kick/gap-close while P2 tries to fire through the movement.
- P1 stops or steadies briefly to give P2 a high-accuracy shot.
- P2 calls a flash timing that depends on P1's approach path.

Neither role should be independently optimal.

---

# 7. P1 Movement

## 7.1 Base Kit — CURRENT DIRECTION

P1's movement toolkit includes:

- standard movement,
- body facing/orientation,
- unlimited sprint,
- jump,
- directional dodge,
- slide,
- vault/traversal,
- close-range attacks.

## 7.2 Sprint — CURRENT DIRECTION

Standard sprint is unlimited.

There is no generic stamina bar for normal running.

This keeps movement responsive and avoids turning P1 into a stamina-management role.

## 7.3 Dodge — LOCKED DIRECTION

Dodge is:

- directional,
- limited by a short cooldown,
- a single immediate dodge rather than a multi-charge system in the first implementation,
- not granted invincibility frames.

A successful dodge works by genuinely moving the body/hitbox out of the shot path.

The enemy can still hit the player during the dodge if their aim is correct.

Exact distance, duration, speed, recovery and cooldown are **TUNING**.

## 7.4 Slide / Vault — CURRENT DIRECTION

Slide and traversal should reward:

- map knowledge,
- momentum control,
- route optimization,
- coordinated aggression.

They should not exist merely as animation flavor.

Exact slide velocity, friction, jump chaining and vault behavior are **TUNING**.

---

# 8. P1 Close-Range Combat

## 8.1 Light Attack — CURRENT DIRECTION

P1 has a fast light kick.

Draft behavior:

- low commitment,
- relatively low damage,
- small knockback and/or flinch,
- short enough recovery to use as a normal close-range option.

## 8.2 Heavy Attack — CURRENT DIRECTION

P1 also has a stronger heavy/flying kick.

Draft behavior:

- more forward commitment,
- longer cooldown,
- greater damage,
- stronger displacement,
- possibly a very short stagger,
- more punishable on miss.

Long hard-stuns and normal competitive ragdoll-lockouts are not intended.

All detailed values are **TUNING**.

## 8.3 P2 During Kicks — LOCKED

P2 remains able to fire while P1 performs light/heavy attacks.

There is no hard weapon lockout.

However, aggressive body movement — especially a flying kick — creates a strong accuracy penalty.

This intentionally allows high-chaos/high-skill combinations such as:

- flying kick + shotgun blast,
- kick into close-range SMG burst,
- P1 gap-close while P2 attempts a difficult moving shot.

---

# 9. P2 Gunplay

## 9.1 Desired Feel — CURRENT DIRECTION

Combat is:

- responsive,
- somewhat arcade,
- aim-skill driven,
- movement-skill driven,
- headshot-sensitive,
- faster than bullet-sponge shooters,
- generally less instantly lethal than Counter-Strike.

The target is not a copy of Counter-Strike's stop-and-shoot model, but CS is a useful reference for:

- meaningful aim,
- headshot reward,
- readable weapon roles,
- competitive round tension.

Exact TTK is **TUNING**.

P1 should usually have enough time for dodge, movement and positioning to matter, while a highly skilled P2 must still be capable of rapidly swinging a fight through strong aim/headshots.

## 9.2 Movement vs Accuracy — LOCKED DIRECTION

P1 movement affects P2 weapon accuracy.

Current hierarchy:

- standing still → best accuracy,
- normal walk/strafe → light penalty,
- sprint → clear penalty,
- jump / slide / dodge → strong penalty,
- heavy/flying kick → strong penalty.

The system should reward coordination without requiring full Counter-Strike-style hard stopping for every shot.

Exact recoil, spread and recovery curves are **TUNING**.

---

# 10. Weapons and Loadout

## 10.1 Loadout Structure — CURRENT DIRECTION

P2's basic combat inventory is:

1. **Primary weapon**
2. **Pistol / secondary**
3. **Knife / melee weapon**
4. **Utility**

Primary categories can include, for example:

- rifles,
- SMGs,
- shotguns,
- other future primary archetypes.

The exact launch roster is not locked.

## 10.2 Knife — LOCKED DIRECTION

The knife is usable as an actual melee weapon.

It is not cosmetic-only.

Knife combat should remain relatively simple and should not overshadow P1's leg-based melee role.

Likely uses:

- point-blank finisher,
- emergency attack,
- close-range option while preserving ammo,
- cosmetic expression.

Exact slash/stab behavior is **TUNING**.

## 10.3 Ground Pickups — LOCKED DIRECTION

Weapons are **not picked up from the ground** in the current design.

This keeps:

- loadouts readable,
- economy predictable,
- the P2 inventory clean,
- fallen players from turning rounds into weapon-scavenging loops.

This can be revisited for a special future mode, but it is not part of the standard initial competitive design.

---

# 11. Utility

## 11.1 Utility Types — CURRENT DIRECTION

Core utility includes:

- smoke,
- flash,
- grenade / explosive.

Additional utility can be explored later.

## 11.2 Control Ownership — LOCKED

P2 controls and throws utility because P2 physically controls the arms.

This preserves physical logic rather than artificially splitting tasks to keep both roles busy.

Exact:

- capacity,
- prices,
- cooldowns,
- round availability,
- throw physics,
- effect durations

are **TUNING / DRAFT**.

---

# 12. World Interactions

## 12.1 Physical Ownership Rule — LOCKED PRINCIPLE

World interaction should follow the anatomy of the combined character.

### P2 owns interactions that require hands

Examples:

- pressing a button,
- pulling a lever,
- picking up/placing a hand-held objective,
- manipulating a device,
- opening something manually.

### P1 owns body/traversal interactions

Examples:

- vaulting,
- climbing/mantling,
- body collision,
- kicking,
- movement-triggered traversal.

Do not give P1 arbitrary interaction buttons merely to make the role busier.

If a map mechanic does not naturally belong to either role, it should be designed case-by-case.

Specific interactive map elements are not yet locked.

---

# 13. Health, Damage and Hitboxes

## 13.1 Shared HP — LOCKED

The combined P1+P2 body has **one shared health pool**.

There are no separate P1 and P2 HP bars in the initial competitive design.

If the body dies, both humans controlling it are eliminated for the round.

## 13.2 Health Regeneration — LOCKED

There is no passive health regeneration during a round.

Damage persists until the round ends unless a future explicit healing mechanic is introduced.

No healing mechanic is part of the current initial core.

## 13.3 Armor — CURRENT DIRECTION

There is no armor/helmet layer in the initial version.

Damage goes directly to shared HP.

Armor can be reconsidered later if the economy and TTK require another balancing layer.

## 13.4 Critical Region — LOCKED

P1's head is the critical/headshot region.

P2's:

- arms,
- shoulders,
- upper-chest body,

take normal damage.

P2's cosmetic eye/lens/crystal/sensor is **not** a separate critical hitbox.

Cosmetics never alter gameplay hitboxes.

---

# 14. Match Modes

## 14.1 Important Notation

When this document says **1v1** or **2v2**, it refers to **shared combat bodies**, not individual humans.

### Duel / 1v1 bodies

- 1 shared body vs 1 shared body
- 4 human players total

### 2v2 bodies

- 2 shared bodies vs 2 shared bodies
- 8 human players total

This distinction must remain explicit in code, design documents, matchmaking, analytics and UI terminology.

## 14.2 Initial Serious Modes — LOCKED DIRECTION

The first serious competitive modes are:

- Duel,
- 2v2.

Future modes may include:

- larger teams,
- objective modes,
- rotating casual modes,
- battle royale,
- social/party modes.

No single mode must permanently define the game.

---

# 15. Round Structure

## 15.1 Elimination Format — CURRENT DIRECTION

Initial competitive structure:

- round-based elimination,
- no respawn during a round,
- body eliminated → both players eliminated,
- team loses when all its bodies are eliminated,
- match is first to 3 round wins,
- maximum 5 rounds.

## 15.2 Pacing Target — CURRENT DIRECTION

Desired pacing:

- meaningful enemy contact usually around 10–20 seconds after round start,
- normal round approximately 2–3 minutes,
- minimal time spent simply searching for opponents.

These are targets, not fixed constants.

## 15.3 Anti-Stall Zone — CURRENT DIRECTION

If a round runs too long, the playable area begins closing and/or the outside zone deals damage.

This is structurally inspired by Brawl Stars-style anti-stall pressure.

Purpose:

- prevent indefinite camping,
- avoid prolonged hide-and-seek,
- force a conclusion,
- keep match duration predictable.

Exact start time, shrink rate, shape, visual treatment and damage are **TUNING**.

---

# 16. Economy / Buy Phase

## 16.1 Initial Economy — DRAFT

A simple first implementation:

- every round starts with the same fixed budget,
- budget does not carry across rounds,
- no win/loss bonus,
- no economy snowball,
- P2 purchases the weapon/utility loadout,
- P1 can see the chosen loadout.

This preserves tactical loadout choice while avoiding a second layer of economy complexity before combat is proven.

This is explicitly **not a permanent product commitment**.

A fuller Counter-Strike-like persistent economy can be considered after playtesting.

---

# 17. Map Philosophy

## 17.1 Duel / 2v2 Scale — CURRENT DIRECTION

Initial Duel and 2v2 maps should be compact, readable combat arenas rather than full Counter-Strike-scale maps.

Brawl Stars is a useful **structural pacing reference**, not an art or exact layout reference.

Maps should contain:

- clear approach routes,
- strong cover,
- readable sightlines,
- a small number of meaningful flank routes,
- enough room for P1 movement skill,
- enough structure for P2 angle management,
- some verticality,
- little unnecessary maze-like traversal.

First meaningful contact should normally happen quickly.

The M7 production arenas implement this direction with a modular greybox-plus kit and a Duel/2v2
map family (closed vs open flanks); see `docs/M7_CONTENT.md`.

## 17.2 Map Families

A practical content strategy is to create map families with different curated footprints:

- Duel version: tighter routes / selected side paths closed,
- 2v2 version: extra lanes or flanks opened.

They can share:

- environment theme,
- assets,
- lighting language,
- landmarks,

without needing identical playable geometry.

Larger CS-like tactical maps can be explored later for 3v3 bodies or larger formats.

---

# 18. Matchmaking and Ranked

## 18.1 Role Queue — LOCKED

Players may queue as:

- P1,
- P2,
- Either / no preference.

Players can queue solo or as a premade duo/party.

Solo matchmaking finds the complementary role.

## 18.2 Separate Role Ratings — LOCKED DIRECTION

P1 and P2 have separate competitive Elo/MMR progression.

Example:

- P1 rating: Diamond-level
- P2 rating: Platinum-level

A player's skill in one role must not automatically imply equal rating in the other role.

When a player completes a ranked match:

- playing P1 changes their P1 rating,
- playing P2 changes their P2 rating.

Account level/progression remains separate from role rating.

## 18.3 Body MMR — DRAFT

Because one body contains two separately rated humans, matchmaking needs a derived body rating.

Initial formula candidate:

`BodyMMR = average(P1_MMR, P2_MMR) - k × abs(P1_MMR - P2_MMR)`

where `k` is a small playtest-derived penalty coefficient.

Purpose of the optional gap penalty:

- a highly imbalanced duo may not perform exactly like two equally skilled players with the same average,
- the weaker role may become a meaningful bottleneck because both players share one body.

The penalty is **not locked**. Playtests may show that plain average is sufficient.

## 18.4 2v2 Team Rating — DRAFT

For 2v2 bodies:

1. calculate each body's derived rating,
2. combine the two body ratings into a team rating,
3. match teams based on:
   - expected skill balance,
   - latency/region,
   - role constraints,
   - party constraints,
   - input/device profile when cross-platform play is active.

Matchmaking rules can relax gradually as queue time increases.

## 18.5 Input-Aware Matchmaking — CURRENT DIRECTION

When multiple platforms/input methods exist, matchmaking should understand the input composition of each shared body.

Example body profiles:

- P1 mouse/keyboard + P2 mouse/keyboard,
- P1 controller + P2 controller,
- P1 touch + P2 touch,
- P1 touch + P2 mouse/keyboard,
- P1 controller + P2 mouse/keyboard.

The matchmaker does not need to require exact mirrors in every match, but ranked should avoid pretending that all input combinations are automatically equivalent.

The eventual formula may consider:

- role-specific MMR,
- derived BodyMMR,
- each role's input method,
- premade vs solo status,
- latency/region,
- queue time.

Input weighting is a matchmaking/tuning problem, not a reason to split the game into unrelated platform versions.

## 18.6 Performance-Based Rating — CURRENT DIRECTION

Do not initially reward ranked rating directly for:

- kills,
- damage,
- headshots,
- scoreboard position.

Primary rating result should come from match outcome and expected opponent strength.

Reason: individual-stat Elo incentives could make P1/P2 chase selfish metrics instead of coordinating as one body.

## 18.7 Overall Rank — OPEN

An optional general/profile rank may exist.

However:

- matchmaking should rely on role-specific rating,
- an overall badge should not obscure P1/P2 skill differences.

A simple first presentation could be:

- P1 Rank,
- P2 Rank,
- Account Level.

An additional overall prestige rank can be designed later.

---

# 19. Communication

## 19.1 Product Requirement — LOCKED PRINCIPLE

Because coordination is effectively part of the control scheme, communication cannot be treated as an afterthought.

The game should eventually provide:

- reliable duo/team voice,
- quick pings,
- short tactical communication options,
- clear indicators of teammate intent/state.

For solo queue in particular, a P1 and P2 who have never met must be able to coordinate rapidly.

## 19.2 Voice Structure — CURRENT RECOMMENDATION

A reasonable initial structure:

- direct body/duo voice channel,
- team channel in 2v2,
- mute/report tools,
- push-to-talk and voice activation options.

Provider is not yet locked.

A platform-neutral solution is strongly preferred so future Steam/mobile/console cross-play is not blocked.

---

# 20. Visual Direction

## 20.1 Current Art Target

The game is **stylized, readable and expressive**, not ultra-realistic.

Current rough visual range:

- more grounded/detailed than Brawl Stars,
- not realistic military simulation,
- Valorant-level readability/stylization is a useful reference,
- final look can be more physical/textured and less flat than Valorant,
- proportions should not become overly chibi/toy-like unless a specific skin deliberately does so.

The exact art direction is still open and should be resolved through:

- concept art,
- prototype scenes,
- character silhouette tests,
- combined P1+P2 skin tests,
- readability tests at gameplay distance.

An M6 production test authored a first-pass stylized/readable direction purely to prove the
Blender → Unity pipeline; it is a production test, **not** a final art-direction decision
(`docs/M6_ART_PIPELINE.md`). M7 refined the character forms (articulated joints, layered armour,
exposed P1 head) and adopted a grounded stylized tactical-sci-fi **working production baseline**
(refinable) (`docs/M7_ART_DIRECTION.md`).

## 20.2 Visual Priorities

Regardless of final style:

- P1/P2 silhouettes must remain readable,
- enemy/friendly identification must be immediate,
- weapons and utility must read clearly,
- headshot region must be visually understandable,
- skins cannot conceal hitbox logic,
- competitive visibility must beat visual spectacle.

---

# 21. Cosmetics and Monetization

## 21.1 Core Principle — LOCKED

Monetization is cosmetic.

No pay-to-win gameplay differences.

## 21.2 Cosmetic Categories

Potential products include:

- P1 skins,
- P2 skins,
- weapon skins,
- knife cosmetics,
- lobby animations,
- mounting/combine animations,
- poses,
- emotes,
- effects,
- profile banners/cards,
- duo presentation cosmetics.

The ability for two independently selected P1/P2 skins to combine is itself a major cosmetic system.

## 21.3 Progression — CURRENT DIRECTION

The game can have:

- account levels,
- role rank progression,
- occasional free cosmetic drops/rewards,
- event rewards,
- possibly battle-pass-like progression later.

Paid cosmetics are expected to be the primary monetization path.

The experience should not constantly flood players with boxes or random reward popups.

If paid randomized items are ever considered, legal/regulatory requirements by platform and country must be reviewed separately. They are not required for the core business model.

---

# 22. Platform Strategy

## 22.1 Initial Launch — LOCKED DIRECTION

Initial target:

- PC,
- Steam.

The game should take advantage of Steam where useful for:

- authentication,
- friends/invites,
- presence,
- achievements,
- store/DLC/entitlements,
- community integration.

## 22.2 Future Platforms

Long-term possibilities:

- mobile,
- PlayStation,
- Xbox,
- Nintendo platforms,
- other storefronts.

These are not launch promises.

The initial architecture should simply avoid unnecessarily making them impossible.

## 22.3 Cross-Progression — LOCKED DIRECTION

Player identity should not be permanently equivalent to a Steam ID.

The intended long-term model is:

- Steam identity links to a platform-neutral game account,
- future Apple/Google/console identities can link to the same logical account,
- cosmetics, account progression and role ranks can carry across platforms where platform-holder policies permit.

Cross-progression is a product goal, not an afterthought.

## 22.4 Cross-Play — CURRENT DIRECTION

The game should be architected to support cross-play when additional platforms launch.

Cross-play does **not** mean every platform/input method must be forced into one identical ranked pool.

The intended policy is:

- casual modes may use broader cross-platform pools,
- ranked matchmaking is input-aware,
- mouse/keyboard, controller and touch are treated as meaningful matchmaking attributes,
- mixed-platform / mixed-input premade parties are allowed where practical,
- matchmaking attempts to place them against reasonably comparable body/team input profiles,
- platform-specific opt-in/opt-out settings may be offered where required or useful.

A single shared body may contain players on different platforms or input devices. For example:

- P1 on mobile/touch,
- P2 on PC/mouse.

This is allowed by the product concept and should not be artificially blocked by architecture.

The exact fairness model, controller aim assist and mobile touch assistance are future tuning/design decisions.

## 22.5 Platform Adaptation Principle — LOCKED

The PC version should **not** be simplified or mechanically weakened in advance merely to make a future mobile port easier.

The order of priorities is:

1. make the PC competitive game excellent,
2. keep the underlying architecture portable,
3. adapt controls/UI for mobile or console later without changing the core identity unnecessarily.

The target is the same fundamental game and ecosystem across platforms, with platform-appropriate controls and matchmaking rules rather than separate unrelated games.

---

# 23. Multiplayer Technical Architecture

## 23.1 Core Networking Requirement — LOCKED PRINCIPLE

The final ranked game should use a **server-authoritative client/server model**.

For serious ranked play, do not make a player's machine the authoritative listen server.

Reasons:

- no host advantage,
- better cheat resistance,
- consistent simulation,
- cleaner reconnect/disconnect rules,
- easier ranked integrity,
- better future cross-platform support.

Peer-hosted/Relay sessions remain useful for local tests, prototypes or potentially casual/private modes, but should not define the ranked production architecture.

## 23.2 Shared-Body Network Authority

The shared body is unusual because two remote clients send control inputs into one authoritative combat entity.

The server should treat the body as a single authoritative entity with **split input domains**.

### P1 input stream

P1 sends:

- movement axes,
- sprint,
- jump,
- dodge,
- slide,
- body orientation,
- kick commands,
- traversal actions.

### P2 input stream

P2 sends:

- view/aim,
- fire,
- reload,
- weapon selection,
- knife attack,
- utility selection/use,
- hand interaction commands.

The server combines the two input streams into one body simulation.

Neither client should be trusted to directly declare:

- damage dealt,
- confirmed kills,
- final hit results,
- impossible movement,
- weapon fire beyond allowed rate,
- inventory state.

## 23.3 Prediction and Reconciliation

Fast competitive play requires local responsiveness.

### P1

P1 should locally predict locomotion and receive authoritative server reconciliation.

### P2

P2's local camera/aim must feel immediate even while the underlying body is networked.

Weapon presentation can be locally responsive, while authoritative results are server validated.

A special technical priority is **P2 camera comfort when P1 movement is corrected by the server**. Reconciliation cannot cause violent first-person camera snapping.

The network model should therefore separate:

- local visual camera smoothing,
- predicted input response,
- authoritative body state.

## 23.4 Hit Detection and Lag Compensation

Production gunplay should be server authoritative with appropriate lag compensation.

The server should validate:

- fire cadence,
- ammunition,
- weapon state,
- player state,
- aim sector constraints,
- body orientation,
- hit result.

Because P2's firing arc depends on P1's body orientation, historical body orientation needs to be considered during lag-compensated hit validation.

This shared-body relationship is one of the project's highest-risk networking features and must be tested early.

---

# 24. Networking Stack Selection — DECIDED

The project uses **Netcode for GameObjects (NGO)** on Unity Transport, selected by the M0.5
bake-off and confirmed by the M2 networking spike. Because NGO has no built-in prediction or lag
compensation, the game maintains a custom deterministic body simulation with client-side
prediction/reconciliation and server-side lag compensation. Netcode for Entities was evaluated and
rejected; it remains isolated on a branch. Decision record: `docs/M05_NETCODE_BAKEOFF.md`;
comparison and architecture: `TECHNICAL_PLAN.md` §8 and §9.

---

# 25. Sessions, Matchmaking and Hosting

Product constraints:

- Ranked play runs on authoritative dedicated servers. Player-hosted or listen-server sessions
  are for prototypes, private matches and casual play only, and do not define the ranked
  architecture. **[LOCKED]**
- The service layer, hosting provider and hosting vendor are not locked.

Service and hosting implementation guidance — Unity Multiplayer Services, the provider-neutral
hosting adapter, and the retirement of Unity Multiplay Game Server Hosting — is maintained in
`TECHNICAL_PLAN.md` §7 and §9.

---

# 26. Steam Networking Position

Steam Datagram Relay / Steam Networking can provide useful Steam-specific networking features, including dedicated-server routing and IP protection.

However, because the product may later ship on mobile/consoles, the **core simulation/network architecture should not depend exclusively on Steam-specific P2P networking**.

Steam integration should be an outer platform layer rather than the fundamental gameplay authority model.

This keeps future cross-platform work realistic.

---

# 27. Authentication and Accounts

## 27.1 PC Launch

Steam should be the frictionless identity provider on Steam.

The game can link Steam authentication to a platform-neutral backend player account.

## 27.2 Future Identity Linking

The same logical account can later link:

- Steam,
- Apple,
- Google,
- console identities,
- other supported identity providers.

This is the desired basis for cross-progression.

## 27.3 Authoritative Data

The client must not be authoritative for:

- purchased cosmetics,
- currency,
- rank/MMR,
- account progression,
- match results,
- inventory entitlements.

These belong in trusted backend/server systems.

Exact backend storage/service vendor is not yet locked.

---

# 28. Dedicated Server Build

Dedicated headless server builds, tick-rate targets and server operational requirements are
implementation concerns maintained in `TECHNICAL_PLAN.md` §7 and §9. The product-level
requirement is that ranked play uses authoritative dedicated servers rather than player-hosted
ones (see §23 and §25).

---

# 29. Reconnect, Leaving and Failure Cases

Competitive design must eventually define:

- temporary disconnect grace period,
- reconnect behavior,
- what happens to a body if P1 disconnects,
- what happens if P2 disconnects,
- AFK detection,
- surrender/remake policy,
- intentional leave penalties.

### Current principle — LOCKED

A disconnected role is **temporarily controlled by a bot** until that player reconnects. Only the
disconnected role is bot-controlled; the remaining human **never** gains control of both roles.
The shared body and the match stay alive while a role is substituted. When the original player
reconnects with a valid session/token, that role transfers **atomically** from the bot back to the
player, preserving body/role state. There must never be two active owners of the same role.

Exact grace-window length, bot competence, and AFK/leave policy are **TUNING / product decisions**.

---

# 30. Anti-Cheat and Competitive Integrity

## 30.1 Server Validation — REQUIRED

The first anti-cheat layer is authoritative architecture: the server validates gameplay and
rejects impossible or unauthorized actions. The specific list of validated domains is
maintained in `TECHNICAL_PLAN.md` §9.

## 30.2 Client Anti-Cheat — OPEN

A PC anti-cheat solution may be required later depending on scale and threat model.

Provider is not locked.

The codebase should avoid architecture that makes authoritative validation impossible and then attempts to solve cheating only through a client anti-cheat product.

---

# 31. Analytics / Playtest Telemetry

Because many combat decisions are intentionally left to playtesting, telemetry is part of design, not an afterthought.

Useful measurements include:

- round length,
- time to first contact,
- TTK distribution,
- headshot rate,
- P1/P2 win correlation,
- role queue popularity,
- dodge success rate,
- kick usage and hit rate,
- accuracy by P1 movement state,
- body skill-gap vs win rate,
- smoke/flash effectiveness,
- map heatmaps,
- angle/route usage,
- disconnect rate,
- P1/P2 satisfaction ratings.

This data should be used to resolve tuning questions rather than inventing numbers from theory.

---

# 32. Explicit Non-Goals for the Initial Competitive Core

The initial serious version does **not** require:

- hero classes,
- stat-changing skins,
- pay-to-win items,
- mid-round P1/P2 separation,
- passive health regeneration,
- armor/helmet system,
- ground weapon pickups,
- giant Counter-Strike-scale maps for Duel,
- complex persistent economy,
- long hard-stun melee,
- player-hosted ranked authority,
- battle royale,
- mobile launch on day one.

These may be reconsidered later only if they strengthen the shared-body identity.

---

# 33. Open Decisions

The following are intentionally unresolved and should not block the concept document.

## Playtest / Balance

- exact TTK,
- health value,
- headshot multipliers,
- weapon roster,
- recoil/spread,
- ammo counts,
- utility prices/capacity,
- kick damage/knockback/cooldowns,
- dodge timing/distance,
- slide physics,
- aim-sector angle,
- closing-zone timing,
- server tick rate.

## Product

- final game name,
- exact art style,
- overall/profile rank presentation,
- final cosmetic store structure,
- battle pass or equivalent,
- detailed account-level progression,
- future objective modes.

## Technical

- Netcode for Entities vs NGO after networking spike,
- dedicated-server hosting vendor,
- exact backend persistence stack,
- voice provider,
- anti-cheat provider,
- exact cross-play pool rules and opt-in/opt-out policy,
- controller aim-assist tuning,
- mobile touch-assistance tuning,
- input-weighting rules inside ranked matchmaking.

---

# 34. Design Guardrails

Any future feature should be tested against these questions:

1. Does it make the **two humans controlling one body** idea stronger?
2. Does it give P1 or P2 meaningful skill without making the other role irrelevant?
3. Does it preserve physical logic between body/legs and arms/hands?
4. Does it create coordination rather than arbitrary dependency?
5. Is it readable in a competitive match?
6. Does it preserve cosmetic-only monetization?
7. Does it avoid making one role obviously less fun?
8. Does it work with server-authoritative multiplayer?
9. Does it avoid unnecessarily blocking future mobile/console support?
10. If it affects competitive fairness, does it still work with input-aware cross-platform matchmaking?
11. Is this a product decision, or should it simply be tested and tuned?

If a feature does not reinforce the shared-body experience, it should need a strong reason to exist.

---

# 35. One-Sentence Pitch

> **A competitive multiplayer shooter where two human players physically combine into one fighter — one controls the body and movement, the other controls the arms, weapons and aim — forcing them to master coordination as if they were a single person.**

---

# 36. Short Store/Presentation Pitch — Draft

Two players. One body.

Your partner controls the arms. You control everything else.

Dodge bullets, kick enemies across the arena, line up firing angles, throw utility, and somehow agree on where you're going before the other team does.

At first, controlling one fighter together is chaos. At high level, the best duos move and shoot like one mind.

---

# 37. Final Product Principle

The novelty gets people to try the game.

The goal is for **movement, aim, coordination and competitive mastery** to make them stay.
