# Pass 2 — rebuilt fighter and rifle presentation

> **Historical first attempt, rejected at human review.** The active smooth-character
> foundation redo and its playtest executable are documented in
> [PASS2_FOUNDATION_REDO.md](PASS2_FOUNDATION_REDO.md). The results below describe the
> original `Builds/Pass2` player and do not establish acceptance of the redo.

The user authorized this pass after Pass 1 generally felt good, selecting the elastic sector
as the sole behavior with 15° of overtravel. Pass 2 is handed back for human playtesting;
Pass 3 requires the next human review.

## Scope

- One combined tactical fighter: an exposed P1 face, armoured body/legs and a distinct P2
  clavicle/mechanical-sleeve rig. Team trims use teal/orange. The source mannequin head was
  replaced with a CC0 authored face; the suit and rifle geometry were rebuilt in Blender.
- Native, baked eight-way jog and crouch cycles with planted stance travel, lifted swing arcs
  and knee poles. Blend inputs read replicated/predicted body-local direction and speed.
  Sprint, airborne/landing, slide, vault, dodge-brace and kick presentation have action-specific responses.
- Dedicated camera-local FPS forearms/hands and rifle composition with authored grip positions, hand orientations,
  finger curls and explicit elbow poles. The animation solver runs after camera/animation updates.
- Reload choreography follows server reload state and weapon duration: support hand reaches and
  removes the magazine, inserts it, operates the bolt, then recovers the foregrip. The trigger hand
  remains on its grip. Magazine and bolt are separate geometry. Epoch/death resets cancel the sequence.
- Immediate local shot sound, attached FPS muzzle burst and rifle kick; world rifle reports
  replicated ammo use with spatial audio/muzzle effects. Local P2 feedback is not doubled.
- Softer transparent particles, sharper short muzzle/impact sparks, subtle footstep/landing dust,
  movement/landing/slide/vault/kick/gear sounds, and timed magazine/bolt sounds.
- Independent pooled spatial-audio voices, corrected VFX return-to-pool and overlay-layer handling.

The aim camera remains stable and logically aligned. Cosmetic movement/shot kick is on the viewmodel.
The presentation reads state; simulation, role authority, prediction and damage confirmation remain
the authoritative foundations for the slice.

## Sector

Normal aim inside ±70°. Outward gain beyond the resting boundary begins at 8%, decreases with
stretch, and is bounded at **±85°**. Release immediately starts a critically damped return;
inward motion remains responsive. Direct mode and the comparison menu were removed. This is
the only sector behavior. Both clients must use the new player because the old mode flag was
removed from command/state serialization.

## Human playtest

Run `Builds/Pass2/BeMyArms.exe`.

1. **PLAY → Duel → P1 → Start Match:** move forward/back/sideways and diagonally while keeping
   body facing fixed. Try sprint, crouch, jump/land, dodge, slide, vault and kicks. Check directional
   readability, body cohesion and animation transitions at gameplay distance.
2. **PLAY → Duel → P2:** inspect the rifle/hand composition while still, moving and crouching.
   Fire short/long bursts, spend some ammo, then reload while stationary/moving/turning.
   Check grip credibility, magazine/bolt timing, muzzle flashes, impacts and sound balance.
3. **DUO PRACTICE → role → HOST DUO:** host uses ESC → Open local partner, or the second PC
   joins through DUO PRACTICE with the host IP/displayed port. Both roles share Team A's fighter.
   Use F7 to see each role and observe the combined fighter with independent P1 movement/P2 aim.
4. Stretch both elastic sector edges, release pressure, reverse inward and fire while stretched.
5. Test F6 encounter reset, F7 role exchange and F8 fresh match during firing/reload/action states.
   Check there is no stuck weapon, magazine, dead pose, duplicate sound or stale camera effect.

Useful feedback: role, exact action/direction, whether it reproduces after F6, and what visually
or audibly breaks the combined fighter/rifle illusion. Human play determines presentation feel.

## Reproduction

- Source/rights and rebuild instructions: `Assets/Art/Characters/Pass2/README.md`.
- Correctness: `M7FighterPresentationTests` plus the existing EditMode suite.
- Build: `unity command eval_file tools/pipeline/qa/build-pass2.cs`, then `build_status`.
- Authority/session regression: `powershell -NoProfile -ExecutionPolicy Bypass -File
  tools/pipeline/test-pass1.ps1 -BuildDirectory Builds/Pass2`.
- Rendered-player correctness: `powershell -NoProfile -ExecutionPolicy Bypass -File
  tools/pipeline/test-pass2-rendered.ps1` (opens an actual rendered Windows client).
- Build, tests, render fixtures and runtime logs are written under ignored `Builds` output.

## Validation

- **161/161 EditMode checks passed**, including the sole 15° elastic sector and the imported
  eight-direction cycles, closed loops, standing/crouched extreme-aim grip reach, FPS reload reach/reset.
  After trimming the FPS shoulder geometry, the **19/19 presentation cases passed again**.
- **2/2 PlayMode menu checks passed**.
- **22/22 dedicated-server/two-client integration checks passed** with the Pass 2 runtime code.
  These cover movement/crouch, elastic overtravel/release/fire, resets, role exchange, hard peer loss,
  reconnect, replacement guests, repeated allocation/entry, server shutdown and owner watchdog.
- Final Windows x64 player **`build_a248698f31a1` succeeded**, zero build errors and zero warnings
  in its incremental build report. Output: **`Builds/Pass2/BeMyArms.exe`**.
- **7/7 rendered correctness checks passed** in the final player, exercising an actual Windows P2 client, audio listener/voices, FPS grips,
  live reload, and P1 directional animation after role exchange. Final results and frame captures are
  in `Builds/Pass2/QA/rendered-results.json` and `runtime-*.png`.
- Final headless/rendered logs contain no runtime errors/exceptions. All spawned QA clients and
  dedicated servers were stopped.

Reports: `Builds/pass2-editmode-results.json`, `Builds/pass2-presentation-results.json`,
`Builds/pass2-menu-results.json`, `Builds/pass2-build-results.json`, and
`Builds/Pass2/QA/integration-results.json`. Feel/readability acceptance remains the human playtest.
Source changes are uncommitted; no push has been made.
