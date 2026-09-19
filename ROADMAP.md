# Be My Arms — Development Roadmap

**Status:** Approved working roadmap
**Source of truth for game design:** `GAME_CONCEPT.md`
**Companion document:** `TECHNICAL_PLAN.md`
**Current production project:** `C:\Users\stapi\GameDev\be-my-arms`
**Engine:** Unity `6000.4.3f1`, URP
**Last updated:** 2026-09-19

> This roadmap owns implementation order, milestone status and acceptance criteria. Game design
> belongs to `GAME_CONCEPT.md`, which defers sequencing to this document. Technical
> architecture belongs to `TECHNICAL_PLAN.md`, and local tooling to `docs/DEV_ENVIRONMENT.md`.

---

## 0. How to read this document

Every item is tagged:

- **[LOCKED]** — from `GAME_CONCEPT.md`; do not casually change.
- **[PROPOSED]** — technical decision adopted for planning; revisit with evidence.
- **[SPIKE]** — must be proven by test or playtest before it becomes a rule.
- **[DEFERRED]** — deliberately postponed.

If this roadmap and `GAME_CONCEPT.md` ever conflict on **game design**, the concept
document wins. If they conflict on **implementation order**, this roadmap wins.

---

## 1. Guiding principles

1. **Prove the mechanic first.** The shared-body control relationship is the product.
   Nothing is built at scale until it feels good and survives networking.
2. **Do not overengineer before requirements exist.** No custom deterministic simulation
   framework, no custom character controller, and no assembly-definition ceremony until a
   proven requirement forces them.
3. **Keep gameplay code modular and data-driven.** Tuning lives in data assets. Input,
   simulation and presentation stay separable — that is as far as early structure goes.
4. **Netcode is decided.** M0.5 chose Netcode for GameObjects; M2 must prove prediction,
   reconciliation and lag compensation on it (see `docs/M05_NETCODE_BAKEOFF.md`).
5. **Keep the architecture portable** to future mobile/console without optimizing the PC
   game around them.
6. **Placeholder assets are temporary.** Greybox geometry, capsule bodies, proxy props and
   untextured materials are development stand-ins only — never the intended final
   presentation. Production art begins after the core and networking milestones pass; see
   `TECHNICAL_PLAN.md` §13.

---

## 2. Milestones

### Status

| Milestone | Status |
|---|---|
| **M0** — local shared-body spike | Implemented; automated verification passing (12 EditMode, 2 PlayMode); single-tester spot-check confirmed the Model C coupling; the two-human feel playtest was intentionally deferred to the M1 gate |
| **M0.5** — netcode bake-off | **Complete — chose Netcode for GameObjects** with a custom prediction/lag-comp layer; see `docs/M05_NETCODE_BAKEOFF.md`. NfE isolated on branch `m0.5/nfe` |
| **M1** — local vertical slice | Implemented; automated verification passing (19 EditMode, 2 PlayMode); human two-duo playtest gate deferred; see `docs/M1_VERTICAL_SLICE.md` |
| **M2** — networked spike | In progress — 3-process run verified: role authorization (approval + wrong-role client rejected), bandwidth, prediction error, transport-level + app-level conditioning, sector + lag-comp validation, cadence/ammo, P2 boundary snap fixed (<1°); **remaining: runtime reconnect (server does not accept the reconnecting connection)**; see `docs/M2_NETWORKING_SPIKE.md` |
| **M3** — PvP round loop | Not started |
| **M4** — 2v2 and matchmaking | Not started |
| **M5** — product systems | Not started |
| **M6–M10** — production to release | Not started |

### M0 — Very small local shared-body mechanic spike

**Purpose:** answer exactly one question — *does the P1/P2 control relationship feel good?*

**Scope (deliberately tiny, one process):**

- Greybox floor plus a little cover; one body; two or three dummy targets.
- Placeholder body: capsule for P1 plus a simple arm/chest proxy and a muzzle anchor.
  No real rig, no skins.
