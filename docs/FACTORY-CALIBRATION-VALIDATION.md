# Post-delivery calibration validation

The implementation uses public HTTP endpoints, native DuckDB through the pinned
public EFCoreProvider, actual Git repositories and owned OS processes. GitHub and
Codex/Claude responses are local protocol fixtures. These checks establish
eligibility, lifetime, isolation and canonical file behavior; they do not establish
live-model learning quality, billing or a production calibrator installation.

## Local results (31 October 2026)

- Calibration/delivery native/API/process/canonical-file checks: **61 passed**;
  saved Postman assertions against that actual API: **13 passed**.
- Existing shared agent regression: **29 passed**. Client tests: **99 passed**.
- Prior-schema native migration/update/restart regression passed. Existing agent
  fields/indexes and missing-usage semantics survive unchanged; the new nullable
  association remains absent from legacy JSON and new calibration tables start empty.
- Client and backend builds passed. Actual generated OpenAPI, saved OpenAPI syntax
  and saved collection lint passed.

## Reproducible checks

- `dotnet build Planner.sln --configuration Debug`
- `dotnet run --project tests/Planner.CalibrationMigrationRegression`
- `python3 scripts/factory-calibration-regression.py --postman`
- `python3 scripts/agent-regression.py`
- `npm test` and `npm run build` in `client/`.
- `python3 scripts/ci-native.py --suite calibration` runs calibration and its
  saved collection in a separate protected matrix group. Existing core, provider,
  deployment and parallel groups remain required by the aggregate.

The calibration suite first completes an actual controlled delivery with real
commits, verification command, criterion verdict, independent approval and merge.
It does not inject a successful native delivery or learner outcome. It proves:

- Disabled/missing scope, current installed identities, no dispatch on reads or
  policy saves, concurrent idempotent capture and strict request/origin boundaries.
- Fresh merged PR/check identity after branch deletion, actual current target
  ancestry, retained native QA/review proof, stale revision/proposal rejection.
- Bounded complete packets, item/byte limits and canonical skill version capture.
- Actual mirror-only edits, exact diffs, separate Git objects/private refs,
  shared provider settings, unknown usage and blocked standalone continuation.
- Product, ignored file, empty directory, ignored/mirrored FIFO, Git config, packet, deletion, mode,
  pinned-ref, commit and oversized edits fail without canonical adoption.
- Real canonical byte changes remain preserved; guarded adoption and persisted
  before/after hashes survive restart without another invocation or write.
- A real second target directory permission failure after a successful first
  write produces a partial outcome. Read-only reconciliation and explicit
  acknowledgment never roll it back or silently retry the remaining target.
- Already-desired bytes, external conflicts, edit-root revocation, process cancel,
  policy revocation, descendant cleanup and abrupt backend death are distinct.
- Automatic adoption is opt-in. Enabling automatic execution skips historical
  deliveries; a new verified delivery creates one native observation/learner.
  A real provider-request barrier changes policy during fresh I/O and proves that
  revoked automatic authority cannot queue a learner.

Test projects and their native records clean up through public APIs. Temporary
skill fixtures never become runtime sources or write to installed private skills.

## Browser interaction

A disposable native workspace exposes the actual proposed mirror diff and a
verified delivery. The browser opens project calibration policy, saves unchanged
settings without another invocation, inspects selected targets/evidence, and adopts
one changed skill explicitly. The canonical file and recorded after hash agree.
The unchanged second permitted target is excluded from adoption confirmation.
Readiness refreshes after completion; no model invocation occurs during adoption.

Desktop and narrow browser inspection covers long canonical paths/hashes, diff,
retained workspace and proposal confirmation. This is browser coverage, not
physical-device proof. A 390×844 viewport at the existing 90% zoom measured
433 CSS pixels with equal document width; the calibration panel also had equal
client/scroll width and wrapped paths/hashes. Screenshots and private native receipts are retained with
the local deployment evidence.

## Review corrections and scope

Review fixed automatic-policy activation races across provider I/O, copied skills
into a separate repository rather than the product object store, audited ignored
files/empty directories/static Git metadata, and verified actual diffs independently
of the index. Nonblocking no-follow opens reject actual named pipes before hashing;
calibration bypasses the shared human-readable Git diff collector and retains its
own complete audited proposal. The automatic test waits through the valid
prepared/observing interval before asserting a finished proposal and receipt.
Child processes settle before parent completion, and native intents
precede dispatch and canonical writes. Actual generated OpenAPI caught optional
DateTime defaults that broke generation; required native timestamps fixed it.
The run inspector now gives delivery, review and repair controls distinct sibling
keys; policy refresh no longer duplicates controls. Adoption confirmation lists
only changed targets, wraps paths and distinguishes file writes from learner work.

No real calibrator or canonical edit roots are enabled in the live app. CLI sandbox
behavior remains the execution security boundary; filesystem audits are not an OS
sandbox. Skill lesson quality remains an external learner/operator judgment.
Authentication/team access, skill installation, memory edits and publication retain
separate scope and authority.
