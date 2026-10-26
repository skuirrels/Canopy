# Validation

Canopy has been checked with the native DuckDB backend using the public API, not a browser-only store. Disposable projects and people are created explicitly for tests and removed afterward.

## Local checks

```sh
dotnet build Planner.sln
npm --prefix client ci
npm --prefix client run build
npm --prefix client test
npm --prefix mcp ci
npm --prefix mcp test
```

The frontend suite currently contains 74 passing checks for hierarchy views, component ordering, assignee preservation, independent effort, calendar arithmetic, timeline range, map orientation, readable project-colour accents and offline export safety. The MCP unit/compatibility suite contains 5 passing checks, including legacy protocol negotiation and specification handling.

## Integration checks

Run these against an isolated backend with a separate native database file:

```sh
Database__Path=/absolute/path/to/test.duckdb dotnet run --project server/Planner.Api --no-launch-profile -- --urls http://127.0.0.1:5275
```

In another terminal:

```sh
PLANNER_URL=http://127.0.0.1:5275 python3 scripts/api-regression.py
PLANNER_TEST_URL=http://127.0.0.1:5275 npm --prefix mcp run test:integration
```

Run mutation suites sequentially. The API suite has passed 57 checks, including hierarchy/dependency cycles, revision conflicts, component ordering, task metadata, multiple assignees, independent fractional effort, explicit clearing and invalid-value atomicity. The real stdio MCP suite has passed 13 checks, including Markdown upload and metadata preservation. Exact workspace comparisons across backend restart and populated migration checks have preserved pre-existing public fields.

Saved local Postman regressions have passed 32 requests and 47 assertions. The OpenAPI syntax and collections have linted successfully. No cloud mock or collection publication is required.

## Interface verification

Browser checks cover task autosave, nested component lists, component resequencing, assignee selection, all four views, Gantt date interactions and automatic centering on today. Mind Map opening and reload framing matches its Fit map to screen control in both orientations. Static HTML chart snapshots retain styling and work offline.

Responsive browser checks include 375, 390, 430 and 460 pixel widths. These do not establish physical iPhone/Safari touch, keyboard or notch behavior. Browser fullscreen permission behavior and native saved-PDF output still require manual device/browser verification.

Development and production Makefile targets have been started on isolated ports/databases. Checks covered Vite hot reload/proxy, Release static asset serving, occupied-port rejection, Ctrl+C shutdown and cleanup when a dev child fails.

Local screenshots, exports, database files and detailed private validation notes are excluded from this repository. No private workspace is seeded at startup.

## Project colour accents

The project picker includes React Flow hot pink. Accent tokens derive from the active project's existing persisted colour, including portalled dialogs and chart snapshots. Draft choices preview without writing; Cancel restores the saved colour. Contrast checks cover both themes, black, white, yellow and production CSS short hex notation. Browser checks covered pink/green previews, cancellation, project switching, Gantt highlights and both appearance themes. Existing project/task records were not edited for validation.

## Large task modal

Task details can expand from the side panel into a centered large modal. Both sizes use the same draft and Tiptap editor instance. The expanded view uses a compact property grid on desktop and a full-width specification editor; narrow screens retain scrolling fields without horizontal overflow. Focus is trapped while expanded, nested dialogs remain above it, and Escape returns to the side panel.

Frontend production build and 32 frontend checks passed. Running-browser validation on an isolated public-API fixture covered unsaved effort/specification retention across resizing, editor undo after resizing, nested assignee-dialog Escape, modal Escape, focus wrapping and a 375-by-667 viewport. Autosaved effort, dates and specification were checked through the public API; an exact workspace comparison across an isolated backend restart passed. Temporary test data and the test backend were cleaned up. The working local database was not used for these mutation tests.

## Expandable Gantt HTML

Downloaded Gantt HTML includes all rows in the current filtered/focused hierarchy, preserving the live chart's starting expansion state. Its embedded offline controller supports per-branch and global expansion, retains nested choices, reroutes dependency arrows and resizes the chart and print layout. Other snapshot controls remain inactive. A per-document script nonce allows only the export controller; network connections remain blocked by CSP. PDF capture continues to use the live visible branches.

The production build and 36 frontend checks passed. Tests execute the serialized controller with a DOM adapter and cover nested expansion, hidden dependency endpoints, routing in both date directions, chart height, dynamic A3 sizing and escaped metadata. A running-browser download from the collapsed example chart retained 23 exported task rows and left the live chart at five visible components. The compiled controller passed JavaScript syntax validation. Browser policy blocked opening a local file URL, so interaction with the downloaded file itself has not been visually verified.

