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

Both are **CC0 1.0 Universal**, including commercial use and modification without mandatory
attribution. License: https://docs.ambientcg.com/license/ and
https://creativecommons.org/publicdomain/zero/1.0/ . Courtesy credit: ambientCG / Lennart Demes.

Downloaded ZIP SHA-256 (archives stay outside the repository):
- Concrete030: `9101871625BDE3CCF3C57CA7AF4372196D9CA9F678C786B8DE698A576784D403`
- Plaster001: `944B4831016E42ACE4A89422E4E7190912CA2F7FED6F561354C48EC7BB54D3A4`

Included per source: Color, NormalGL and Roughness at 1K. NormalGL is imported as a Unity
normal map; roughness is linear. Derived `Concrete_Albedo` / `Plaster_Albedo` retain low-contrast
surface variation while shifting to the sample palette. Derived `*_MetalSmooth` stores zero
metallic in red and inverse, restrained roughness in alpha for URP Lit. We do not put a
roughness texture directly into a Unity smoothness slot, or bake shadows into the albedo.
Texture repetition is 2 m on the sample meshes, with metric UVs and mild normal strength.

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
  Existing PC SSAO is retained (intensity .4, radius .3); no extra AO layer darkens the bake.
  Neutral tonemapping, light grading, no cinematic blur/bloom/DOF/vignette. The separate P2
  viewmodel is not newly graded; POV, FOV and immediate aim are untouched.

`BoatyardLookSample.Author()` must run through the connected Editor and refuses reruns.
The scene should be opened alone for the bake, so another map's lighting is never overwritten.
Future art edits are normal authored edits, not regeneration through this helper.
