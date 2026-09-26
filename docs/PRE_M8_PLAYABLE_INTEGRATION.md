# Pre-M8 — Playable Private-Match Integration (checkpoint)

**Status:** **Human-playability FAILED — recovery pass complete, awaiting human acceptance.** The
first real human playtest showed the private-match flow was not yet controllable: P1 camera/look,
P2 first-person presentation, vertical aim, traversal and the in-match menu were all wrong or
missing. A recovery pass addressed those on the real networked product (see §11). Automated tests
and screenshots are green, but the checkpoint is **not accepted** until a human plays the rebuilt
player and confirms the feel.
**Build:** `Builds/M7/BeMyArms.exe` (development Windows player).

---

## 1. Client flow

`Main Menu → Play → Private Lobby → choose Duel or 2v2 → choose P1/P2 role → empty slots filled
with bots → Start Match → full server-authoritative match on the production arena → post-match →
Return to Lobby / Quit`.

- No terminals or arguments are needed by the player. The lobby's Start Match button brings up the
  game transparently (see §2).
- Settings (mouse sensitivity, master volume) and Quit are part of the same flow.

## 2. Dedicated-server architecture, local and replaceable

The private match keeps the dedicated-server model: the client launches a **separate dedicated
server process of the same build** rather than hosting in-process, then connects as a client.

- `IM7MatchServerAllocator` is the provider-neutral seam (`M7PrivateMatch.Allocator`).
- `M7LocalProcessAllocator` is the local implementation: it starts the current build with
  `-m3-role server -m7-arena <scene> -m4-mode <duel|2v2> ...`, waits for the arena bootstrap to
  bring the server up, and the client connects.
- The production allocator (backend/hosting) replaces `Allocator` without touching the lobby, HUD or
  match code.
- The server process and client are the same executable; scene 0 (`M7MainMenu`) branches to the
  requested arena when launched with server args, otherwise shows the menu.

## 3. Slot model (same as future online lobbies)

A private lobby uses the **same conceptual slot model** as future online/custom lobbies:

- every role slot (team × body × P1/P2) is independently owned by a **human** or a **bot**;
- one connection owns exactly one slot, so a human **never** controls both P1 and P2;
- **“fill empty slots with bots”** creates genuine bot **role ownership** (`M3DuelRoster.SetBot` at
  match start), distinct from the disconnect bot-takeover/reconnect mechanism, which is unchanged.

## 4. Gameplay bots (normal authority)

Bots are real role owners driven through the normal input paths, not a separate simulation:

- **P1 bots** steer the body via P1 authority: turn/align toward the nearest enemy, advance to
  fighting distance, strafe, and use simple stuck/obstacle avoidance.
- **P2 bots** use P2 authority: acquire the nearest live enemy, respect the `BodyYaw` sector (the
  server clamps the same way it does for humans), fire/reload, and throw grenades through the normal
  utility path.
- Bot-owned P2 roles are given a legal loadout through the normal buy economy at round start.

They are scoped for private/practice competence, not a large AI project.

## 5. Arena collision and boundaries

The authoritative simulation now applies deterministic arena collision: map bounds plus XZ
rectangles for cover/walls, resolved after each P1 step and replayed identically in client
prediction (`M2BodySim.MovementConstraint` + `M3MovementCollision`). This keeps bodies inside the
arena and out of geometry on both server and client.

## 6. Animation / skinning / IK layer (presentation only)

The segment-rig characters gained a **joint hierarchy** (Blender empties as pivots) and a runtime
procedural layer (`M7CharacterAnimator`):

- readable P1 locomotion (leg swing, hip bob) and decoupled head look;
- P2 shoulder layer and weapon follow the authoritative aim, preserving the two-hand rest grip;
- recoil kick and a muzzle-flash VFX on fire.

It reads simulation state only. It never writes simulation, aim, hitboxes or the contract anchors;
the M5 rig contract, camera anchors and authoritative hitboxes are unchanged. A skinned-mesh pass can
replace it later behind the same contract.

## 7. Player-facing UI (foundation for M8)

- Main menu, private lobby (mode/role selection, slot list with human/bot labels), settings and quit.
- Match HUD: phase/round/score/timer/zone, P1 health/kills, P2 weapon/ammo, crosshair.
- Buy panel (P2) wired to the real server buy RPCs; round/match states; post-match overlay with the
  return-to-lobby path.

