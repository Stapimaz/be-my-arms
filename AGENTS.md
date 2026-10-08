# Be My Arms — persistent agent guidance

## Small iterations, proportional verification

These are user-confirmed working rules, recorded after the 2026-10-09 dynamic-crosshair
iteration took disproportionately long. Apply them in future sessions, not just this one.

**Deliver the requested player-facing improvement, not the largest implementation or the
largest collection of green checks.** Tests support development; they are not the objective.

### Keep scope narrow

- Start with the smallest coherent solution. Reuse existing state, helpers and patterns.
- Do not turn a small HUD/feel request into a networking redesign, new prediction framework,
  generalized architecture, unrelated refactor or additional product features by default.
- If correctness really requires broader changes, identify the concrete reason and explain
  the tradeoff before expanding scope. Ask when it is a genuine product/scope decision.
- Stop at the agreed human-review point. Do not silently start the next roadmap item.

### Verify the changed risk, once

Choose the verification scope before running it. Every expensive check should answer a
specific question about this change; existing test-suite size is not a mandate to run it.

| Change | Default verification |
|---|---|
| Documentation / agent instructions only | Review the diff and `git diff --check`; no Unity compilation, tests or build. |
| Small HUD / crosshair / presentation | Compilation, relevant focused tests, and one representative runtime check if needed. |
| Spread / hit / damage / simulation calculation | Relevant calculation tests and one focused combat/simulation integration check. |
| Ownership / reconnect / protocol / shared networking foundation | Broader relevant regression suite, including lifecycle checks when affected. |

- Do not run the full EditMode suite for every small change. Use it when the changed risk
  crosses boundaries or focused coverage is insufficient; explain why.
- Aim for one final playable build when a build is part of delivery. Verify imports and
  compilation first, then build. Do not repeatedly rebuild unchanged code for reassurance.
- After a small late fix, rerun only the checks that fix invalidates. Do not restart the
  entire QA sequence automatically. Rebuild only if the delivered binary needs updating.
- Preserve useful existing tests. Reducing unnecessary executions is not permission to
  delete tests, weaken assertions or ignore a real failure.
- Compilation/build acknowledgements are not final verdicts. Refresh source imports when
  needed, wait for compilation to finish, and inspect the actual result. Avoid races and
  repeated runs caused by misreading command output.
- Run runtime QA scripts sequentially: they share build endpoint discovery. Use their
  disposable fixtures only, never a human playtest session.

### Prioritize feedback over ceremony

- No obligatory screenshots or automated visual/game-feel acceptance. Human review decides
  readability, feel, difficulty and balance; technical checks do not establish these.
- Keep updates brief: meaningful findings, tradeoffs or blockers only. Report the outcome,
  relevant verification and any remaining limitation without burying the feature in QA logs.
- If a simple task is growing into a long tooling/QA exercise, reassess scope and repeated
  work instead of continuing mechanically. Fix the agent workflow before blaming test count.

## Project continuity

- Canonical context: `GAME_CONCEPT.md`, `ROADMAP.md`, `TECHNICAL_PLAN.md`; practical commands
  and current review questions: `docs/DEV_ENVIRONMENT.md`, `docs/PLAYTEST.md`.
- Preserve accepted human controls, P2 POV/immediate aim, Elastic Soft, prediction/authority,
  historical firing legality, control epochs and session/reconnect behavior unless explicitly
  changing them. Respect Core/Gameplay/Networking/Match/Matchmaking/Product/Content/Client boundaries.
- Use a connected Unity Editor for scene/prefab/asset authoring, not raw YAML edits.
- Never overwrite unrelated user work or discard useful checkpoints. Commit/push completed
  iterations when authorized by the ongoing workflow, then stop for human playtest/discussion.
