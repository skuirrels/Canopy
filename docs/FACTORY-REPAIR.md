# Bounded repair and verification loops

Repair is an explicit opt-in workflow on a finished run. Adopt a project repair
policy, select failed acceptance criteria or unresolved review findings, prepare
the captured loop, then explicitly start it. Selection and preparation execute
nothing. Task workflow status remains independent.

Project policy names published repair plans for implementation and verification.
A plan has write steps for its responsible role, followed by verification commands
and an independent read-only reviewer. Manual gates remain explicit. Commands
must be explicitly declared safe to repeat; repair plans must not perform merges,
releases, deployments or other non-repeatable external actions.

Each attempt is a separate factory run/worktree with immutable lineage, policy,
selected issues and feedback. It forks the parent's actual tracked/non-ignored
code and index, including uncommitted edits, staged changes and deletions. Parent
worktrees, earlier outputs, specifications and review receipts are retained.
Ignored dependencies/output files are not cloned; configured setup/verification
must resolve them. Unsupported symlinks/submodules remain visible gaps.

A green run waits for fresh explicit acceptance QA and independent structured
review. No report is parsed into approval or finding resolution. Remaining issues
return to the captured responsible role, automatically only when policy permits.
New/out-of-scope findings, changed task inputs and inconclusive review require a
human decision; they cannot silently expand the authorized repair scope.

## Budgets and stopping

Policy specifies maximum attempts, elapsed minutes, consecutive rounds without
progress and reserved agent invocations. Agent calls are conservatively reserved
for all agent steps before an attempt queues. Elapsed time includes queueing and
human QA/review waits. Lack of progress is measured from selected unresolved issues
and actual failed attempts, not reassuring model output or arbitrary elapsed time.

The CLI runners do not currently provide an enforceable monetary spending cap.
A configured monetary limit blocks dispatch with an explicit unavailable-cost
reason; unknown cost is never treated as zero. Running without that cap requires
explicit policy consent to unmetered cost and still obeys the other hard limits.
Costs and reserved invocation counts are presented separately. Reservations are
conservative and are not refunded for steps that did not start; actual started
agent counts are shown separately.

Stop cancels the real active process tree and settles only after child shutdown
and final evidence capture. Restart marks every unfinished loop stopped and its
active run interrupted, without replay. Stopped loops cannot silently resume;
starting new work requires a new explicit captured loop.

## API contract

All endpoints use `/api/factory`, the existing loopback/same-origin boundary and
`Cache-Control: no-store`. Unknown request fields are rejected.

| Method | Path | Purpose |
| --- | --- | --- |
| GET | `/projects/{id}/repair-policy` | Adopted policy or null; no invented defaults |
| PUT | `/projects/{id}/repair-policy` | `{expectedRevision,policy}` adopts explicit project policy |
| GET | `/runs/{id}/repair-loops` | Task loop history, eligible causes, policy and current context |
| POST | `/runs/{id}/repair-loops` | `{requestId,expectedRevision,expectedContext,owner,findingIds,criterionIds}` prepares only |
| GET | `/repair-loops/{id}` | Captured policy/scope, attempt lineage, budgets and actual outcomes |
| POST | `/repair-loops/{id}/advance` | `{requestId,expectedRevision}` explicitly starts/continues an eligible loop |
| POST | `/repair-loops/{id}/stop` | `{requestId,expectedRevision,reason}` stops without replay |

Policy contains `enabled`, `implementationPlanVersionId`,
`verificationPlanVersionId`, `maxAttempts`, `maxElapsedMinutes`,
`maxNoProgressRounds`, `maxAgentInvocations`, `autoContinue`,
`commandsSafeToRepeat`, `allowUnmeteredCost`, `maxCost`, `currency`.
Published plans must belong to this project and remain immutable. Captured policy
is not replaced by later edits. One unfinished loop per task is permitted.

Preparation uses the source run revision/context. Later mutations use the loop
revision. Exact request-ID retries replay the recorded action; conflicting reuse
returns 409. Malformed input/policy returns 400, missing resources 404, and stale scope,
context, revisions or conflicting execution return 409. Historical repair children cannot bypass
loop budgets through ordinary run start. Fresh verification and review always
refer to the current attempt, never an earlier successful revision.

See [validation evidence and boundaries](FACTORY-REPAIR-VALIDATION.md).
