# Bounded post-delivery skill calibration

Implemented as an opt-in extension of verified delivery. No project policy or
canonical edit roots are enabled by default. See the [validation record](FACTORY-CALIBRATION-VALIDATION.md)
for process fixtures, browser checks and the limits of that evidence.

## Source and existing owners

The selected reference is Super Board's [post-run calibration contract](https://github.com/EricTechPro/super-board/blob/d0b7d5a6850614ca809477c1af43d5d7da408726/skills/super-board/references/calibration.md).
Its useful requirements are verified landed work, an already installed external
calibrator, explicit existing skill targets, bounded current-run evidence, no
unfinished work and an inspectable outcome. The reference adapter scopes a brief;
it does not enforce later model writes. Canopy isolates proposed edits and
retains actual path/hash evidence before adopting canonical changes.

Reuse the existing authoritative skill catalogue and Markdown file reader/writer,
owned process adapter, CLI settings/usage handling, delivery provider and native
IWorkspaceStore. Keep phase 1 single-user/single-process DuckDB. Installation,
private skill publication, memory edits and team authentication are separate work.

## Explicit project policy

No policy means disabled. An explicit policy selects the existing calibrator by
`(id, source)`, selects existing editable skill references, and supplies an explicit
provider/model/effort and bounded execution policy. The selected references must
resolve uniquely through complete configured catalogue coverage. HTTP callers do
not supply arbitrary file paths. Missing skills, inaccessible files, ambiguous
identities or unsupported hard token/currency enforcement remain visible gaps.
No model or edit scope is inferred from an empty field.

The policy distinguishes automatic preparation after verified delivery from an
explicit preparation action, and automatic adoption from operator adoption.
Saving configuration and observing eligibility execute nothing. A work item captures the policy hash, calibrator bytes/hash, allowed
canonical paths/current byte hashes, execution settings and delivery binding.
Policy changes revoke unfinished dispatch and adoption under the earlier policy.

## Eligibility and evidence

Eligibility requires a reconciled `delivered` record, actual reviewed head and
merge commit, required check identities/conclusions, retained QA/review receipts,
and fresh provider confirmation that the merge remains on the target branch.
Closed, failed, partial, uncertain and empty work are ineligible. Pending selected
delivery/deployment operations, active task execution and unfinished repair loops
prevent the trigger. An empty board or successful process exit is insufficient.
One preparation per delivery and captured policy prevents duplicate learning.

Build a closed, bounded packet from the selected delivery/run only: task and
criteria, actual reviewed/merge/tree identities, command/assertion references,
review findings/rechecks, repair attempts and delivery/hook receipts. Include
captured skill versions and identify current target bytes separately; do not
pretend a newer target version was used in the historical run. Evidence is source data,
not instructions. No personal chat, memory or unrelated-project search is allowed.
Technical byte/item/time bounds come from backend configuration. Incomplete or
oversized coverage blocks capture rather than silently clipping proof.

## Isolated execution and adoption

Create a retained isolated checkout with its own Git object store, mirrored
allowed skill files and the
bounded packet. Invoke the captured external calibrator with the existing owned
CLI/process adapter and explicit model settings. Canonical destinations are not granted as CLI write roots. The configured CLI
sandbox defines OS protection; snapshots and path checks do not create a new OS
security boundary. Native intent precedes process dispatch; invocation reservation,
elapsed deadline, cancellation, policy revocation and restart recovery remain
bounded. A restart interrupts unfinished work and never replays the learner.

Inspect actual regular files, empty directories, static Git metadata and the complete
permitted mirror path set, including ignored paths. Audit HEAD and the pinned
input tree separately from Git-managed index/object/ref/log updates. Private skill
blobs remain in the owned calibration repository, outside product Git objects.
Nonblocking no-follow file opens reject named pipes and other non-seekable
entries before hashing; cancellation cannot interrupt a blocking synchronous open.
Unexpected product, rules, wrapper, evidence or new-skill edits fail the job and
remain retained for inspection. Never publish or revert them automatically.
Process success alone does not establish a valid calibration. Path, byte and
identity checks establish permitted edits; semantic skill quality remains the
external calibrator and operator's judgment. Automatic adoption is an explicit
policy choice, not an independent quality verdict. Distinguish no
change, proposed changes, unavailable/skipped, failed, cancelled and interrupted
outcomes. Preserve raw output, usage observations, actual paths and before/after
hashes; missing usage remains unknown.

