# Pass 1 — shared-body control baseline

This is the implementation handoff for human testing, not a claim that feel has been accepted.
Human review subsequently authorized Pass 2. The current handoff is `PASS2_PRESENTATION_BASELINE.md`.

## Build and entry

Run `Builds/Pass1/BeMyArms.exe`. Both humans must use this build (the command/state wire format changed).

- **PLAY → Duel → P1/P2 → Start Match**: solo practice with a bot filling your partner role.
- **DUO PRACTICE → choose role → HOST DUO**: launch a dedicated server; wait for both Team A roles.
- Same PC: host presses **ESC → Open local partner**. A second client opens, joining the other role automatically. Keyboard/mouse go to the focused window.
- LAN: partner opens **DUO PRACTICE**, selects the other role, enters the host PC's LAN IP and the port shown on its waiting screen, and clicks **JOIN DUO**.
- Both humans share Team A's body. Team B is filled by Easy bots. The lobby does not time out and silently replace the missing human.
- ESC releases input locally; the dedicated match continues. Leaving the host closes its dedicated server. A guest leaving explicitly frees its role; an unexpected disconnect keeps its token's reconnect reservation and temporarily uses a bot.

## Controls and fast iteration

| P1 — body / third-person | P2 — arms / first-person |
| --- | --- |
| WASD, look-relative movement | Mouse, world-space aiming |
| Shift, sprint | LMB, fire |
| Ctrl, held crouch | R, reload press |
| Space, jump | Tab, ask P1 to turn toward your aim |
| Alt, align body toward your look | ESC, practice tools |
| Q dodge; C slide; E vault | |
| F light kick; V heavy kick | |

Either human can use **F6** to restart the encounter, **F7** to swap roles and reset the match,
or **F8** to start a fresh match. The same actions are available under ESC, including after match end.
Restarting an encounter preserves its round index and score; a fresh match or role swap clears score/kills.
Resets restore health, rifle/ammo, stance/action/cooldown state, spawn position, aim/look, input buffers,
blindness, utilities, zone state, and camera/weapon smoothing. There is a short three-second setup phase.
The first four live seconds have spawn protection, explicitly counted down on the HUD.

## Current sector (updated for Pass 2)

The **elastic sector** treats ±70° as a resting elastic boundary. Aim is
normal and one-to-one throughout the resting sector. Beyond it, outward sensitivity starts at **8%**
and decreases as the stop stretches. Sustained outward mouse pressure can slowly force up to **15°**
of overtravel (a server-enforced outer limit of ±85°). There is no stored outward overflow debt.

When outward mouse pressure stops, a **critically damped spring** immediately returns the overtravel
toward ±70°. It is almost settled in about a quarter second, with no bounce past the resting boundary.
Inward mouse travel is never resisted and cancels stored return velocity; release while still outside
the sector allows the spring to help the return. Body turns still bound the world-space aim.

Direct mode and the comparison selector were removed at the user's request.
The HUD shows `ELASTIC LEFT/RIGHT +N°` while stretched. The distance-based
resistance and exact spring integration behave consistently across frame rates. Reset/role exchange
clears spring velocity.

The displayed/submitted yaw is the same value. Server legality uses the exact historical body snapshot
P2 aimed against, rather than a smoothed presentation yaw plus a guessed rewind time. Boundary feedback
is informational HUD text. The aiming camera has no cosmetic rotational recoil; rifle kick remains on
the viewmodel, so cosmetic shot feedback cannot silently displace the crosshair from firing direction.

## Coordination feedback

- P1 sees human/bot arms ownership, reload/empty/firing state, partner aim offset, aim limit side,
  and a short explicit left/right turn request.
- P2 sees human/bot body ownership, movement/action state and speed, elastic stretch and aim-limit side.
- Phase, role, health, ammo and score remain visible. A waiting/connection panel shows the server port
  and disconnection reason. Wrong occupied roles are rejected rather than silently assigned elsewhere.

## Suggested human review

1. Solo P1: walk, strafe, sprint diagonally, hold/release crouch, then jump/dodge/slide/vault and both kicks.
   Check that movement speed, low stance, action recovery and camera control are predictable.
2. Solo P2: aim normally up to each ±70° edge, continue moving the
   mouse outward to stretch it slowly, then stop moving the mouse and watch the return. Reverse a small
   amount while stretched to check immediate inward response. Check body turns and firing in overtravel.
3. Two-human duo: P2 holds world aim while P1 strafes, turns slowly, then aligns rapidly. Test P2's Tab
   turn request, P1's response, reload while moving, crouch behind cover, and moving while P2 fires.
4. Hold controls during F6. Confirm the new encounter starts cleanly. Repeat F7 twice; both views,
   role inputs and feedback should follow the exchanged ownership. F8 should clear the score/kills.
5. Open/close ESC, change window focus and return. Movement and firing should stop while inactive;
   pressed actions should not replay when control returns. Release LMB before firing again after
   a menu/focus change or reset; a held resume click cannot fire accidentally.
6. Leave the guest through the menu, rejoin the open role, then leave the host and start another match.
   Report any stale body, wrong role, duplicate camera, stuck cursor or failed join.

Most valuable feedback: which role/mode you tested, the exact action that felt wrong, whether it was
repeatable after F6. Human comfort and coordination
decide acceptance; correctness tests do not decide feel.

