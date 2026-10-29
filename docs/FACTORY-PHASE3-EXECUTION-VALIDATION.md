# Phase 3 — dispatch and execution validation

Validated on 29 October 2026 against an isolated native DuckDB database and
throwaway Git repository. No live project/task data was created or changed.

| Check | Result |
| --- | --- |
| Debug and Release builds | Passed, no compiler warnings/errors |
| Frontend production build | Passed; existing bundle-size advisory remains |
| Client regressions | 93 passed |
| Factory configuration regression | 50 passed |
| Work-order preparation regression | 35 passed |
| Standalone agent regression | 29 passed |
| Factory execution/process regression | 36 passed |
| Maintained local Factory Postman collection | 18 requests / 28 assertions passed |
| Browser execution workflow | 14 checks passed, desktop and phone viewports |
| Tested task factory accessibility | No axe WCAG A/AA violations |
| Upgrade from the preparation schema | Persisted snapshots, identities, standalone history and workspace preserved |

The process suite invokes real Git, creates detached worktrees, executes real
commands, checks exit codes/environment references and kills actual fixture
processes. Codex/Claude responses are explicit CLI protocol fixtures; these checks
do not certify credentials, paid model access, model behaviour, or provider
sandbox enforcement. CLI arguments for both permission paths are verified.

Coverage includes concurrent idempotent start, both human gates, required explicit
approval, replay protection, frozen commit/model/settings/skill content, real
command failures and timeouts, downstream blocking, evidence hashes, task-state
independence, process-tree cancellation, final agent diff retention and shutdown.
A crash while Git diff cleanup is deliberately paused recovers both stopping
records as interrupted and never replays the child. Factory child sessions cannot
be resumed through standalone agent dispatch.

Browser checks include cancelling the start confirmation without dispatch,
recovering a lost start response without duplicate work, polling actual state,
showing the last saved state during an outage, explicit human decisions, command
results, logical evidence and stopping a real command from the phone layout.

The native upgrade uses a database populated by the previous preparation-only
backend, then opens it with the new backend. DuckDB's constrained-column ALTER
limitation is handled with the existing transactional table-rebuild pattern;
every old column and index is retained. The public EFCoreProvider dependency and
single-process deployment remain unchanged.

Schema-1 artifacts are hashes of logical step evidence, not proof that a physical
file exists. Completed agent steps record actual reports and diffs, without
asserting semantic correctness or automatically accepting/changing a task.
Postman contracts/examples remain local Git artifacts; no shared workspace push
was performed. The React Flow run workspace is delivered in the following slice.
