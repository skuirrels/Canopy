# Acceptance QA validation — 29 October 2026

## Scope and review

Task criteria are explicitly adopted and captured in immutable work orders.
Command verification receipts and append-only operator verdicts use native
DuckDB through EFCoreProvider. The React Flow inspector explains per-criterion
coverage, evidence, history and freshness. Task workflow state stays independent.

Review corrected these material issues before delivery:

- Human-readable Git helpers truncate output. Verification uses complete NUL
  enumeration and streamed file hashing, so omitted source cannot appear current.
- Ignored asserting-test files could escape freshness tracking. QA now requires
  tracked or non-ignored test files, an existing nonblank line, content hashes,
  assertion explanation and a negative/failure case.
- Reads now check command evidence integrity as well as context freshness.
- Command execution deadlines start after fingerprint inspection, preserving the
  configured command's execution time budget.
- Malformed, foreign, missing and traversal test references return validation
  failures; symlink/submodule inputs expose unavailable context.
- Uncertain verdict responses retain the original payload and request ID for
  reconciliation. Criterion draft recovery preserves IDs and requires explicit
  adoption; concurrent server edits cannot be silently replaced.

## Automated evidence

- Debug and Release .NET builds: clean.
- Client production build and 96 client tests: passed; existing large-bundle
  advisory remains.
- Native HTTP/Git/DuckDB acceptance QA regression: 29 checks passed. Includes
  real passing and deliberately failing assertions, no implicit acceptance,
  stable IDs, malformed edits, concurrent idempotency, changed code/HEAD/config,
  unsupported symlinks, unstable source-mutating commands, failed evidence,
  missing worktrees, restart persistence and transactional project cleanup.
- Existing preparation regression: 39 checks passed.
- Existing execution regression: 36 checks passed, including cancellation,
  interruption recovery and manual gates. Provider output in that suite is an
  explicit CLI protocol fixture, not a paid live-provider call.
- Saved Postman Factory regression: 24 requests / 38 assertions passed against
  the isolated backend, with reporting disabled; collection lint passed.
- Refreshed generated OpenAPI and criteria/QA request artifacts with real
  isolated API response examples and parameterized caller inputs;
  generated collection lint and OpenAPI syntax validation passed. The local
  Postman CLI printed a macOS keychain diagnostic after reporting no syntax
  issues; this is not evidence of remote workspace publication.
- Upgrade from the previous primary-checkout binary into the new migration:
  existing prepared work order and stage/step IDs, standalone agent session,
  result/model history and complete workspace preserved.

## Running browser evidence

Verified the isolated app in a real Chromium browser:

- Criterion editing/saving keeps IDs; unsaved edits disable readiness.
- New criteria start flagged for clarification. Draft survives leaving the
  task and reloading; explicit discard returns to adopted server criteria.
- Recorded pass with an actual asserting test and real command output.
- Invalid test path reports an error without adding a verdict.
- Criteria edits make prior QA stale while preserving captured meaning.
- Evidence link selects the actual producing step and activity output.
- Simulated a lost POST response after the server committed. The form froze
  the pending payload; retry appended no duplicate decision.
- Desktop light/dark and a 390×844 phone viewport remain usable. Phone viewport
  had no horizontal page overflow. Axe WCAG A/AA checks found zero violations
  in the tested execution view and expanded form in both themes and phone view.

Screenshots are local ignored artifacts under
`output/playwright/factory-quality/`. Phone viewport checks are not physical-device
certification. Expected network-console errors came from the deliberate 400
validation and aborted-response scenarios.

## Limits

This is explicit operator QA. File/line checks and process outcomes do not judge
semantic relevance, automatically prove test strength, or grant review/merge
approval. Physical file/screenshot collection, independent agent review and
repair loops remain separate roadmap work.

Freshness covers the retained Git worktree, captured work order, selected skill
contents and explicitly referenced environment inputs. Ignored dependencies,
transitive executable installations and external services are outside this local
fingerprint. Filesystem observations can change after a read; mutation writes
recheck context and the UI provides explicit refresh. No OS isolation is added.

All regression/browser projects use temporary databases and repositories. No
live task data or production credentials are used for these scenarios.