This is real player-facing UI, not debug UI; it is deliberately not the full final M8 polish.

## 8. Verification (real build)

| Case | Result |
|---|---|
| Human P1 + bot P2 (Duel) | client launched the local dedicated server, connected to `A0P1`; `A0P1=human A0P2=bot B0P1=bot B0P2=bot`; buy → live → elimination → rounds on `M7DuelArena` |
| Human P2 + bot P1 (Duel) | `A0P1=bot A0P2=human B0P1=bot B0P2=bot`; rounds resolved on the arena |
| Bot-filled 2v2 | `A0P1=human` + 7 bots across 4 bodies; `using map spawns (8)`; rounds on `M7TwoVsTwoArena` |
| Normal launch | `BeMyArms.exe` (no args) boots the menu with no exceptions |
| Automated | EditMode 102/102, PlayMode 6/6 (menu builds; M6/M7 validators still pass) |

A QA/automation entry (`-m7-qa-private <duel|2v2> -m7-qa-role <p1|p2>`) drives the **same**
private-match flow headlessly for CI; it is not a separate game mode.

## 9. Remaining limitations (deferred)

- **Audio/music and VFX** remain the M7 production-test placeholders; production audio/VFX are still
  deferred and the M7 “production quality” bar is not fully closed.
- **Animation is procedural segment-rig**, not a skinned mesh/Mecanim pipeline; it is readable but
  not final presentation.
- **Human input** uses the current networked P1/P2 action set (move, look, align; aim, fire, reload,
  utility, buy). Jump/dodge/slide/kick are not yet in the networked input domain.
- **No online matchmaking/backend/hosting**: the flow is local, behind the replaceable allocator.
- Post-match rating presentation, richer settings and accessibility are M8 scope.

## 10. M8 handoff

M8 should build its UI/UX and optimization pass on this flow and screens, keep the allocator seam
for production hosting, and replace the placeholder audio/VFX and procedural animation with
production content behind the unchanged rig contract.

## 11. Recovery pass (2026-09-20) — awaiting human acceptance

The first human playtest failed the checkpoint. The recovery kept the server-authoritative /
predicted architecture and extended it; it did **not** add a second gameplay stack.

**Fixed systems**

- **P1 look/camera.** Mouse now drives `LookYaw` + a vertical `LookPitch` in the shared sim; a real
  third-person camera orbits the **look** yaw (not BodyYaw) with pitch and a deterministic spring
  arm that keeps it inside the arena. Cursor is captured during live play and released for UI.
- **Movement set restored into the networked sim.** `M2BodySim` gained walk, unlimited sprint,
  jump, directional dodge, slide, vault and light/heavy kicks, with the action state carried in the
  replicated state so prediction/reconciliation replay exactly. P1 input carries the full set and
  the server resolves kicks authoritatively.
- **Vertical traversal.** The flat XZ model was replaced by a deterministic 3D model
  (`M2MovementCollision`): floors, gravity/grounding, steps, walkable ramps/slopes, walls, low
  cover and catwalks, built identically on server and client from the arena hierarchy. The central
  ramp is passable.
- **P2 first person.** A local, presentation-only viewmodel (weapon + hands) rides the
  `P2CameraAnchor`; the own combined body is layer-excluded from the FP camera so it no longer
  blocks the view. Crosshair readability improved; recoil + muzzle flash on fire.
- **Vertical aim agrees with shooting.** P2's authoritative hit ray is built from yaw **and** pitch
  and tested against the enemy's vertical extent, with wall/solid blocking.
- **In-match ESC menu.** `M7PauseMenu` (Resume / Settings / Leave Match / Quit) frees the cursor and
  stops gameplay input; it does not pause the dedicated-server match.
- **Weapon visual.** The in-house box/cylinder rifle was replaced at the mount/viewmodel seams by a
  **CC0 Kenney Blaster Kit** blaster (`Assets/ThirdParty/KenneyBlasterKit`, license + README kept in
  the repo). The old placeholder rifle remains as a fallback.
- **Bot discipline.** Bots now fire in bursts with distance-scaled aim error and a short post-spawn
  damage grace exists, so a body is not deleted within a second of the live phase starting.

**Visual QA evidence** (captured through the runtime QA loop, `Builds/M7/QA2/`):
`p1_live.png`, `p2_neutral.png`, `p2_firing.png`, `p2_pause_menu.png`.

