# Software Factory configuration — phase 1

This phase supplies the persistent configuration API. The [phase-2 React Flow
designer](FACTORY-DESIGNER.md) now uses it. [Run preparation](FACTORY-RUNS.md) adds
local readiness observations and immutable work orders, with explicit dispatch,
manual gates and runtime evidence. Installed-skill discovery remains later work.
Configuration endpoints never launch agents, run commands or change a task's
hierarchy, workflow status, dates or map layout. Existing agent endpoints remain
independent.

## Storage and templates

Factory configuration uses native DuckDB / EFCoreProvider through `IWorkspaceStore`
and the existing single writer/backend process. The additive migration creates
`FactoryPipelines`, `PipelineVersions` and `TaskFactoryConfigurations`. Domain and
application rules enforce relationships without physical foreign keys, following
the existing DuckDB model. Task deletion removes its attachment; project deletion
also removes its pipelines and versions transactionally. Factory data is available
through the factory API, not the existing workspace snapshot/JSON export. Native
database backups include it.

`Factory:TemplatesPath` (environment override `Factory__TemplatesPath`) selects a
UTF-8 JSON catalogue, relative to the API content root or absolute. The default
authoritative catalogue is `server/Planner.Api/factory-templates.json`. Each entry
has an ID, name, description, provenance `source` and complete definition. Empty
dependency arrays are preserved by JSON deserialization. Invalid catalogues fail
startup; there is no fallback template data. An explicitly empty catalogue is
allowed. Restart to reload changes; existing drafts and versions remain unchanged.

Templates cover standard implementation, bug fixes, specification, documentation
and verification. Implementation/verification templates intentionally lack a
verification command: supply the actual project command before publishing.
Custom definitions require an explicit source; template provenance comes from
configuration and cannot be overridden during creation.

## HTTP contract

Routes below are relative to `/api/factory`. They require a loopback connection
and local Host, reject cross-origin/cross-site requests and return JSON/problem
JSON. Mutation responses follow commit.

| Method | Route | Input / result |
| --- | --- | --- |
| GET | `/templates` | Configured template array |
| POST | `/validate` | Definition → `{valid, executionReadiness, diagnostics}`; read-only |
| GET | `/projects/{id}/pipelines` | `{projectRevision, pipelines}` including archived entries |
| POST | `/projects/{id}/pipelines` | `{expectedRevision, name, description, templateId}` or `{expectedRevision, name, description, source, definition}` → 201, detail and Location |
| GET | `/pipelines/{id}` | `{projectRevision, pipeline, versions}` |
| PUT | `/pipelines/{id}/draft` | `{expectedRevision, expectedDraftRevision, name, description, definition}` → detail |
| POST | `/pipelines/{id}/publish` | `{expectedRevision, expectedDraftRevision}` → detail with immutable version |
| POST | `/pipelines/{id}/archive` | `{expectedRevision, expectedDraftRevision}` → archived detail |
| GET | `/versions/{id}` | Published version and captured name, description, source and definition |
| GET | `/tasks/{id}/configuration` | Task configuration detail; unattached tasks return null configuration/version/definition |
| PUT | `/tasks/{id}/configuration` | `{expectedRevision, pipelineVersionId, overrides:{steps:[]}}` → task detail |
| DELETE | `/tasks/{id}/configuration?expectedRevision=…` | Detach while retaining task and versions |

`expectedRevision` is the current project revision; draft mutations additionally
require `expectedDraftRevision`. Conflicts return 409, invalid configuration 400,
missing resources 404 and rejected origins 403. Mutations return the first problem;
`/validate` returns diagnostics with stable `code`, `severity`, `message`, `field`,
`stageId` and `stepId`. No execution resources are created.

Broken graphs are rejected atomically. Drafts may temporarily omit agent/manual
instructions or a command definition; publishing requires all checks to pass.
An unchanged draft cannot be published twice. Editing after publication changes
only the draft; the next publication creates a new numbered version. No endpoint
updates published versions.

Unknown JSON fields are rejected rather than silently discarded, including
attempts to override a step's execution permission through task overrides.

Task attachments accept published versions from the same project only. Task
detail contains `{projectRevision, configuration, version, resolvedDefinition,
updateAvailable, archived}`. New publications advertise an update; adoption is an
explicit PUT. Archived pipelines retain their versions and existing attachments
but reject new attachments and draft edits. Active standalone agent work blocks
changing or detaching its task's factory configuration.

## Definition schema 1

A definition is `{schemaVersion:1, stages:[...]}`; omitted `schemaVersion` means 1,
and other versions are rejected. Stages contain `id`, `name`, display `group`,
explicit `dependsOn:[]` and ordered `steps`. Stable IDs are case-sensitive, at most
200 characters and use letters, numbers, dots, underscores or hyphens. Stage IDs
are unique in a pipeline; step IDs are unique within a stage. Display groups have
no scheduling meaning; dependencies determine the graph and step array order
determines order within a stage.

| Step kind | Required settings |
| --- | --- |
| `agent` | `instructions`, `role`, `permission`; optional `provider`, `model`, `effort`, `skills` |
| `command` | `command:{executable, arguments:[], workingDirectory, timeoutSeconds, environmentReferences?}` |
| `manual` | `instructions` describing the required decision |
| `artifact` | Nonempty `consumes` list |

Agent roles are `specification`, `implementation`, `verification`, `review` and
`documentation`. Permissions are `read-only` or `workspace-write`; review requires
read-only permissions. Provider is `codex`, `claude`, or null for later project
inheritance. Only agent steps accept agent settings. Skills require unique IDs
and explicit sources; this phase retains references without certifying actual
installation, content, compatibility or use.

Commands separate executable and arguments, require a positive explicit timeout
and use repository-relative working directories (`.` is valid) without absolute
paths or parent traversal. Environment references map an environment name to an
external variable name, never a literal secret value. This phase only validates
configuration; future execution must still resolve paths, check tools and enforce
execution/sandbox policies.

Steps may declare `consumes:[]` and `produces:[]`. Each artifact has one producer;
inputs must come from an earlier step or transitive dependency, never a future or
unrelated stage. Dependencies must identify different stages and form a DAG.
Cycle detection is iterative rather than recursively traversing supplied graphs.

`overrides.steps` contains `{stageId, stepId, instructions?, provider?, model?,
effort?, skills?, command?}`. Each existing step may be overridden once. Null or
omission inherits; an empty skills array clears skills. The resolved definition
must remain valid. Overrides cannot change graph structure, role or permission
and never mutate the published version.

Configuration documents are limited to 2 MB, following the existing specification
document boundary. Validation always reports `executionReadiness:"notEvaluated"`,
including for valid definitions: provider/model access, skills, tools, test success
and human approval have not been checked.

## Verification

After building, run `python3 scripts/factory-regression.py`. It starts an isolated
loopback backend with a temporary DuckDB database, exercises the HTTP API and
restarts it to check persistence. It never contacts model services or changes
the live database. The saved `Factory regression` Postman collection provides a
smaller reusable lifecycle check and creates/deletes its own project.

OpenAPI is generated from endpoint types at `/openapi/v1.json`. Refresh the saved
specification and regenerate only `Planner API`; preserve both regression
collections. See the [designer guide](FACTORY-DESIGNER.md) for the configuration UI.

Phase 3 also provides the read-only React Flow execution workspace: project run
history, actual stage/step states, manual decisions, command output, agent reports
and diffs, logical evidence navigation/downloads, phone step-list navigation and
restart recovery. See [execution workflow](FACTORY-RUNS.md) and
[validation evidence](FACTORY-PHASE3-RUN-VIEW-VALIDATION.md).
