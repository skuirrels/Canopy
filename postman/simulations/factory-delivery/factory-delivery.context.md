# Factory delivery integration simulation

Local-only GitHub contract subset, generated from the checked-in consumed
OpenAPI subset and made stateful. Owning-mock discovery required unavailable
Postman sign-in; official GitHub contracts are the source. Do not deploy this
mock: it deliberately reads an explicit local fixture file and actual fixture
Git objects via child_process. It contains no runtime application fallback data.

Run `python3 scripts/factory-delivery-regression.py`. The script starts Postman
simulation with `CANOPY_DELIVERY_FIXTURE` pointing to its temporary configuration,
uses the emitted listening event to resolve its API address, then runs the real
.NET adapter, native DuckDB, real HTTP Git transport and real application restart
probes against it. No real GitHub mutations or paid model calls occur.

The fixture controls provider outages, HTML failures, request hangs, missing and
failed checks, App identity, truncated coverage, strict-protection gaps, lost
create/merge responses, asynchronous merge outcomes, and pending responses with
a mismatched real target/base Git object or claimed rules bypass. Unhandled routes return
501. SHA/tree/parent values come from real Git objects. `__state` is a local-only
inspection route for checking mutation counts and unmerged fixture state; it is
not a GitHub API contract.

REST PR payloads intentionally omit `merge_commit_sha`, matching version
2026-03-10 observed in the live pilot. `/graphql` models only the consumed
read-only merged-PR identity query. Merge OIDs come from actual fixture Git
objects. Partial GraphQL errors, missing commits, wrong repository/PR/head,
unmerged responses and incorrect commit ancestry must block reconciliation and
must never repeat a lost merge mutation.

No remote Postman workspace push or mock deployment is needed. Inspect the
simulation NDJSON request events to establish that the actual adapter used this
provider. Restart the simulation after editing the handler; mocks do not reload.

Merged publication-branch cleanup preserves the actual head OID captured from the
real Git ref at PR creation. REST and GraphQL continue to identify that commit
after its branch is deleted. This is PR metadata, not an invented SHA fallback.
Calibration uses the reviewed commit's check runs after branch cleanup and proves
current target ancestry again through this same provider simulation.

The local-only `holdCalibrationPr` fixture control pauses the repository observation
for one actually merged PR and signals a filesystem barrier. The regression edits
the native automatic policy during that real request, then releases it. This
proves that stale automatic authority cannot queue a learner after fresh I/O; it
does not bypass the adapter or inject a delivered/calibration record.
