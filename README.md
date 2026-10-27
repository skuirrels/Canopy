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

- **Overview** groups incomplete tasks into **Due today**, **Overdue** and **Due tomorrow**, with their project, component, all assignees, workflow status, priority, effort and outstanding prerequisites. Click a task to open its details. Expand the separate list for open tasks without due dates. Completion comes from configured workflow flags; components and completed tasks are excluded from task counts. Calendar dates follow this browser’s local day and refresh when the day changes.
- Create a project. Its starting workflow comes from `WorkflowDefaults` in `appsettings.json`: **To Do → In Progress → Blocked → In Testing → Done**.
- Add tasks and open their details to write a rich specification, create nested checklists, set dates, enter an hours estimate, choose priority, assign two or more people, or link prerequisites. Changes autosave; a failed save is shown explicitly and the draft is kept in this browser.
- Drag cards between columns or reorder them. Use a column's grip to reorder the workflow, and its menu to rename it or mark it as completed. Columns with tasks cannot be deleted.
- **Board** shows tasks and nested subtasks under components. Each card identifies its top-level component. Search matches task or component names; filters and branch focus narrow the task list. Click a card's subitem count to drill into its branch, and use breadcrumbs or **Exit focus** to return. Choose a component or parent task when creating a task from Board; moving a card between stages preserves its parent.
- Switch to **Mind map**. It defaults to **Vertical** (top to bottom), shown before **Horizontal** (left to right). The visible context fits after layout and node measurements settle, with room for the toolbar and map controls. Both Fit buttons use the same framing. Changing orientation or focus refits the context; manual pan and zoom are retained during other updates. Explicit orientation choices are remembered per project in this browser. Both orientations use the same saved map arrangement, with dragged positions translated back to the shared layout. Expand or collapse branches, double-click a task to focus, and click its title to open the shared editor. Drag to arrange. **Hold Shift and drop onto a highlighted node to change the parent.** Drop onto the project node to move a task to the root. Undo is offered for structural moves.
- Connect a task's gold outgoing dependency handle to another task's gold incoming handle (bottom to top in Horizontal; right to left in Vertical): the second task depends on the first. Use the inspector to remove dependencies. Hierarchy links and dashed dependency arrows are independent.
- Switch to **Gantt** for nested tasks on a calendar timeline. Use **New component** to create a project component, or the **+** beside a component to add a task with that component preselected as its parent. Task rows also allow adding nested subtasks. Adding under a collapsed branch expands it; new items remain unscheduled until you set dates. Expand/collapse branches, zoom by days/weeks/months, return to today, and toggle dependency arrows. The timeline opens with today centered in its visible calendar area; **Today** returns to that midpoint after scrolling. Open a task to set dates or edit its spec. Drag a bar to shift its dates, or drag an edge to resize; zoom in to expose handles on short bars. Undo is offered after saving. Keyboard: **Alt + Left/Right** shifts a focused bar, **Left/Right** resizes a focused edge, and **Shift** changes the step to seven days. Tasks without dates remain unscheduled; due-only and start-only tasks use distinct markers. Dependencies never automatically move other tasks.
- Switch to **Components** for a horizontally scrolling component plan, with a separate task preview in each column. **Grab and drag empty space** in the plan or a card to pan left/right with the mouse. Buttons, text, drag handles and native touch swiping retain their existing behavior. Top-level tasks act as components, with their nested tasks, full-component progress, specs, dates, assignees, and blockers. Click a component's **expand chevron** to open its full-width task list, with columns for status/priority, dates and assignee. Expand nested branches, open task titles for their specs, or add tasks directly to that component. **All components** returns to the horizontal plan. Drag a component's top grip left or right to resequence it; keyboard users can focus the grip and press **Space**, **Left/Right**, **Space**. The order is saved independently of Kanban ordering and supports Undo. When filtered, hidden components retain their slots. Search and filters retain parent context. In the horizontal plan, drag a task handle up/down to reorder its siblings, or onto another component to reparent it with its descendants. Keyboard users can focus a task grip and press **Space**, **Up/Down**, **Space**; **Esc** cancels. Filtered-out siblings retain their slots, and reordering preserves task fields and Kanban positions. Undo restores the previous order or parent.
- On **iPhone**, swipe horizontally between phone-sized component cards, then tap the chevron for the full task list. Nested branches, task specs and task/subtask actions stay available. Navigation overlays the project; task details fill the phone screen.
- In **Gantt** or **Mind map**, choose **Export → Print / Save PDF** to preview the complete chart, then choose **Save as PDF** in the browser print dialog. Printing fits the whole snapshot onto landscape A3. **Export → Download static HTML** creates a self-contained web page to attach to an email; the recipient opens the attachment in a browser. It preserves the current appearance, dates, connections, map orientation, filters and expanded branches, including content outside the viewport. Fonts are embedded, so it works offline. Gantt HTML includes the full filtered hierarchy, starts with your current expanded branches, and lets recipients expand/collapse individual branches or all tasks; dependency arrows follow the visible rows. The snapshot has no editing controls or live connection to the project.
- In task details, use **Expand task details** beside the close button to open a large modal. **Return to side panel** or **Escape** restores the panel while retaining the draft, editor undo history and autosave. Close task details saves and closes the task.
- In **Gantt**, grab empty chart space with the mouse and drag left/right or up/down to pan. Bars still move task dates and their edges still resize them; task controls and native touch scrolling remain available.
- Click a task title or bar in **Gantt** to highlight its direct incoming and outgoing dependency arrows in the project colour, with connected tasks outlined and other arrows dimmed. Closing task details clears the highlight. Hidden or unscheduled endpoints do not produce arrows.
- Use **Today** or **Tomorrow** beside either task date to set it immediately with autosave. Shortcuts use the browser’s local calendar and respect the other date: a start after the due date or a due date before the start is disabled.
- In task details, set **Estimated hours** for effort independently of calendar dates. Fractional values (such as 2.5) and zero are supported; leave it blank when unknown. Changing dates, status or assignees does not recalculate the estimate. Estimates appear in Components, Board, Gantt and Mind map, and are included in exports.
- In task details, type directly into **Assignees** to filter people and select additional owners. Use **Add and assign** to create a new workspace assignee inline. Selected people remain visible as removable chips; removing one chip affects only that task assignment. Use arrow keys and Enter to select, Escape to close suggestions, or Done to dismiss unselected text. Assignments autosave. Assignee filters match any selected person.
- Add names under **Assignees** or the workspace avatar. These are planning records, not accounts. Removing a person clears only their assignments, preserving co-assignees and tasks.
- Project options support renaming, colour, deletion, and a JSON export. **Project colour** includes React Flow hot pink and a custom picker; buttons, active views, highlights, avatars and map accents follow the selected colour. Changes preview immediately; Save changes keeps them and Cancel restores the saved colour. Export is a portable data snapshot; a JSON import interface is not included in phase 1.
- Open **Assignees** in the workspace navigation for the full-page people list. Add names inline, or use **Edit** beside a person to rename them with **Save**, **Cancel**, or **Esc**. Renaming preserves their identity and every task assignment. Removing a person still asks for confirmation. The workspace menu and avatar open the same page, and the selected section is remembered in this browser.
- Use **Enter Full Screen** in the top bar to fill the screen from any view. The control changes to **Exit Full Screen** while active; Escape also exits. On narrow screens it uses an icon with the same accessible label. Browser support and permissions apply.
- When exploring a branch, **Exit focus** beside the breadcrumbs returns to the project root in any view. Help uses a wide desktop layout with keyboard shortcuts alongside the guidance and stacks on phones.
- Shortcuts: **⌘/Ctrl K** search, **N** new task, **B/M/G/C** switch views, **Esc** close the task. To drag by keyboard, focus a task or column grip, press **Space**, use arrows, and press **Space** again; **Esc** cancels.

