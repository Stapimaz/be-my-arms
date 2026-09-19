# Be My Arms — Technical Plan

**Status:** Approved working technical plan; network stack not yet selected
**Source of truth for game design:** `GAME_CONCEPT.md`
**Companion document:** `ROADMAP.md`
**Current production project:** `C:\Users\stapi\GameDev\be-my-arms`
**Engine:** Unity `6000.4.3f1`, URP
**Last updated:** 2026-09-19

> This document refines the technical direction in `GAME_CONCEPT.md` §23–§30. Where the two
> disagree on game design, the concept document wins. This document does not change any game
> design decision; it chooses and sequences implementation.

---

## 0. Project context

`be-my-arms` is a clean production project. Current gameplay is the disposable M0 shared-body
spike under `Assets/Scripts/M0`, with its tests under `Assets/Tests` and its generated scene
`Assets/Scenes/M0SharedBody.unity`. No networking package is installed; the netcode stack is
selected at M0.5.

Tooling versions, the Unity Pipeline setup and the OpenCode skill are environment concerns and
are documented once in `docs/DEV_ENVIRONMENT.md` — do not duplicate them here. Milestone status
is owned by `ROADMAP.md`.

---

## 1. Core simulation model

The combined body is a single authoritative combat entity with split input domains
**[LOCKED]**. P1 owns the body; P2 owns the arms and aim.

**P1 input stream [LOCKED]:** movement axes, sprint, jump, dodge, slide, body orientation,
kick commands, traversal actions.

**P2 input stream [LOCKED]:** view/aim, fire, reload, weapon selection, knife, utility,
hand-interaction commands.

**Architecture stance [PROPOSED]:** keep the code modular and data-driven, but do not
pre-build a deterministic simulation framework or a custom character controller. Use a
simple kinematic character controller for M0 and M0.5. Build custom movement only if the
chosen netcode stack proves it is required, and treat that cost as a known output of M0.5
and M2.

Suggested module boundaries (folders, not assemblies, until the netcode choice lands):

- **Input** — role-tagged, action-based, device-agnostic.
- **Body simulation** — movement, `BodyYaw`, movement state.
- **Aim** — sector clamping and aim resolution.
- **Combat** — weapons, hitscan, damage, health.
- **Presentation** — cameras, animation, IK, effects.
- **Data** — ScriptableObject tuning assets.

---

## 2. Aim coupling — prototyped direction

P1 fully owns `BodyYaw`. `SectorHalf` (draft ±70°) is **[TUNING]**.

### Model A — rigid mount

P2 aim equals `BodyYaw` plus a local offset. P1 rotation drags P2's crosshair off target.
Strongest "one body" feel; highest nausea and frustration risk; lag compensation must
reconstruct the firing direction from body yaw.

### Model B — world aim with body chase

P2 aims in world space and the body rotates to keep the aim legal. Reverses the dependency
and contradicts "P1 fully owns `BodyYaw`."

### Model C — world-stabilized aim inside a P1-owned sector **[PREFERRED TO PROTOTYPE]**

- P1 fully owns `BodyYaw`. The body **never** auto-chases P2's aim. This must be explicitly
  forbidden in code, not merely omitted.
- P2 controls a **desired world-space aim direction** (yaw and pitch).
- Each tick, the stored desired world yaw is clamped to
  `[BodyYaw - SectorHalf, BodyYaw + SectorHalf]`.
- While the aim is inside the sector, P1 rotating the body leaves it **world-stable** — the
  crosshair stays on target.
- If P1's rotation carries a sector boundary past the aim, the boundary **pushes** the aim
  with the body; it then re-stabilizes in world space.
- **Clamp the stored state and discard overflow.** Do not integrate mouse input into an
  unclamped accumulator and clamp only for display — that is precisely the phantom-offset
  bug. Pushing outward while pinned does nothing; moving back inward responds immediately
  with no wind-up.
- Add a small boundary softening/deadband so P1 micro-rotation does not jitter the clamp
  **[TUNING]**.

**Why preferred:** it preserves the core dependency (P1 gates what P2 can engage) while
removing the worst comfort problem, since P1 rotation and server reconciliation no longer
drag P2's crosshair. It also simplifies the highest-risk networking feature: P2's firing
direction is world-absolute, so lag compensation needs historical `BodyYaw` only to validate
sector legality, not to reconstruct the ray.

**Trade-off to watch [SPIKE]:** the dependency becomes largely one-directional. P1 gates P2,
but P2 does not move P1. Non-control feedback — a HUD indicator when P2's aim is pinned at
the boundary, and an audio/visual cue to P1 that the gun is at its limit — can restore the
mutual feel without adding a body-chase mechanic. This is design work to be validated at M1.

**Technical implications [SPIKE], not locked:**