## Branding, Help and focus controls

Canopy uses a matching tree icon in navigation, loading states, illustrations and the browser favicon. The SVG passed XML parsing and rendered in the running browser; expanded and collapsed navigation retained the project accent. Help fits a landscape desktop dialog without internal scrolling and stacks without horizontal overflow at 375 pixels. Exit focus restores the project root from a focused branch; the control remains visible and has a 44-pixel target in phone layouts, including component task lists. The Gantt HTML expand/collapse control is aligned to the right of its toolbar.

The final frontend production build and all 36 frontend checks passed. The working DuckDB remains ignored by Git and is outside the source changes.

## Gantt creation controls

Gantt offers New component in the toolbar and empty state, plus a task/subtask button on each row. Both actions reuse the existing creation dialog and public API; the row action preselects its parent and expands the branch. Creation controls are omitted from chart exports.

The frontend production build and 36 frontend tests pass. Running-browser checks against a separate DuckDB covered creation from the empty state and toolbar, correct parent selection, adding under a collapsed component and a 375-by-667 viewport with no page or toolbar overflow. The public workspace contained two root components and two correctly parented tasks, with dates left unset. Exact workspace comparison across an isolated backend restart passed. A downloaded HTML export retained all four rows and omitted creation buttons. The disposable project and backend were cleaned up; the working database was not used for these mutation tests.

## Workflow defaults and task Board

New projects use the five configured columns in order: To Do, In Progress, Blocked, In Testing and Done. The public-API maintenance script is dry-run by default, requires a recovery snapshot outside the repository to apply, preserves custom columns and uses explicit legacy mappings. Regression checks cover configured defaults, legacy task migration, task metadata, hierarchy, dependency record identities, custom stages, snapshots and idempotence. A populated isolated database copy and the working local backend both preserved their exact workspace state across restart after the update.

Board excludes root components and shows their tasks, including deeper subtasks. Each card resolves its top-level component even while filtered or focused. Frontend regression checks cover ancestor resolution, multiple components and malformed hierarchy data. Running-browser checks covered task-only card counts, nested component labels, component-name search, required parent selection in the creation dialog, and dragging a task to Blocked while preserving its component and metadata. The five configured columns rendered in order. The final production build and 39 frontend checks passed; the isolated native DuckDB API suite passed 48 checks and cleaned up its fixtures.

## Grab-to-pan in Components

The component plan supports mouse dragging on explicitly marked blank background and card surfaces. A horizontal movement threshold separates panning from clicks; pointer capture keeps the grab active outside the starting card. Text, buttons, drag handles, native scrollbars, modifier clicks and touch/pen events retain their existing behavior. Release, Escape, cancellation, lost capture, blur and unmount clear the gesture. Existing wheel, keyboard and touch scrolling remain available.

The production build and 44 frontend checks passed. Five controller regressions cover movement thresholds, click suppression, direction, bounds, gesture cancellation, cleanup and excluded inputs. Running-browser checks confirmed horizontal and reverse panning without changing component order, vertical card scrolling, text selection, task/specification dialogs and component expansion. Component reordering and task transfers were exercised with Undo on the isolated database copy; the working database was not used for those mutation checks.

## Assignees workspace page

Assignees opens a main-workspace list from the sidebar, workspace menu and avatar. Navigation highlights the current section, breadcrumbs identify it, and the section is restored after reload. The existing add operation is presented inline; the existing removal confirmation remains available. Names continue to come from the shared workspace data and native DuckDB backend.

The production build and 44 frontend checks passed. Browser checks covered all entry points, switching between projects/Overview/Assignees, empty-form validation, inline creation, removal cancellation and reload. A 375-by-667 browser check had no horizontal overflow, closed mobile navigation on selection and retained 44-pixel removal targets. The added test person persisted across an isolated backend restart; cleanup restored the exact pre-test workspace. Working assignee records were not edited.

## Daily Overview

Overview prioritises incomplete tasks due today and overdue tasks across projects. Each row retains its top-level component, project colour, configured status, priority, estimated effort, outstanding prerequisites and all stored assignees. Missing ownership is explicit. An expandable section lists open tasks without due dates; start dates and estimates never substitute for due dates. Components are excluded from task totals. The local calendar day refreshes on a timer, window focus and visibility changes.

The production build and 51 frontend checks passed. Seven new projection checks cover completion flags, component/task independence, past/present/future and undated work, deterministic urgency ordering, co-assignees, explicit empty assignments, immutable inputs, missing references, daylight-saving boundaries, empty workspaces and calendar rollover. Browser checks verified the populated cross-project groups, task-detail navigation and expanded undated work with both co-assignees. At 375 pixels the first complete task and its owner fit in the initial viewport, all rows fit horizontally, and the New project target remained 44 pixels. Existing task records were not edited for this work.

