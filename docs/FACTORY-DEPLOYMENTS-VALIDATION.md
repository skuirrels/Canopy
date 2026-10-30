# Deployment environment validation

Validation uses the public API, public EFCoreProvider/native DuckDB, real Git
repositories and HTTP transport, actual configured commands, restarted application
processes and persisted SQLite target data. GitHub itself is represented by the
existing local Postman protocol simulation; no remote GitHub mutation, production
deployment, production credentials or paid model invocation occurs in this suite.

| Acceptance criterion | Evidence |
| --- | --- |
| Named targets require explicit project adoption | Unconfigured/unauthorized target rejection; policy checklist exercised in the running browser |
| Capture and approval execute nothing | Concurrent exact capture retries retain one record; missing approval creates no command intent; browser cancellation leaves revision 1 and an empty journal |
| Approval binds environment, revision and configuration | Changed script and project policy block execution; actual source merge identity/tree/parents/ancestry are checked before deployment |
| Deploy the actual merged artifact | The target checks out the actual merged Git commit, imports and runs its changed `increment` implementation, and reports the checkout's real commit identity |
| Verify health and persistence | Real process identity, actual revision and canonical SQLite snapshot; no-op, wrong revision, changed data, nonzero exit and offline target scenarios remain visible; mutable revision labels block dispatch before a command intent |
| Recovery is separate and preserves failures | An explicit recovery approval restores the observed prior commit and original SQLite backup; original deployment failures and failed recovery receipts are unchanged |
| Lost responses/restarts never replay commands | Exact completed request retries cause no restart/merge; interrupted commands retain intents and reconcile through observation only |
| Target aliases cannot deploy concurrently | A second profile with the same health/snapshot endpoints conflicts; UI disables a reserved target and explains the reservation |
| Native persistence survives upgrade and restart | A previous deployed build creates a test database; the new migration preserves its workspace exactly. Captures, approvals, successful/recovered/interrupted/closed histories persist across restart |

The targeted deployment suite runs the existing 15-check shared delivery prelude
plus deployment assertions. Its saved Postman collection runs six requests and
eight assertions against the actual native API, covering readable passive captures,
unknown fields/actions, stale approval and unchanged journals after rejection.

The existing full delivery regression remains separate. CI runs `core`, `providers`
and `deployments` groups independently; the required native aggregate fails if any
group fails, is skipped or is cancelled. The required client gate remains separate.

Commands:

```sh
npm --prefix client test
npm --prefix client run build
dotnet build Planner.sln
python3 scripts/ci-native.py --suite deployments
python3 scripts/factory-delivery-regression.py
postman spec lint postman/specs/planner.openapi.json --issueType syntax --no-report-events
postman collection lint 'postman/collections/Factory deployment regression' --reporter cli --fail-severity error
postman collection lint 'postman/collections/Planner API' --reporter cli --fail-severity error
```

Browser validation exercises target selection, passive capture, cancel, approval,
actual dispatch, project allowed-target editing, persistence failure, separate
recovery, immutable original receipts, reservation explanations and closing
tracking. Phone-size inspection has no page overflow; the browser reports no
console errors. All test projects, databases, configured target processes,
worktrees and simulation groups are isolated and removed after validation.

Remote deployment infrastructure and each administrator's real recovery procedure
require their own target-specific evidence. An application's successful exit or a
healthy target at the wrong revision cannot substitute for that verification.

## Native startup recovery finding

The Linux provider gate exposed `Failed to delete all rows from index` after the
existing intake test force-killed its backend. A separate three-process test
reproduces the same error deterministically on the pinned public provider:
retain a committed WAL with a secondary unique index, replay it and close without
an explicit checkpoint, then update/delete the recovered row in a fresh process.
This matches the [upstream WAL/shutdown-checkpoint investigation](https://github.com/duckdb/duckdb/pull/23848).

`PlannerDatabase.Initialize` holds the public EF connection open, checkpoints with
an active client context before migration, migrates and checkpoints again before
closing. It neither changes the database/provider nor recreates data or indexes.
`tests/Planner.WalRegression` retains the failing native control and calls this
actual initialization path for its passing case, verifying the original row and
subsequent update/deletion after another process restart. CI runs this regression
in the core group. The original intake interruption assertions and timing remain
unchanged; the provider group still exercises the observed failure path.