- **P1:** movement plus explicit `BodyYaw` control.
  - **Superseded assumption:** M0 used a temporary simplification where P1 look input directly
    controlled `BodyYaw`. This was replaced in M1 by the decoupled look/body model (neck limit,
    smooth body follow, explicit align) — see the M1 section and `TECHNICAL_PLAN.md` §4.
- **P2:** independent first-person camera at the standardized shoulder anchor; world-stable
  yaw inside the firing sector (Aim Model C); free pitch.
- One hitscan weapon: fire plus fire rate. Reload/swap optional.
- Shared single HP pool on the body; P1 head proxy is the only headshot region.
- Dual input in one process (keyboard/mouse for one role, gamepad or second device or a
  scripted bot for the other).
- `SectorHalf` and basic weapon values in a ScriptableObject.

**Acceptance criteria:**

1. Two roles are playable simultaneously on one machine.
2. At least one target sits outside the sector until P1 rotates roughly 40°, and the test
   duo naturally uses P1 rotation to expose it.
3. While P2 holds a target, P1's body rotation does **not** drag the crosshair off target
   while the aim is inside the sector.
4. At the sector boundary, aim is pushed with the body, re-stabilizes in world space, and
   shows **no accumulated phantom mouse offset**.
5. The body does **not** rotate on its own toward P2's aim at any point.
6. Written playtest notes on: is it fun and readable; does P2 feel gated by P1 rather than
   like a passenger; does P1 feel meaningfully responsible for P2's damage. If time allows,
   a quick A/B against Aim Model A.
7. **Timebox:** roughly 1–2 weeks. This is a throwaway spike; do not harden it.

**Status:** Implementation and automated verification are complete (details in
`docs/M0_PLAYTEST.md`). Criteria 2–5 are covered by automated tests, criterion 1 was confirmed
by a single-tester spot-check, and criterion 6 — the two-human feel judgement — was
intentionally **deferred** and is **not** marked as passed. The definitive aim-model and
game-feel validation remains the M1 playtest gate.

**Assets:** the body is a placeholder capsule plus an arm proxy, and the arena is greybox. These
are temporary development assets, not the intended final presentation.

---

### M0.5 — Timeboxed NGO vs NfE networking bake-off

**Purpose:** choose the netcode stack with numbers, using only the minimum loop.

**Scope, per candidate (Netcode for GameObjects, Netcode for Entities):**

- Headless server plus one client; one body; P1 move and `BodyYaw`; P2 aim and fire;
  hitscan against a dummy; 60 Hz.
- Simulated 100 ms RTT and 2% packet loss.
- No art, no UI, no content, no polish.

**Measure / record:**

- Effort to bind **two role-tagged input domains to one entity**.
- Built-in prediction presence and quality for the P1-owned portion; how P2's own aim is
  handled.
- Lag-compensation story: what exists versus what must be written.
- CPU, memory and bandwidth per client.
- Animation/IK and skin-mounting implications.
- Estimated cost to reach a shippable competitive layer.

**Acceptance criteria:**

1. Both prototypes run under the simulated network conditions.
2. A written recommendation with the numbers above and a clear go/no-go.
3. **Timebox:** 3–5 days. If a stack cannot demonstrate the minimum loop inside the box,
   that is itself a finding.

**Outcome:** **Netcode for GameObjects** selected, with an explicitly budgeted custom
prediction/lag-compensation layer. Full record and rationale: `docs/M05_NETCODE_BAKEOFF.md`.
Netcode for Entities remains isolated on branch `m0.5/nfe` and is not merged.

---

### M1 — Proper local vertical slice on the chosen architecture

**Purpose:** the real local vertical slice, built on the architecture the bake-off selected.

**Scope:**

- **P1:** walk, unlimited sprint, jump, directional dodge (cooldown, no invincibility
  frames), slide, vault, light kick, heavy kick.
