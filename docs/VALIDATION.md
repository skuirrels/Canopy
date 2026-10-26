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

The frontend suite currently contains 30 passing checks for hierarchy views, component ordering, assignee preservation, independent effort, calendar arithmetic, timeline range, map orientation and offline export safety. The MCP unit/compatibility suite contains 5 passing checks, including legacy protocol negotiation and specification handling.

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
