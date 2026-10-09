# Boatyard look sample — sources and scope

This is a small environment look-development sample, **not final art direction**. The user
accepted the bent coastal layout (`e551b23`) and authorized a bright, readable, lightly stylized
realism experiment at the workshop entrance, loading ramp and adjacent yard. Characters,
accepted collision envelopes, spawns, controls and combat are unchanged.

## Third-party textures

Downloaded 2026-10-09 from the official ambientCG download endpoints. The source JPG files
are retained with their original names. No pack/plugin/subscription purchase was required.

| Asset | Official source | Download |
|---|---|---|
| Concrete030 | https://ambientcg.com/a/Concrete030 | https://ambientcg.com/get?file=Concrete030_1K-JPG.zip |
| Plaster001 | https://ambientcg.com/a/Plaster001 | https://ambientcg.com/get?file=Plaster001_1K-JPG.zip |

Follow-up after human feedback on `641063b`: 2K maps downloaded from the same official source,
including dedicated coated/bare-metal surface sets rather than plain color materials:

| Asset | Official source | Download |
|---|---|---|
| Concrete030 | https://ambientcg.com/a/Concrete030 | https://ambientcg.com/get?file=Concrete030_2K-JPG.zip |
| Plaster001 | https://ambientcg.com/a/Plaster001 | https://ambientcg.com/get?file=Plaster001_2K-JPG.zip |
| Metal027 (powder-coated steel) | https://ambientcg.com/a/Metal027 | https://ambientcg.com/get?file=Metal027_2K-JPG.zip |
| Metal032 (bare metal) | https://ambientcg.com/a/Metal032 | https://ambientcg.com/get?file=Metal032_2K-JPG.zip |

Both are **CC0 1.0 Universal**, including commercial use and modification without mandatory
attribution. License: https://docs.ambientcg.com/license/ and
https://creativecommons.org/publicdomain/zero/1.0/ . Courtesy credit: ambientCG / Lennart Demes.

Downloaded ZIP SHA-256 (archives stay outside the repository):
- Concrete030: `9101871625BDE3CCF3C57CA7AF4372196D9CA9F678C786B8DE698A576784D403`
- Plaster001: `944B4831016E42ACE4A89422E4E7190912CA2F7FED6F561354C48EC7BB54D3A4`

2K archive SHA-256:
- Concrete030: `CF129C209F714650E7AD6A9DCF34B1BC31E06192DB1CCF3470B754C989A3993C`
- Plaster001: `9894765632CFA0FEB3349B581825A9C224DEE9EFA1CEBB8ABA63BD1C7BC9FC3C`
- Metal027: `17A17E2E83342E9280D06671C3136C9869FE112E31A7C9DEE3E3A05ED3759544`
- Metal032: `4B8884843C490963D5734BE036C35639D064E7A817EB61145322F69C6C19895A`

Included per source: Color, NormalGL and Roughness. Initial 1K sources are retained for provenance;
current materials use 2K sources/derived maps. NormalGL is imported as a Unity normal map;
roughness is linear. Palette-adjusted albedos now preserve relative measured surface variation,
rather than the initial barely visible additive contrast. Concrete/plaster have stronger
normal response; teal/ochre coating and bare metal have their own matched PBR map sets.
Derived `*_MetalSmooth` stores metallic in red (0 for plaster/concrete/coating, 1 for bare steel)
and inverse source roughness in alpha; material smoothness adjusts its overall response without
clamping away the source's roughness variation. We do not put a
roughness texture directly into a Unity smoothness slot, or bake shadows into the albedo.
Metric UVs repeat concrete at 2 m, plaster at about 1.33 m, coating at .5 m and bare metal at 1 m.
Thin paint/drawer/trim pieces now have metric UVs too, rather than stretched cube coordinates.
Source and derived maps use mipmaps, trilinear/anisotropic filtering and high-quality compression;
color is sRGB, normal and packed material data are linear, with no retained runtime CPU copies.
No displacement/parallax, fake painted-on lighting, or gameplay geometry changes are introduced.

## Project-authored content

- Flat-faced ramp visual meshes with texture UVs/tangents and separate lightmap UVs. Original
  wedge positions, triangles/volume and numeric slope collision remain equivalent.
- Small inward edge bevels on sample architecture/machinery, retaining original AABBs and
  BoxColliders. Remaining geometry receives UV2 for GI context only; its blockout look remains.
- Workshop lower paint, doorway edging, loading safety stripe, bench drawer/hardware and
  carriage vents. Surface detail sits under separate `LookSample`, never in `Arena` collision.
  No independent colliders, pretend new cover, route changes or purchased/AI-generated props.
