# Software Factory phase 2 validation

Validated on 29 October 2026 in the managed feature worktree. All running UI
checks used a separate loopback backend and temporary native DuckDB database;
the live workspace and unrelated primary-checkout edits were not used.

| Check | Result |
| --- | --- |
| Client tests | 93 passed, including 9 new factory tests |
| Production frontend build | Passed; existing large-chunk warning remains |
| Backend build | Passed without warnings/errors |
| Factory API/DuckDB regression | 50 passed |
| Maintained Factory Postman regression | 12 requests, 20 assertions passed locally |
| Browser workflow checks | 15 passed |
| Extended canvas/concurrency/responsive checks | 14 passed |
| Browser restart/adoption/cleanup checks | 11 passed |
| Custom creation and keyboard connections | Passed through the actual UI/API |
| Automated WCAG A/AA scan | No violations in the tested factory workspace in either theme |

Browser checks used Chromium, including desktop, tablet and phone viewport
emulation. These are not physical-device validation. The unavailable bundled
CLI was replaced by the browser connector and the installed Playwright runtime;
no browser-test dependency was added to the application.

## Evidence and boundaries

The running interface created configured templates and custom pipelines, edited
commands/skills/stages, validated missing data, saved drafts and published two
immutable versions. Task overrides persisted through the same API; version two
never replaced a pinned version until explicit adoption. Invalid artifact wiring
could not overwrite a saved draft. Status/hierarchy remained unchanged and no
agent dispatch occurred.

Pointer tests dragged stage grips, connected dependency handles and rejected a
cycle. Arrangement and ELK auto-arrange left the authoritative configuration
unchanged. Keyboard Enter activation created a dependency which persisted through
the ordinary save path. Phone users could create an ordinary task from the
factory library and use the list/settings editor.

Two browser tabs verified revision conflicts and recovery after reload: local
edits survived, newer server data was preserved and stale recovery could not be
saved over it. The backend was stopped and restarted with the same temporary
DuckDB file; pipeline/version and task-configuration resources matched their
pre-restart snapshots exactly. Adoption, detaching, archiving and cleanup were
then exercised through the UI/API.

Local screenshots and accessibility results are under
`output/playwright/factory-e2e/` in the validation worktree (ignored artifacts).
No model calls, pushes from agents, application deployment or live database
restart/migration formed part of these checks.

## Code review

Reviewed API use, immutable-version editing, revision capture, cache recovery,
state boundaries, dependency direction, pointer/keyboard behavior, missing-data
labels and responsive rendering. Fixes include:

- Guard validation state while the first pipeline is loading.
- Compare configurations independently of object key order and optional nulls,
  preserving successful validation and clearing genuinely saved dirty state.
- Reconcile an already-saved recovery copy with its current server revision,
  while retaining original revisions for genuinely stale edits.
- Prevent canvas step clicks from being replaced by parent-node selection.
- Make connection handles keyboard accessible and correct light-theme contrast.
- Prevent task attachment/detachment from overwriting a configuration changed
  elsewhere, using the reread resource's project revision.
- Reject duplicate environment-reference names in the editor rather than
  silently replacing another entry.

No blocking findings remained. Template/domain values continue to come from the
existing configured catalogue and explicit API/user inputs. New constants are
UI geometry or values of the established typed protocol. Camera/arrangement and
unsaved recovery remain browser preferences; the saved configuration stays in
native DuckDB. No backend contract, migration, provider path or execution policy
changed in this phase. Skill availability and execution readiness remain
explicitly unverified; execution is a later phase.

## Release review — 29 October 2026

PR #11 was already merged when the follow-up review began. Review found one
configuration defect: the valid configured template ID `custom` collided with
the UI's custom-pipeline action. Template options now use a separate UI namespace
while the API receives the original configured ID. No catalogue entries or
backend validation rules were changed.

Real Chromium verified that selecting that configured template opens the
template flow, posts `templateId: "custom"` without a caller-supplied definition
or source, and persists the authoritative definition and source. The separate
custom-pipeline action still opens its explicit fields. The test catalogue and
database were temporary, and the browser reported no runtime errors.

Fresh checks passed: 93 client tests, production frontend build, Debug and Release
backend builds (zero warnings/errors), 50 factory persistence checks, 57 planning
checks, 18 linked-specification checks, 6 MCP tests and 20 maintained Postman
assertions. The planning suite was rerun serially after an initial
whole-workspace snapshot was disturbed by concurrent browser fixture edits.
MCP tests passed after installing their unchanged lockfile dependencies.

The Release backend also passed the full 15 designer workflow checks, 14 extended
canvas/concurrency/responsive/accessibility checks and 11 restart/adoption/cleanup
checks again. The pipeline created from configured template ID `custom` matched
its original persisted snapshot exactly after restart. Automated accessibility
scans cover the tested workspace; viewport emulation does not establish physical
tablet/phone support.
