# Be My Arms — Development Roadmap

**Status:** Approved working roadmap
**Source of truth for game design:** `GAME_CONCEPT.md`
**Companion document:** `TECHNICAL_PLAN.md`
**Current production project:** `C:\Users\stapi\GameDev\be-my-arms`
**Engine:** Unity `6000.4.3f1`, URP
**Last updated:** 2026-09-19

> This roadmap supersedes the phase ordering implied by `GAME_CONCEPT.md` §34 where the
> two disagree. Design intent is unchanged; only the order and size of work is refined
> so the highest-risk identity-defining systems are proven before content.

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
4. **Netcode choice is provisional** until the bake-off and the networked spike produce
   numbers. Do not write netcode-bound code before then.
5. **Keep the architecture portable** to future mobile/console without optimizing the PC
   game around them.

---

## 2. Milestones

### M0 — Very small local shared-body mechanic spike

**Purpose:** answer exactly one question — *does the P1/P2 control relationship feel good?*

**Scope (deliberately tiny, one process):**

- Greybox floor plus a little cover; one body; two or three dummy targets.
- Placeholder body: capsule for P1 plus a simple arm/chest proxy and a muzzle anchor.
  No real rig, no skins.
- **P1:** movement plus explicit `BodyYaw` control.
  - **Temporary prototype assumption:** P1 look input directly controls `BodyYaw`, and the
    third-person camera follows that yaw. There is **no free-look yet**. This is a
    throwaway simplification for M0 only and is not a product decision. Whether P1 keeps
    a decoupled free-look camera is **[SPIKE]** and is revisited no later than M1.
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

---

### M1 — Proper local vertical slice on the chosen architecture

**Purpose:** the real Phase A slice, now built on the architecture the bake-off selected.

**Scope:**

- **P1:** walk, unlimited sprint, jump, directional dodge (cooldown, no invincibility
  frames), slide, vault, light kick, heavy kick.
- **P2:** shoulder-anchored first-person camera; sector-clamped aim; rifle, pistol and
  knife; fire, reload and swap; hitscan spread and recoil; can fire during every P1
  movement or attack state (accuracy penalty only, never a hard lockout).
- Movement-state to accuracy table **[LOCKED direction]**.
- Shared HP, no passive regeneration; P1 head is the only critical region; P2 arms,
  shoulders and upper chest take normal damage; cosmetics never change hitboxes.
- Compact greybox arena: cover, readable sightlines, some verticality.
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
6. The P1 free-look question is resolved and documented.

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
4. Sector legality is checked against the **historical `BodyYaw`** at the fire tick; P2's
   aim direction remains world-absolute.
5. Network correction causes no phantom aim offset, and the boundary push behaves as it did
   in M0 across the wire.
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

### M5 — Product layer

Account progression, cosmetics, mounting presentation, social/friends, reporting and
moderation, polished UI/UX, ranked presentation.

**Acceptance criteria:** the rig and skin contract is enforced before cosmetic content
scales; the shared-body identity is intact across all shipped combinations.

---

## 3. Decision gates

| Gate | Milestone | Question | Evidence required |
|---|---|---|---|
| Aim coupling | M1 playtest | Model A, B or C | Playtest notes from at least three duos |
| P1 camera | M1 | Does P1 keep free-look, or is `BodyYaw` always camera-locked? | Playtest notes |
| Netcode stack | M0.5, confirmed M2 | NfE or NGO | Bake-off numbers plus networked spike results |
| Simulation customisation | M2 | Is a custom character controller / deterministic sim actually required? | Bandwidth, prediction error, stack constraints |
| Disconnect policy | before M3/M4 | What happens to a body when one role disconnects mid-round? | Design decision consistent with concept §29 |

---

## 4. Deferred until the core and networking are proven

**[DEFERRED]**

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
