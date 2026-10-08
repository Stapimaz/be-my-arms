# Pass 2 foundation redo — work record

**Pass 2 foundation accepted (7 October 2026).** Human review accepted the rebuilt
foundation and authorized closure after the focused cleanup below. Subsequent solo-playtest
fixes (rifle handling, bot turns, kick direction/extension and POV wrists) are tracked in
`docs/PASS2_SOLO_CLEANUP.md`. Pass 3 has not started and requires separate authorization.

## Safety checkpoint

`refs/checkpoints/pass2-before-foundation-redo` points to
`811ef0d7d79f4862187ed06c74004833c5a0f621`. It snapshots all non-ignored working
files, including pre-existing user changes, using a temporary Git index. The working
branch, user's index, and working files were left intact. Inspect or recover individual
files from this reference; do not blindly restore the whole snapshot over newer work.

## Asset evaluation (7 October 2026)

| Candidate | Findings | Decision |
| --- | --- | --- |
| [Quaternius Universal Base Characters](https://quaternius.itch.io/universal-base-characters) | Smooth cohesive topology, real face and articulated fingers; same modern humanoid bone naming as UAL. Standard includes two superhero-proportion bases and hairstyles under CC0. | Use the complete base mesh, skin weights and skeleton, with continuous suit colours. Evaluated buzzed hair omitted after bind-orientation inspection. |
| [Universal Animation Library](https://quaternius.itch.io/universal-animation-library) | Current v3.0 corrects phase synchronization and Unity scale; earlier releases also corrected 24/30 fps export. Free Standard has forward jog/crouch, sprint, idle, jump/death; the full directional and rifle sets require Pro/Source. | Use current free Standard for available motions. Do not describe it as a free eight-direction library. |
| [Universal Animation Library 2](https://quaternius.itch.io/universal-animation-library-2) | Free Standard includes slide start/loop/exit and a 1 m climb. Modern naming matches the base character. | Use these actual authored actions. |
| [KayKit Character Animations 1.1](https://kaylousberg.itch.io/kaykit-character-animations) | All 161 animations free, CC0; separate forward run, backward walk and left/right strafe, plus two-handed ranged poses. Short-legged/large-headed source proportions need careful retargeting. No full directional crouch set or articulated finger motion. | Use authored directional stand locomotion and two-handed pose; adapt crouch variants offline and author finger poses against Quaternius's smooth hands. |
| [Mixamo](https://www.mixamo.com/) | Large mocap selection and rigging; Adobe account/download workflow, non-CC0 terms, inconsistent selection/proportions across assets. | Less reproducible for this free, shared-source pipeline. |
| [Synty Base Locomotion](https://syntystore.com/products/animation-base-locomotion) | $69.99 at review, 247 clips, proper strafing/crouch/transitions and Mecanim examples. Does not itself supply the desired smooth character/finger setup. | Useful paid option, not necessary for the selected free route. |

UAL1 Pro ($9.99) / Source ($14.99) was raised before any purchase because it supplies
the directly compatible full directional/rifle set. The user selected **free assets only**.
No purchase was made. Preview/viewer files are not substitutes for licensed paid downloads.

## Foundation

- Two role surfaces are partitioned from one ready-made body without independently capped
  shoulder seams. They reference one set of bone transforms and one locomotion Animator.
- Humanoid retargeting happens during asset preparation; resulting native transform clips
  drive the shared skeleton. This keeps the runtime graph straightforward and exposes the
  actual baked poses to correctness checks.
- Source forward conventions are normalized by humanoid root-orientation import settings.
  Source-rig orientation is fixed per library rather than inferred from each strafe's average
  facing. Cardinal clips share a left-foot contact phase through cyclic offline sampling.
  Root motion is disabled. Cosmetic action-facing never writes authoritative movement.
- A four-direction authored blend provides continuous diagonal motion. It is not eight
  separately authored diagonal clips. Crouch side/back variants are adaptations of those
  authored motions with lower stance, shorter stride and explicit knee planes.
- P1's visible head look is set deliberately from P1 look state, independently of clip or
  P2 chest aiming. P2 rotates the shared upper chest, and both smooth arm surfaces follow it.
- FPS forearms/hands derive from the same base character, with anatomically defined saved
  rifle sockets and per-joint finger poses. The existing split-component rifle/reload baseline
  is retained.

## Simulation changes

- Slide direction uses the same look-relative input direction as WASD/dodge.
- Jump defaults: launch 7.5 m/s, gravity -30 m/s², descending gravity multiplier 1.35.
  Both network authority and prediction use the same simulation defaults.
- The earlier redo repaired vault traversal; human review subsequently retired the separate
  mechanic. Its input, command/state fields, collision probe, simulation path, active Animator
  state, feedback and HUD hint have now been removed from the live Duel foundation.
- Presentation movement signals are measured from actual collision-resolved displacement
  for actions as well as ordinary locomotion.

## Final human-requested cleanup

- P1 is one matte blue material across body, legs, head and face; P2 is one matte orange
  material across shoulders, arms and hands, including the FPS mesh. Patchy skin/sole/team
  bands and runtime trim recoloring are removed. Separate role skin/color selection is later work.
- No dedicated vault action or E control hint. Normal jump, slide, dodge and kicks remain.
- Independent P1 look is retained inside a **25° follow threshold**, with a **45° hard look
  limit** and **180°/s body follow**. Authority, prediction and the regenerated body prefab use
  the same values. Alt explicit align and P2's elastic aim sector retain their behavior.
- Visible head rotation is also bounded against the shared aiming chest (50° yaw, 35° pitch,
  15° roll). Opposed P1 look/P2 aim cannot force an unnatural neck twist.
- Kicks chamber the knee, extend sharply, then recover. They rotate about body-right rather
  than the imported bone-local X axis. Light/heavy simulation timings and damage are unchanged.

## Current executable / controls

Run **`Builds/Pass2Redo/BeMyArms.exe`**. Both PCs/clients must use the entire new build
folder; removing vault command/state fields changes serialization from older executables.

1. **PLAY → Duel → P1 → Start Match.** Compare forward/back/left/right and diagonals
   while keeping body facing fixed. Check the smooth combined body, crouch side steps,
   deliberate head look, sprint, jump/fall/land and look-relative slide.
2. Try **Space** jump, **C** slide and **F / V** kicks around cover. E has no movement action.
   Check independent look inside the smaller range and earlier body follow during fast turns.
3. **PLAY → Duel → P2.** Inspect the smooth hands and rifle during movement, crouch,
   firing and **R** reload. Check the grip/pose and the retained audio/VFX in actual play.
4. **DUO PRACTICE → role → HOST DUO.** Open the local partner from ESC, or join from the
   second PC using the displayed IP/port. Check independent P1 look/movement and P2 aim
   on the shared fighter. Stretch both elastic sector edges and release them.
5. Check **F6** reset, **F7** exchange/reset and **F8** fresh match during actions/reload.

Useful feedback: role, action and direction, whether it repeats after F6, and whether the
failure is movement, pose, camera or timing. Automated correctness is separate from human
feel/readability acceptance. Pass 3 awaits separate authorization.

## Reproduction and validation

- Mesh sources and rights: `Assets/Art/Characters/Pass2Redo/README.md`.
- Rebuild assets: `unity command eval_file tools/pipeline/qa/prepare-pass2.cs 120000
  --timeout 180 --format json`. The positional timeout is the eval main-thread budget;
  the CLI `--timeout` is in seconds.
- Build: `unity command eval_file tools/pipeline/qa/build-pass2-cleanup.cs --format json`,
  then inspect `build_status`.

### Foundation redo validation before final cleanup (historical)

- **172/172 EditMode checks passed**, including 18 movement cases and 21 fighter cases.
  These cover action displacement, jump timing, collision, vault footprint/ceiling/replay,
  actual Duel vault geometry, directional contact/closed loops, crouch lane separation,
  shared bone ownership, extreme-aim grip reach and FPS reload/reset.
- **2/2 PlayMode menu checks passed**.
- Reports: `Builds/pass2-redo-editmode.json`, `Builds/pass2-redo-menu.json`.

- Final Windows x64 development player **`build_24678cd57507` succeeded** with zero build
  errors. Its incremental build report contains zero warnings; the earlier full compilation
  reported 43 existing obsolete-API/unused-field warnings. Output:
  **`Builds/Pass2Redo/BeMyArms.exe`**.
- **22/22 dedicated-server/two-client integration checks passed** against this executable:
  shared role ownership, authoritative movement/crouch, elastic overtravel/release/fire,
  resets, role exchange, hard peer loss/reconnect, replacement guest, repeated menu session
  allocation, owner shutdown and owner-crash watchdog.
- **7/7 rendered-player checks passed** against this executable: one local owner/camera,
  one audio listener, FPS layer, live grip reach, shot audio/ammunition, reload reach/completion,
  and P1 directional blend after role exchange.
- The rendered QA script queues R through the normal Input System update. Pipeline's
  `simulate_key` performs an extra immediate update that can consume the one-frame key edge
  before gameplay polls it; the queued keyboard event exercises the normal input path.
- Removed identified old preview-only scene objects (layer-31 `M7PlayerBody(Clone)` and
  `Pass2PreviewCamera`/`Pass2PreviewLight`) through the editor. They caused duplicate runtime
  ownership/cameras in the first redo build. The preview fixture now uses an isolated preview
  scene and matches the runtime FPS near plane.
- Reports: `Builds/pass2-redo-build.json`, `Builds/Pass2Redo/QA/integration-results.json`,
  `Builds/Pass2Redo/QA/rendered-results.json`. Editor composition captures are `fighter.png`
  and `fps.png`; runtime captures are `runtime-*.png` in the same QA folder. Screenshot RPC
  latency means a reload capture can show the completed ready pose; reach/progress checks
  sample the live choreography directly.
- At the foundation-redo handoff, integration/rendered logs contained no runtime errors/
  exceptions and all QA clients and dedicated servers were stopped. Source changes were
  then uncommitted/unpushed; publication is covered by final closure below.

### Final cleanup validation / closure (7 October 2026)

- **169/169 EditMode checks passed**. Obsolete vault-specific tests were retired with the
  mechanic; snapshot replay coverage now exercises slide. Focused cleanup cases check uniform
  role/FPS materials, matching authority/prediction look tuning, early body follow and bounded
  fast-turn replay, opposing P1 look/P2 chest aim, forward kick extension and return to stance.
- **10/10 rendered-player checks passed**. The prior seven presentation cases are joined by
  small independent P1 look, fast-look early follow/45° bounds, and no action on E.
- **22/22 dedicated-server/two-client integration checks passed**, including ownership,
  movement, elastic sector firing, resets/exchange, reconnect/replacement, repeated sessions
  and dedicated-server owner shutdown/watchdog.
- Final Windows x64 development player **`build_d7a51b96c94c` succeeded**, zero build errors.
  Its full clean-build report has 44 warnings (obsolete APIs/unused fields and the Pipeline
  development-runtime tooling warning); these are not runtime failures.
- A first incremental cleanup build did not start its QA endpoint despite enabled settings.
  A clean build restored the baked runtime configuration. `build-pass2-cleanup.cs` includes
  `CleanBuildCache` for this reproducible final build.
- Output: **`Builds/Pass2Redo/BeMyArms.exe`**. Both PCs must use this build together.
- Reports: `Builds/pass2-cleanup-editmode.json`, `Builds/pass2-cleanup-build.json`,
  `Builds/Pass2Redo/QA/rendered-results.json`, `Builds/Pass2Redo/QA/integration-results.json`.
  Composition/pose captures: `fighter.png`, `fps.png`, `kick-chamber.png`, `kick-strike.png`.
- Final QA logs contain no runtime errors/exceptions. All spawned QA processes were stopped.
- Full Pass 2 implementation, source assets, tests, tooling and the required control/transport
  foundation are committed for publication on `main`. Pre-existing unrelated editor/render/
  cloud-settings changes are preserved in the working tree. The safety snapshot remains local.

**Pass 2 accepted. Pass 3 remains gated on explicit authorization.**
