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

The frontend suite currently contains 32 passing checks for hierarchy views, component ordering, assignee preservation, independent effort, calendar arithmetic, timeline range, map orientation, readable project-colour accents and offline export safety. The MCP unit/compatibility suite contains 5 passing checks, including legacy protocol negotiation and specification handling.

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

Run mutation suites sequentially. The API suite has passed 42 checks, including hierarchy/dependency cycles, revision conflicts, component ordering, task metadata, multiple assignees, independent fractional effort, explicit clearing and invalid-value atomicity. The real stdio MCP suite has passed 13 checks, including Markdown upload and metadata preservation. Exact workspace comparisons across backend restart and populated migration checks have preserved pre-existing public fields.

Saved local Postman regressions have passed 21 requests and 30 assertions. The OpenAPI syntax and collections have linted successfully. No cloud mock or collection publication is required.

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