- Teal/ochre paint, steel/rubber, contextual sea color and procedural sky use standard Unity
  shaders. Sky, sun and grading necessarily affect the whole map; detailed art stays local.
- Mixed sun, baked indirect light, sparse legacy light probes and two baked reflection probes.
  The follow-up preserves exterior sun/sky/grading but adds three downward baked area task lights
  with small ceiling fixtures in the workshop. Direct room light and its bounce are baked into
  room surfaces/body probes; this is not a global ambient/exposure boost. The oversized initial
  `WorkshopServiceSign` was explicitly rejected and removed. Four bounces and denser workshop
  charts replace the initial insufficient room illumination.
  Existing PC SSAO is retained (intensity .4, radius .3); no extra AO layer darkens the bake.
  Neutral tonemapping, light grading, no cinematic blur/bloom/DOF/vignette. The separate P2
  viewmodel is not newly graded; POV, FOV and immediate aim are untouched.

`BoatyardLookSample.Author()` must run through the connected Editor and refuses reruns.
The scene should be opened alone for the bake, so another map's lighting is never overwritten.
Future art edits are normal authored edits, not regeneration through this helper.
`tools/maps/refine-boatyard-surfaces.cs` is the guarded one-time follow-up: it updates only the
known sample materials/detail UVs, removes only its rejected sign and adds local room fixtures.
It verifies identical shared collision and exterior sun before saving; do not rerun it over later edits.

## Continuity / remaining environment follow-up

Human review of `6325193` found the sample acceptable to continue, but flagged obvious concrete
repetition and separate-block-looking joins. The user explicitly authorized finishing the remaining
environment pieces, with more attention to natural continuity. Characters remain deferred.

No paid asset or third-party pack was added. `make-boatyard-concrete-variation.py` derives a
deterministic **4K / 12 m-repeat** surface from the retained CC0 Concrete030. Sixteen phase offsets
with curved feathered overlap are shared across color, GL normals and roughness; no rotations or
mirrors that would invalidate normal directions, procedural color noise, or painted-on lighting.
URP's normal import/sample normalizes the blended normal response. Albedo stain contrast is reduced
from the rejected repetitive sample, while aggregate/pores remain. Packed smoothness is still inverse
linear roughness. These are variation composites, **not newly photographed 4K source detail**.

`BoatyardContinuityPass.Author` is another guarded one-time live-Editor operation, not regeneration.
All decks and four ramps share world-XZ UVs at 1/12 scale and the same concrete material; contact
edges on construction boxes no longer have independent inset bevels. Walls use common world-plane
UVs. Existing collider components and all numeric collision entries/bounds were preserved/asserted.

Remaining quay/apron/maintenance pieces, buildings, cliff faces, railing, rack, winch, pipe housing
and dry-dock housing now use the recorded PBR sets/palette. Added project-authored surface details
live under collider-free `EnvironmentDetails`: continuous fascia/plinths, roof standing seams,
closed window/door panels, downpipes, rubber dock bumpers, equipment service doors/latches/vents,
base bolts, pipe straps and mould ribs. Closed panels are not gameplay openings. Equipment remains
solid cover, including the dry-dock mould/cradle; it is not a hollow hull with misleading bullet gaps.
Backdrop-only hills and moored workboat receive non-box authored silhouettes and remain nonplayable.
Tinted material variants reuse the recorded maps; no displacement or new runtime environment system.

Exterior sun/sky/grading and the three baked workshop area lights are unchanged. Lighting/reflections
are rebaked after the surface/mesh changes. This is an expanded reviewable art pass, not a claim that
every environment object is final production art or that automated checks establish naturalness.

The final preview identified cliff faces reading like more concrete walls. Coastal rock now uses
**ambientCG Rock058, CC0**, downloaded 2026-10-09 from
https://ambientcg.com/get?file=Rock058_2K-JPG.zip (source https://ambientcg.com/a/Rock058;
license https://docs.ambientcg.com/license/). Retained Color/NormalGL/Roughness maps are 2K; the
derived mask stores 0 metallic and inverse linear roughness. Archive SHA-256:
`4B6DC8C6A5314957C656785C3A8FF95D721399FAB31C9B1B13D90DA8683E2F58`.
`tools/maps/finish-boatyard-rock.cs` replaces only that iteration's rock material/maps/UV scale,
and adds four project-authored **unreachable backdrop** crest silhouettes above the existing
cliff volumes to break the box skylines. It asserts unchanged shared collision. The final bake
was refreshed for this concrete visual correction, before the single delivery build.
