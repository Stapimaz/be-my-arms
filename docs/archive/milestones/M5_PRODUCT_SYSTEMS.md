# M5 — Product Systems and the Cosmetic/Rig Contract

**Status:** **Complete at the M5 acceptance level.** Product-system foundations (account/profile,
role-specific ranked state and presentation, cosmetic ownership/equip, mounting presentation,
social/friends, reporting/moderation) are implemented against clean interfaces with local
in-memory implementations, and the **P1/P2 cosmetic and rig contract is enforced and proven**
across multiple placeholder variants. Production content and backend/vendor integration are
explicitly out of scope and deferred.
**Module:** `Assets/Scripts/M5` (`BeMyArms.M5`).

---

## 1. Milestone boundary (M5 vs M6)

- **M5 (this milestone) establishes product foundations and enforces the asset contract.** It
  proves the architecture with interfaces and local implementations and produces **no production
  art**.
- **M6 begins the production DCC/art pipeline and real asset creation**: choosing the DCC tool,
  import/scale/LOD/material conventions, and producing the first production characters, weapons and
  environment kit. No DCC tool is installed or configured here.

## 2. Product-system foundations (pure, engine-free)

- `M5Account` / `M5Progression` / `IM5AccountStore` / `M5InMemoryAccountStore` — platform-neutral
  account identity (never a Steam id, concept §22.3/§27) and a placeholder account-level XP curve
  behind a storage interface. `IM5IdentityProvider` / `M5LocalIdentityProvider` is the identity seam.
- `M5RankTable` / `M5RankedProfile` / `M5RankInfo` — **role-specific ranked state and
  presentation**. P1 and P2 are rated and presented independently (concept §18.2); a match updates
  only the played role, reusing the M4 role-rating update. Tier names/thresholds are placeholder
  presentation data, not a locked ladder.
- `M5CosmeticItem` / `IM5CosmeticCatalog` / `M5InMemoryCatalog` / `M5CosmeticInventory` /
  `M5Loadout` — cosmetic catalog, ownership and equip. Equip requires ownership and a valid
  kind-to-slot mapping; the **P1 slot only accepts a P1 skin and the P2 slot only accepts a P2
  skin**, so the two cosmetic identities cannot collapse into one.
- `IM5SocialProvider` / `M5InMemorySocialProvider` — friends (request/accept/remove) and
  blocking, provider-neutral.
- `IM5ModerationProvider` / `M5InMemoryModerationProvider` — report submit/list/resolve with
  categories, provider-neutral.

## 3. The P1/P2 cosmetic and rig contract (the acceptance proof)

`M5RigContract.Default()` specifies the standardized shared body (concept §5.1, `TECHNICAL_PLAN`
§5):

- **Gameplay skeleton:** `Hips → Chest → Neck → Head`.
- **Attachment/camera/weapon/utility anchors:** `ShoulderAnchor`, `P2CameraAnchor`,
  `P1CameraAnchor`, `WeaponAnchor`, `UtilityAnchor`, plus the cosmetic mount sockets
  `Cosmetic_P1` and `Cosmetic_P2`.
- **Authoritative hitboxes:** `Hitbox_Head` (critical) and `Hitbox_Body`; cosmetics must never add
  or move a hitbox.
- **Animation interface:** every rig must expose an `IM5RigAnimation`; skins drive the interface but
  never replace it.
- **Gameplay stats marker:** `M5GameplayRigStats` lives on the authoritative rig only.

`M5RigValidator` enforces the contract: required sockets with the expected parent chain, the exact
hitbox set with correct regions, the animation interface, the stats marker, and that cosmetic
layers carry **no hitboxes and no gameplay-stat components**. `M5MountAssembler` combines an
arbitrary P1 skin and an arbitrary P2 skin at the fixed cosmetic sockets — no pair-specific work —
and validates the result.

`M5PlaceholderRigFactory` produces **three P1 variants and three P2 variants** of greybox skins
(shape/offset/scale only). These are deliberately not production art; they exist to prove that any
valid P1 skin combines with any valid P2 skin.

## 4. Verification

**EditMode 89/89** (5 product-system tests + 6 rig-contract tests; 76 pre-existing) and
**PlayMode 3/3** (2 pre-existing M0 smoke tests + 1 runtime mount proof).

The rig proof covers:

| Check | Result |
|---|---|
| Contract validates the placeholder rig | `M5RigContract.Default()` passes on a factory rig |
| **Any P1 skin × any P2 skin** | all 3 × 3 = 9 combinations assemble and validate, no pair-specific work |
| Cosmetics never move authoritative hitboxes | hitbox signature (name/region/local transform) is identical before and after every combination |
| Cosmetics never change gameplay stats | `M5GameplayRigStats` signature identical across every combination |
| Cosmetic layers are presentation-only | mounted skin subtrees contain zero `HitboxRegion` and zero gameplay-stat components |
| Independent identities | the assembled body keeps distinct P1 and P2 skin ids/roles; disassembly leaves the contract valid |
| Cosmetics cannot cheat the contract | a skin carrying a hitbox **fails**, a skin carrying gameplay stats **fails**, a missing cosmetic socket **fails**, and a P2 skin in the P1 socket **fails** |
| Runtime | `M5MountDemo` validates the full matrix in a live player loop (`9/9 combinations valid`) |

## 5. Deliberately unresolved product decisions

M5 establishes seams, not commitments. The following are intentionally **not** finalized here:
store structure, monetization/pricing, battle-pass-like progression, detailed progression economy,
backend/persistence vendor, social/voice provider, moderation provider, and the exact rank
tier/division names. Their interfaces (`IM5AccountStore`, `IM5IdentityProvider`,
`IM5SocialProvider`, `IM5ModerationProvider`, `IM5CosmeticCatalog`) are the integration points.

## 6. M6 handoff

The contract in §3 is the specification M6's pipeline must satisfy. M6 should: choose the DCC and
define import/scale/naming/LOD/material conventions, author real P1/P2 characters against
`M5RigContract.Default()` (or an explicitly versioned successor), and run `M5RigValidator` +
`M5MountAssembler` as the acceptance check for every new skin. No production asset creation or DCC
installation happens in M5.