## Updating existing workflows

Changing `WorkflowDefaults` affects new projects. To update existing projects through the public API, review a dry run:

```sh
python3 scripts/apply-workflow-defaults.py --url http://127.0.0.1:5274 --all-projects --map 'Backlog=To Do'
```

Back up the native DuckDB file before applying changes. Add `--apply --snapshot /absolute/path/outside-repository/workspace-before.json` to save a recovery snapshot and perform the update. Use repeated `--project-id` arguments instead of `--all-projects` to limit scope. Configured columns are placed first; custom columns remain afterward. Legacy column merges require explicit mappings, retain each task's parent and metadata, and refuse to change completion semantics. The operation uses project revisions and is safe to repeat after completion.

## Claude and Codex (MCP)

The local MCP server exposes projects, nested tasks, Markdown spec uploads, hours estimates, dates, assignees, dependencies and component ordering through the same Canopy API and DuckDB backend. Install with `npm --prefix mcp ci`, then run `node mcp/src/index.mjs --check` and `node mcp/src/index.mjs --print-config` for ready-to-copy Claude Desktop/Codex connection settings. See [MCP setup and tools](mcp/README.md) for the complete guide, upload folder configuration, read-only mode and example prompts.

## Optional agent writing and execution

Canopy works as a planner without connecting an agent. Use **Project options → Agent connection** to optionally choose an existing local repository, base Git ref, preferred Codex/Claude CLI, separate model and effort defaults for each agent, and shared instructions. This binds a code workspace; it does not synchronize vendor app project containers. The installed CLI supplies authentication. Backend executable paths and run limits are configured under `Agents` in `appsettings.json` (or .NET environment overrides such as `Agents__CodexExecutable`).