## Engineering changes

- Mouse sampling remains per-frame; P1 prediction and authoritative movement now share fixed 60 Hz
  command steps. Arrival bursts cannot multiply movement speed. Held controls bridge brief gaps;
  look deltas and pressed actions are consumed once, with a silence timeout releasing holds.
- Crouch now reaches the server. Frame-level edges are latched until consumed; phase/focus/death
  gates clear invalid actions. Duplicate, stale-generation and nonfinite commands are rejected.
- Full vault endpoints live in replicated state, making mid-vault reconciliation replayable.
  Vault completion reaches its endpoint; slide uses low stance; crouch-sprint reports actual walk state.
- Round/ownership changes advance a control epoch and clear server queues and client prediction/aim.
  Reconnection no longer interrupts a connection attempt every two seconds.
- Practice has exact-slot approval, server-authoritative resets/role exchange, fresh private session
  settings, real address/port entry, and the existing owned dedicated-process shutdown/watchdog.
- Process launch arguments are consumed once, so returning to the menu does not auto-join again.
- Unity Transport 2.7.2 is embedded with a focused UDP receive-buffer ownership fix needed for abrupt
  peer loss on Windows. See `Packages/com.unity.transport/BMA_PATCH.md`. Dedicated/headless processes
  run at 60 FPS instead of consuming unlimited CPU while stepping a 60 Hz simulation.
- Normal practice adds no artificial network delay/loss. Conditioning remains available through CLI
  for correctness checks. Easy bots' intentional range holding no longer accumulates stuck recovery time.

## Reproduction tools

- Build through the connected Editor: `unity command eval_file tools/pipeline/qa/build-pass1.cs`;
  read `unity command build_status` for the complete result.
- Control/authority regressions: `M3ControlBaselineTests` (EditMode). These exercise frame-rate scheduling,
   delayed input resets, held/edge controls, elastic sector response, snapshot history, vault replay, stance,
  role exchange, voluntary leave and reconnect reservation.
- Menu layout/navigation: `M7MenuRuntimeTests` (PlayMode; use Pipeline's async test runner).
- Development-only `qa_headless_controls`, `qa_inject_input`, `qa_inject_look`, `qa_practice_action`, and
  `qa_player_state` enable multi-process correctness checks without rendered playtesting.
- `powershell -NoProfile -ExecutionPolicy Bypass -File tools/pipeline/test-pass1.ps1` runs the real
  dedicated-server/two-client control, reset, role-exchange, crash/reconnect and guest-replacement check,
  followed by real menu hosting, allocated-server shutdown, a fresh solo session and the owner watchdog.

## Original validation handoff — 2026-10-07

- Editor compilation completed with no errors.
- **136/136 EditMode tests passed**, including 20 control-baseline cases.
- **2/2 PlayMode menu tests passed**, including duo navigation, editable address/port and on-screen buttons.
- **18/18 headless multi-process checks passed** in the actual Windows development build. These cover
  two humans sharing one body, fixed-path movement/crouch, legal edge fire, encounter reset, role exchange,
  hard peer loss while the partner stays connected, token reconnect after exchange, voluntary guest leave,
  fresh guest assignment, menu-driven allocation, clean server shutdown, repeated solo entry and owner-crash cleanup.
- Build **`build_a8ba12aa831c` succeeded**, Windows x64, zero build errors. The build report lists 40 warnings
  (deprecated object-find APIs and existing package/build warnings); no runtime errors/exceptions were found
  in the final integration logs. All spawned test clients and servers were stopped.
- No subjective gameplay evaluation was performed. Human feel acceptance is pending; Pass 2 has not started.

Local artifacts: `Builds/pass1-editmode-results.json`, `Builds/pass1-menu-results.json`,
`Builds/pass1-build-results.json`, and `Builds/Pass1/QA/integration-results.json` plus process logs.
The build and generated artifacts are ignored by Git; source changes remain uncommitted at handoff.

## Human-feedback revision — elastic Soft stop

Human review: Pass 1 generally feels good, but the first Direct/Soft comparison was effectively
indistinguishable. The requested replacement is normal aim → stiff elastic stop → slow overtravel
under continued outward pressure → spring return on release. That feedback supersedes the original
six-degree *inside-the-sector* resistance candidate; the current behavior is described above.

Revision validation:

- **143/143 EditMode tests passed**, including normal in-sector gain, mirrored heavy/bounded outward
  response, no overflow debt, release timing and no overshoot at 30/60/144 FPS, frame-independent spring
  traces, re-press/mode-change velocity resets, and preservation of authoritative overtravel across P1 steps.
- **22/22 headless integration checks passed** in the rebuilt player. Added real-client pressure/return
  checks and authoritative firing while the displayed aim is outside ±70° in Soft. Existing reset,
  role exchange, reconnect, guest replacement and server-lifecycle checks also pass.
- Windows build **`build_745d889e0be2` succeeded**, zero build errors. No runtime errors/exceptions were
  found in the final integration logs; all test processes were stopped.
- Updated player remains at **`Builds/Pass1/BeMyArms.exe`**. Both PCs need the updated build because
  P2 commands/body state now carry the selected Soft mode for consistent aim presentation and validation.
- Revised Soft feel is awaiting human review. Pass 2 has not started.