**Known remaining limitations (not addressed here)**

- Character art is still the blocky segment rig; the weapon is a placeholder. Readability is the
  bar for this pass, not final art.
- Bot combat is now survivable but still tuned permissively; it is not a balance pass.
- Buy/utility for a human P2 is via the buy panel and G/T/Y; there is no in-world affordance yet.
- Duel/2v2 are the only modes; no online backend.
- A re-entered lobby after returning to the menu was seen to leave stale duplicated bodies in one
  scripted QA session; a fresh launch is clean and a normal single match is unaffected.

## 12. Foundation recovery pass 2 (2026-09-20) � awaiting human acceptance

The second human playtest still failed at the local input/camera/session foundation. This pass was
deliberately narrow (no buy/loadout, weapon switching, bot difficulty or art work).

- **Input sampling decoupled from the network rate.** The local player samples the mouse and
  updates look/aim every rendered frame and predicts the body every frame; the RPC stream runs at
  `SendRateHz` and consumes the input accumulated since the last tick. No camera smoothing is used
  to hide a low sample rate. P1 look/pitch/prediction and P2 aim presentation both follow this.
- **Explicit gameplay/UI input mode** (`M3LocalInput`, written from match phase + focus + pause):
  cursor capture is now a consequence of gameplay state, not its source. Focus loss/regain is
  handled and the flag is cleared when the arena unloads.
- **Mouse sensitivity** (`M7Settings.MouseSensitivity`) is pushed into the gameplay path every
  frame.
- **P1 combined body fixed.** The P1 skin prefab was ~100x too small and laid along Z because
  `M6PrefabBuilders.AttachModel` clobbered the FBX root rotation/scale. The builder now preserves
  the imported root transform, the skins and player-body prefab were rebuilt, and the procedural
  animator rotates the head/hips about the character's real up/forward axes (correct for either
  skin orientation). `qa_player_state` now reports `p1=ok p2=ok weapon=ok bboxH=1,9 inView=True`.
- **Main-menu order** corrected to PLAY (top) / SETTINGS / QUIT (bottom).
- **Private-match lifecycle fixed.** The `NetworkManager` is `DontDestroyOnLoad`, so leaving and
  re-starting a match leaked a second manager and duplicated bodies/clients and could flip the
  requested role. `M7PrivateMatch.ResetSessionState` now resets the local-slot/director/roster
  statics and tears down stale managers/network objects before a new match. Verified:
  `Leave Match -> menu (0 bodies) -> new match (2 bodies, 1 local, no duplicates, correct role)`.
- **Structured runtime diagnostics** added: `qa_player_state` (slot/body/phase/input/cursor/camera/
  look/aim/per-part bounds+viewport/weapon/viewmodel/duplicates) and `qa_inject_look` (drive the
  real look path without a physical mouse). See `docs/DEV_ENVIRONMENT.md` section 7. Prefer these
  over routine screenshots.

The playable checkpoint remains **awaiting human acceptance**; M8 has not started.

## 13. Human game-feel vertical slice (2026-09-26) — awaiting human acceptance

A deliberately narrow presentation/feel pass on top of the unchanged authoritative/networked
architecture: one Duel private match, one P1, one P2, one rifle.

**Packages adopted**

- `com.unity.cinemachine` `3.1.7` — P1 third-person camera.
- `com.unity.animation.rigging` `1.4.1` — P2 two-bone arm IK.
- (pulled transitively: `com.unity.splines` `2.0.0`, `com.unity.burst`).

**Character presentation replaced (CC0)**

- The blocky segment rigs and `M7CharacterAnimator`'s sinusoidal locomotion are replaced by the
  **Quaternius Universal Animation Library** (CC0) humanoid rig + authored clips. Attribution and
  regeneration steps: `Assets/ThirdParty/QuaterniusUniversalAnimationLibrary/README.md`.
- `tools/pipeline/build-quaternius-bodies.py` derives three anatomy-locked skinned bodies from the
  library's single 53-bone rig by deleting the vertices weighted to hidden bones and capping the
  loops: P1 (head+torso+pelvis+legs, no arms), P2 (chest+shoulders+arms, no head/legs), and a
  P2 arms-only first-person set. The derived FBX are committed.