- **P1 look/body model:** camera/head look decoupled from `BodyYaw` within a neck-offset limit;
  the body smoothly follows the look past a threshold; an explicit "align body to look" action;
  WASD stays body-relative. All values are tuning; the align binding is temporary/configurable.
- **P2:** shoulder-anchored first-person camera; sector-clamped aim; rifle, pistol and
  knife; fire, reload and swap; hitscan spread and recoil; can fire during every P1
  movement or attack state (accuracy penalty only, never a hard lockout).
- Movement-state to accuracy table **[LOCKED direction]**.
- Shared HP, no passive regeneration; P1 head is the only critical region; P2 arms,
  shoulders and upper chest take normal damage; cosmetics never change hitboxes.
- Compact greybox arena: cover, readable sightlines, some verticality. Greybox and proxy
  assets remain temporary; final art is a later milestone.
- All tuning in data assets.

**Acceptance criteria:**

1. All role abilities above function, and none of P1's actions hard-lock P2's weapon.
2. A target placed outside the sector forces P1 rotation, and the dependency reads clearly.
3. Playtest gate (at least three duos, at least 15 minutes each): both roles report agency;
   no reported motion sickness; the aim model is chosen and **documented** with evidence.
4. The architecture demonstrably matches the chosen netcode path (for example: if NfE,
   gameplay entities are ECS-ready; if NGO, the simulation/presentation boundary is ready
   for a prediction layer).
