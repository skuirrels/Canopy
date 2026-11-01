# Guided factory setup

The planner works as before. Factory setup, task preparation, execution and
acceptance remain separate actions. No existing project is configured by upgrade.

## Configure once

1. Open a project's **Software Factory → Set up automatically**.
2. Connect its actual repository and agent, or reuse its saved connection.
3. Choose a work type from the configured templates.
4. For a workflow with verification, review the checks. Canopy reads the
   repository folder (its root and first-level folders) and pre-fills the
   build and test commands it finds evidence for, naming the file each came
   from: a single `.sln`/`.slnx` (or single project file) suggests `dotnet build`
   and `dotnet test`; `package.json` scripts suggest `build` and `test` with the
   matching package manager; `Cargo.toml`, `go.mod`, a `pyproject.toml` that
   mentions pytest, and `build`/`test`/`check` targets in a `Makefile` are also
   recognised. The first group found is pre-filled; others appear as one-click
   additions. When the repository has no build files yet (a new project whose
   first tasks will create the code), setup offers the common commands from
   `Factory:Setup:StarterChecks`, grouped by stack; after one is picked only
   the rest of that stack stays on offer. Nothing is run. Timeouts default to
   `Factory:Setup:DefaultCheckTimeoutSeconds`. Commands execute directly; quote
   arguments containing spaces. Shell operators and expansion are unsupported.
5. Review the suggested installed skills. Unavailable suggestions stay visible as
   gaps and are not selected. **Customize skills and workflow name** offers the
   installed catalogue and explicit references.
6. Choose **Review workflow**, inspect all stages and exact checks, then
   **Save project workflow**. **Explore workflow map** uses the existing React Flow
   view. Setup runs nothing.

![Configured factory workspace](images/factory-setup/workspace.png)

## Use it for a task

Every task follows the project workflow automatically, always at its current
published version. There is no per-task attachment step. A task that chooses
its own version under **Advanced configuration** keeps that version until it is
changed or detached; detaching returns it to the project workflow.

The task list shows each task's readiness from planning data: Ready to build,
Building, Waiting on (its open prerequisites, named), Needs a specification, or
Done. Filters default to Ready when any task is ready. Choose a task to open its
focused build view. Open prerequisites are listed with links, and **Prepare
build** stays unavailable until they are in a completed column. The backend's
readiness check remains authoritative and names any open prerequisites too.

![Focused build setup; missing inputs remain visible](images/factory-setup/task.png)

Add and save measurable acceptance criteria. **Prepare build** checks prerequisites
and captures the work order. Missing specifications or other requirements remain
visible. A prepared build still needs **Start run** and its explicit confirmation.
Preparation never starts an agent or command.

![Review the generated workflow before using it](images/factory-setup/review.png)

Any action that cannot proceed shows why beside it; a disabled control without
a stated reason is a defect.

## Change a workflow

**Project setup → Edit project setup** changes the managed workflow. New settings
publish a new version; tasks following the project workflow use it for their next
work order, prepared work orders keep their captured version, and tasks with their
own version keep it. The same
inputs reuse the existing version. Changing template provenance creates a new
correctly attributed pipeline, preserving history.

**Advanced → Pipeline designer** retains custom stages, versions and overrides.
Task-level **Advanced configuration** can explicitly choose another version or
tailor steps. Guided setup refuses to overwrite an existing different attachment
or an advanced draft/publication it no longer owns. Advanced model routing,
intake and calibration remain available under **Advanced**.

## Contract and validation

`GET /api/factory/projects/{id}/setup` reads the saved setup, configured templates,
project connection and `suggestedChecks` (groups of `ecosystem`, `evidence` file and
`checks`). `GET /api/factory/tasks/{id}/configuration` reports `inherited: true` when
the task follows the project workflow. `POST .../setup/preview` validates without saving.
`PUT .../setup` publishes and optionally attaches one task atomically.
`POST /api/factory/tasks/{id}/use-project-setup` attaches the reviewed version.
Mutations require the current project revision; stale or conflicting inputs return
409. Native EF/DuckDB stores the project-to-version reference. Planner fields,
task status and execution/delivery/calibration policies are unchanged.

Defaults, starter checks and suggested role skills come from `Factory:Setup` configuration;
workflow definitions and provenance come from `factory-templates.json`. Setup is
deterministic template assembly. Suggested checks come only from files present in
the repository folder; setup does not run them, install tools, authenticate a CLI,
or establish model/review quality.

Validation includes real native API rollback/reuse/versioning/restart checks,
an upgrade from a checkpointed prior database, command round-trip checks, and
desktop/mobile-viewport browser interaction. Screenshots show a disposable native
workspace using explicit local Python verification and agent availability fixtures.
No live model was dispatched. Phone viewport testing does not establish physical
device compatibility.
