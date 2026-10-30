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

The native gate aggregates independent `core`, `providers` and `deployments` groups. It fails
if any group fails, is cancelled or is skipped. All groups must finish;
the client gate remains separate. `--suite core`, `--suite providers` and `--suite deployments` run
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

## Live findings and repair

The real protected pilot in PR #26 exercised implementation, captured command
verification, independent read-only review, draft publication, missing-CI and
changed-local-commit rejection, and one asynchronous merge. It exposed two gaps
that the earlier simulations had not established:

- REST 2026-03-10 omits the merged commit field. The adapter now queries the
  authoritative GraphQL merge identity, binds repository/PR/head, and still checks
  the actual Git tree, parents and target ancestry. The fixture omits the removed
  field. Seven invalid identity responses block recovery without another merge;
  complete identity recovers a lost response. The full suite has 66 checks;
  `--merge-identity-only` runs the shared 23-check focused scenario.
- A target restart must not inherit the controlling application's isolated
  database/worktree/skill/hook overrides. The machine-local target launcher now
  removes those section overrides and passes its own configured database and
  worktree paths explicitly. Its scripts remain declared integrity inputs; no
  restart command is adopted as a product default.

The first delivery's nonzero hook receipt remains failed and was explicitly
closed. The app was recovered with the exact pre-hook workspace. Do not relabel a
manual recovery as a successful configured hook or replay the failed action.
A new delivery captures the repaired code, fresh QA/review and corrected hook
source hashes. Its successful merge/restart/persistence receipts are the completion
authority. Local evidence is retained under the API content root's ignored
`data/validation/20261030-live-delivery/` directory when that run completes.

Unknown remote or hook outcomes require reconciliation. Never repeat a merge or
restart just because the caller lost its response. A delivered task remains in its
existing workflow column until the operator separately accepts it.
