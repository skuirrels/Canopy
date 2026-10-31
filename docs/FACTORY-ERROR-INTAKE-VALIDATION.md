# Structured application-error intake validation

Validated against real temporary structured files and native DuckDB through the
public HTTP APIs. No production error logs, model invocations or live user tasks
were test inputs. The public EFCoreProvider version and deployment model are unchanged.

## Reproducible checks

- `python3 scripts/factory-error-intake-regression.py --postman`: 55 API/file/native
  checks and 8 saved Postman assertions. Exercises configured scope, exact duplicates,
  recurrence thresholds, full receipts, absent/empty/partial/stale/malformed/oversized
  coverage, duplicate JSON fields, explicit offsets, symlinks and nonblocking FIFO
  rejection. Unknown raw context fields are rejected.
- Native restart precedes internally consistent lifetime event conflicts: changing
  a retained event's release or assigning it to another group fails the collection.
  Candidates, observations, decisions and prior receipts remain unchanged; current
  triage authority becomes unavailable. Different configured scopes may use the same
  upstream IDs independently.
- Exact concurrent collection and filing retries retain one outcome/task. Verification
  is required; metadata-only exports need recollection but unchanged group evidence
  stays verified. New occurrence evidence revokes verification. Empty windows do not
  mark problems fixed. Explicit linking preserves existing task metadata, cross-project
  links fail, deleted linked tasks cannot be filed again, removed feeds can be disabled.
- `dotnet run --project tests/Planner.ErrorIntakeMigrationRegression`: creates the
  prior release's schema, retains source configuration bytes, scope/binding, verified
  candidates, immutable observations and decisions across upgrade. New tables start
  empty. Existing sources remain editable after upgrade and restart.
- Existing GitHub/review intake regression: 37 checks against the existing Postman
  simulation, including dependency HTTP evidence and unavailable coverage behavior.
- Client: 99 tests and production build passed. Backend build passed. Saved collection
  lint (5 requests) and OpenAPI syntax lint passed. CI runs the new native migration
  check in core and structured-feed/Postman regression in providers.

## Running interface

Used the isolated fixture's real backend and compiled frontend. Selected Problem
intake, inspected the configured feed, edited and saved its source name, observed
immediate stale authority and a disabled verification action, collected again and
verified with an operator rationale. Opened the filing confirmation for an explicitly
selected incomplete column and cancelled; no task was created by previewing. Expanded
actual code frames and distinct event identities. Desktop and 390-pixel viewport
checks showed no document overflow; browser zoom produced a 433 CSS-pixel viewport.
No browser console errors were reported. This is browser responsive testing, not a
physical-device claim. Test projects were deleted through the public APIs.

## Review changes

Review added a persistent event identity ledger because within-file validation alone
could accept an ID reassigned in a later export. Snapshot parsing rejects ambiguous
whitespace IDs, duplicate fields, numeric-string schema versions and offsetless
timestamps. File open is nonblocking on supported POSIX hosts so a FIFO cannot hang
before managed cancellation. Source edits refresh selected candidate details. Filed
work uses concise source summaries and immutable evidence references, so large
producer messages/event lists do not overflow the task specification.

## Limits

This implements a configured complete-file adapter, manual collection and explicit
triage. The producer must supply real immutable IDs, atomic complete exports and
sanitized text; Canopy does not certify sanitization from a flag. Hash checks provide
sampled evidence, not a lock against external writers. macOS/Linux adapters are
supported; other platforms report unavailable capability. History and identity
hashes persist until project deletion. No telemetry agent, automatic exporter,
automatic remediation or production-feed validation is claimed.
