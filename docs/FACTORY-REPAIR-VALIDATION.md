# Bounded repair validation

Validated on 29 October 2026 in isolated native DuckDB databases and real Git
worktrees. Provider responses were explicit CLI protocol fixtures; no paid model
calls, live project edits, PR delivery or external command side effects were used
by the application tests.

## Results

- `dotnet build server/Planner.Api --no-restore`: no warnings or errors.
- `npm --prefix client run build`: passed. Existing Vite large-chunk advisory remains.
- `npm --prefix client test`: 96 passed.
- `python3 scripts/factory-repair-regression.py`: 30 passed. Covers captured
  policy and scope, exact concurrent retries, real parent/index copying,
  responsible-role routing, fresh QA and independent rechecks, command failures,
  attempt/invocation/no-progress/elapsed ceilings, explicit unmetered consent,
  unavailable monetary caps, auto-continuation, scope escalation, actual child
  process cancellation, restart without replay and transactional cleanup.
  The elapsed case waits for the real one-minute ceiling without altering time
  or database records.
- Existing regressions: preparation 39, execution 36, acceptance QA 29 and
  independent review 28 passed.
- Native upgrade fixture: a database created by the previous live binary retains
  its full workspace, prepared work orders, standalone sessions, model settings,
  acceptance criteria, real command receipts and verdict history after migration.
  The migration adds only repair policies, loops and attempts.
- Saved `Factory repair regression` Postman collection: 9 requests / 17 assertions
  passed against the isolated fixture. Includes scope/history, unavailable cost,
  stale operations, unsafe repetition and mismatched write-role rejection.
  All requests require explicit isolated-fixture variables.
- Local OpenAPI syntax lint reported no issues, then the installed macOS CLI
  emitted `SecItemCopyMatching failed -50`. This is a local CLI diagnostic;
  no remote Postman publication was performed. All seven endpoints have actual
  isolated success/error examples and parameterized request inputs.

## Browser evidence

Real browser interaction covered policy editing, rejecting zero limits, selecting
an original finding, capture without execution, explicit start confirmation,
parent/attempt navigation and stopping. After the backend accepted a start, its
response was deliberately dropped. Retrying the exact saved request produced
one child and one reservation, with an accessible retry action.

The desktop execution graph remains React Flow. Desktop light/dark and a
390 x 844 viewport were inspected. Settled WCAG 2 A/AA and 2.1 A/AA axe checks
reported zero violations in the execution region and policy dialog. The narrow
viewport had no horizontal overflow. A transient contrast report during the theme
transition disappeared after the theme settled. Viewport emulation does not
certify physical-device behavior.

Screenshots are local test artifacts under
`output/playwright/factory-repair/` (ignored by Git).

## Review repairs

- Copy actual tracked/non-ignored work and the Git index, retaining staged versus
  unstaged state. Never reconstruct a repair solely from the repository commit.
- Reject narrow scopes that omit other unresolved blockers; keep improvements
  nonblocking. Recheck previously resolved findings through independent review.
- Count lack of progress once per attempt, using unresolved selected issues.
  Completing an agent or making irrelevant edits cannot reset the counter.
- Enforce dispatch budgets before reserving/queueing. Evaluate elapsed lifetime
  separately so the last permitted attempt can finish and receive fresh QA.
- Recheck run identity and revision after asynchronous observations. A concurrent
  attempt transition cannot consume an earlier candidate's verification context.
- A terminal-loop deletion race cannot take down the coordinator. Stop signals
  reach the actual processes, and restart never replays unfinished loops.
- Preserve an uncertain request body for exact replay, close its confirmation
  so retry remains accessible, refresh workspace revisions after policy edits,
  and refresh sidebar attempt states when repair history changes.

## Boundaries

Money usage is unavailable from the current CLI protocol. A configured monetary
cap prevents execution; running without it requires explicit consent to unmetered
cost. This does not claim enforceable provider billing limits. Ignored output and
dependencies are not cloned; plans must resolve them. Symlinks/submodules fail
visibly. Structured QA and review remain operator decisions; a model report does
not accept a task, approve code, merge, release or deploy it. Completion records
bind the verified context at that point; later edits are assessed through the
existing QA/review freshness controls.