- `M7CharacterBodyBuilder` builds Generic Mecanim controllers (idle/walk/jog/sprint blend tree,
  jump/fall/land/death) and the skinned skin prefabs; `M7CharacterAnimator` drives them from the
  replicated/predicted state only (in-place, never root-motion authority).
- **P2 arm IK**: Animation Rigging `TwoBoneIKConstraint` (shoulder → elbow → hand) on the skin,
  targeting grip transforms on the body's aim pivot, so the arms aim with the authoritative aim
  instead of rotating the P2 root. The first-person view reuses those same real arms + rifle: the
  local P1 skin is hidden and the camera sits at the `P2CameraAnchor`.
- The M5/M6 contract (sockets, camera/weapon anchors, hitboxes, stats) is unchanged and still
  validates; the new skins mount through the existing cosmetic sockets.

**Camera and input feel**

- P1 uses a Cinemachine 3 rig (`CinemachineCamera` + `ThirdPersonFollow` + `RotationComposer` +
  built-in obstacle avoidance) around a dedicated look/pivot target placed from LookYaw/LookPitch;
  BodyYaw stays simulation-owned. A map-bounds clamp keeps the camera inside the arena at doorways.
  Avoidance colliders were added to the map prefabs on the Default layer (hitboxes moved to Ignore
  Raycast).
- Mouse sampling stays per-render-frame and predicted; network commands stay rate-limited.
- Cursor context: gameplay (buy or live, focused, no interactive UI) captures the mouse, including
  the P1 pre-round/buy window; ESC/pause releases it and Resume restores it. The P2 buy panel is
  removed for this slice, so the cursor stays captured through buy as well.

**One rifle**

- P2's playable loadout is a single rifle, auto-equipped at round start for both humans and bots;
  the SMG/shotgun/pistol selection UI is removed. One rifle per body; the crosshair shows for P2
  while live.

**Combat feedback (authoritative only)**

- `M3DuelBody` broadcasts server-confirmed damage/impact events; `M7CombatFeedback` turns them into a
  hitmarker + confirm sound (shooter), a red damage vignette + hurt sound (victim), an
  `ImpactFlesh`/`ImpactWorld` particle, and a distinct ELIMINATED overlay + sound on death.

**Forgiving bot baseline**

- Bots hold through a reaction window, use larger distance-scaled aim error, fire short bursts with
  pauses, and the post-spawn damage grace is 4s.

**Known limitations**

- Character art is a simple CC0 mannequin with two clay materials and shaded-smooth joints; the
  first-person arms are the third-person rig (a clean silhouette, not final FP art).
- The P1 torso and P2 chest overlap where the two halves combine, so the combined silhouette is a
  little bulky.
- Combat feedback uses the existing procedural audio/VFX placeholders.
- `qa_player_state` still reports `vm=0` (there is no separate viewmodel object any more).

**Targeted checks**

- EditMode compile clean; M7 content validation PASSED; map prefabs 15/15 have camera colliders.
- Runtime on the built player (`Builds/M7/BeMyArms.exe`), real private Duel match, both roles:
  `bboxH=1.94 p1=ok p2=ok weapon=ok inView=True`, no duplicate bodies, cursor locked during
  gameplay, rifle equipped, no runtime console errors.

## 14. Runtime/session correctness + animation + FPS feel (2026-09-26, second pass)

The first human playtest of §13 still failed on concrete runtime systems. Root causes and fixes:

**Private-match / process lifecycle**

- `ResetSessionState()` called `NetworkObject.Despawn(true)` on the **client**, which NGO rejects
  ("Only server can despawn objects"). A client must never server-despawn; the method now only shuts
  down listening managers and destroys leftover objects locally.
- Each match now uses a **fresh free UDP port** (`M7PrivateMatch.PickFreePort`), so an orphaned
  previous server can never make a new client connect to an old match or fail to bind.
- The locally launched dedicated server is owned by the client: the allocator passes
  `-m7-owner-pid`, `Application.quitting` kills it on normal exit/Alt+F4, and `M7ServerWatchdog`
  (armed in the server, probing the owner PID with `HasExited`) terminates it on a hard client kill.
  Verified: hard client kill → server exits; WM_CLOSE → both exit; relaunch → fresh menu and fresh
  match; return-to-lobby exercises the path with zero despawn/exception logs.

**Buy-phase P1**

