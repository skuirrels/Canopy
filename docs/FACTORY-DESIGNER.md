# Software Factory designer — phase 2

Open a project and choose **Software Factory** beside the existing planning
views, or use **Project settings → Software Factory**. The designer uses the
phase-1 configuration API and the existing native DuckDB persistence path.
It never runs an agent, command, merge or deployment.

## Create and edit a pipeline

1. Choose **New pipeline**. Select a configured template or **Custom pipeline**.
   A custom pipeline requires its source and explicit first-stage/step inputs.
2. Use **Canvas** to explore stage groups and connections, or **List** for the
   same configuration and settings without canvas navigation. Select a stage
   or step to open its inspector.
3. Edit stage names/groups and prerequisites, add/remove stages and steps, and
   reorder steps. Stage groups arrange the display; prerequisites define order.
   Removing a stage does not invent replacement artifact producers.
4. For agent steps, choose a role and permissions, optional provider/model/effort,
   and skill references with sources. **Unverified** means installation and
   compatibility have not been checked. These are explicit references, not an
   installed-skill catalogue.
5. For command steps, enter the executable, arguments, repository-relative
   working directory and timeout. Each argument occupies one line, including
   any spaces within it. Environment values reference external variable names;
   enter no secret values. Human decisions and artifact checks use the same
   typed API definitions.
6. Choose **Validate** to get server diagnostics. Selecting an issue opens the
   affected stage/step and focuses the field when available. Invalid structure
   cannot overwrite the saved draft. Incomplete drafts follow the API's existing
   rules; publication requires a fully valid configuration.
7. **Save draft** commits through the revision-checked API. **Publish version**
   confirms creation of an immutable version. Validation certifies configuration
   structure only; execution readiness remains unevaluated.

Connect source and target handles to add prerequisites. Cycle-forming links are
rejected interactively and by the API. The list inspector provides the same
dependency controls. Connection handles also support Enter/Space activation.
Dragging stage grips and **Auto arrange** only change browser-local arrangement;
ELK lays out stages within display groups. Layout and camera keys are separate
from task Mind Map preferences and from pipeline structure.

The version selector opens immutable versions read-only. **Copy version to draft**
copies one into the local editor for an explicit save and later publication.
**Archive** retains versions and existing attachments while blocking new editing
and attachment, following the phase-1 contract.

## Attach tasks

Use **+** beside Tasks in the factory library to create an ordinary Canopy task,
or open an existing task. Its **Factory** tab selects a published pipeline
version. Task-specific settings can override instructions, provider/model/effort,
skill references and commands; structure, role and permissions stay with the
published version.

**Apply configuration** validates the resolved configuration, saves pending task
edits and attaches the version. New publications do not automatically change
existing attachments. Reopen the tab to see updates, select a newer version,
confirm any override reset, then apply it explicitly. **Detach** keeps the task
and its status. **Open designer** opens that pipeline's draft.

## Recovery and concurrency

Unsaved pipeline/task configuration is kept as a browser recovery copy. Server
configuration remains authoritative in DuckDB. Failed writes retain local edits;
pipeline stale-revision conflicts cannot overwrite newer drafts. Stale recovery
is identified after reload and must be reconciled by explicitly discarding and
reloading; pipeline drafts can be downloaded first. Task writes reread the saved
configuration and use that snapshot's revision to prevent overwriting changes
made elsewhere. A recovery copy that already reached the server adopts the
current saved revision rather than becoming falsely stale.

Published snapshots and task attachments survive backend restarts. Browser
layout/camera preferences and recovery copies stay on that browser; they are not
substitutes for database backups. The existing **Agents** actions remain
independent. [Explicit factory execution](FACTORY-RUNS.md) uses the captured
version and records actual step state/evidence. Installed-skill discovery remains
later work.

The Factory tab now also offers [readiness and run preparation](FACTORY-RUNS.md).
It captures a work order and pending stage/step records without dispatching work.
