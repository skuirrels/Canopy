# Canopy

A single-user project planner with a draggable Kanban board, an interactive mind map, a Gantt timeline, and a horizontal Components view over the same persistent tasks. Built for personal validation; authentication and team membership are phase 2.

## Run

Clone the repository and enter the checkout:

```sh
git clone https://github.com/skuirrels/Canopy.git
cd Canopy
```

Requires .NET 10 and Node.js 22.12+ (or a newer supported Node version).

```sh
make dev
```

Open **http://127.0.0.1:5173** for development. This installs the locked frontend dependencies, starts Vite and the API with hot reload, and proxies `/api` and `/openapi` to the backend. Ctrl+C stops both processes; if either process fails, the other is stopped too.

```sh
make prod
```

Open **http://127.0.0.1:5274** for the production build. This installs frontend dependencies, builds the static frontend, builds .NET in **Release**, and serves the app and API from one backend with the Production environment. It runs in the foreground; Ctrl+C stops it. Stop an existing Canopy backend before switching modes. Neither target deploys to a remote server.

Both modes use the same configured native DuckDB file (`server/Planner.Api/data/planner.duckdb` by default). First launch applies EF migrations; restarting preserves your work. No example projects or assignees are inserted.

Optional development addresses can be configured with `PLANNER_API_URL` and `PLANNER_WEB_PORT`; Vite follows the chosen API address. For an isolated run, also set .NET's `Database__Path` to a separate absolute DuckDB path. Changing a port alone does not isolate the database.

The existing `./scripts/start.sh` command remains available for a single-process Debug build; `./scripts/start.sh Release` uses Release.

## Use

- Create a project. Its starting workflow comes from `WorkflowDefaults` in `appsettings.json`.
- Add tasks and open their details to write a rich specification, create nested checklists, set dates, enter an hours estimate, choose priority, assign two or more people, or link prerequisites. Changes autosave; a failed save is shown explicitly and the draft is kept in this browser.
- Drag cards between columns or reorder them. Use a column's grip to reorder the workflow, and its menu to rename it or mark it as completed. Columns with tasks cannot be deleted.
- Click a card's subitem count to drill into its branch. Use breadcrumbs to go back. **All levels**, search, and filters expose nested tasks; moving a card always preserves its parent.
- Switch to **Mind map**. It opens fitted to the visible branches, using the same framing as **Fit map to screen**. Choose **Horizontal** (left to right) or **Vertical** (top to bottom); the direction is remembered per project in this browser. Both orientations use the same saved map arrangement, with dragged positions translated back to the shared layout. Expand or collapse branches, double-click a task to focus, and click its title to open the shared editor. Drag to arrange. **Hold Shift and drop onto a highlighted node to change the parent.** Drop onto the project node to move a task to the root. Undo is offered for structural moves.
- Connect a task's gold outgoing dependency handle to another task's gold incoming handle (bottom to top in Horizontal; right to left in Vertical): the second task depends on the first. Use the inspector to remove dependencies. Hierarchy links and dashed dependency arrows are independent.
- Switch to **Gantt** for nested tasks on a calendar timeline. Expand/collapse branches, zoom by days/weeks/months, return to today, and toggle dependency arrows. The timeline opens with today centered in its visible calendar area; **Today** returns to that midpoint after scrolling. Open a task to set dates or edit its spec. Drag a bar to shift its dates, or drag an edge to resize; zoom in to expose handles on short bars. Undo is offered after saving. Keyboard: **Alt + Left/Right** shifts a focused bar, **Left/Right** resizes a focused edge, and **Shift** changes the step to seven days. Tasks without dates remain unscheduled; due-only and start-only tasks use distinct markers. Dependencies never automatically move other tasks.
- Switch to **Components** for a horizontally scrolling component plan, with a separate task preview in each column. Top-level tasks act as components, with their nested tasks, full-component progress, specs, dates, assignees, and blockers. Click a component's **expand chevron** to open its full-width task list, with columns for status/priority, dates and assignee. Expand nested branches, open task titles for their specs, or add tasks directly to that component. **All components** returns to the horizontal plan. Drag a component's top grip left or right to resequence it; keyboard users can focus the grip and press **Space**, **Left/Right**, **Space**. The order is saved independently of Kanban ordering and supports Undo. When filtered, hidden components retain their slots. Search and filters retain parent context. In the horizontal plan, drag a task handle onto a component to reparent it with its descendants. Undo restores its previous parent.
- On **iPhone**, swipe horizontally between phone-sized component cards, then tap the chevron for the full task list. Nested branches, task specs and task/subtask actions stay available. Navigation overlays the project; task details fill the phone screen.
- In **Gantt** or **Mind map**, choose **Export → Print / Save PDF** to preview the complete chart, then choose **Save as PDF** in the browser print dialog. Printing fits the whole snapshot onto landscape A3. **Export → Download static HTML** creates a self-contained web page to attach to an email; the recipient opens the attachment in a browser. It preserves the current appearance, dates, connections, map orientation, filters and expanded branches, including content outside the viewport. Fonts are embedded, so it works offline. The snapshot has no editing controls or live connection to the project.
- In task details, set **Estimated hours** for effort independently of calendar dates. Fractional values (such as 2.5) and zero are supported; leave it blank when unknown. Changing dates, status or assignees does not recalculate the estimate. Estimates appear in Components, Board, Gantt and Mind map, and are included in exports.
- In task details, open **Assignees** and check each person responsible. Assignments autosave; uncheck one person to remove only their assignment. Assignee filters match any selected person.
- Add names under **Assignees** or the workspace avatar. These are planning records, not accounts. Removing a person clears only their assignments, preserving co-assignees and tasks.
- Project options support renaming, colour, deletion, and a JSON export. Export is a portable data snapshot; a JSON import interface is not included in phase 1.
- Use **Enter Full Screen** in the top bar to fill the screen from any view. The control changes to **Exit Full Screen** while active; Escape also exits. On narrow screens it uses an icon with the same accessible label. Browser support and permissions apply.
- Shortcuts: **⌘/Ctrl K** search, **N** new task, **B/M/G/C** switch views, **Esc** close the task. To drag by keyboard, focus a task or column grip, press **Space**, use arrows, and press **Space** again; **Esc** cancels.

