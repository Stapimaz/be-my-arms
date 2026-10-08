# Current playtest — rifle precision and shared-body exposure

**Phase 3 first iteration · 2026-10-08 · Awaiting human review**

Build: `Builds/Windows/BeMyArms.exe`. Start through **PLAY → Duel → P2 → Start Match**.
Also try P1 solo, or **DUO PRACTICE** with the second human in the other role. Use matching
new binaries for server and clients. F6 resets an encounter; F7 exchanges practice roles.

## What changed

- Shots no longer hit an oversized sampled line around an enemy. A narrower, standardized
  body volume and an exposed P1-head volume resolve the actual first surface hit.
- Crouched/elevated target poses rewind together with their position, rather than mixing
  historical horizontal position with current height.
- Rifle: **18 body / 45 head damage** against shared 100 HP. From full health that is six body
  hits or three head hits; recoil, bloom, cadence and movement controls are unchanged.
- Head hits have a gold marker, **HEAD HIT** text and the existing distinct headshot sound;
  kills have the elimination cue. These appear only after server-applied damage.
- Both partners share confirmation. P1 sees **PARTNER HIT / PARTNER HEAD HIT** for P2's shots;
  P2 sees **PARTNER KICK** for P1's melee, rather than mistaking it for a rifle hit.
- Impacts use the actual hit point, not an arbitrary chest location. Cover stops the shot
  when its surface is nearer than the enemy's surface.

The four-second post-spawn protection is unchanged. Check the **PROTECTED** HUD status before
interpreting an early shot with no hitmarker as a bug. There are still no dodge invulnerability
frames and neither human acquires the other role's controls.

## Specifically evaluate

1. **P2 precision:** compare deliberate chest shots with short bursts at the exposed P1 head.
   Does the head reward feel worthwhile without turning the match into instant deletion?
   Do clearly aimed shots and near misses agree with what you see?
2. **P1 exposure:** crouch behind low cover, peek, change elevation and dodge while your partner
   fires. Does positioning feel consequential without losing responsive control? Try both roles.
3. **Shared information:** can you tell a body hit, head hit, elimination and partner kick apart
   quickly? Do the cues help coordination, or are text/sound/duration distracting?
4. **Profile mismatch:** watch especially crouching, sliding, kicks and the edges of shoulders/
   arms. The authoritative profile is not an animated per-limb mesh. Report apparent hits that
   miss, apparent misses that hit, or head confirmations away from the visible P1 head.
5. **Preserved foundations:** body/look movement, immediate P2 aim, Elastic Soft, recoil control
   and the accepted rifle POV should still feel familiar. Flag any regression separately.

Near-cover camera/shot-origin disagreements and detailed animated limb coverage are not declared
solved by this iteration. Please describe role, action, cover and where you aimed when reporting
a mismatch; no screenshot capture exercise is required.

Technical evidence: **196/196 EditMode tests**, **28 focused built-player combat checks**, and
**7 normal Duel smoke checks** passed; Windows build succeeded with zero errors. Those checks
cover numerical/authority/event correctness, not visual alignment, readability or balance.
The build reported 45 warnings: existing deprecated Unity/NGO API calls and unused fields,
not build failures. They were reviewed but are not a reason to rewrite networking in this iteration.
