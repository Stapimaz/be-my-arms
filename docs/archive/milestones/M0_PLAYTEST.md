# M0 Playtest Guide — Local Shared-Body Mechanic Spike

**Scope:** M0 only. This is a throwaway spike, not production code.
**Scene:** `Assets/Scenes/M0SharedBody.unity` (built by `Be My Arms > M0 > Build M0 Scene`)
**Goal:** decide whether the P1/P2 control relationship feels good, and whether Aim Model C
(world-stabilized P2 aim inside a P1-owned firing sector) is the right direction.

**Status (2026-09-19):** implementation and automated verification are complete — 12/12 EditMode
and 2/2 PlayMode tests pass. A single tester manually inspected the prototype and confirmed that
the Model C coupling behaves as intended. Extensive two-human feel/playtesting was
**intentionally deferred**, so the feel criteria here are not all verified; the definitive
aim-model and game-feel validation is the **M1 playtest gate**.

---

## 1. Setup

1. Open the project in Unity `6000.4.3f1`.
2. Open `Assets/Scenes/M0SharedBody.unity`.
3. Connect a gamepad (P2). Mouse + keyboard drives P1.
4. Press Play. The Game view is split: **left = P1 third person, right = P2 first person**.

If no gamepad is available, select the `SharedBody` object and set `M0BodyRoot > P2 Input Mode`
to `Scripted Bot`. The bot aims at `Dummy_NeedsRotation` and fires when aligned, which lets a
single tester drive P1 and observe the coupling.

---

## 2. Controls

| Role | Input | Action |
|---|---|---|
| P1 | `W A S D` | Move (relative to BodyYaw) |
| P1 | Mouse X | Rotate `BodyYaw` (M0 temporary: camera follows yaw, no free-look) |
| P1 | Mouse Y | Camera pitch only |
| P1 | `Shift` | Sprint |
| P1 | `B` | Apply 10 test damage to the shared HP pool |
| P2 | Gamepad right stick | Aim (yaw inside sector, free pitch) |
| P2 | Gamepad RT / RB | Fire hitscan rifle |

---

## 3. Acceptance checklist

Items 2–5 are covered by automated tests, item 1 was spot-checked, and item 6 is deferred.
Criterion numbers match `ROADMAP.md` M0.

- [x] **1. Two roles playable simultaneously on one machine.**
      *(Single-tester spot-check. Full two-human pass deferred.)*

- [x] **2. A target outside the sector forces P1 rotation.**
      `Dummy_NeedsRotation` sits 110 degrees from the start facing (40 degrees beyond the
      +/-70 sector). P2 cannot bring the crosshair onto it until P1 rotates the body.
      *(Automated: scene-layout test.)*

- [x] **3. P1 rotation does not drag P2's crosshair (inside the sector).**
      *(Automated: `AimSectorTests.BodyRotation_DoesNotMoveAimInsideSector`.)*

- [x] **4. Boundary push with no phantom offset.**
      *(Automated: `...NoPhantomOffset_WhenPushingOutwardThenInward` and
      `...BoundaryPush_RestabilizesInWorldSpace`.)*

- [x] **5. The body never rotates on its own toward P2's aim.**
      *(Automated: `P2Input_CannotMoveTheBody`.)*

- [ ] **6. Feel judgment — DEFERRED to the M1 playtest gate. Not passed.**
      - Is it fun and readable within the first minute?
      - Does P2 feel *gated by P1* rather than like a passenger?
      - Does P1 feel meaningfully responsible for P2's damage?
      - Does the boundary clamp read clearly (see the HUD `[PINNED]` flag and sector bar)?
      - Any discomfort or nausea, especially while P1 turns?

Optional A/B: set `M0BodyRoot > P2 Input Mode = Scripted Bot` and compare holding a target
against a manual gamepad pass.

---

## 4. Secondary checks (context, not acceptance)

- Shoot `Dummy_Inside` body vs head: the red head takes `damage * headshotMultiplier`
  (HUD shows only the shared body HP, so read damage from the console/`Damageable`).
- Press `B`: the single shared HP pool decreases for the whole body (both roles share it).
- Dummies respawn ~2 s after death.

---

## 5. Known M0 limitations (expected, not bugs)

- No free-look for P1: look input drives `BodyYaw` directly. Resolving this is an M1 task.
- No dodge, slide, jump, vault, kicks, reload, weapon swap, utility, or spread/recoil.
- No real rig, IK, animation, or skins: the body is a capsule plus an arm proxy.
- The P2 camera culls the `PlayerBody` layer, so P2 sees no own-body/arms. This is an M0
  shortcut and would not work once enemy players share the layer.
- Dummies never shoot, so the shared HP pool is only exercised by the `B` debug key.
- No networking, no economy, no rounds, no matchmaking.

---

## 6. Result record

**Status:** the two-human feel pass was intentionally deferred to the M1 playtest gate. Do not
record it as passed here until it has actually been run.

Playtest date:
Testers:
`BodyYaw`/aim behavior notes:
Feel verdict (Model C keep / change / test Model A):
Open problems:
