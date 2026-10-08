# Be My Arms — Architecture

**Implemented architecture · Updated 2026-10-09**

This describes the current project, not an obsolete prototype plan. Design intent lives in
[GAME_CONCEPT.md](GAME_CONCEPT.md); sequencing in [ROADMAP.md](ROADMAP.md); operations in
[docs/DEV_ENVIRONMENT.md](docs/DEV_ENVIRONMENT.md).

## 1. Stack and boundaries

- Unity **6000.4.3f1**, URP **17.4.0**, GameObject/MonoBehaviour presentation.
- **Netcode for GameObjects 2.13.2** over embedded **Unity Transport 2.7.2**.
- Custom numeric body simulation, prediction/reconciliation, input history and lag compensation.
  This is snapshot-based client/server, **not deterministic multiplayer lockstep**.
- Input System, Cinemachine and presentation IK; Blender sources outside `Assets`.
- Unity Pipeline is development tooling. Its runtime command endpoint belongs only in development
  builds, never public shipping builds.

Runtime assemblies are now named by domain. Their existing dependency edges were retained:

| Module | Responsibility and principal dependencies |
|---|---|
| `BeMyArms.Core` | Aim-sector math, role input contracts, shared health/region primitives and local-control sample support. |
| `BeMyArms.Gameplay` | Local movement/look/weapon helpers and movement-accuracy model; depends on Core. Local combat harness is not the shipping match controller. |
| `BeMyArms.Networking` | `BodySim`, input/state DTOs, collision, reconciliation, lag-comp/ownership primitives and prediction harness; depends on Core/NGO. Numeric sim methods are engine-independent; the assembly includes network serialization and a sample harness. |
| `BeMyArms.Match` | NGO match/body integration, authority, input streams, combat rules, economy, rounds, bots and role roster; depends on Core, Gameplay, Networking. |
| `BeMyArms.Matchmaking` | Role/team pairing, rating, allocator seam and match integration; depends on Match and shared lower layers. |
| `BeMyArms.Product` | Accounts, cosmetics, ranked/social/moderation interfaces and rig contract/mounting; local in-memory providers, depends on Core/Matchmaking. |
| `BeMyArms.Content` | Asset descriptors/conventions and rig layout; depends on Product/Core. |
| `BeMyArms.Client` | App/session entry, cameras/viewmodel/animation, UI, map records, audio/VFX; composes Content, Product, Match, Networking, Core. |
| Diagnostics / QA | Isolated comparison/transport probes and development runtime commands. Not an alternative gameplay authority. |

Editor and test assemblies follow the same domain names. This normalization intentionally
does not redesign the assembly graph or replace mature components with a new framework.

## 2. Process entry and sessions

Normal scenes: `MainMenu`, `DuelArena`, `TwoVsTwoArena`. `AppBootstrap` is the entry point.

- Menu/private flow creates a `MatchRequest` with mode, body/role, bot difficulty and required humans.
- `PrivateMatch` manages fresh session state, join versus owned-server flow and return to menu.
- `IMatchServerAllocator` / `LocalProcessAllocator` launch **a separate headless process of the
  same player build**. A fresh local port per owned match avoids reconnecting to stale sessions.
- `ServerWatchdog` watches the owner's PID; normal leave shuts down the allocated server.
- `MatchBootstrap` configures NGO and Transport, connection approval, direct slots/matchmaker,
  reconnect and scene's director prefab. NGO scene management is disabled; each process loads
  the configured arena explicitly.

This is a local/private development deployment, not a production hosting/backend integration.
The allocator seams allow that integration later without making the client's machine the
authoritative listen host for ranked games.

## 3. One body, two authorized input domains

`NetworkBody` is the authoritative shared entity; `MatchDirector` owns match/round state.
Each connection maps to a server-assigned team/body/role slot. A connection cannot declare
a new role by changing an input payload. Input authorization checks slot ownership and
control epoch before consuming a stream.

- P1 sends movement, look, stance/action edges and alignment input.
- P2 sends world-absolute aim plus fire/reload/weapon/utility requests.
- Frame-level edges latch until a simulation tick consumes them. Movement advances once per
  server tick, not once per received packet. Input sequencing/queue bounds are explicit.
