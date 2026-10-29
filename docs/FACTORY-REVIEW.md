# Independent review and persistent findings

Review is an explicit workflow on a finished factory run. It captures the actual
worktree context and raw change, the acceptance criteria and prior findings. The
reviewer records an independent assessment before this workflow reveals builder
reports. An existing read-only pipeline reviewer can supply that assessment only
when its recorded independent packet matches the current code and inputs.
The raw change retains the first review baseline for this task/repository, so
repairs already committed before a later run remain visible. Each packet records
that baseline and the current verification context. Missing/pruned baselines and
changes exceeding the configured output limit are errors, not partial approval.
Reviewer process completion is never approval. This single-user deployment records
caller-declared reviewer names; it does not authenticate separate people.

Findings belong to the task and keep their IDs across runs and review rounds.
Every new round rechecks all prior findings first. Older agent assessments become
ineligible when their prior-finding context no longer matches. A fixed finding requires current
code references and successful command/test verification. An unresolved finding
cannot become fixed against unchanged source merely because another report
claims success; false positives/withdrawals require a no-longer-applicable reason. A no-longer-applicable
finding requires an explicit reason. Defects, missing behaviour, verification gaps,
scope drift and optional improvements remain distinct; improvements are backlog
suggestions and do not trigger refactoring or create tasks automatically.

Approval requires explicit submission, complete current acceptance QA and no open
blocking findings. It records the reviewed HEAD, code/configuration fingerprint,
exact QA verdict IDs/statuses, assessment and finding decisions. Code, task-input or QA changes make
approval stale. A later submitted review also supersedes earlier approval. Review does not modify task workflow state, launch repairs, merge
code or grant release authority.

Open a run's **Execution inspector → Independent review**. Enter the reviewer
name and choose **Start independent review**. Inspect the captured packet, record
an operator assessment (or import an eligible read-only reviewer report), then
compare builder explanations and submit the structured review. Rechecks come
before new findings. New finding/recheck form edits are persisted on submission;
the saved packet and assessment survive restart. Exact retries reconcile uncertain
responses without duplicating findings.

Semantic relevance of code/tests and the correctness of the review remain the
reviewer's responsibility. This workflow does not authenticate role separation
or prevent the local operator from reading reports through other existing views.
It does not add OS isolation. Agent reports remain untrusted assessments requiring
explicit structured review; no Markdown report is parsed into automatic approval.

## Contract

All endpoints use `/api/factory` and its existing loopback/same-origin boundary.
Unknown request fields are rejected. IDs and revisions come from actual API reads.

| Method | Path | Meaning |
| --- | --- | --- |
| GET | `/runs/{id}/reviews` | Current context, task finding history, compatible independent reviewer assessments and task-wide rounds |
| POST | `/runs/{id}/reviews` | `{requestId,expectedRevision,expectedContext,reviewer}` captures an independent packet; expectedRevision is the run revision |
| GET | `/reviews/{id}` | Captured packet, assessment, decisions and freshness; builder reports remain withheld until assessment is saved |
| POST | `/reviews/{id}/assessment` | `{requestId,expectedRevision,assessment,assessmentStepRunId}` saves an operator assessment or imports an eligible read-only reviewer report |
| POST | `/reviews/{id}/submit` | `{requestId,expectedRevision,expectedContext,verdict,summary,rechecks,newFindings}` records the review verdict |
| POST | `/reviews/{id}/abandon` | `{requestId,expectedRevision,reason}` retains an unfinished round without approval |

Round mutations use the round revision. Exact request-ID retries replay the saved
operation even after later changes; using the same ID for another payload/action
returns 409. Only one unfinished round per task is permitted. Start, assessment
and submission recheck current code/input context; stale rounds must be abandoned
and a new round prepared. Abandon does not require an available worktree.

Submission verdicts are `approve`, `requestChanges`, `inconclusive`. Each recheck
has `{findingId,status,reason,codeReferences,evidence}`; status is `unfixed`,
`fixed`, `noLongerApplies` or `unverified`. Every prior finding must occur exactly
once. Code references are `{path,line}` within the retained worktree; verification
evidence uses the acceptance-QA `{stepRunId,testPath,line,assertion,failureCase}`
contract. Fixed findings require code and successful verification evidence.

New findings have `{id,title,description,category,severity,owner,criterionIds,
codeReferences,evidence}`. Categories are `defect`, `missingBehavior`,
`verificationGap`, `scopeDrift`, `improvement`; severity is `critical`, `high`,
`medium`, `low`; owner is `implementation`, `verification`, `planning`.
New IDs are caller-generated UUIDs. The server assigns a stable task-local
finding number. At least one code, criterion or verification reference is required.
All findings except optional improvements block approval while unfixed/unverified.

Invalid fields/references return 400, missing resources 404, and stale revisions,
changed context, conflicting IDs or missing approval prerequisites return 409.
The native DuckDB migration is additive; rounds, findings and receipts survive
restart. Task/subtree/project deletion removes associated review history through
IWorkspaceStore. No automatic execution/replay is added.
