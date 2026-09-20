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
