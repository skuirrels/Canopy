# Installed skill validation

Validated on 30 October 2026 using actual local files, native DuckDB, public APIs
and the running browser. Discovery adds no database tables or migration and
preserves the public EFCoreProvider path.

## Automated checks

- Backend build: zero warnings/errors. Client production build passed with the
  existing Vite large-chunk advisory. EF reported no pending model changes; its
  installed CLI 10.0.0/runtime 10.0.13 version advisory remains.
- 21 skill-discovery checks passed using real files and isolated native processes:
  relative-root resolution, exact source identity, actual hashes, read-only refresh,
  immutable captures, changed-file dispatch blocking, selected skill injection,
  actual command assertions, restart, explicit/root deduplication, ambiguous paths,
  successful empty reads, symlink files/directories, invalid UTF-8, oversized
  files, declared depth, missing roots and directory/entry scan bounds.
- Existing regressions passed: configuration 50, preparation 39, execution 36,
  independent review 28 and client 99. Combined with discovery: 273 checks.
- An isolated content-root test verified optional machine-local settings, higher
  environment precedence and highest command-line listener precedence. Test
  projects were deleted through the public API and owned processes stopped.
- Saved Postman collection: five requests / nine assertions passed against the
  actual native API. Observations and refresh leave the full workspace unchanged.
  The owning API and regression collections lint without warnings/errors, and the
  owning OpenAPI syntax lint reports no issues. Actual available, incomplete,
  foreign-origin and missing-project examples are saved. No remote publication
  or external-service mock is needed for this filesystem catalogue.

## Real provider pilot

A bounded isolated pipeline used the actual installed
`avoid-hardcoded-domain-values/SKILL.md` from an explicitly configured real root.
Its captured SHA-256 matched the installed file bytes. Actual Codex implementation,
three unchanged Python assertions and a separate read-only Codex review ran through
Canopy's existing dispatch path. No model ID was invented or overridden.

The first live review found generated Python bytecode in the fixture's raw change.
That finding was recorded as request-changes with a stable ID; no approval was
inferred from successful processes. After adding the fixture's bytecode ignore
rule, a fresh isolated pilot completed implementation and actual tests, and its
real reviewer found no defects. The generic implementation was inspected, current
QA and explicit review approval were recorded, and task workflow metadata stayed
unchanged. The installed skill file itself was unchanged. No GitHub mutations,
release, deployment or live workspace test project were used for the pilot.

This verifies one actual installed skill/provider path. It does not establish
compatibility of every skill with every provider, model or task. Other automated
agent responses remain explicit protocol fixtures rather than live model calls.

## Browser and code review

Browser checks cover search, selecting a discovered entry, saving/reloading a
draft, unchanged published versions, changed-hash refresh indication and task-only
skill overrides. These actions retain the existing run count and never execute.
Missing/manual references remain editable. Paths and hashes are inspectable in
progressive disclosures, so long paths do not squeeze card contents in the narrow
React Flow inspector. Existing canvas/list navigation remains intact.

Settled scoped WCAG 2 A/AA and 2.1 A/AA axe checks pass in desktop light/dark and a
390 × 844 viewport with no horizontal overflow. Scrollable results are keyboard
focusable. Screenshots under ignored `output/playwright/factory-skills/` were
inspected; viewport emulation is not physical-phone certification. The Playwright
CLI wrapper was unavailable through its package/network cache, so available
Playwright browser tools drove verification.

Review checked configuration precedence, deterministic identity/fingerprints,
source failure semantics, duplicate resolution, bounded traversal, content limits,
unchanged capture/dispatch checks, request cancellation and selection-only writes.
Fixes include missing-directory diagnostics, bounded heading display, explicit
no-source wording, keyboard scrolling and stacked result cards. Runtime and picker
use one resolver and the existing Markdown reader; no fallback content or guessed
installation paths were added. Newly introduced limits are deployment configuration,
existing Markdown/name limits or declared discovery depth. Source hashes always
come from actual bytes.
