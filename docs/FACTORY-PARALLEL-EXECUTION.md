# Parallel factory execution

## Contract established before implementation

Parallel dispatch is an explicit extension of the enabled project execution policy:
`maxConcurrentStages` is nullable (legacy/absent means serial), positive when set,
and bounded by the backend's configured parallel capacity. Setting a policy,
publishing a graph, arranging nodes or preparing a run never executes work. The
work order captures the policy. Policy drift, unsupported process ownership or
unavailable capacity blocks further dispatch; existing invocation and elapsed
budgets still apply.

Each stage executes its ordered steps in an isolated retained Git worktree. Its
input contains the captured initial work plus outputs of its explicit transitive
dependencies, each applied once in stable topological order. It does not see
unrelated siblings merely because they finished earlier. Command, agent, approval
and logical artifact steps retain their existing meanings.

Native records capture materialization intent, exact input/output Git trees,
file-mode manifests, workspace identity and verification checkpoints. Snapshot
trees are pinned by private run-scoped Git refs. The initial Git index is retained
as a hashed artifact; stage-local staging/commits remain in retained history while
integration preserves that initial index and normalizes the execution HEAD. Code
content, additions/deletions, binary changes and file modes are integrated; ignored
build output is not distributed as source or inferred to be a physical artifact.

Dependency joins apply the actual input-to-output deltas of each unique ancestor.
Conflicting content or mode changes fail visibly, retaining branch workspaces and
records. There is no arbitrary winner or automatic conflict resolution. Before
modifying the final run worktree, verify that its captured initial state is intact.
Integrate all completed stage deltas once, then retain the actual final context.
QA/review/delivery continue to use that final integrated worktree. Branch command
receipts can support QA only when their complete code/index/HEAD/configuration
context actually matches it; parallel completion alone never grants approval.

Manual decisions are stage-local. Other ready stages may continue. When only
human waits remain, the parent is waiting; explicit decisions requeue work without
replaying prior steps. Cancellation, policy revocation, timeout and failure stop
all owned children and retain their final evidence before the parent becomes
terminal. Restart marks unfinished materialization/execution/integration
interrupted and never replays it. The deployment remains one backend process with
native DuckDB through IWorkspaceStore/public EFCoreProvider.

Verification must prove overlapping real processes and isolated real writes,
dependency visibility, deterministic joins, content/mode conflicts, stable final
QA, budgets, gates, cancellation, interruption and persistence. Preserve serial,
repair, review and delivery regressions and verify the running React Flow/step
inspector before review, PR, protected merge and live restart.

## Configuration and inspection

Use **Software Factory → Model routing & budgets → Maximum concurrent stages**.
Leave it empty for the legacy serial path; `1` explicitly selects serial execution.
A value above `1` requires an enabled policy, supported owned-process adapter and
capacity on this backend. The work order captures this value; editing a policy
never starts work or changes the captured run.

The backend's `Factory:Parallel` section supplies technical admission bounds:
`MaximumConcurrentStages`, `MaximumIndexBytes` and `MaximumPatchBytes`. These are
host capacity, retained-index and generated-patch limits, not task/business data.
Readiness exposes unavailable capacity. A missing/oversized index or patch fails
visibly rather than distributing partial source. Git operations also retain the
existing configured operation timeout and each executing step its active limit.
The work-order elapsed budget includes materialization, joins and manual waits.

The existing run API adds optional `parallel` and `parallelStages` records. Serial
responses omit them. Each step checkpoint identifies its actual execution HEAD,
source tree and hashed self-contained index artifact. Split indexes are expanded
only in the retained artifact; the logical staged entries remain the same.
Private refs retain stage commits/trees. The stage inspector shows retained
workspace, input/output trees and checkpoints; the overview shows final integration
state. Multiple human waits have an explicit stage picker, and a gate is visible
while unrelated work continues.

Parallelism distributes source deltas only. It does not distribute ignored build
output, physical artifacts, process state or services. Commands still run as the
local account; isolated Git worktrees are not an OS security sandbox. Symlinks,
submodules and unsupported process/file-mode adapters remain explicit gaps.
Conflicting joins retain integration worktrees/patches for inspection; preparing
new work after resolution remains an explicit action. No branch, PR, release or
deployment authority is gained by enabling concurrency.

See [validation](FACTORY-PARALLEL-VALIDATION.md) for executable evidence and scope.