## Inline assignee editing

Each assignee has an inline Edit action with Save, Cancel and Escape support. Invalid input retains its draft and displays the validation error; cancellation restores focus to the Edit button. The additive `PATCH /api/people/{id}` endpoint updates the existing record through `IWorkspaceStore`. `Person.Rename` shares trimmed-name and case-insensitive uniqueness rules with creation. There is no schema migration, ID replacement or task reassignment.

The .NET and frontend builds passed with 51 frontend checks. The isolated native DuckDB API suite passed 57 checks, including name trimming, identity/co-assignee preservation, exact task metadata and project revisions, case-only edits, idempotence, missing assignees and invalid/duplicate-name atomicity. Both Postman collections linted; 26 local requests passed all 38 assertions, and the generated OpenAPI passed syntax lint. Browser checks covered Save, Cancel, Escape, duplicate recovery, task-label updates and the phone layout with 44-pixel controls. An exact isolated restart comparison preserved the renamed record and all assignments. Test changes were reverted. The working database was backed up before installing the endpoint, and its exact workspace state was preserved across restart.


## Mind map vertical default and fitted context — 26 October 2026

Vertical is first and becomes the initial orientation for existing projects; later explicit choices are remembered per project. Initial fitting waits for ELK layout and measured React Flow nodes, reserves the toolbar and bottom overlays, and shares its framing with both Fit buttons. Context changes refit the visible branches; nonstructural updates preserve manual zoom. Saved task coordinates are unchanged by orientation or fitting.

