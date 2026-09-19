# M0 Playtest Guide — Local Shared-Body Mechanic Spike

**Scope:** M0 only. This is a throwaway spike, not production code.
**Scene:** `Assets/Scenes/M0SharedBody.unity` (built by `Be My Arms > M0 > Build M0 Scene`)
**Goal:** decide whether the P1/P2 control relationship feels good, and whether Aim Model C
(world-stabilized P2 aim inside a P1-owned firing sector) is the right direction.

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

Mark each item. Criterion numbers match `ROADMAP.md` M0.

- [ ] **1. Two roles playable simultaneously on one machine.**
      P1 and P2 both act at the same time without one blocking the other.

- [ ] **2. A target outside the sector forces P1 rotation.**
      `Dummy_NeedsRotation` sits 110 degrees from the start facing (40 degrees beyond the
      +/-70 sector). P2 cannot bring the crosshair onto it until P1 rotates the body.

- [ ] **3. P1 rotation does not drag P2's crosshair (inside the sector).**
      Aim at `Dummy_Inside`, then hold P1 still on the aim and rotate P1's body left/right.
      While the target remains inside the sector, the crosshair must stay on it.
      *(Automated: `AimSectorTests.BodyRotation_DoesNotMoveAimInsideSector`.)*

- [ ] **4. Boundary push with no phantom offset.**
      a) Push the aim hard against a sector edge, then rotate P1 further in that direction:
         the crosshair is pushed along with the body and re-stabilizes in world space.
      b) Against the edge, push further out, then reverse the stick. The crosshair must move
         immediately inward with no wind-up.
      *(Automated: `...NoPhantomOffset_WhenPushingOutwardThenInward` and
      `...BoundaryPush_RestabilizesInWorldSpace`.)*

- [ ] **5. The body never rotates on its own toward P2's aim.**
      With P2 aiming at a target, release all P1 input. `BodyYaw` in the HUD must not change.

- [ ] **6. Feel judgment (write answers below).**
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

Playtest date:
Testers:
`BodyYaw`/aim behavior notes:
Feel verdict (Model C keep / change / test Model A):
Open problems:
