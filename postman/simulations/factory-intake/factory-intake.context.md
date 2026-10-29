# GitHub issue intake simulation

Local consumed subset of the official GitHub Issues REST contract (2026-03-10).
Orbit search/integration confirmed list pagination, label filters and the
`pull_request` discriminator. Its brief only covered listing; official GitHub
single-issue documentation supplied the fresh identity/content read contract.
Owning-mock discovery required unavailable Postman sign-in, so the mock was
generated from the checked-in subset and adapted to an explicit local fixture.

Run `python3 scripts/factory-intake-regression.py`. The runner starts Postman
simulation, resolves its emitted listening address including `/github`, then
starts the real .NET adapter and native DuckDB. The fixture is supplied through
`CANOPY_INTAKE_FIXTURE`; no application sample fallback exists. Do not deploy this
mock: it intentionally reads local fixture files. Unsupported calls return 501.

The regression proves real paginated HTTP reads, PR/label/closed filtering,
immutable IDs, exact retry deduplication, malformed/error/HTML/hanging/oversized
responses, truncated/duplicate pagination, aggregate byte and time limits, source
edit races, provider-binding adoption, actual review-record recurrence, explicit
filing/linking and native restart/interruption recovery. The member's NDJSON
request events include `/issues` traffic; it is not a stubbed application client.
All provider calls are GET. Real GitHub permissions and rate-limit availability
still depend on the configured operator credentials and repository.

`--serve` retains the isolated fixture for browser and Postman checks and writes
its address and ownership into `/private/tmp/canopy-intake-browser-fixture.json`.
Create `stop-browser` inside that explicit scratch directory to let the runner
clean its project through public APIs and stop its owned processes. Mocks do not
reload; restart the simulation after changing its handler. No remote workspace
push, shared mock deployment or real GitHub mutation is part of validation.