In task details, **Specification → Import Markdown** previews a UTF-8 `.md`/`.markdown` file before you append or replace its content. **Download Markdown** exports the editable specification. These actions need no agent connection and share the same Markdown codec as MCP, including headings, tables and nested checklists.

The optional **Agents** tab offers **Draft / refine spec**, **Write documentation** and **Run implementation**. Writing runs have read-only tools and return Markdown to preview/import or download. Only an explicit run request starts work; connecting a project, saving a spec, assigning a task or changing status never dispatches it. Choose **Model** and **Effort** for each run: inherit project defaults, use the agent’s own defaults, or specify overrides. Model IDs/aliases must be available to your signed-in account; effort support depends on the model. Each run captures the saved task spec and requested settings, which remain visible in history. Follow-ups inherit the selected run’s settings. Results never overwrite the specification automatically.

Implementation runs use a separate detached Git worktree from the configured base ref. The agent runs relevant checks and reports evidence; Canopy retains the result, activity, session, starting commit and diff for review. It does not merge, push, deploy or change the task’s workflow status. Uncommitted changes in the original checkout are not copied into the worktree. Agent completion is separate from your acceptance of the work.

Open **Project options (⋯) → Agent connection → Build effects** to configure **Build graphics** and **Build sound**. Both default off and save immediately for all projects in this browser; the build window uses the same settings. With graphics enabled, starting a Codex or Claude implementation opens the interactive reactor; **View build** reopens it. The reactor follows recorded run state and new activity output, with no estimated completion percentage. The switch remembers your choice in this browser. **Mute sound**, closing the view, or switching the experience off leaves the run active; **Stop run** cancels it. Sound uses the implementation button’s user gesture. When opening an already-running build after a reload, your browser may require **Enable sound**. Reduced-motion preferences are respected, and losing live updates pauses the presentation until you refresh agent activity.

Use **Stop run** to cancel the process tree, or **Continue this session** to send follow-up instructions to a specific saved session and workspace. One local worker executes runs in order. Restarted unfinished runs are marked interrupted and never automatically replayed. Active runs prevent deleting their tasks/projects or changing their connection. Disconnecting retains history; explicitly deleting a task/project removes its history. Worktrees are retained on disk for review and may be removed manually with Git once no longer needed.

Agent controls are local and reject cross-site browser requests. Run output, timeouts and workspace locations are configurable. Missing executables, sign-in, repositories, refs and denied permissions are surfaced explicitly. See [agent contract and lifecycle](docs/AGENT-WORKFLOW.md). Run the isolated process-boundary regression with `python3 scripts/agent-regression.py` after building the backend; it uses a temporary database/repository and fixture CLIs, and never contacts a model service.

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