The production build and all 54 frontend tests passed, including preference migration and wide, tall, negative-coordinate and phone-width fit cases. Running-browser checks covered both existing projects, reload, remembered Horizontal followed by restoration to Vertical, component focus and Exit focus, sidebar resize, matching entry/toolbar/control fits and manual zoom while toggling dependencies. All rendered nodes fit inside the canvas below the toolbar in these checks. A 375-by-667 browser override (417 CSS pixels at the browser's existing zoom) also fit all visible nodes with the wrapped toolbar; this is responsive browser evidence, not a physical-iPhone test. The temporary tab and viewport override were cleaned up.


## Inline task assignees — 26 October 2026

The shared task editor now has a searchable inline multi-assignee field in place of its assignment modal. Existing people can be selected or removed individually; a new name offers Add and assign through the existing POST /api/people contract, followed by the editor's existing task autosave. Creation resolves the actual new server ID, retains all current selections, prevents duplicate submissions while pending and displays failures beside the field. Exact existing names do not offer duplicate creation. Escape closes suggestions before the task editor; Done dismisses unselected text.

The production build and 57 frontend tests passed. The existing local Postman regression completed 26 requests and 38 assertions against the isolated DuckDB backend, including creation, co-assignment and invalid hierarchy coverage. Browser checks on that backend verified keyboard selection of an existing person, chip removal, inline creation retaining two original assignees, duplicate prevention, Done, Escape, task reload and the large editor. A phone-sized browser override fit the chips and creation option without horizontal overflow (417 CSS pixels at the browser's existing zoom). Exact workspace equality across a backend restart confirmed all three assignments persisted. The disposable project and created person were removed through the public API; the exact test baseline was restored and the test backend stopped. The working workspace was only inspected; no test person or assignment was created there.


## Selected Gantt dependency connectors — 26 October 2026

Task-detail selection now drives Gantt highlighting for direct incoming and outgoing dependencies. Highlighted paths and arrowheads use the project accent, draw last and use a thicker stroke; unrelated paths dim. The selected task and connected endpoints receive distinct outlines and label backgrounds while workflow colours remain intact. Closing details clears selection styling. Only drawable dependencies between visible scheduled rows participate, so collapsing branches or hiding dependency arrows does not leave stale highlights.

The production build and 58 frontend tests passed, including direct incoming/outgoing, unrelated and transitive exclusions, selection replacement and reset. Browser checks against the existing example project exercised task-title selection, bar selection with one incoming and three outgoing links, connected endpoints, branch collapse/expansion, dependency visibility and clearing selection by closing details. No project data or task dates were edited.


## Today and Tomorrow task-date shortcuts — 26 October 2026

Both task-date fields now provide Today and Tomorrow actions, using the browser's local calendar and the existing autosave. Shortcuts honor inclusive start/due constraints without moving the other date. The native date inputs remain available. Enabled states refresh on focus, visibility changes and the next minute; clicking resolves the current local date again.

The production build and 61 frontend tests passed, including west/east timezone cases, DST, leap-day/year rollover and inclusive date bounds. Browser checks on a disposable task in the isolated DuckDB covered all four shortcuts, disabled reversed ranges, reload, the expanded editor and phone layout with 44-pixel actions. Only dates and the update timestamp changed; co-assignees, effort, specification, status and hierarchy were retained. Exact workspace comparison across an isolated backend restart passed. The disposable project was removed, the exact baseline restored, the test server stopped and the temporary phone override reset. No working task dates were edited.


## Vertical task order in component columns — 26 October 2026

Task grips now sort their siblings vertically, including nested task branches. Each sibling list has its own sortable scope; task drops take precedence over the enclosing component card. Reordering uses the existing sibling-order contract across workflow statuses, includes the full sibling set and retains hidden siblings in their filtered slots. Component-card drops continue to move branches between components. Task keyboard sorting uses Space, Up/Down, Space, with Escape to cancel. Horizontal component ordering is independent, and order changes offer guarded Undo.

The production build and 62 frontend checks passed. The local Postman regression passed 30 requests and 45 assertions, including task ordering across statuses, preserved metadata, rejected incomplete orders and reload. Running-browser checks on a disposable nested fixture covered mouse dragging, keyboard movement, nested sorting, filtered sorting, Undo, cancellation, horizontal component sorting and cross-component moves carrying subtasks. The final order-only change preserved all task fields except sibling positions, including workflow positions and stored map coordinates, and cancellation caused no extra revision. Exact workspace equality across an isolated backend restart passed. The fixture was deleted through the public API, the exact isolated baseline restored and the test backend stopped. The working project's order was not changed during testing.


## Four-direction Gantt grab panning — 26 October 2026

Gantt blank tracks and empty viewport space now support mouse grab panning on both scroll axes. The shared pan binder retains the Components horizontal-only behavior. Panning starts only on explicitly marked blank surfaces after a movement threshold; task bars, resize handles, labels, controls, scrollbar gutters, modifiers and touch input retain their existing behavior. Pointer capture, cancelled gestures, Escape, focus loss and unmount clean up the grab. The cursor changes between grab and grabbing.

The production build and all 66 frontend tests passed, including both-axis movement and clamping, vertical-only overflow, the movement threshold, excluded controls/touch, cancellation, and the original Components pan checks. Running-browser checks verified diagonal panning and exact reverse movement with task details open, released capture and normal bar opening afterward. A disposable timeline in the isolated DuckDB confirmed that panning leaves the entire workspace unchanged. Dragging a bar moved both dates by one day, and dragging its end handle extended only the due date by one more day, retaining all other task metadata and all unrelated tasks. Switching away and back remounted the Gantt cleanly. The disposable project was deleted, the exact isolated baseline restored and the test backend stopped. Working project dates were not edited.

## Styled tooltips — 26 October 2026

Application hover hints use one delegated tooltip layer, with a 150 ms hover delay, immediate keyboard-focus hints, readable wrapped text, project-colour accents and theme colours. Pointer presses and Escape dismiss hints; scrolling keeps focused keyboard hints positioned. The layer adds no wrappers to chart bars, drag handles or buttons. Existing ARIA descriptions are retained, and offline chart snapshots retain native title text. Dialog headings and iframe labels remain unchanged.

The frontend production build and all 66 tests passed. Running-browser checks verified Gantt title/date/assignee content, single-day wording, placement within a 484-pixel viewport, React Flow and Navy theme colours, keyboard focus and Escape, preserved Gantt instructions, normal task opening and accessible hints inside the task dialog. No console errors were recorded. This frontend-only change required no database or API mutations.

## Overview Due Tomorrow — 26 October 2026

Overview includes a Due tomorrow lane beneath the existing due-today and overdue lanes, before undated tasks. It uses the next calendar date from the same local-day projection, excludes components and completed tasks, and retains component context, status, priority, estimates, dependencies and all assignees. Tomorrow's tasks contribute to the attention count and missing-assignee note. Existing day rollover refreshes the groups.

The production build and all 68 frontend tests passed, including tomorrow's date across DST, leap day and year boundaries, completion/component exclusion and rollover into today and overdue. The running interface showed three tasks due tomorrow with their assignees and a combined attention count of five. Opening a tomorrow task loaded the correct task and both assignees in the shared editor. No task fields or database data were changed during verification.

## Optional agent writing and execution

Projects remain usable without agent connections. New local endpoints persist optional repository bindings and explicit task runs through the existing `IWorkspaceStore` and native EFCoreProvider path. A new additive migration creates only agent connection/run tables. Captured task specifications, settings and revisions remain distinct from live task fields and workflow status. Browser and MCP Markdown imports use one shared codec.

The .NET build passed with zero warnings/errors and EF reported no pending model changes. The frontend production build and 71 checks passed; the MCP unit/compatibility suite passed 5 checks. The isolated real HTTP/DuckDB/process-boundary suite (`python3 scripts/agent-regression.py`) passed 19 checks covering optional connections, origin/repository/ref validation, explicit dispatch, idempotency, immutable work orders, agent protocol handling, exact session continuation, permission arguments, isolated worktrees and diffs, prerequisite gating, cancellation, visible failures, delete/disconnect rules, restart persistence and interruption recovery. Its CLI responses are test-only protocol fixtures; they make no model-service claim.

Separate live checks used installed Codex CLI 0.150.1 and Claude Code 2.1.309 against a disposable repository. Both returned writing results and both implemented a small input-derived Python function in separate worktrees. Independent Python assertions passed for each implementation; the original repository remained unchanged. A live Claude documentation follow-up retained its session. These checks establish the installed local CLI path, not hosted execution or vendor app project synchronization.

Running-browser verification covered project connection settings, explicit dispatch, live results, generated-spec preview/import, editable imported content, UTF-8 Markdown file upload, preserved existing text, nested checklists/tables, unchanged workflow status, and follow-up prompt retention across expanding the task details. Reopening an imported spec exposed a destroyed-editor synchronization error; guarding editor lifecycle and synchronizing on tab/size changes fixed the observed failure. A narrow browser check measured 375 CSS pixels with no page overflow, stacked agent fields and scrollable output. This is browser breakpoint verification, not physical-phone validation.

The unchanged planning suite passed 57 real API/DuckDB checks and 13 real stdio MCP checks. The saved Postman collection linted and passed 32 requests / 47 assertions against the isolated backend with cloud event reporting disabled. The checked-in OpenAPI contract was refreshed from the running service; no remote resources were published.

An exact comparison across an isolated backend restart retained the browser-imported spec, connection and live run history. A stopped populated workspace copy passed migration with every pre-existing public field unchanged. The original local database was backed up, upgraded and compared against its pre-upgrade public snapshot with no differences. Disposable fixture projects/people and test servers were cleaned up; the original local backend runs the new build.

## Per-run model and reasoning effort

Project connections retain separate Codex and Claude model/effort defaults. Explicit run overrides, agent-default choices and continuation settings are captured with the work order. Capabilities expose configured effort levels; model IDs remain user inputs, and unavailable or incompatible choices surface provider errors without model substitution. The legacy connection model input still updates only the preferred provider.

All 74 frontend checks and 28 isolated HTTP/native DuckDB/process-boundary agent checks passed. New coverage verifies provider-specific defaults, exact CLI flags and omitted flags, explicit clearing, invalid effort atomicity, request-id conflicts, immutable history and continuation after project defaults change. The backend and production frontend builds passed. A populated database copy migrated and rolled back without changing existing workspace fields, connection models or historical runs. The original local database was backed up and upgraded with an exact comparison of existing tables and fields.

Running-browser checks covered saving and reopening independent defaults, switching agents, model/effort overrides, captured history, expanded task details and continuation settings. At 375 CSS pixels, controls stacked with no page overflow. Live disposable writing runs succeeded with Codex’s default model plus explicit low effort and Claude’s sonnet alias plus low effort. A model ID advertised in the local Codex cache was rejected by the signed-in provider; Canopy retained the failed run and surfaced the error without falling back. These checks establish requested settings and provider outcomes, not the provider’s internal resolved model or effective reasoning budget.

The refreshed OpenAPI and saved collection linted, and all 32 Postman requests / 47 assertions passed locally, including configured effort capability checks. The existing planner API suite passed all 57 checks on the migrated isolated database. EF reported no pending model changes. The disposable verification project was removed; the retained Tiny Counter project and original repository were left unchanged.

## Agent connection landscape layout

The connection dialog uses two columns on desktop and tablet, with repository settings and project instructions alongside provider model/effort defaults. At 600 CSS pixels and below it stacks into a scrollable phone layout. The production frontend build passed. Running-browser checks measured a 999-by-687 dialog on desktop and a 735-by-687 dialog at a 768-pixel tablet viewport, both without internal overflow. At 375-by-667 pixels, the form used one column with no horizontal page or dialog overflow, and keyboard focus brought the footer actions into view. No project connection values were saved during verification; the backend stayed running.
