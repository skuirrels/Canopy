# Independent review validation — 29 October 2026

## Result

Review captures criteria, the raw cumulative task change and prior finding
identities before builder explanations. Read-only pipeline reviewers receive
that independent packet and actual command verification, excluding implementation
report artifacts. Their completion only creates an eligible assessment.

Explicit rounds retain immutable findings and decisions in native DuckDB through
IWorkspaceStore. The reviewer rechecks every prior finding, supplies proof for a
fix, or explains why an issue no longer applies. Approval requires current complete
acceptance QA and no unresolved blocking findings. Task workflow remains separate.

## Review fixes

- Old agent assessments are ineligible after prior findings change; a report
  predating new findings cannot substitute for rechecking them.
- Unchanged source cannot close an unresolved finding on a new claim. Fixed
  findings require current code references, successful command/test evidence and
  an assertion/failure-case explanation.
- Raw changes retain the first task/repository review baseline. Repairs committed
  before a later run remain visible. Oversized raw changes fail capture rather
  than silently truncating the reviewed source.
- Later reviews supersede earlier approvals. Code/configuration, source-input,
  QA-receipt and assessment-integrity changes expose stale review state. The
  library marks stale approvals before opening details.
- Approval stores exact criterion QA verdict IDs/statuses as well as their digest,
  so its verification basis is inspectable after later decisions.
- Request IDs and optimistic revisions are checked inside the native writer
  transaction. Lost responses can be reconciled without adding duplicate findings.
- QA and review share evidence validation, including safe paths, real nonblank
  source/test lines, file hashes and current command receipts.

## Automated verification

- Debug and Release backend builds: clean; EF reports no pending model changes.
- Client production build and 96 tests: passed. Existing large-bundle advisory
  remains.
- Independent review regression: 28 checks, using actual Git/worktrees, commands,
  assertions, HTTP and native DuckDB. Provider reports are explicit CLI protocol
  fixtures, not live model calls or semantic model-quality evaluation.
- Scenario: first review finds an uncovered negative-input defect; a second
  retains its ID and detects it unfixed; corrected code plus a real negative-input
  assertion then verifies the repair on a later run. Incomplete QA, missing prior
  rechecks and unchanged-source claims cannot approve it.
- Also checks concurrent begin/retry, input/role/reference validation, assessment
  ordering and import eligibility, missing context, stale approval, exact QA IDs,
  oversized raw changes, explicit no-longer-applicable reasons, optional backlog
  suggestions, unfinished-review restart persistence and transactional cleanup.
- Existing QA regression: 29 checks passed after sharing its validator.
- Existing preparation regression: 39 checks passed.
- Existing execution regression: 36 checks passed after changing reviewer inputs.
- Saved Postman review lifecycle: 8 requests / 17 assertions passed, with all 8
  per-request fixture guards executed. Reporting/publication disabled. Review,
  Factory and generated collection lint passed.
- Existing saved Factory lifecycle: 24 requests / 38 assertions passed against
  the final isolated backend.
- OpenAPI syntax validation reported no issues. The local CLI emits its existing
  macOS keychain diagnostic afterwards; no workspace publication is claimed.
- Native upgrade from the previous primary binary preserved actual adopted
  criteria, command receipts and QA verdict history, plus prior prepared records,
  standalone session/result/model history and the complete workspace.

## Browser verification

Used the isolated backend in a real Chromium browser:

- Started an independent round and confirmed builder comparison was absent until
  the independent assessment was saved.
- Rechecked the same persistent finding as fixed with actual code and test proof;
  retained the explicit no-longer-applicable decision for a second finding.
- Added a nonblocking optional finding and submitted explicit approval.
- Aborted the POST response after the backend committed. The form retained and
  froze the original payload; retry left exactly one new finding.
- Changed code after approval and confirmed both the round and its library
  summary were visibly stale. Returning to the identical context restored the
  matching historical receipt.
- Followed a finding's verification link to the actual producing command and its
  zero/negative assertion output.
- Desktop dark/light and 390×844 phone viewport had zero axe WCAG A/AA findings in
  the tested execution view. Phone viewport had no horizontal page overflow.

Screenshots are ignored local artifacts in `output/playwright/factory-review/`.
Phone viewport checks do not certify a physical phone. Expected network-console
errors come from the deliberately aborted response.

## Boundaries

This remains single-user/single-backend-process native DuckDB. Reviewer identity
is caller-declared; authenticated team authority remains a later deployment track.
No automatic fix loop, merge, release, task acceptance or paid model call is added.
Reviewers still judge semantic relevance and whether tests detect the reported
problem. Logical evidence does not imply physical screenshot/file collection.

Verification freshness has the same documented worktree/configuration boundaries
as acceptance QA. Unsupported symlinks/submodules, missing/pruned Git baselines and
unavailable evidence remain visible gaps. Recheck/new-finding form edits are saved
on submission; captured packets, saved assessments and submitted history survive
restart. All regression/example projects use temporary data and repositories.
