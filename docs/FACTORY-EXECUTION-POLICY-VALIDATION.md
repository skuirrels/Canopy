# Model routing and execution policy validation

Validated on 30 October 2026 using disposable repositories, native DuckDB databases,
real HTTP calls and owned local processes. Test-only provider executables implement
CLI JSON protocols; these tests make no model calls and establish no account access,
model quality, billing completeness or provider-internal spending ceiling.

## Checks

- `npm test --prefix client`: 99 tests passed.
- `npm run build --prefix client`: passed. The existing large-chunk warning remains.
- `dotnet build Planner.sln`: passed with no warnings/errors.
- `python3 scripts/factory-routing-regression.py --postman`: 41 checks and the saved
  collection's four requests/eight assertions passed. Policy validation, optimistic
  revisions, same-origin controls, role/step/task precedence, provider isolation,
  actual model/effort arguments, unknown/invalid/mixed/truncated usage, stage-local
  IDs, reservations, manual/command/agent deadlines, policy revocation, restart and
  test-project deletion are covered.
- `python3 scripts/factory-repair-regression.py --execution-policy`: 16 checks
  passed, including a real repaired child, current QA/review, captured model routes,
  durable reservations, actual CLI arguments and rejection of an old repair plan
  after execution policy changes.
- `dotnet run --project tests/Planner.ExecutionPolicyMigrationRegression`: passed.
  An agent row created against the previous release retains every execution field
  and existing index across upgrade, update and restart. Historical usage stays
  unknown. Migration uses the repository's transactional table-copy pattern.
- `postman collection lint 'postman/collections/Factory routing regression'`:
  four requests, zero errors/warnings. OpenAPI JSON parsed and all 503 local schema
  references resolved. The live generated endpoint and changed schemas were copied
  without replacing configured servers or reordering unrelated contracts.

`python3 scripts/ci-native.py --suite core` passed locally, including all existing
native core scenarios and the new policy checks. The CI core suite includes the migration and routing regressions, the policy-enabled
repair scenario, and existing workspace/spec/agent/factory/QA/review/repair/skills
checks. The provider suite runs the saved routing collection against the real
isolated backend alongside existing delivery/intake coverage. Required Client
verification and Native API verification gates remain unchanged.

## Running interface

An isolated fixture verified opening **Model routing & budgets**, editing/saving
and reopening a model ID, and observing its persisted API value. A concurrent save
advanced the revision; the stale browser save displayed the conflict and preserved
the current model. The intentional 409 generated an expected failed-request console
entry; ordinary interaction had no console errors.

The execution inspector showed requested model/effort, role provenance, provider
completion counters and captured reservation limits. Desktop and mobile viewport
screenshots were inspected; route controls stack on mobile, the form scrolls and
its footer stays reachable, with no horizontal content overflow. Browser viewport
emulation is not physical-device testing. Test data and processes were isolated
from the live workspace.

## Review corrections

A late negative-case test reproduced a genuine hang on the original PR head: a
command spawned a child inheriting stdout, exited, and left the work order running
after its five-second budget. The worker still waited after 35 seconds. The fixture
then killed only its identified child and cleaned up. The corrected runner passes
the same case, stopping the child and settling the failed work order.

- Added a macOS/Linux process supervisor and cancellable output reads, preventing
  inherited-output children from escaping an active command deadline. Unsupported
  platforms block enabled-policy preparation/dispatch rather than imply enforcement.
- Replaced unsupported constrained `ALTER ADD` with transactional copy, preserving
  native data and indexes rather than changing provider/storage paths.
- Kept the effective timeout at the earlier of the backend active-step ceiling and
  captured work-order deadline; queued dispatch and actual child start recheck policy.
- Evaluate completed elapsed usage at recorded finish time so history cannot later
  appear newly exhausted.
- Retain missing completion observations alongside valid reports; partial coverage
  cannot become a complete report. Provider protocol parsing stays in Infrastructure.
- Record task override provenance separately from published step settings and role
  routes; explicit model/provider settings cannot inherit a different role's effort.
- Bind policy changes into current QA source and verification capture, including
  repair children. Existing policy-free hashes retain their prior representation.

No default execution policy, model IDs, prices, automatic fallback or concurrency
was introduced. Hard token/currency caps block preparation; explicit unmetered
consent is required for agent steps under an enabled policy. Standalone assistance
and normal planning retain their existing explicit controls.

The shared supervisor change also passed the existing 36 factory execution checks
and 29 standalone-agent checks, preserving permissions, timeouts, cancellation,
shutdown diff collection and restart recovery. The original required CI run passed
before the inherited-output correction; merge requires a fresh run on the corrected
head, including delivery and deployment integration coverage.
