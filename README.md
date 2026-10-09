# Be My Arms

Two players, one fighter: **P1 controls body/movement; P2 controls arms/weapons/aim**.
Unity 6 / URP, NGO + Unity Transport, server-authoritative shared-body multiplayer.

**Current state:** first **Boatyard Duel blockout**, ready for layout playtest: workshop/loading
platform versus lower quay, a central repair yard and a low maintenance passage. Starting sides
alternate each round; team/role ownership stays fixed. Visual direction, including characters,
is deferred. Accepted controls, P2 POV, bots and motion-dependent rifle accuracy remain unchanged.

## Start here

1. [GAME_CONCEPT.md](GAME_CONCEPT.md) — current product and design intent.
2. [ROADMAP.md](ROADMAP.md) — current checkpoint and future phases.
3. [TECHNICAL_PLAN.md](TECHNICAL_PLAN.md) — architecture that actually exists.
4. [Development / run / build](docs/DEV_ENVIRONMENT.md) — practical setup and commands.
5. [Repository structure](docs/REPOSITORY_STRUCTURE.md) — domain map and migration exceptions.
6. [Current playtest](docs/PLAYTEST.md) — what changed and what to evaluate.

Build entry: `tools/build/build-player.cs`. Output: `Builds/Windows/BeMyArms.exe`.
Play: **PLAY → Duel → P1/P2 → Start Match**, with a bot teammate for solo testing.

`Assets/Scripts` and tests use domains; `Assets/Scenes` contains the four playable scenes
and a separate Samples folder. Current shared-rig character assets live under
`Assets/Art/Characters/SharedRig`. History and investigative tooling are in
[docs/archive](docs/archive/README.md) and `tools/archive`, not the current source of truth.

Use Git LFS. Preserve `.meta` GUIDs, role ownership and the embedded Transport patch. Build
matching client/server binaries. Automated checks do not accept visual readability or game feel.