- State, cooldowns, ammo, economy, damage, elimination and match results are server-owned.
- Control epochs distinguish resets, role changes and stale packets from the current owner.

The body simulation step used by the current match is **60 Hz**. `BodyState` holds mutable
motion/action/orientation state for replay; `MovementCollision` implements bounded arena
collision, stance clearance, steps and ramps. It is not a server Mecanim/PhysX ragdoll simulation.

## 4. Prediction, aim and camera separation

`NetworkBodyClient` predicts P1 command ticks, records inputs/states and reconciles against
acknowledged server state. Pending inputs replay through the same `BodySim`. Large discontinuities
and epochs reset history; small corrections are presented through smoothing rather than changing
logical aim. P2 inherits interpolated body motion while its own aim remains local and immediate.

- P1 look is independent within the neck bound. Follow/alignment updates `BodyYaw`.
- Current locomotion axes use `LookYaw`; the P2 sector still uses `BodyYaw` only.
- P2 stores legal world yaw/pitch; no body chase, deferred mouse overflow or authoritative
  viewmodel-based aiming. Elastic Soft supports bounded overtravel and return.
- `LocalPlayer` owns the role-specific camera and input/cursor/UI state. P1 uses Cinemachine
  third person. P2 hides its local world body and renders a separate camera-local arms/rifle
  prefab on the viewmodel layer, with positional camera smoothing independent from aim.
- The viewmodel's composition, shot kick and IK never determine server hit rays or hitboxes.

Do not replace this with a generic NetworkTransform-only shooter or reintroduce single-client
control of both roles. These are accepted architecture foundations.

## 5. Current combat truth and limitations

Authoritative fire validates ownership/epoch, cadence, ammo/weapon/live state and historical
aim-sector legality. Historical body orientation matters because P1 owns the sector. Rewind
is bounded. `LagCompensation` now retains the target's complete `BodyState`, life flag and
control epoch together; rifle resolution cannot combine historical X/Z with live Y/stance or
hit a pre-reset life. The old X/Z API remains for the diagnostic prediction sample.

`RifleHandling` advances burst state only on accepted rifle rounds; a recovered first round
has zero burst spread. Server fire adds P1-motion spread from the current authoritative,
collision-resolved `BodyState.PlanarSpeed`, grounding, stance and action (not held input or a
client-reported accuracy value). Both humans and bots use the same resulting hit/obstruction ray.
Target rewind and historical aim-sector validation are unchanged; the motion penalty is not
rewound from P2's displayed body tick. Hit evaluation and impact presentation share the spread ray.
Local rifle recoil updates controllable P2 aim, separately from cosmetic camera/viewmodel kick.
`WeaponState`, `Loadouts`, buy and utility rules remain authoritative.

Provisional additive movement spread radii: full crouch-walk **0.35°**, walk **0.75°**, sprint
**2.5°**, airborne/jump/fall/dodge **3.5°**, slide **0.75°–3°** (linear actual speed 0–9 m/s),
light/heavy kick **1.5°/4°**. Grounded
speed ≤0.10 m/s is treated as stationary; translation penalty scales with actual speed, while
actions retain their disturbance even at zero translation. The existing movement sim stops
without inertia, so movement accuracy returns on the stopped tick; no new CS-style acceleration
or counter-strafe system was introduced. Burst bloom resets only after a >0.30 s shot pause.
The BODY HUD text remains movement-only. P2's `DynamicCrosshair` additionally projects the next
round's total movement + burst radius: `tan(spread) * pixelHeight / (2 * tan(verticalFov/2))`,
converted to canvas units using the actual world camera (not viewmodel FOV). A thin ring bounds
the angular spread disk; four high-contrast ticks retain a small minimum gap for readability,
and the center dot retains aim. `TargetSpreadDegrees` is the current next-round estimate;
`SpreadDegrees` follows it with frame-independent exponential smoothing (0.04 s time constant,
about 95% response in 0.12 s), then projects it. Opening and closing are both smoothed, without
overshoot; the ring is therefore a responsive visual estimate, not an instantaneous exact boundary.
At zero spread only the dot/readability ticks remain. Pause/re-enable and control-epoch changes
initialize from the new target rather than carrying a stale spray animation. No recoil multiplier,
target lock, bullet obstruction or hit confirmation is inferred. `DynamicSpread=false` renders only
the fixed center dot for a future settings selector; no settings-menu/persistence work was added.

