# Optional agents

Canopy remains a planner. Project connections, imported specifications and agent runs are optional. Saving a connection, changing a workflow status or editing a specification never dispatches work.

## Local execution contract

- A project connection records its repository directory, base Git ref, preferred provider, optional model and instructions. These are explicit user inputs. It is a local code workspace binding; it does not claim to create or synchronize vendor app project containers.
- `spec` and `documentation` runs have read-only tools and return Markdown for review, import or download. `implementation` runs work in a detached Git worktree created from the configured ref. They retain changes for review and do not merge or deploy.
- Each explicit request has a unique `requestId`, project `expectedRevision`, provider, purpose, instructions and optional previous run ID. Retrying that same request returns the original run. Concurrent attempts on the same task are rejected.
- A run captures the persisted task title/specification, connection settings and project revision. Editing the live task never changes a captured work order. Resume is allowed only for the same task, provider and purpose, using its exact saved session and workspace.
- Run state is independent of task workflow status: queued, running, cancelling, succeeded, failed, cancelled or interrupted. Success means the agent completed; it does not mean the work passed review. No workflow status is automatically updated.
- Cancellation terminates the process tree. Restart marks unfinished work interrupted; it never silently replays a task. Missing tools, credentials, refs and repository paths produce explicit failures.
- Runs and connections persist in native DuckDB through `IWorkspaceStore`. Run output is bounded by configured limits. Agent endpoints accept only local, same-origin browser requests. Credentials stay with the installed CLIs.

## HTTP interface

All routes are below `/api/agents` and use JSON/problem responses. Reads return the resource rather than a workspace snapshot.

| Method | Path | Contract |
| --- | --- | --- |
| GET | `/providers` | Configured executables, installation availability and UI polling interval; installation is not authentication evidence |
| GET | `/projects/{id}/connection` | Connection or null; 404 for an absent project |
| PUT | `/projects/{id}/connection` | Save `{expectedRevision, repositoryPath, baseRef, provider, model, instructions}`; validate the Git workspace; return the connection |
| DELETE | `/projects/{id}/connection?expectedRevision=…` | Disconnect; preserve historical runs; return null |
| GET | `/tasks/{id}/runs` | Run history, newest first |
| POST | `/tasks/{id}/runs` | Explicit dispatch with `{requestId, expectedRevision, provider, purpose, instructions, previousRunId?}`; return persisted run; duplicate request IDs return the same run |
| GET | `/runs/{id}` | Captured work order, state, session, workspace, progress, result and diff |
| POST | `/runs/{id}/cancel` | Idempotent cancellation; return current run |

Stale project revisions and already-active tasks return 409. Invalid bindings, blank work orders, mismatched resumes and unknown providers/purposes return 400. A missing configured executable returns 503. Project/subtree deletion is rejected while affected runs are active. Historical run rows are removed with explicitly deleted tasks/projects; worktree files are retained.

Markdown import uses the same Tiptap codec as MCP, previews before append/replace and saves through the normal revision-checked task API. Agent output is never automatically imported. Documentation may be downloaded as Markdown independently of the task specification.

## Model and effort selection

Project connections optionally accept `settings`, a map keyed by provider (`codex`, `claude`) with `{model, effort}` values. Defaults are stored separately for each provider. The existing `model` input/output remains compatible with the preferred provider; a legacy save preserves the other provider's defaults and existing effort. Model IDs are caller inputs, not a built-in model catalogue. `/providers` supplies configured provider-specific `effortLevels`; supported levels also depend on the selected model and account policy. Configuration provenance is retained beside those levels in `appsettings.json`.

Explicit dispatch accepts optional `model` and `effort` overrides. Omission/null inherits that provider's project defaults; an empty string explicitly uses the agent default. For a follow-up, omission/null inherits the previous run's captured settings, even if project defaults changed. Users can explicitly override settings on a follow-up. Every run stores both the requested overrides and the resolved model/effort, so changing a request ID's settings returns 409 while identical retries keep the original snapshot.

The CLI receives `--model` only for an explicit resolved model. Codex receives effort as `-c model_reasoning_effort="…"`; Claude receives `--effort …`. Unset settings omit those arguments. Run history labels these as requested settings, because provider defaults, aliases and managed policy can change the model/effort actually applied. No inference run is started by saving defaults or editing selection fields.