- **Input encoding:** P2 sends desired world yaw/pitch (quantized) plus tick, not raw
  deltas. Absolute encoding makes Model C clean and removes accumulator drift. The server
  clamps and validates.
- **Anti-cheat:** the server validates sector legality **and** maximum angular velocity, so
  a client cannot snap aim anywhere inside the sector instantly.
- **Prediction/reconciliation:** P2's aim is local and immediate. Corrections occur only on
  genuine boundary contact and must read as the limit pushing the crosshair, not random
  snapping.
- **Lag compensation:** rewind targets to the fire tick, validate the sector against the
  historical `BodyYaw`, and raycast along the world aim direction.

Models A and B remain available behind a flag through M1 so the playtest can settle the
question with evidence. All three are **[SPIKE]**; Model C is not permanently locked.

---

## 3. Input ownership

- One connection equals one **role slot** (P1 or P2), assigned by the server or session,
  never self-declared mid-match **[PROPOSED]**.
- Two distinct action maps: the P1 map (move, body-look/`BodyYaw`, jump, dodge, slide, kick,
  traverse) and the P2 map (aim, fire, reload, weapon, knife, utility, interact) **[LOCKED]**.
- Each client sends a role-tagged input packet with tick and sequence. The server binds the
  packet to the correct input domain of the body and rejects a P1 connection writing P2
  fields, or vice versa.
- The local development harness must be able to emit **both** streams in one process
  (keyboard/mouse for one role, gamepad or second device or a scripted bot for the other),
  so M0 and M1 run without networking.
- The input layer is normalized to abstract actions, never raw mouse deltas, so touch and
  controller can map to the same two roles later **[DEFERRED]** actual mobile/console
  bindings.

---

## 4. Cameras

- **P1:** third-person camera. Spring arm with collision is required. Third-person camera
  peeking is a competitive exploit surface to plan for.
  - **Temporary M0 assumption:** P1 look input directly drives `BodyYaw` and the camera
    follows that yaw. No free-look in M0. Whether P1 retains decoupled free-look is
    **[SPIKE]** and is resolved by M1.
- **P2:** first-person camera at the standardized gameplay anchor (shoulder/upper chest),
  explicitly independent of cosmetic sensor placement **[LOCKED]**.
- Keep the three states the concept calls for in §23.3 separate: **authoritative body
  state**, **predicted input response**, and **local visual camera smoothing**. Presentation
  smoothing must never alter the authoritative aim ray or hitbox.
- **Nausea guardrail [SPIKE]:** if the camera is visually stabilized during jumps and kicks
  while the crosshair keeps true spread, players must not be misled about their accuracy.
- In multiplayer each client renders only its own camera. Split-screen or dual-view is a
  development-only harness, not a shipped feature.

---

## 5. Animation and IK

- **Placeholder stage (M0–M1):** a capsule body and a simple arm/chest proxy with a muzzle
  anchor. A functional two-layer rig — a P1 body layer plus a P2 arm/upper-body layer mounted
  at a standardized socket — is introduced when it is needed to validate aim/IK plumbing, using
  temporary assets until production art begins.
- P2's shoulder mount rotates within the sector, arms IK to the weapon grip, and the weapon
  anchor follows the hands. Additive recoil is animation-only; authoritative aim is numeric.
- Enforce a documented **rig contract**: gameplay skeleton, attachment socket(s) on P1's
  upper chest/clavicle, camera anchors, capsule hitboxes, weapon and utility anchors, and
  animation interfaces. All P1 skins must work with all P2 skins **[LOCKED]**. Draft the
  contract during M1 and enforce it before cosmetics scale **[high content risk]**.
- **Hitbox authority:** server-side hitboxes must derive from the same pose logic as the
  visual skeleton, or headshots will misregister. Keep a capsule rig driven by simulation
  state and consider kinematic bone math on the server **[SPIKE]**.
- **Silhouette constraint [LOCKED readability]:** P1's head is the only critical region and
  must remain visually exposed in the combined form; P2's upper body must never conceal it.

---

## 6. Combat

- Hitscan for launch gunplay, server-authoritative when networked (fire cadence, ammo,
  weapon state, aim-sector legality, damage, results) **[LOCKED]**.
- Movement-state to accuracy table with standing still most accurate and jump/slide/dodge
  and heavy kick penalized **[LOCKED direction]**.
- P2 keeps firing during all P1 movement and attacks; no hard weapon lockout **[LOCKED]**.
- P1 melee: light and heavy kicks with short recovery, no long hard-stun and no
  ragdoll-lockout **[LOCKED direction]**.
- Dodge has no invincibility frames and works by moving the hitbox out of the shot path
  **[LOCKED]**; this makes TTK tuning load-bearing.
- All values are **[TUNING]** and live in data assets.

---

## 7. Networking architecture requirements

