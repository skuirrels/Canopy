# Acceptance criteria and evidence-based QA

A task's **Factory → Acceptance criteria** section is the adopted source of
criteria. Add a description, an observable expected result and a verification
method, then **Save criteria**. IDs survive edits and reordering. New UI criteria
start marked **Needs clarification**; clear that flag only after reviewing the
meaning and testability. Missing fields and explicit clarification flags appear
in readiness before work. These checks do not claim to detect every vague
sentence. Clarifications are never silently generated or adopted.

Preparing a run captures the exact criteria with its specification, effective
pipeline, skill hashes and environment configuration. Later task edits do not
rewrite the work order. Missing criteria remain a visible coverage gap;
preparation/execution still require explicit user actions. Unsaved criterion edits
block run preparation in the task form and have a browser recovery copy; only
explicit saving adopts them in DuckDB. Recovery never silently replaces newer
server criteria.

After execution finishes or stops, open the run's React Flow **Execution
inspector → Acceptance QA**. Each captured criterion is initially **Unverified**.
Record an explicit **Pass**, **Fail**, **Unverified**, or justified **Not applicable**
verdict. Every decision requires a rationale and is appended to history.
Process success, a green unrelated suite, and an agent report never automatically
establish a criterion verdict or change the task's workflow status.

For pass/fail, select current command evidence and enter the asserting test's
relative path/line, what its assertion proves, and the defect or negative case it
detects. The file and nonblank line must exist in the run's retained worktree.
The file content hash and command evidence hash are saved. The operator must
check semantic relevance and assertion quality; path validation and exit codes
cannot establish those. High-risk logic should have targeted mutation checks
in the configured verification command where useful. QA does not execute a new
command from this form or interpret generated reports as approval.

## Verification context and freshness

Every newly executed command has a receipt containing observations before and
after it ran, together with the exact command evidence hash. Context includes:

- Actual worktree HEAD commit.
- Content, executable modes, deletions and index state of tracked and non-ignored
  untracked files. Enumeration is complete and never uses truncated UI output.
- Captured work order configuration, specification and criteria; current selected
  skill contents and referenced environment input values are included in a
  composite digest. Resolved environment values are never stored or returned.

An output directory ignored by Git is not a code input. Ignored dependencies,
external services, operating-system state and transitive executable installations
are outside this fingerprint. Configure verification commands/environments to
exercise their relevant inputs and inspect their evidence. Submodules and symlink
traversal report an unavailable verification context rather than claiming full
coverage. Asserting test files must be tracked or non-ignored. Logical artifacts
remain step-evidence references; no screenshot, physical attachment or destination
record is invented from a declared artifact name.

A command that modifies code/index during verification has an unstable receipt.
A successful command can support pass only when its receipt is stable, its
exit code is zero, its evidence is intact and its context matches current code
and configuration. Failed commands can support explicit failure. Old commands
without receipts cannot support new pass/fail decisions; execute a new work order.

**Refresh QA** re-observes local inputs. Changed code/configuration, unavailable
worktrees, changed task specifications/criteria or changed pipeline attachments
make affected prior decisions **Stale**, preserving their original verdict and
proof. Not-applicable decisions also require a current context and become stale.
Unverified stays unverified. Task source changes require a new work order;
code/config changes require fresh verification. Returning to exactly the same
fingerprint restores the matching historical context. No failed request causes
an automatic rerun, repair, task acceptance or external action.

## API contract

All paths are below `/api/factory`, use the existing loopback/same-origin boundary
and `Cache-Control: no-store`. Unknown request fields are rejected.

| Method | Path | Contract |
| --- | --- | --- |
| GET | `/tasks/{id}/criteria` | `{projectRevision,criteria,checks}` |
| PUT | `/tasks/{id}/criteria` | `{expectedRevision,criteria:[{id,description,expectedResult,verificationMethod,needsClarification}]}`; explicitly adopts the complete ordered list |
| GET | `/runs/{id}/qa` | `{runId,runRevision,context,contextError,sourceChanged,criteria:[{criterion,status,staleReason,history}],receipts}` |
| POST | `/runs/{id}/qa` | `{requestId,expectedRevision,expectedContext,criterionId,verdict,reason,evidence:[{stepRunId,testPath,line,assertion,failureCase}]}` → current QA detail |

Criteria writes use the project revision and do not modify captured runs. Empty
lists are valid; IDs must be nonempty and distinct (at most 100 criteria).
Active factory execution blocks criteria edits. `expectedRevision` for a verdict
is the **run** revision. `expectedContext` is the fingerprint observed by GET QA
(or null for unverified when no context is available). Active/prepared runs cannot
receive verdicts. Pass/fail requires complete captured criteria and at least one
current evidence reference (maximum 100); pass requires successful zero-exit
command evidence. Not applicable requires justification and an available context.

A verdict request ID is idempotent for the exact payload, including its original
revision/context. Concurrent retries record once. Reuse with changed content or
for another run returns 409. Stale revisions/contexts and changed task inputs
return 409; invalid fields, test references or missing rationale return 400;
unknown tasks/runs/criteria return 404. Refresh after conflicts. Retry the same
saved request after an uncertain response; absence of a response does not prove
that the verdict was not recorded.

Records live in native DuckDB via the public EFCoreProvider and IWorkspaceStore.
The additive migration preserves all prior work orders and step evidence. Task
subtree/project deletion removes its criteria and QA history in the existing
transaction. This remains a single-user, single-backend-process deployment.

## Verification

Build the backend, then run `python3 scripts/factory-quality-regression.py`.
The suite uses its own repository/database, real commands and deliberately
passing/failing assertions, and tests stale code/configuration/source inputs,
invalid structural edits, concurrent retries, persistence across restart and
cleanup. No live model service or live application database is involved.