`RifleAccuracyState` is a new server-written 16-byte accepted-shot snapshot (control epoch, shot
tick, shot count and burst index), replicated only when changed/reset. It does not alter existing
input/body-state payloads. `RifleSpreadPreview` combines that history with immediate local shot
predictions, consumes confirmations without double-counting, recovers by shot age and expires
unconfirmed predictions after a firing pause. Client `NextRifleSpreadDegrees` adds observed body
motion using `RifleHandling`; no client accuracy value feeds into server firing. As with observed
body snapshots, latency and rejected/queued shots can temporarily correct the displayed estimate.
The HUD updates after this frame's aim/camera and hides outside alive P2 gameplay. No camera
motion, damage value, pistol or knife handling changed. Near-cover camera/shot-origin offsets
remain a separate limitation, so the ring is an angular error preview, not a guaranteed impact area.

`CombatHitGeometry` provides exact ray/surface intersection for the standard competitive
profile: a **0.35 m radius** torso/legs capsule ending below a **0.18 m radius** exposed P1-head
sphere. It follows authoritative feet/height/facing; the crouched head has a 0.20 m forward
offset informed by the existing shared rig. It never reads client bones or cosmetic meshes.
Nearest surface entry—not center distance—orders targets against solid cover. An accepted
damage event carries the actual historical hit point, region, applied HP loss, source kind,
kill flag and attacker/victim epochs. Spawn protection produces no damage confirmation.

Rifle damage is **18 body / 45 head (2.5×)** as provisional playtest tuning; other weapon stats
are unchanged. `CombatFeedback` consumes only server events and rejects stale local epochs.
Both roles receive shared confirmation, with own/partner weapon-versus-kick labels and a
distinct head-hit cue using the existing headshot audio. There is no client-guessed hitmarker.

**Not implemented as final gunplay:** per-limb/animated skeletal hitboxes, final profile
readability during action poses, complete camera-to-shot-origin agreement near cover, full
production ballistics/feedback and final weapon/utility balance. Target volumes remain an
approximation of anatomy, and rewind still uses the existing bounded fixed-time sampling,
not per-client latency estimation. Utility/smoke retain their existing simplified rules.
These are ongoing combat work, not reasons to rewrite prediction or the accepted cameras.

Kicks capture look direction in action state. Server hit selection and `CharacterAnimator`
use that captured direction; the cosmetic leg extension is not an authoritative bone collider.
P2 can fire during P1 actions; movement accuracy penalties remain separate from weapon legality.

## 6. Roster, bots, reconnect and match loop

- `MatchRoster` / `MatchRoleService` govern direct role assignment, bot ownership, disconnect
  reservation, reconnect and intentional replacement. Role exchange/reset increments epochs.
- Disconnect temporarily substitutes only the missing role. Token reconnect atomically removes
  its substitute; intentional leave releases the reservation for a new human. The other human
  never gains both roles.
- Bot fill and temporary takeover share normal P1/P2 authority paths. `BotSteering` retains
  its 90°/s P1 turn bound. `BotPositioning` replaces unconditional advancing/random strafing
  with stable, reachable firing/cover positions and threat-driven repositioning.
- `BotNavigation` runs a bounded .75m-cell reachability search (15m local half-width, weighted
  route budget 22). Whole-body corridor tests use `MovementCollision` boxes, bounds, step/drop
  limits and surface heights; low headroom can require crouch. Route smoothing cannot cut
  blocked corners. Tactical planning is normally 1 Hz; repeated damage does not replan per tick.
- Position scoring distinguishes standing/crouched head/torso exposure and firing visibility,
  comfortable range, useful duck/peek cover, recent damage, reload and zone safety. Conditional
  hide/peek windows avoid permanent crouch with no firing opportunity. A slide checks its full
  ~6.3m stopping corridor and reduced exposure; bots add no invulnerability or physical shortcuts.
