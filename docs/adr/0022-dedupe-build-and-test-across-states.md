# 22. De-duplicate build-and-test across states in state-ci.yaml

Date: 2026-09-08

## Status

Accepted. Narrows the per-state matrix decision in [ADR 0004](0004-state-based-ci-architecture.md) for one specific job (`build-and-test`). This ADR documents what changed and why.

## Context

`build-and-test` in `state-ci.yaml` matrixed over every state config in `.github/config/states/` (`dc`, `co`), producing two parallel jobs (`dc - Build & Test`, `co - Build & Test`) on every PR and push to `main`. Both legs loaded their respective state config, then called the same backend/frontend build and test scripts. Neither leg ever received `STATE`/`NEXT_PUBLIC_STATE`, and `dc.yaml`/`co.yaml`'s build-relevant fields (`infrastructure.use_docker`, `versions.*`, `build.configuration`, `build.frontend_flags`, `build.backend_flags`, `build.skip_tests`) were byte-identical.

Verified empirically from recent `state-ci` run's full log (run `33900109800`) and confirmed both legs reported identical backend test counts (42 / 336 / 1857 tests, 0 errors) and matching frontend/Vitest durations — the same tests with the same results, just reordered by parallel execution. A repo-wide search confirmed `.github/config/states/*.yaml` had exactly one consumer anywhere in the codebase — this one workflow step — and that the `environment.API_BASE_URL`/`environment.features.*` fields that *do* differ between `dc.yaml`/`co.yaml` were never read by CI or by any app code. So every PR and push paid for the full backend + frontend test suite twice, for zero additional coverage.

Playwright E2E (`playwright-e2e.yaml`) and the enrollment-checker job are unaffected — they're genuinely state-aware (they set `STATE`/`NEXT_PUBLIC_STATE`; DC-727 stack PR #642 made the enrollment checker's build vary by state) and continue to matrix/build per state.

Removing `build-and-test`'s matrix left `discover-states` (the job resolving `deploy/{state}` branches and the `workflow_dispatch` `state` input into a matrix) with no remaining consumer in `state-ci.yaml`. No `deploy/dc-*`/`deploy/co-*` branches existed in the repo at the time of this change, and `discover-states` only ever fed this one job. That in turn left `.github/config/states/co.yaml` and `_template.yaml` unread by anything in CI — only `discover-states` ever looked at state config *filenames*, and only the matrixed `build-and-test` ever read a given file's *contents*. A separate, already-stale duplicate of `.github/config/states/` also existed at the repo root (`config/states/`), unreferenced by any workflow, script, or app code, and already drifted out of sync (a different pinned .NET SDK version) — a preexisting orphan unrelated to this change, cleaned up alongside it.

## Decision

- Collapse `build-and-test` to a single, non-matrixed job named `Build & Test`. It still gates on `needs.changes.outputs.backend`/`frontend` per step (unchanged), still has no job-level `if:` (a required status check must always post — this is the exact bug PR #611 fixed for the previous two-check-name setup).
- Remove `discover-states`, the `workflow_dispatch.inputs.state` input, and the `ci:test:state:dc`/`ci:test:state:co` ACT scripts in `package.json` as dead code — no remaining consumers, and no branches in active use depended on the `deploy/{state}` fast-path they implemented.
- Delete the orphaned top-level `config/states/` directory.
- Delete `.github/config/states/co.yaml` (unread by anything once the matrix is gone). Rename `.github/config/states/dc.yaml` → `.github/config/states/state-config.yaml`, kept as the one shared build/test reference `build-and-test` loads. Keep `.github/config/states/_template.yaml` as schema documentation for a state config.
- Coverage artifacts (`build-artifacts`, `backend-coverage`, `frontend-coverage`) drop their `${{ matrix.state }}-` prefix; `coverage-comment` downloads them by exact name instead of by glob pattern across matrix legs.

## Consequences

- **Branch protection must be updated manually** (not part of this change — GitHub required status checks aren't repo config): the required checks `dc - Build & Test` and `co - Build & Test` need to become the single `Build & Test`.
- CI time for `build-and-test` roughly halves on every PR and push — one backend + frontend build/test run instead of two identical ones.
- The PR coverage comment (`post-coverage-comment.js`) now sums exactly one backend and one frontend report instead of two near-duplicate ones; the summation logic itself didn't need to change (it was already count-agnostic), only its comments.
- `deploy/dc-*`/`deploy/co-*` branch names no longer have any effect on CI — nothing in the repo honored that pattern except the removed `discover-states` job, and no such branches were in use.
- If a state's build ever needs to genuinely diverge (different Docker/native choice, different tool versions, different build flags), reinstate a per-state matrix in `build-and-test` specifically, following the pattern `discover-states` used (recoverable from git history) rather than adding conditionals to `state-config.yaml`.

## References

- `.github/workflows/state-ci.yaml` — the collapsed `build-and-test` job.
- `.github/workflows/scripts/post-coverage-comment.js` — updated comments reflecting single-report coverage.
- `.github/config/states/state-config.yaml`, `_template.yaml` — the remaining state config files.
- `docs/development/state-ci.md` — updated to describe the single-run job.
- [ADR 0004](0004-state-based-ci-architecture.md) — the per-state matrix architecture this narrows for one job; left unedited as the historical record.
