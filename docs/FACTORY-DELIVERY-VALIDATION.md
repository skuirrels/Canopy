# Controlled delivery validation

Validated on 29 October 2026 using isolated native DuckDB, actual Git objects and
HTTP Git pushes, the real .NET HTTP adapter and a stateful local Postman simulation.
No real GitHub test mutations, paid model calls or live test projects were used.

## Results

- Backend build passed with zero warnings/errors; client build passed, retaining
  the existing Vite large-chunk advisory. EF reported no pending model changes.
- Client tests: 96 passed.
- Full delivery regression: 52 passed. Captured authority, environment/file scope,
  actual command proof, concurrent exact retries, branch publication, draft and
  approval gates, required checks/App IDs, provider outages/HTML/timeouts,
  incomplete coverage, changed head/base and server rejection, lost responses,
  asynchronous outcomes, expired/failed requests, real process restart and
  wrong-revision/persistence failures are exercised through public HTTP APIs.
- A targeted hook integrity run additionally edits the actual configured script,
  verifies captured authority is revoked before mutation, restores its bytes and
  completes an explicitly approved real restart with intact persistence.
- Existing regressions: preparation 39, execution 36, QA 29, independent review 28
  and bounded repair 30 passed.
- Native upgrade fixture preserved a full workspace, prepared work orders,
  standalone sessions/model settings, acceptance criteria, command receipts and
  verdict history. The migration adds only delivery policies, records and journal
  operations; the public EFCoreProvider/native DuckDB path remains unchanged.
- Saved `Factory delivery regression`: 8 requests / 15 assertions, explicitly
  guarded for isolated fixtures. Both owning API and regression collections linted
  without errors/warnings. All six endpoints have actual isolated examples with
  parameterized caller input, including validation, conflict and origin errors.
- OpenAPI syntax lint reported no issues, followed by the installed macOS CLI's
  `SecItemCopyMatching failed -50` diagnostic. No remote Postman publication was
  attempted. The consumed GitHub contract subset is checked in beside its mock.

## Browser verification

Real browser interactions cover invalid policy rejection, explicit policy save,
capture/publication controls, lost publication response and exact saved retry,
draft blocking, required human rationale, separate approval/merge actions and
actual merged-state rendering. A deliberately dropped accepted response created
one publication intent, then reconciled without another branch/PR action.

The existing React Flow execution graph remains in place. Desktop light/dark,
390 x 844 viewport and policy dialog checks found zero settled scoped WCAG 2 A/AA
and 2.1 A/AA axe violations and no horizontal overflow. Screenshots were inspected
under ignored `output/playwright/factory-delivery/`. Viewport emulation does not
certify a physical phone. The Playwright CLI wrapper was unavailable through its
package/network cache, so the available Playwright browser tools drove the checks.

## Review repairs and safeguards

- Business merge rules live in Domain; application use cases coordinate evidence,
  immutable captures and persistent intents through IWorkspaceStore. Infrastructure
  implements Git, GitHub HTTP, configured commands and probes.
- Publish only the clean committed and independently approved candidate. A current
  successful command receipt is required even if manual criterion waivers exist.
- Bind environment and actual changed sensitive/migration files. Diff classification
  disables rename folding so moving a sensitive file cannot conceal its old path.
- Do not overwrite a different remote head. Use create-only Git leases and verify
  repository identity, current target ancestry and exact GitHub head guard.
- Require strict enforced server protection and configured check App identities.
  Unknown coverage and unsupported merge queue/method outcomes fail closed.
- Persist returned merge SHA/request IDs before further inspection. Retain earlier
  receipts if subsequent verification fails. An expired request remains blocked;
  only a proven terminal rejection permits a new explicit, rechecked request.
- Restart interrupts queued/in-flight intents without replay. Reconciliation uses
  actual PR markers, merge results, trees, parents, ancestry and deployed probes.
- Post-merge approval binds the actual merge, hook configuration, executable and
  declared static source hashes. Changed instance, actual deployed revision,
  expected health and matching persistence hashes are all required.
- The UI closes uncertain confirmations so exact retries remain accessible and
  disables writes while external outcomes remain unresolved.

## Boundaries

Provider discovery through Orbit returned 403; the owning-mock search required
unavailable Postman authentication. Official GitHub contracts were used instead.
The local simulation's NDJSON request log proves the real adapter used it. Its
SHA/tree/parent data comes from actual fixture Git objects. It intentionally uses
local filesystem/process APIs and must not be deployed as a shared/public mock.

GitHub integration behavior is simulation-tested; configured provider credentials,
repository protections and deployment hooks remain operator-owned prerequisites.
The application does not change repository protections, approve a GitHub draft,
release packages or infer task acceptance. Only merge commits are supported. The
provider's strict protection guards concurrent target writers; any returned merge
whose recorded parents/tree differ from authority remains needs-attention.
Administrative hook declarations must include all static command sources; probes
verify the configured application's reported identity/revision and persisted data,
not a general attestation of arbitrary external systems.