- Observation is range/LOS limited, blocked by solids/smoke/blindness. A short four-second
  memory stores only the last observed target pose. Hysteresis discourages target/route churn;
  without a known target the bot explores locally, remembering recent arrivals.
- `BotAimMotion` limits actual yaw/pitch motion, not merely cosmetic interpolation: Easy/Hard
  have 100/160°/s angular speed bounds and 420/650°/s² acceleration limits. P1 moving a sector
  wall may still physically push aim as it does for humans. Easy offset destinations are chosen
  in burst pauses, not after each shot. Fire requires clear sight, legal sector and settled aim;
  accepted bullets still go through ordinary weapon cadence, spread and hit resolution. Easy's
  accurate-destination probability is tuning, not a guaranteed per-shot observed hit percentage.
- Bot motion/perception/route state clears on resets and control epochs. The human P2 immediate
  aim path, accepted cameras/viewmodel, numeric movement sim and networking payloads are unchanged.
- `MatchState` drives buy/live/round-end/match-end, elimination and first-to-three scoring.
  `ClosingZone`, `BuyPhase`, `UtilitySystem`, `Telemetry` and `Loadouts` support that loop.
- Duel and 2v2 reuse team/body/role encoding with one or two bodies per team. Local matchmaking
  and role-specific ratings exist; production persistence and ranked trust are not complete.

Navigation currently stores one reachable floor per XZ cell and explores locally, not through
a final global/layered graph. Complex stacked floors, long maze routing, coordinated multi-threat
team tactics and richer cover reservations remain open work. Bots consume valid `MovementCollision`
data from the map provider; arbitrary new art is not automatically navigable. The current provider's
kit classification is unchanged. No arena-specific coordinates appear in the bot code.

Transport is deliberately embedded. Its UDP receive-buffer ownership patch prevents failed
receives from exhausting the pool after peer loss. See
[Packages/com.unity.transport/BMA_PATCH.md](Packages/com.unity.transport/BMA_PATCH.md).
Do not silently replace it with the registry version during repository cleanup.

## 7. Presentation, content and rig contract

- `RigContract`, `RigValidator` and mount helpers enforce standardized sockets, role-compatible
  skins and separation of cosmetic meshes from stats/hitboxes.
- Current world character uses the **SharedRig** assets: one skeleton/animation authority,
  separate uniform role surfaces, dedicated rifle grip/IK and procedural action posing.
- `RiflePose` keeps world-grip authoring separate from first-person authoring. The currently
  accepted rifle POV is serialized in `SharedRig/Resources/P2ArmsViewmodel.prefab`; idle
  breathing is disabled and the firing-side renderer is reload-only.
- Map records and scene links supply spawns, geometry/collision and family metadata.
- Audio/VFX libraries and services are presentation. Generated clips/particles are production
  test content, not subjective production-quality acceptance.
- Blender authoring lives in `art/blender` and `tools/pipeline`; exported models/materials/prefabs
  live in `Assets/Art`. Asset licensing/provenance is preserved.

Keep Samples and historical character studies distinct from active assets. Running every old
generator is not a safe way to rebuild the accepted shared-rig checkpoint.

## 8. Refactor and validation rules

- Move Unity assets with AssetDatabase, carrying existing `.meta` files/GUIDs; preserve serialized
  field names, network payload layouts, prefab identities and tuning.
- Renamed component/asset types carry `MovedFrom` metadata. Current code and reflection strings
  use domain-qualified types; migration strings may retain old names intentionally.
- Update scene-name fields, Resources keys, path constants, asmdef references and build settings
  together. Namespaced runtime types/RPC identities changed: use matching client/server binaries,
  not an old player with a normalized server.
- Compile and check imports/references, run relevant existing tests, build once, smoke startup and
  core P1/P2 Duel. Do not equate automated images or numerical wrist metrics with human acceptance.
- Never regenerate art/animations, change gameplay values or add Phase 3 features just to make
  a structural refactor appear complete.

Future integration risks include production credentials/reconnect trust, hosting/observability,
hit-region accuracy, cross-platform input fairness and backend entitlement security. They are
real open work—not evidence that the existing netcode stack or free-look model is undecided.