- **Topology:** one dedicated headless server per match; four to eight human connections.
  Clients send inputs; the server sends snapshots. Client-server snapshot plus prediction,
  **not** deterministic lockstep, which avoids cross-platform float determinism entirely.
- **Tick:** 60 Hz draft **[TUNING]**. Decouple the gameplay tick from `Time.fixedDeltaTime`
  (the project is currently 50 Hz). The final rate must come from network testing.
- **Server authority domains [LOCKED principles]:** movement and collision, `BodyYaw`, aim
  sector legality, weapon state/cadence/ammo, utility, HP/damage/kills, loadout/economy,
  round and match state, results. Clients never declare final results.
- **Replication:** small entity count, so replicate body transform/velocity/state, P2 aim
  angles, HP, weapon and utility state, and round state. Keep it minimal and bounded.
- **P2's view of the body:** interpolate the authoritative body transform with a short
  buffer (two ticks). Do not re-simulate another platform's inputs, which would reintroduce
  cross-platform determinism. An optional input-relay prediction variant is a **[SPIKE]**
  only if interpolation feels laggy.
- **Transport:** Unity Transport as the core datagram transport, provider-neutral.
- **Services [PROPOSED]:** Unity Multiplayer Services SDK for authentication, sessions,
  lobby, matchmaking and QoS. Relay for prototypes, private sessions and listen-server
  tests only; it is not a dedicated server.
- **Hosting:** a provider-neutral adapter owns allocation, match metadata, credentials,
  region, shutdown and health reporting. Do not build on Unity Multiplay Game Server
  Hosting, which was deprecated in April 2026. Unity Matchmaker can allocate external
  providers through Cloud Code allocation modules.
- **Reconnect/disconnect:** a grace window plus "a disconnected role contributes no input."
  Do not hand full control to the remaining player **[LOCKED]**. The concrete mid-round
  policy is unresolved and must be decided before M3/M4.

---

## 8. Netcode for GameObjects versus Netcode for Entities

| Dimension | Netcode for GameObjects | Netcode for Entities |
|---|---|---|
| Fit with the current repo | High — GameObject/MonoBehaviour, URP, no DOTS | Low — needs Entities/DOTS and an authoring overhaul |
| Server authority | Manual | Native |
| Client prediction/reconciliation | None built-in; build history, replay, rollback and input buffer yourself | Built in (predicted ghosts, rollback, interpolation) |
| Lag compensation | None built-in; build hitbox history and rewind yourself | Framework plus physics/ghost history utilities to build on |
| Determinism for prediction | Must avoid PhysX; custom controller required anyway | Unity Physics is rollback-friendly; still needs a custom-style controller |
| Animation/IK/cosmetics | Native strength (Mecanim, skins, sockets) | Hard — no Mecanim server-side; custom pose and hitbox pipeline |
| Iteration speed on game feel | Fast | Slower, higher expertise barrier |
| Player scale needed | Tiny (4–8) — fits | More than needed on scale, but scale is not the deciding factor |
| Bandwidth/CPU at 60 Hz | Fine | Better, not decisive here |
| Real long-term risk | Shooter-grade prediction and lag comp become an in-house netcode product | DOTS migration plus animation/IK/hitbox maturity and team ramp |

**Assessment:** player count does not settle this — prediction and lag compensation do. NfE
provides the authoritative predicted-combat model the concept wants, but places the game's
differentiating strength (rich two-layer animation, IK and skin mounting) in the stack's
weakest area. NGO gives animation authoring for free but means building and maintaining a
competitive prediction and lag-comp layer, which must be budgeted explicitly.

**No stack is selected yet.** The M0.5 bake-off decides between the two and M2 confirms the
result; the documentation records no winner before then, and no permanent netcode-bound code is
written until the choice is made. The bake-off is judged on the criteria the M2 spike then
verifies: two role-tagged input domains on one entity, prediction quality for the P1-owned
portion, how much prediction and lag compensation the stack provides versus what must be
written, CPU and bandwidth, and the cost of the animation/IK and skin-mounting work. Note that
neither stack removes the need for a custom kinematic character controller; NfE would remove the
need to write snapshot and rollback plumbing.

---

## 9. Authority, prediction, reconciliation, lag compensation, dedicated servers

- **Authority [LOCKED]:** the final ranked game uses a server-authoritative client/server
  model. A player's machine is never the authority in ranked play.
- **Prediction:** P1 predicts locomotion and reconciles. P2's aim is fully local and 1:1,
  and weapon presentation (recoil, muzzle, tracer, audio) is local and immediate while
  results are authoritative. P2's view of body motion uses interpolation.
- **Reconciliation:** P1 keeps a state and input ring buffer and replays unacknowledged
  inputs after each authoritative snapshot. Define maximum correction thresholds and a hard
  clamp for teleport-scale corrections. Reconciliation must never cause violent P2 camera
  snapping **[LOCKED]**.