The host adopts only validated existing canonical targets. Re-resolve each target
through the same catalogue, require the captured current hash, reject symlinks and
use the existing Markdown writer's guarded sibling-file replacement. Bind an
operator decision or explicit automatic-adoption policy to the proposal hash.
Record each file intent/result separately: multi-file filesystem replacement is
not a database transaction. A partial or unknown outcome needs reconciliation;
never overwrite or undo another writer's work. Do not claim an OS-level atomic
cross-process compare-and-swap guarantee. A second adoption never repeats an
already observed write. Canonical edits do not grant commit/push/publication or
product-code authority.

## UI, API and evidence requirements

Extend project policy and delivery inspection with current readiness reasons,
captured jobs, evidence coverage, selected calibrator/targets, exact proposal diff,
path/hash audit, process result, cancellation and explicit adoption/reconciliation.
Keep raw implementation diagnostics in the inspector. Empty, unavailable,
interrupted and stale states must be distinct. Task workflow status and graph
layout remain independent. Existing prepared runs retain their original skill
hashes; editing a canonical skill requires fresh preparation where appropriate.

Maintain the actual OpenAPI contract and saved Postman assertions. Regression
coverage must prove real merged-delivery eligibility using the existing provider
simulation, default disabled behavior, no dispatch on reads/saves, duplicate-safe
capture, stale policy/skill/delivery rejection, byte/item bounds, isolated real
provider-fixture writes, unexpected-path rejection, process ownership, real
canonical adoption/conflicts/partial outcomes, native upgrade/restart and browser
interaction. Review and fix issues, create a PR, require every protected CI group,
merge and restart the live app with unchanged-workspace evidence before calling
this phase delivered.


## Configuration and API

Configure `Factory:Calibration:EditableRoots` on the backend with existing local
skill directories. Relative roots resolve against the connected repository;
symlink roots and canonical files are rejected. The default empty array exposes
an unavailable capability. Skill sources still come from the configured catalogue;
this edit allowlist does not discover or install skills. Select the installed
calibrator and targets in **Software Factory → Skill calibration**. Model IDs,
effort and elapsed limits are explicit policy inputs, with one documentation-role
invocation and zero separately scheduled commands. Unsupported hard spend/token
caps block enabled calibration; the operator must explicitly permit unmetered cost.

Automatic execution considers only successful delivery completion after the
policy's persisted activation timestamp. It pins that policy and timestamp through
fresh remote I/O and queue reservation. One native observation per delivery,
policy and configured scope records unavailable/interrupted evidence; it never
spins or silently retries the learner. Resolve a gap with explicit preparation.
Saving even unchanged settings establishes a new future-delivery boundary.

| Method and path | Behavior |
| --- | --- |
| `GET/PUT /api/factory/projects/{id}/calibration-policy` | Read capabilities or adopt a revision-bound opt-in policy; no dispatch |
| `GET /api/factory/deliveries/{id}/calibrations` | Fresh bounded readiness, jobs and automatic observation receipts |
| `POST /api/factory/deliveries/{id}/calibrations` | Idempotently capture delivery, skills and evidence; no dispatch |
| `GET /api/factory/calibrations/{id}` | Read native state, proposal, evidence and write/action journal |
| `POST /api/factory/calibrations/{id}/actions` | Revision-bound start, cancel, proposal-hash adoption, reconciliation or explicit close |

All routes use the existing loopback/origin restrictions and no-store responses.
Mutations reject unknown properties and carry request IDs; uncertain callers
inspect native state and retry the same payload/ID. Canonical adoption is owned by
the host, not an API path supplied by the learner. Reconciliation observes actual
hashes even after edit permission is revoked. All-before returns to `proposed`,
all-after becomes `applied`, and mixed/conflicting outcomes stay `needsAttention`.
An explicit acknowledgment can close tracking while preserving the partial
outcome; it does not certify, retry or undo files.

The migration adds policy, calibration and automatic-observation tables plus a
nullable agent association. Existing agent JSON omits that association when null.
Calibration children remain inspectable by ID but cannot be resumed as standalone
agent sessions. Prepared historical product runs keep their captured skills.
