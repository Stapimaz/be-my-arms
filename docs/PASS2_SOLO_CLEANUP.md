# Pass 2 — solo-playtest cleanup

**Status: implemented, rebuilt and validated; ready for human solo playtesting.** This is a
focused follow-up to the accepted Pass 2 foundation, prompted by solo human/bot playtesting.
Pass 3 remains gated on explicit authorization.

## Reproduction / root causes

- Sustained P2 fire: the authoritative ray used the exact input aim for every round. Existing
  viewmodel/world-rifle recoil was cosmetic and did not affect local aiming or server hits.
- Solo bot P1: target acquisition requested up to 25° **per simulation tick**, using the 540°/s
  explicit-align path, then toggled align off near its target. Replicated changes could be large
  enough to jerk the body/sector despite presentation smoothing.
- Kicks: hits resolved against BodyYaw, while P1 could look independently. The leg overlay
  rotated about the network root's right axis, which is not the rotated Presentation/model
  basis on clients. The old zero-yaw pose fixture missed the resulting sideways/backwards kicks.
- FPS grip: grip-target reach did not establish anatomical wrist shape. The firing wrist's
  hand/forearm longitudinal angle measured about 101°. The source shoulder placement, POV
  sockets and elbow poles folded the arm; wrist-only roll correction also pinched the skinned
  mesh at the forearm/hand seam. Third-person grips used a different effective arm placement.
- The executable probe also caught an idle-cycle issue missed by a phase-zero still fixture:
  the imported world-rifle breathing animation changed the support wrist to roughly 59°
  later in its loop. The user clarified the desired simple rifle POV with a shooter reference:
  one visible support hand under the handguard at rest; the second hand appears for reload.

## Changes

### Rifle handling

- `M3RifleHandling` owns shared burst timing, a controllable recoil pattern and spread sampling.
- The server advances bloom only on **accepted rifle rounds**, after cadence/ammo and historical
  aim-sector validation. First three shot spread radii are .10°/.16°/.22°; continued fire grows
  to a 1.8° cap. A gap longer than .30 s restores the first-shot burst.
- A uniform disk sample perturbs the actual aim-local ray. Damage and world impacts use the
  same ray. Bots also use the authoritative rifle bloom path.
- Local shot feedback now kicks actual P2 mouse aim upward (.24°/.32°/.40°, then .72° per round)
  with a predictable lateral pattern. Mouse input can counter it; compensating recoil still
  leaves the server-enforced sustained-fire bloom. Confirmation accounting avoids applying
  the same predicted shot recoil twice. Control epochs clear recoil/burst state.

### Solo bot P1

- `M3BotSteering` requests a **90°/s** turn using delta time and shortest-angle movement.
  The intermediate target is based on current BodyYaw, so a bot taking over an independent
  human look cannot immediately fast-align through the inherited neck offset.
- Body/look align continuously, including target changes and yaw wrap. P2 mouse input has no
  authority over bot steering. Existing range/strafe/reaction behavior remains the solo path.

### P1 kicks

- Kick start captures P1 LookYaw in the existing replicated/predicted ActionDirX/Z fields.
  Server hit direction and cosmetic model facing both use that captured direction throughout
  the strike; later independent look/body-follow cannot redirect it mid-action.
- A two-bone foot target chambers, rapidly extends to 97% leg reach, holds the sole strike
  visibly, retracts, then recovers. The solve uses the presented model basis, including turned
  client rigs. Light/heavy action durations and damage retain their existing values.

### First-person grip

- Dedicated POV shoulder placement, saved grip sockets, elbow poles and palm orientation
  put the support hand beneath the handguard with a natural forearm continuation. The camera-local support
  shoulder is authored forward to reach the handguard without stretching the arm.
- Forearm pronation is solved at the forearm before applying the hand socket orientation.
  This distributes roll away from the weighted wrist seam. Saved finger poses remain active.