- P1 look is accepted during Buy but movement/actions are stripped server-side (`LookOnly`) and in
  the client prediction, so the body stays frozen while look stays responsive. Verified with the new
  `qa_inject_input`: position unchanged through Buy, then moves once Live begins.

**Animation (root cause: the derived FBX had constant curves)**

- `tools/pipeline/build-quaternius-bodies.py` exported every take with the **rest pose** because no
  action was evaluated during export. It now stashes each action as an NLA strip and bakes the NLA
  strips, so the clips carry real keyed motion (`maxFCurveDelta` Idle 0.07 … Sprint 0.93).
- Import settings mark `*_Loop` clips as looping (previously every clip played once and froze), and
  the controller has a Death→Locomotion transition so a revived body cannot stay in the terminal
  Death state. Humanoid auto-mapping was tested and rejected (avatar `isHuman=false`, 0 bones
  mapped), so the Generic path-bound rig is kept deliberately.
- Verified at runtime: Animator `Speed` is driven by movement (0.39/1.60 for moving bots), clip time
  advances and loops, bones visibly change, and a mid-live capture shows the fighter mid-stride.

**P2 first-person view**

- Replaced the "camera on the animated P2 anchor" idea with the conventional shooter split: a stable
  logical eye at a fixed height over the shared body, camera rotation straight from local aim, the
  whole local world-body hidden, and a dedicated camera-local arms+rifle viewmodel.
- The viewmodel renders through a URP **Overlay** camera at a narrower FOV so it composites over the
  world (a plain second camera replaced the frame). Rifle sits on the right like a normal FPS.

**Immediate firing feel**

- `M3DuelClient` detects a locally valid rifle trigger pull every frame (cadence-limited, independent
  of replicated ammo) and `M7LocalPlayer` immediately plays the rifle sound, muzzle flash, viewmodel
  kick and a small recoverable camera recoil impulse. Hitmarkers/damage/kills remain authoritative.

**Bot de-synchronisation**

- Each body/round seeds its own reaction window, burst length, pause, firing phase and aim-error
  character (`InitializeBotProfile`), so practice bots no longer fire in lockstep.

**Targeted checks added**

- `qa_inject_input` (held move/fire through the real gated input path) and `LocalShots` in
  `qa_player_state`; the viewmodel overlay camera is excluded from the duplicate-camera check.

## 15. Locomotion signal + grip + rifle asset (2026-09-26, third pass)

Follow-up feel pass on the improved build.

- **Locomotion flicker root cause:** `M7CharacterAnimator` derived speed from frame-to-frame
  presentation position, which bursts on replicated/predicted steps and prediction corrections.
  `M2BodyState` now carries an authoritative `PlanarSpeed` (applied m/s) and `MoveForward`
  (body-local forward component) computed by the sim and replicated/predicted with the rest of the
  state. The animator reads those directly; the locomotion blend is stable (constant 4.5 m/s while
  holding W instead of oscillating). Blend thresholds are now in m/s (0/4.5/5.8/7.0) so walking and
  sprinting select the right clips, and backing up plays the cycle in reverse (the library ships no
  dedicated backward/strafe clips, so strafing still uses the forward cycle).
- **Hand grip root cause:** both the world-body and viewmodel hand targets were hand-authored
  offsets next to the weapon. They are now markers **on the weapon itself** (`Grip_R`, `Grip_L`,
  `Muzzle`), derived from the weapon's own bounds by `M7WeaponBuilder`, so both hands grip the rifle
  wherever it is aimed. Structured check `qa_grip_state` reports hand-tip→grip distance for the body
  and the viewmodel.
- **Rifle asset:** replaced the chunky Kenney blaster with the stylized CC0 Quaternius *Low Poly Guns
  Pack* assault rifle (`Assets/ThirdParty/QuaterniusLowPolyGunsPack`, licence + source in its
  README). `M7WeaponBuilder` normalizes the imported FBX (barrel aligned to +Z, muzzle forward,
  0.9 m length) and bakes the grip/muzzle markers, so the world and first-person presentations use
  the same clean weapon.

**Targeted checks**

- Running player, P2: `qa_grip_state` body L/R = 0.000 m, viewmodel L/R = 0.000 m; no exceptions.
- Running player, P1: animator `Speed` constant 4.5 while holding W (no flicker); bones advance and
  loop; fighters animate mid-stride in world and no bind-pose/death regressions.
- `M7PipelineCommands.Validate()` PASSED (weapons 3); compile clean.
