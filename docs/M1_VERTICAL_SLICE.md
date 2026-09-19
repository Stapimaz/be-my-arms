# M1 — Local Vertical Slice

**Status:** Implemented; automated verification passing. The human two-duo playtest gate
(criterion 3) is **deferred and not passed**.
**Scene:** `Assets/Scenes/M1VerticalSlice.unity` (built by `Be My Arms > M1 > Build M1 Scene`)
**Tuning asset:** `Assets/Scripts/M1/Data/M1Tuning.asset`
**Module:** `Assets/Scripts/M1` (`BeMyArms.M1`), reusing the M0 `AimSector`/hitbox/damage types.

---

## 1. Scope delivered

**P1 (body):**
- walk, unlimited sprint, jump;
- directional dodge (short cooldown, **no invincibility frames**);
- slide (sprint entry, friction decay, min/max duration);
- vault (obstacle-detected, timed move);
- light kick and heavy kick (timed, cooldown, melee damage, no hard stun).
- **look/body model:** camera/head look decoupled from `BodyYaw` within a neck-offset limit; the
  body smoothly follows the look past a threshold; explicit AlignBody action; WASD stays
  body-relative.

**P2 (arms):**
- shoulder-anchored first-person camera;
- sector-clamped world aim (Model C, reused from M0) + free pitch + recoil kick;
- rifle, pistol and knife; fire, reload and weapon swap;
- movement-state-based spread and per-shot recoil.

**Shared:**
- one shared HP pool (M0 `SharedBodyHealth`); P1 head is the only critical region;
- P2 can fire during every P1 posture — posture changes spread, never locks the weapon;
- compact greybox arena with cover and a raised platform (verticality);
- HUD with posture, sector marker, weapon/ammo/reload/spread;
- all tuning in `M1Tuning` (ScriptableObject): no recompile needed to change values.

**Simulation/presentation separation** (for the NGO prediction layer): `P1Motor`, `P2AimRig` and
`WeaponController` are pure simulation components with no camera/HUD/input dependency;
`M1BodyRoot` is the orchestrator, and cameras/HUD/input live separately.

---

## 2. How to run

Open `Assets/Scenes/M1VerticalSlice.unity` and press Play (split view: left P1, right P2).

- P1: WASD move (body-relative), mouse = look/head, **LMB / Alt = align body to look**, Shift
  sprint, Space jump, Q dodge, C slide, E vault, F light kick, V heavy kick, B test damage.
- P2: gamepad right stick aim, RT fire, X reload, Y swap, B knife.

For an automated/self-driving run, set `M1BodyRoot.inputMode = Scripted` (the scripted source
moves, sprints, jumps, dodges, slides, kicks, aims, fires and reloads).

---

## 3. Automated verification

- **EditMode: 19/19 passing.** M1 tests cover: accuracy ordering and row overrides; the aim rig's
  world-stability and no-phantom-offset boundary behaviour; weapon start/swap/reload; ammo
  consumption; and that **posture changes spread but never blocks firing** (fire during
  `KickHeavy`).
- **PlayMode: 2/2 passing** (M0 smoke tests still green).
- **Scripted play-mode run observed:** weapon spread changed with posture (≈4.8° while moving,
  ≈3.6° steadier) and hits registered on the dummy (100 → 82 → 64). No runtime errors
  (`consoleErrors: 0`).

## 4. Acceptance criteria status

| # | Criterion | Status |
|---|---|---|
| 1 | All role abilities function; none hard-locks P2 | **Passed** (implemented; verified by test + scripted run) |
| 2 | Target outside the sector forces P1 rotation; dependency reads clearly | **Passed** (aim-sector math tests + HUD sector bar) |
| 3 | Two-duo playtest: both roles report agency, no motion sickness, aim model chosen | **Deferred — not run.** This is the human gate; do not record it as passed |
| 4 | Architecture matches the chosen netcode path (NGO prediction-ready) | **Passed** (simulation/presentation separation documented) |
| 5 | Tuning changeable without recompiling | **Passed** (`M1Tuning` asset) |
| 6 | P1 free-look question resolved and documented | **Documented** (see below); still a decision gate |

---

## 5. P1 look/body model

P1's camera/head look is decoupled from `BodyYaw`:

- mouse drives the look yaw; the third-person camera follows the look, not the body;
- the look is clamped to `BodyYaw` ± `neckYawLimitDegrees`;
- past `bodyFollowThresholdDegrees`, the body smoothly turns toward the look at
  `bodyFollowSpeedDegreesPerSecond`;
- the explicit AlignBody action turns `BodyYaw` to the look at `bodyAlignSpeedDegreesPerSecond`;
- WASD movement stays relative to `BodyYaw`; P2's firing sector is relative to `BodyYaw`.

All of these are tuning values in `M1Tuning`. The align binding (`alignBodyOnLeftMouse`,
`alignBodyKey`) is temporary/configurable and is not a design decision.

This supersedes the M0 assumption that mouse directly drove `BodyYaw`.

---

## 6. Known limitations (expected for M1)

- Greybox geometry, capsule body and proxy visuals are temporary, not final presentation.
- No animation/IK rig yet (final rig is M6); abilities are state-driven without animation.
- Vault is a simple timed move, not a full mantling system.
- No networking: M1 is local; the NGO prediction/reconciliation layer is M2.
- No utility, economy, rounds or 2v2 (M3+).
