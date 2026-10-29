# Verified problem intake

Intake is optional and manual. Configure a source in **Software Factory → Problem
intake**, collect its signals, inspect the captured evidence and record a triage
rationale. Verify current evidence before previewing a new backlog task or linking
existing work. There is no scheduler, automatic filing, agent dispatch, pipeline
attachment, acceptance decision or merge authority in this slice.

## Source contracts

GitHub issue sources name one `owner/repository` and an explicit required-label
list (all labels must match; an empty list reads all). Reads use the existing
operator-owned `Delivery` GitHub endpoint, version, host and credential settings.
No token is exposed to the browser. The integration uses GET repository issues
with `state=all`, `sort=created`, `direction=asc`, `per_page=100` and numbered pages.
The API includes pull requests; rows with `pull_request` are excluded. Closed or
no-longer-matching issues cannot be filed. A fresh individual issue GET checks
identity and current content again before verification and filing/linking.

Recurring review sources select categories and a minimum of at least two distinct
tasks. Only persisted findings from submitted review rounds participate. The most
recent submitted recheck determines whether each finding remains unresolved
(`unfixed` or `unverified`). Rechecking the same stable finding never creates an
occurrence. Groups use normalized title, category and sorted distinct code paths;
line numbers, severity and report wording do not change their identity. Every
occurrence retains its finding/task/run/round IDs, original data, captured code
context and latest recheck. Unfinished/abandoned rounds do not count. Submitted snapshot evidence retains its
original verification context; intake does not rerun review or certify current code.

These rules identify source-backed candidates, not proven software defects.
An operator must assess relevance, reproduction and scope in the explicit
verification rationale. Similar titles alone are not semantic deduplication.

## Persistence and authority

The existing IWorkspaceStore transaction and public EFCoreProvider/native DuckDB
path persist sources, collection receipts, candidates, immutable observations and
triage decisions in five additive tables. One source per provider/repository scope
is allowed in each project; edit its filters or disable it instead of duplicating
configuration. Source kind/repository and the configured provider binding are
captured. Changed deployment bindings require an explicit source save to adopt the current
provider configuration, followed by a fresh complete collection. The unique scope
guard also applies when saving across a host change.

GitHub fingerprints use configured host and immutable issue ID. Review fingerprints
use project and normalized grouping fields. A unique project/fingerprint index
prevents duplicate candidates. Every successful collection creates an immutable
observation for each candidate, retaining earlier evidence. Changed evidence
revokes verification. Disappearance from complete coverage is recorded as
unavailable; failed reads never imply absence or replace observations.

Collection receipts distinguish running, complete, failed and interrupted states.
Successful empty coverage is explicit. HTTP failures, timeout, malformed evidence,
repeated page identities, response/collection limits or exhausted pagination reject
the entire result. A source edit during a read rejects that result before applying
candidates. A later failed or interrupted collection blocks triage against older
coverage until a fresh complete collection succeeds. Startup never replays reads.
The operator may explicitly collect again using a new request ID.

Verification, filing and linking require current enabled source authority and a
complete collection. Review data is rechecked transactionally. External GitHub
observations cannot be locked atomically with local writes; the immediate fresh
GET is a timestamped observation, not a promise that the issue can never change.
A source edit or collection started during that GET blocks the local decision.

Exact request retries return recorded outcomes without repeating collection or
filing. Conflicting reuse returns 409. Candidate/project revisions protect triage;
source revisions protect configuration and collection. Filing creates a root task
in an explicitly selected incomplete column with its source, original evidence,
fingerprint and rationale in the rich-text specification. No workflow column is
inferred from its name. Linking changes no existing task fields. An existing exact
normalized task title blocks filing and offers linking. Operators must inspect
other similar work themselves. A durable candidate/task link prevents re-filing
even if the task is subsequently deleted; deleting the whole project removes its
intake records transactionally.

## API

All routes use the existing loopback/same-origin/no-store `/api/factory` boundary.
Unrecognized request fields are rejected. Native OpenAPI and owning Postman
requests describe the same implementation contract.

- `GET /projects/{id}/intake`: source configs, collection history and previews.
- `POST /projects/{id}/intake/sources`: `expectedRevision`, `configuration`.
- `PUT /intake/sources/{id}`: project/source expected revisions, configuration.
- `POST /intake/sources/{id}/collections`: `requestId`, `expectedSourceRevision`.
  Returns the durable collection receipt; `state=failed` means no complete coverage.
- `GET /intake/candidates/{id}`: current preview, immutable observations and decisions.
- `POST /intake/candidates/{id}/actions`: request ID, candidate/project revisions,
  action (`verify`, `dismiss`, `reopen`, `file`, `link`), reason, nullable `taskId`,
  `statusId` and `taskTitle`. Only `link` accepts task ID; only `file` accepts
  title and column. Verification and all triage decisions require a rationale.

`Intake:MaximumCollectionBytes`, `Intake:MaximumReviewFindings` and
`Intake:CollectionTimeoutSeconds` are explicit
operator-configured coverage limits. Reaching a limit reports incomplete coverage;
there is no sample-data fallback. Existing GitHub timeout/response/page limits
also apply. No existing project automatically adopts any source.
