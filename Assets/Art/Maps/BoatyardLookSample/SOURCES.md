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
