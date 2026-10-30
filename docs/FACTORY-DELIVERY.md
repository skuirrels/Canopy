# Controlled factory delivery

Delivery is opt-in project policy. Capturing a delivery executes nothing. The
candidate must be a clean committed run worktree with current passing criterion
QA and a current independent approval. Commit before verification and review;
committing afterwards changes HEAD and invalidates that evidence. Factory agents
still cannot push, merge, deploy or accept tasks.

Policy names the repository, local remote, target branch, allowed branch prefix,
draft/merge permissions, required GitHub check names and App IDs, and an optional
administrator-configured post-merge hook. There are no adopted policy defaults.
A delivery captures policy, task source, code/configuration fingerprint, review,
QA digest, commit/tree, target base, branch, environment and actual changed files
matching the configured sensitive/migration patterns. Patterns use case-sensitive
whole-relative-path `*`/`?` matching; [] explicitly means no classified paths. Subsequent policy changes revoke
future actions until a new delivery is captured. Sensitive-change and migration
notes accompany the explicit revision-bound human approval.

Publication creates a new remote branch without overwriting another head, then a
draft PR with a durable delivery marker. Merge is a separate action after the PR
has been marked ready on GitHub. Required checks must succeed from their configured
App IDs. Merge requires strict branch protection, enforcement for administrators,
no review bypass allowances, and a branch containing the observed target base.
Missing, unavailable, ambiguous or truncated provider observations block the gate.
Only merge commits are supported so the result can be reconciled against both
parents and the verified tree; merge queues and alternate methods are explicit gaps.

Remote requests have persistent intents before dispatch. Exact request retries
return the existing operation. Lost responses and backend restart never replay a
mutation. Explicit reconciliation looks up the PR marker, asynchronous merge
result, actual merge commit and target ancestry. A queued/enqueued response is
not delivery success. The provider's head guard and server-side strict checks
protect against other writers; a local lock is not remote concurrency control.
Observed head/base changes invalidate approval. If an in-flight base race changes
the recorded parents, it remains visible as needs-attention rather than completed.

Post-merge hooks are separately approved and selected from deployment configuration,
not supplied as arbitrary API commands. They run with the merge SHA as an explicit
environment input. Receipts bind command outcome, actual changed instance identity,
expected health JSON, the actual running commit revision and canonical persistence
snapshot hashes. The captured hook/provider configuration hashes must also match. Hook integrity
includes the resolved executable and explicitly declared `integrityFiles`; list
all static scripts/supporting command sources there. Mutable runtime data is not
an integrity source. Unknown/interrupted
hook outcomes require observation and never automatically repeat a restart. A
self-restart can therefore be reconciled after the backend returns. A failed or
missing health/persistence check remains an operational follow-up.

## API contract

All routes share `/api/factory`, loopback/same-origin enforcement and no-store.
Unknown input fields are rejected. Policy uses project revisions; capture uses
run revisions; subsequent actions use delivery revisions. Mutations take a unique
requestId with exact content replay. Conflicts are 409, malformed inputs 400,
missing resources 404, unavailable provider observations 503.

- GET/PUT `/projects/{id}/delivery-policy`: adopted policy and configured hook names.
- GET `/runs/{id}/delivery`: local evidence readiness and task delivery history.
- POST `/runs/{id}/deliveries`: `{requestId,expectedRevision,branchName}` captures.
- GET `/deliveries/{id}`: captured evidence, provider readiness and operation receipts.
- POST `/deliveries/{id}/actions`: `{requestId,expectedRevision,action,approvalNote}`;
  action is publish, approveMerge, merge, approvePostMerge, postMerge, reconcile or close.

Action dispatch is asynchronous with a persistent journal. A restart interrupts
queued/in-flight operations; neither implies permission to replay. Closing a
record retains its history and does not close a remote PR or reverse a merge.
Task workflow status stays independent. Open deliveries reserve task execution
and block deletion/connection rebinding until explicitly closed or completed.

## Provider contracts

The Postman Orbit discovery endpoint returned 403 and the owning-mock search
required an unavailable Postman login. The adapter and local stateful simulation
therefore use GitHub's official contracts:

- https://docs.github.com/en/rest/pulls/pulls
- https://docs.github.com/en/rest/branches/branch-protection
- https://docs.github.com/en/rest/checks/runs
- https://docs.github.com/en/rest/git/commits
- https://docs.github.com/en/graphql/reference/pulls#pullrequest
- https://docs.github.com/en/rest/about-the-rest-api/breaking-changes
- https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-protected-branches/about-protected-branches

Credentials come from the configured environment-variable name or local `gh auth`
for the configured host. Tokens and resolved environment values are never returned
or stored. Provider URLs, request limits, polling and hook execution/probes are
deployment configuration. The application never weakens repository protections.

REST version 2026-03-10 omits `merge_commit_sha`. Merged PR observations use a
read-only query at the configured provider root's `/graphql` endpoint for the
actual `mergeCommit.oid`. Repository, PR number, merged state and head must match
the REST observation. GraphQL errors, partial/unavailable identities and mismatches
block reconciliation. The actual commit's tree, two parents and target ancestry
are still verified; no SHA is inferred from a branch tip or substituted literal.

## Deployment configuration

`Delivery` in backend configuration pins the API root/version and `GitHubHost`,
credential source, timeout, response-size and pagination limits. The default
configuration adopts no project authority and defines no post-merge commands.
The GitHub credential must be able to read repository metadata, checks and branch
protection; publication and merge additionally need the corresponding write
permissions. Unavailable protection observations block merging, including when
credential permissions are insufficient. No repository rules are changed here.

Each configured hook supplies `id`, `name`, `executable`, `arguments`, absolute
`workingDirectory`, `timeoutSeconds`, `integrityFiles`, `healthUrl`,
`expectedHealthJson`, `instanceJsonPointer`, `revisionJsonPointer` and `snapshotUrl`.
Health must report a real changing instance identity and actual deployed commit.
Snapshots are canonicalized by object keys and, where all array entries have
string IDs, by those IDs. No desired snapshot, instance or revision is fabricated.
The Canopy health API exposes a startup instance ID and the build's Git revision;
an unavailable build revision is null rather than a guessed release identifier.

Configure static scripts and their supporting command sources in `integrityFiles`.
Their hashes and the resolved executable hash are captured, so editing a script
or changing the binary/configuration invalidates existing action authority.
Hook probe URLs and all command inputs come from deployment configuration; the
API cannot submit arbitrary commands. Hooks must handle the explicit
`CANOPY_MERGE_SHA`, `CANOPY_DELIVERY_ID` and `CANOPY_REPOSITORY` inputs.