- **Lag compensation:** the server keeps roughly one second of history of hitbox poses,
  `BodyYaw` and P2 local aim. On a fire event it converts client tick to server time using
  the RTT estimate, rewinds targets, validates the sector against the historical `BodyYaw`,
  reconstructs P2's world aim direction, then raycasts. Maximum rewind is clamped to bound
  the abuse window. Because Model C's aim is world-absolute, the direction does not need
  body yaw to reconstruct — only validity does. If a different aim model is chosen at M1, this
  changes.
- **Dedicated server build [PROPOSED]:** headless, excluding unneeded render and audio
  assets; structured logs; match configuration at startup; health and readiness endpoints;
  clean shutdown after the match; reconnect grace windows; authoritative result upload;
  containerized from the beginning of production multiplayer work.
- **Anti-cheat layer one is architecture:** validate movement bounds, cooldowns, fire rate,
  ammo, inventory and utility counts, aim-sector legality, turn rate, damage and results.
  A client anti-cheat provider is **[DEFERRED]** and remains open.

---

## 10. Steam-first with future portability

- Steam (authentication, friends and invites, presence, achievements, DLC and entitlements)
  is an **outer platform layer** over a platform-neutral game account. Steam identity is not
  the account **[LOCKED direction]**. Game servers validate backend-issued session tokens,
  not Steam tickets directly.
- Do not depend on Steam Datagram Relay or Steam P2P for the authority model. UTP is the
  core transport; SDR is an optional routing layer later **[LOCKED]**.
- Keep the input and simulation layers free of mouse-absolute or PC-only assumptions so
  touch and controller can map onto the same two roles later. Actual mobile/console
  bindings are **[DEFERRED]**.
- URP already suits mobile portability. Keep gameplay free of render dependencies.
- Client-server snapshot architecture is inherently cross-platform friendly and requires no
  cross-platform determinism.
- Ranked matchmaking carries an **input profile per role** **[LOCKED direction]**; the exact
  weighting is **[DEFERRED]**.
- Per concept §22.5, do not weaken PC mechanics in advance for mobile; only keep the
  architecture portable **[LOCKED]**.

---

## 11. Top technical risks

1. **Aim coupling.** Model C reduces comfort and lag-comp risk but may create a
   one-directional dependency. Highest product risk; settled by playtest at M1.
2. **Netcode choice versus animation/IK/skin needs.** The two candidates trade
   competitive-networking capability against the animation, IK and skin-mounting work; the
   bake-off must quantify both rather than assume either is free.
3. **Overengineering.** The temptation to build a custom deterministic simulation or
   character controller before it is required. Guarded against explicitly.
4. **Authoritative versus visual hitbox alignment** across skins, poses and animation
   states.
5. **Two role-tagged input streams into one entity** — binding, races, spoofing, turn-rate
   abuse and reconnect edges.
6. **Lag compensation with a body-gated sector.** Reduced by Model C but still novel.
7. **Rig and skin standardization debt** if the contract is not enforced before content.
8. **Dedicated hosting vendor vacuum** after Multiplay's deprecation, mitigated by the
   provider-neutral adapter.
9. **Mid-round disconnect policy gap**, which conflicts with the locked no-separation and
   no-handover rules. Decide before M3/M4.
10. **Scope creep** — economy, cosmetics or 2v2 before M2 passes.

---

## 12. Open technical questions — do not finalize silently

- Netcode stack: NfE versus NGO, pending the M0.5 bake-off and M2 confirmation.
- Whether a custom character controller or deterministic simulation is actually required.
- Final server tick rate and lag-comp window.
- How P2's view of body motion is reproduced (interpolation versus input relay).
- Whether P1 retains a decoupled free-look camera.
- Server-side hitbox derivation method (capsule rig versus kinematic bone math).
- Dedicated-server hosting vendor and the exact backend persistence stack.
- Voice provider and anti-cheat provider.
- Cross-play pool rules, input-weighting, controller aim-assist and touch-assistance tuning.

---

## 13. Production art and content pipeline

M0, M0.5 and M1 use greybox and placeholder geometry only. Capsule bodies, arm proxies,
untextured materials and blockout arenas are development assets and are **not** the intended
final presentation; no agent should treat them as permanent art direction.

- A DCC/content pipeline is established when production art begins. The tool is **not chosen
  yet**; Blender is a viable candidate alongside other DCCs, and the choice can be made when
  that phase starts.
- Whatever pipeline is chosen must respect the standardized P1/P2 rig contract: gameplay
  skeleton, attachment socket(s) on P1's upper chest/clavicle, camera anchors, weapon and
  utility anchors, hitbox definitions, and animation interfaces — so that every P1 skin works
  with every P2 skin.
- The pipeline also defines import and scale conventions, naming, LODs and materials, and a
  way to test arbitrary P1/P2 skin combinations.
