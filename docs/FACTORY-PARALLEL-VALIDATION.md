# Parallel execution validation

The implementation uses public HTTP endpoints, native DuckDB through the pinned
public EFCoreProvider, real Git worktrees and real overlapping OS processes.
Provider responses in automated parallel tests are explicit Codex/Claude protocol
fixtures; they establish process/workspace behavior, not live-model quality or cost.

## Local results (31 October 2026)

- Parallel native/API/process checks: **37 passed**; saved Postman assertions:
  **9 passed** against the real isolated API.
- Existing serial execution: **36 passed**; independent review: **28 passed**.
- Execution-policy repair: **16 passed** through the serial runner and **16 passed**
  through the parallel runner.
- Native prior-schema migration/restart passed. Client tests: **99 passed**;
  client/backend builds passed. OpenAPI syntax and collection lint passed.

## Reproducible checks

- `dotnet build Planner.sln --configuration Debug`
- `dotnet run --project tests/Planner.ParallelMigrationRegression`
- `python3 scripts/factory-parallel-regression.py --postman`
- `python3 scripts/factory-repair-regression.py --execution-policy --parallel`
- Existing serial execution, independent review and execution-policy repair
  regressions, plus `npm test` and `npm run build` in `client/`.
- `python3 scripts/ci-native.py --suite parallel` runs the parallel suite and saved
  Postman assertions as a separate protected native matrix group. The existing
  core/provider/deployment groups and required aggregate stay enforced.

The parallel regression proves no dispatch during policy/save/readiness/preparation;
actual overlapping commands and Codex/Claude fixture children; sibling isolation;
ordered dependency/transitive visibility; unique ancestor application; real
nonoverlapping same-file merges; content and full-mode conflicts without a winner;
binary bytes, renames, deletions and executable/full permissions; retained stage
commits, staged changes and portable split-index checkpoints; final initial-index
normalization; exact final QA/review context; multiple approvals and decisions
while siblings run; gate/root edit rejection; private-ref integrity; declined-gate
command descendant cleanup; cancellation, revocation and abrupt-death process
ownership; elapsed waits, host capacity, index/patch bounds and legacy serial
response compatibility. Native restart preserves terminal records and interrupts
unfinished work without replay. Test projects clean up through public APIs.

The native upgrade regression migrates a prior database containing project/task,
serial policy bytes/hash, captured work order, stage and manual gate. It verifies
byte-equivalent historical data, empty new tables and editable serial records
across restart. The phase adds two native tables only.

## Browser interaction

A temporary native database supplied two real stage gates, ordered commands and a
dependency join. The running interface selected the second gate, approved it,
approved the remaining stage and observed the successful join. React Flow and the
step list showed actual stage/step states. The inspector exposed the retained
workspace, actual input/output trees, final integration and step checkpoints.
Saving the unchanged concurrency policy left one completed run and dispatched no
new work. The policy showed configured backend capacity and the opt-in setting.

Desktop and narrow browser screenshots were visually inspected. A 390×844 browser
viewport at the existing 90% zoom measured 433 CSS pixels with equal document
width and no horizontal overflow; the stage inspector remained navigable and
wrapped long workspace paths. This is browser coverage, not physical-device proof.

## Review corrections

Review restored logical artifact validation before manual gates in the extracted
shared executor, made approvals available while sibling stages run, added the
stage picker/identity, expanded retained split indexes, checked input and output
refs, bounded expanded snapshot indexes and rechecked root integrity after the
native integration intent. Content conflicts compose in a separate worktree so a
failed join does not partially modify the actual QA/delivery root. Child shutdown
settles before terminal parent state. Completed branch evidence remains inspectable
when the parent join fails.

Existing QA verdict, independent review, repair and controlled delivery authority
remain explicit and bind the actual final context. No automatic QA approval,
production feed, task acceptance, PR publication or model selection is inferred.