5. Tuning values can change without recompiling.
6. The P1 look/body model is implemented and documented (decoupled look, neck limit, smooth
   follow, explicit align; movement and P2's sector use `BodyYaw` only).

**Outcome:** implemented and automatically verified. P1's look/body model is implemented
(decoupled look, neck limit, smooth follow, explicit align). The human two-duo playtest gate
(criterion 3) is **deferred and not passed** — comprehensive human playtesting moves to the
alpha/beta stage. Details and limitations: `docs/M1_VERTICAL_SLICE.md`.

---

### M2 — Full two-client and dedicated-server networking spike

**Purpose:** prove the networked shared body under latency, before content.

**Scope:** two remote clients plus one dedicated headless server; one body; both roles
remote; simulated latency and packet loss.

**Acceptance criteria:**

1. P1 locomotion is predicted and reconciled; no visible rubber-banding at 100 ms / 2%
   loss; residual visual error stays under a defined threshold.
2. P2 aim adds 0 ms of latency; camera correction stays under the defined snap threshold.
3. Firing during sprint, slide, dodge and kick is validated server-side.
4. Sector legality is checked against the **historical body orientation** at the fire tick,
   consistent with whichever aim model the M1 playtest selects.
5. Network correction does not introduce aim error beyond the defined threshold, and the
   boundary behavior matches the selected aim model across the wire.
6. Lag-compensated hit/miss agreement is at least 95% versus the offline baseline at
   100 ms; maximum rewind is clamped.
7. The server rejects out-of-sector fire, over-rate fire, excessive turn rate, impossible
   movement, and ammo/inventory tampering.
8. Reconnect within the grace window restores the same role; a disconnected role
   contributes no input.
9. Bandwidth per client and server CPU are measured at the target tick rate.

---

### M3 — Real PvP round loop

Duel bodies (one body versus one body, four humans), elimination, first-to-3 with a
maximum of five rounds, P2 loadout/buy draft, utility, closing zone, role queue.

**Acceptance criteria:** a complete competitive round is playable and server-authoritative,
with basic telemetry capturing the measurements listed in `GAME_CONCEPT.md` §31.

---

### M4 — 2v2 and matchmaking

Two bodies per team, derived body MMR, role-specific matchmaking, parties, dedicated
server allocation.

**Acceptance criteria:** ranked-ready structure with provider-neutral server allocation.

---

### M5 — Product systems

Account progression, cosmetics, mounting presentation, social/friends, reporting and
moderation, ranked presentation.

**Acceptance criteria:** the rig and skin contract is enforced before cosmetic content
scales; the shared-body identity is intact across all shipped combinations.

---

### M6 — Production art and content pipeline

Establish the DCC/content pipeline and final asset standards before mass production. The
pipeline tool is not chosen yet; Blender is a viable candidate among others.

- Choose the DCC/content pipeline and define import, scale, naming, LOD and material conventions.
- Finalize the standardized P1/P2 rig contract: gameplay skeleton, attachment sockets, camera
  anchors, weapon and utility anchors, and hitbox definitions.
- Produce the first production P1/P2 characters, weapons and environment kit tests.

**Acceptance criteria:** a production character and weapon travel the pipeline into the game,
and an arbitrary P1 skin combines with an arbitrary P2 skin without per-pair work.

---

### M7 — Audio, VFX, maps and content scale

- Audio, music and VFX production.
- The production map set for Duel and 2v2, following the map-family strategy.

**Acceptance criteria:** content volume supports a shippable match set at production quality.

---

### M8 — UI/UX, accessibility and optimization

- Final UI/UX, settings, onboarding, input polish and accessibility options.
- Client and server performance optimization against defined targets.

**Acceptance criteria:** defined frame-rate, memory and load targets are met, and an
accessibility checklist passes.

---

### M9 — QA, security, anti-cheat and release hardening

- Structured QA, regression and soak testing.
- Client anti-cheat selection and integration, plus server validation hardening.
- Backend, account and entitlement security review.

**Acceptance criteria:** the release-candidate stability and competitive-integrity bar is met.

---

### M10 — Steam integration, store and release preparation

- Steam authentication, friends, achievements, store, entitlements and community integration.
- Store page, age ratings, and legal/publishing requirements.
- Alpha, beta and release-readiness gates, launch operations and the live-service plan.

**Acceptance criteria:** a releasable PC/Steam build passes launch-readiness review.

---

## 3. Decision gates

| Gate | Milestone | Question | Evidence required |
|---|---|---|---|
| Aim coupling | M1 playtest | Model A, B or C | Playtest notes from at least three duos |
| P1 look/body tuning | M1 model resolved; numbers are TUNING | Neck limit, follow threshold/speed, align speed | Tuning playtest at alpha/beta |
| Netcode stack | M0.5 (decided) | NGO chosen | `docs/M05_NETCODE_BAKEOFF.md`; M2 confirms prediction/lag-comp |
| Simulation customisation | M2 | Is a custom character controller / deterministic sim actually required? | Bandwidth, prediction error, stack constraints |
| Disconnect policy | before M3/M4 | What happens to a body when one role disconnects mid-round? | Design decision consistent with concept §29 |

---

## 4. Deferred until the core and networking are proven

**[DEFERRED]** — then sequenced by §2 (production and release work is M6–M10).

Content and art, cosmetics, mounting presentation, economy and buy-phase tuning, utility
effects beyond stubs, closing zone, full round/match structure (until M3), 2v2 (until M4),
matchmaking and MMR, account progression, voice chat, friends and parties, ranked
presentation, anti-cheat provider, mobile and console ports, touch controls, cross-play
pool rules, input-matchmaking weights, backend persistence vendor, hosting vendor
selection, mid-round P1/P2 separation, battle royale and objective modes.

---

## 5. Open design questions — do not finalize silently

These are intentionally left open by `GAME_CONCEPT.md` and are carried forward:

- Which aim-coupling model becomes permanent (A, B or C).
- How P1 commands body yaw, and whether P1 retains a decoupled free-look camera.
- Exact aim-clamp semantics at the boundary, plus vertical aim limits.
- Mid-round disconnect behavior for a single role.
- P1/P2 information asymmetry from third-person versus first-person views.
- Whether P2 receives any non-control feedback channel from P1 beyond voice and pings.
