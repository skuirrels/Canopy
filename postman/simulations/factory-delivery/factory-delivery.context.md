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

No remote Postman workspace push or mock deployment is needed. Inspect the
simulation NDJSON request events to establish that the actual adapter used this
provider. Restart the simulation after editing the handler; mocks do not reload.