- Normal rifle hold has **one visible support arm/hand** and no idle/breathing Animator.
  The authored neutral hold is saved into the prefab; procedural weapon-mounted grip posing
  stays steady. The firing-side renderer appears during reload and hides on return to ready.
  Shot kick and small whole-viewmodel movement bob remain separate from the arms' fixed hold.
- This visibility/composition is authored on the rifle prefab, not a universal one-hand rule
  for future pistols or other weapons, which can have their own POV prefabs.
- The world-body grip authoring stays on its accepted layout. The smooth orange POV geometry
  and shared-source skeleton are retained.

## Checks / handoff

- Preparation: `unity command eval_file tools/pipeline/qa/prepare-solo-cleanup.cs 30000 --timeout 60 --format json`.
- Build: `unity command eval_file tools/pipeline/qa/build-pass2-cleanup.cs --format json`, then `build_status`.
- Executable checks: `tools/pipeline/test-pass2-rendered.ps1 -BuildDirectory Builds/Pass2Redo`,
  followed sequentially by `tools/pipeline/test-pass1.ps1 -BuildDirectory Builds/Pass2Redo`.
- `qa_solo_probe` samples real frames/ticks, including transient kick strike geometry,
  bot turn rate, ready-wrist bend and authoritative ray/burst data. Slow CLI polling cannot
  skip the kick's strike frame. The rendered fixture protects bodies from deaths while
  keeping actual bot teammate/opponent movement, aim and firing enabled.

### Final results

- Windows x64 development build **`build_eb5e8f642a18` succeeded**, zero errors (47 full-build
  warnings). Output: **`Builds/Pass2Redo/BeMyArms.exe`**. Use the complete updated build folder.
- **179/179 EditMode checks passed**: burst spread/recoil/pause reset, bot takeover/retarget/yaw
  wrap, captured kick direction/replay, rotated kick geometry, uniform role materials, steady
  single-support-hand hold, camera framing, reload-only second-hand visibility and grip reach.
- **18/18 rendered-player checks passed** with the normal solo bot teammate paths. The fixture
  forces a 130° enemy reacquisition, then measures the live bot's bounded turn; it also checks
  actual sustained aim climb/ray spread, one steady support arm, two reload arms, return to one
  arm, and the real transient kick strike after exchanging into P1 with bot P2.
- **22/22 dedicated-server/two-client checks passed**, covering the accepted authority/sector,
  ownership, reset/exchange, reconnect/replacement and repeated-session lifecycle foundation.
- Live metrics: bot maximum turn rate **90.00022°/s** (floating-point tolerance), ready support
  wrist bend **29.64°**, sustained spray aim pitch **0° → -16.8°**, authoritative spread cap
  **1.8°** with observed ray deviation **1.77°**, kick forward reach **0.862 m** and horizontal
  attack alignment **0.99999994**.
- Final QA logs contain no runtime errors/exceptions; all QA clients and servers were stopped.
- Reports: `Builds/solo-cleanup-editmode.json`, `Builds/solo-cleanup-build.json`,
  `Builds/Pass2Redo/QA/{rendered-results,integration-results,solo-handling-metrics,solo-kick-metrics}.json`.
  Captures include `fps.png`, `runtime-p2.png`, `kick-strike.png` and `runtime-kick-strike.png`.

### Human playtest

1. **PLAY → Duel → P2 → Start Match** (bot P1). Check the simple one-hand rifle POV and smooth
   bot body turns. Hold LMB against distant cover: first shots stay tight; a long spray climbs
   and spreads. Pull the mouse down to control recoil, then pause briefly to regain tight bloom.
2. **R** reload: the second hand appears and the normal one-hand hold returns afterward.
3. **F7** exchange/reset, or start as **P1** (bot P2). Look independently, use **F / V** kicks,
   and repeat while facing other world directions. The leg should visibly extend along the
   captured attack direction rather than the unrotated network root.

Correctness checks are complete. Human feel/readability review is the next step; this cleanup
does not authorize or begin further Pass 3 work.
