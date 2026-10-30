# Live delivery pilot

The pilot proves the existing factory against an explicitly configured GitHub
repository and a real local application. Its source is a small documentation task;
planning, execution, merge approval and task acceptance remain separate actions.

## Verification gate

`.github/workflows/verification.yml` runs on every pull request, every push to
main and explicit dispatch. No path filters or draft filters omit required checks.
It grants read-only repository contents permission, persists no checkout
credentials and pins actions to verified upstream commit IDs.

- `Client verification`: locked dependency installation, client tests and build.
- `Native API verification`: pinned public EFCoreProvider/native DuckDB build,
  deterministic timestamp write/read and rollback checks,
  isolated workspace/spec/agent/factory/QA/review/repair/skills regressions,
  real adapters against local Postman delivery/intake simulations, and local
  collection structural lint. Simulation passes do not prove real GitHub delivery.

The native gate aggregates independent `core` and `providers` groups. It fails
if either group fails, is cancelled or is skipped. Both groups must finish;
the client gate remains separate. `--suite core` and `--suite providers` run
those same groups locally; omitting the option runs both.

`python3 scripts/ci-native.py` runs the same native gate after a Debug backend
build. Each regression owns temporary databases and worktrees. The gate needs
Postman CLI 1.75.0, Git, Python and the .NET 10 runtime; it needs no provider
credentials, paid model access or shared Postman workspace publication.

Simulation launchers run in explicitly owned process groups. Teardown sends
SIGINT and uses a bounded kill fallback for remaining launcher/CLI children;
real process-tree tests cover both paths. A launcher cleanup error is a failed
gate even when its behavioral assertions passed.

Repository administrators must explicitly adopt strict required-check protection,
enforce it for administrators and bind both check names to their observed GitHub
App IDs. Do not guess App IDs or weaken protection to get a pilot through.

## Pilot evidence

Use an isolated native workspace to retain captures and operation receipts without
adding temporary projects to the live planner. Connect it to the actual repository,
use a real implementation agent and independent read-only reviewer, commit before
verification and bind QA to actual assertion evidence. Publish through the factory
to a new branch and draft PR. A ready PR, current successful required checks and a
separate revision-bound approval are prerequisites for merge.

Retain the actual head/base, check IDs and App IDs, review and QA records, PR URL,
merge result and ancestry, post-merge approval and restart receipt. Verify a new
instance, actual merged build revision, expected health and unchanged persistence
snapshot. The restart command and probes belong in machine-local deployment
configuration; no paths, ports, credentials or desired snapshot values are
application defaults.

Exercise draft/missing-check/changed-head rejection before the successful delivery.
Do not cause failures by changing required checks or rewriting a reviewed branch.
An isolated comparison capture may use an explicitly configured absent check or a
different observed remote head; all rejected attempts must retain their receipts
without a merge request. Close those captures explicitly before proceeding.

Unknown remote or hook outcomes require reconciliation. Never repeat a merge or
restart just because the caller lost its response. A delivered task remains in its
existing workflow column until the operator separately accepts it.