## Claude and Codex (MCP)

The local MCP server exposes projects, nested tasks, Markdown spec uploads, hours estimates, dates, assignees, dependencies and component ordering through the same Canopy API and DuckDB backend. Install with `npm --prefix mcp ci`, then run `node mcp/src/index.mjs --check` and `node mcp/src/index.mjs --print-config` for ready-to-copy Claude Desktop/Codex connection settings. See [MCP setup and tools](mcp/README.md) for the complete guide, upload folder configuration, read-only mode and example prompts.

## Optional example project

The **Example · Website launch** project is fictional and editable. Its source inputs live in `examples/website-launch.json`; the explicit loader calls the same public APIs as the application. It contains 23 tasks across three levels, rich specifications, checklists, dates, dependencies, and four fictional assignees. Dates are calculated from an explicit anchor date (or the current London date).

```sh
python3 scripts/add-example.py --base-url http://127.0.0.1:5274
```

Run this only when you want example data. It never runs on startup and refuses to overwrite an existing project of the same name, preserving any edits you make to the example.

## Architecture

React, TypeScript and Vite; Tailwind with shadcn-style Radix primitives; dnd-kit; React Flow with ELK in a web worker; Tiptap. ASP.NET Core / EF Core 10; released `DuckDB.EFCoreProvider` **1.27.0**.

`Domain` owns hierarchy, dependency, date and ordering rules. `Application` orchestrates use cases behind `IWorkspaceStore`. `Infrastructure` supplies EF persistence and tracked migrations. Endpoints only bind requests and call application services. Project revisions prevent stale writes; a single writer serializes transactions. Every successful mutation response follows database commit.

References use scalar UUIDs with domain-enforced relationships. This intentionally avoids DuckDB's restrictions on updating physically referenced principal rows. The project graph validates same-project references and cycles transactionally. There is no sample-only persistence path.

This native DuckDB deployment requires **one backend process owning the database file**, on persistent local storage. It is not configured for multiple web replicas. Keep it on loopback while authentication is absent. For a different file, set `Database__Path`; for a different address use `--urls` (and update Vite's development proxy if needed). Back up the database **after stopping the API**, copying the `.duckdb` file; retain any existing WAL alongside it. Never copy a changing live file as a backup.

The existing `Planner.sln` / `Planner.Api` names, `PLANNER_*` settings, browser storage keys and JSON snapshot identifier remain compatible with earlier local installations.

## Validation

```sh
dotnet build Planner.sln
npm --prefix client run build
npm --prefix client test
python3 scripts/api-regression.py
postman spec lint postman/specs/planner.openapi.json --issueType=syntax
postman collection lint 'postman/collections/Planner regression'
postman collection run 'postman/collections/Planner regression' --report-events=false
```

The regression script uses the running API, creates its own isolated projects and assignee, and removes them in `finally`. Set `PLANNER_URL` to test another loopback instance. The saved Postman regression collection likewise creates and removes its own project. The generated **Planner API** collection documents the contract; its placeholder mutation requests are not a standalone test sequence.

OpenAPI is generated from endpoint request/response types at `/openapi/v1.json`; `docs/API.md` describes invariants and error semantics. Refresh `postman/specs/planner.openapi.json` from that endpoint after contract changes; regenerate only the generated collection, retaining the regression collection.

See `docs/VALIDATION.md` for the checks performed for this build.
