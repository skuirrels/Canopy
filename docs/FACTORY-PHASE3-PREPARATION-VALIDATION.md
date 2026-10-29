# Phase 3 preparation validation

Validated on 29 October 2026 using temporary native DuckDB databases, Git
repositories and executable fixtures. No provider API was contacted and no live
task was edited by the regression suites.

| Check | Result |
| --- | --- |
| Client tests | 93 passed |
| Production frontend build | Passed; existing chunk-size advisory remains |
| Debug and Release backend builds | Passed, zero compiler warnings/errors |
| Run preparation API/DuckDB | 35 passed |
| Existing factory configuration regression | 50 passed |
| Existing standalone agent regression | 29 passed with protocol fixtures |
| Maintained Factory Postman regression | 15 requests / 25 assertions passed locally |
| Preparation browser workflows | 13 passed |
| Browser restart/history | 4 passed |
| Accessibility | No WCAG A/AA violations in the tested task Factory controls |
| Nonempty database upgrade | Existing 2 projects / 31 tasks preserved exactly on a private backup copy |

The preparation suite covers missing attachments/repositories/specifications,
provider defaults and explicit empty overrides, executable permissions, missing
commands/environment references, invalid model settings, skill mapping/hashes,
symlink rejection, local-origin restrictions, concurrent idempotent retries,
stale revisions, immutable snapshots after task/skill/Git changes, restart
persistence and task/project cleanup. Neither the fixture agent nor configured
verification command is invoked during preparation or startup.

Real Chromium exercises readiness and explicit preparation, stage/step display,
stale readiness after a task edit, lost-response recovery without duplicate
records, unapplied-override guards, reloading history and phone viewport access.
The full work order and stage/step records match exactly after backend restart.
Viewport emulation is not physical-device validation.

Review corrected the prepared-snapshot landmark and attachment-label contrast,
made stale/lost-response errors actionable, kept a known successful preparation
distinct from a failed history refresh, handled a disappearing executable, and
returned invalid agent settings as structured readiness diagnostics. No blocking
review findings remain for this slice.

The additive migration uses the public EFCoreProvider path. Existing contract
operations and maintained examples are preserved; new OpenAPI operations and
their generated Postman artifacts are saved locally. No Postman workspace was
published. Prepared records do not dispatch work, change task status, imply
verified credentials/model access, or certify command/test success. Execution
and the run-progress graph remain subsequent slices.
