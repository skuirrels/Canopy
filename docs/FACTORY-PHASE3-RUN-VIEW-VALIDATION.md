# Phase 3 — execution workspace validation

Validated on 29 October 2026 against isolated native DuckDB databases, test-only
Git repositories and executable CLI fixtures. The live workspace was not used
for test data or model calls.

| Check | Result |
| --- | --- |
| Debug / Release backend builds | Passed, no compiler warnings/errors |
| Frontend production build | Passed; existing bundle-size advisory remains |
| Client regressions | 95 passed |
| Preparation/history API regression | 39 passed |
| Real execution/process regression | 36 passed |
| Maintained Factory Postman collection | 19 requests / 29 assertions passed locally |
| Main execution-view browser workflow | 24 checks passed |
| Additional browser checks | Phone report visibility/return navigation; real phone-viewport cancellation; failure/downstream blocking; persisted evidence after restart; light-theme accessibility |
| Automated accessibility | No axe WCAG A/AA violations in tested desktop, phone and light-theme execution views |

The browser opens history through the project library and through task details,
inspects the existing React Flow canvas, and verifies that stage badges match the
actual persisted records. The graph exposes no configuration editing or invented
progress percentage. The phone step list reaches the same actual review report;
its text was checked within the physical viewport bounds.

The workflow checks explicit start confirmation, cancelling that confirmation,
a lost start response, last-known-state warnings during polling failure, leaving
and reopening the view, both human gates and an intentionally delayed approval
acknowledgement. Older revisions cannot replace newer saved states. Polling uses
one chain at the backend-configured interval.

Evidence checks compare the displayed report and diff with recorded step output,
download the report, navigate an artifact to its producing command, verify its
SHA-256, and copy the actual retained worktree path. Completing the run leaves the
task metadata/workflow unchanged. A real command is stopped from the phone view;
a different command exits with code 7 and displays its actual error and blocked
downstream step, without publishing success evidence.

The native API suite verifies project-scoped history, local-origin enforcement,
newest-first ordering, actual run revisions, immutable titles after a task rename,
and persistence across a real backend restart. Browser history, reports, diffs,
step states and logical evidence were also reloaded after restarting the backend.

Review fixes include keyboard access to scrolling command text, adequate caption
contrast in the light theme, readable initial graph zoom, approval controls in the
inspector, compact-layout evidence navigation and stale-response protection.
Existing designer editing, storage/provider pins, task status rules and user
workspace data are preserved.

Git and verification/cancellation processes are real. Provider responses are CLI
protocol fixtures; paid model access, provider sandbox enforcement and model
conclusions remain unverified. Artifacts are labelled as logical evidence, not
inferred physical files. The Postman spec, generated endpoint examples and
maintained assertions are local Git artifacts; no shared workspace push occurred.
