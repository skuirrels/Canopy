# Software Factory runs

Independent assessments, persistent findings and review approval are documented
in [Independent review](FACTORY-REVIEW.md).

Criterion-level verdicts and verification freshness are documented in
[Acceptance criteria and evidence-based QA](FACTORY-QUALITY.md).

The [Software Factory roadmap](SOFTWARE-FACTORY-ROADMAP.md) tracks planned quality
verdicts, review and repair loops, and controlled PR delivery beyond these run
records.

Open a task's **Factory** tab, apply a published pipeline, choose **Check
readiness**, then **Prepare run**. These capture an immutable work order and
pending records without execution. Select **Start run**, inspect the captured
version/steps, then confirm **Start captured run** to dispatch. Approval steps
wait for an explicit decision; closing the view does not stop work. **Stop run**
cancels active work.

## Contract

All endpoints use the existing `/api/factory` loopback/same-origin boundary and
`Cache-Control: no-store`.

| Method | Path | Result / side effects |
| --- | --- | --- |
| GET | `/tasks/{id}/readiness` | `{projectRevision,canPrepare,executionReadiness,checkedAt,checks,environment}`; local inspection only |
| POST | `/tasks/{id}/runs` | `{requestId,expectedRevision}` → 201 with `{run,stages,steps,artifacts}` and Location; prepares only |
| GET | `/tasks/{id}/runs` | Run summaries ordered newest first; no full specification in the list |
| GET | `/runs/{id}` | Full immutable work order and ordered stage/step records |
| POST | `/runs/{id}/start` | `{requestId,expectedRevision}` → current detail; explicitly queues a prepared run |
| POST | `/runs/{id}/cancel` | `{expectedRevision}` → current detail; cancels execution |
| POST | `/runs/{id}/steps/{stepId}/decision` | `{requestId,expectedRevision,approved,note}` → current detail; explicit manual decision |

For start/cancel/decision, `expectedRevision` is `run.revision`, **not** the
project revision used by preparation. `stepId` is the persisted step-run GUID.
`approved` must be present; omission cannot implicitly decline or advance a
gate. Reusing a start ID returns saved state without redispatching. Reusing a
decision ID requires the same approval and normalized note or returns 409.
Controls refresh saved state after uncertain responses; an unconfirmed response
is never proof that execution did not start.


Checks carry `code`, `severity`, `message`, and optional stage/step IDs. Missing
attachments, archived pipelines, missing repository connections, blocked tasks,
active standalone agent runs, missing implementation specifications, unavailable
executables, invalid effort settings, unresolved skills and missing environment
references block preparation. Repository/base commit and effective agent settings
come from the actual project connection and Git. Model/effort `null` inherits;
explicit empty overrides select provider defaults, following the existing agent
contract.

`canPrepare` means the local work order can be captured. It never certifies agent
credentials, model access, command success or human acceptance; those remain
explicit warnings and `executionReadiness` stays `notEvaluated`. No provider
request or command verification is run during inspection. No environment-variable
values or credentials are returned in readiness or saved in the snapshot.

Preparation re-inspects the environment and checks the supplied project revision
inside the transaction after inspection. Stale revisions return 409. An empty
request ID or readiness errors return 400. Retries with the same request ID and
task return the original prepared work order, even after later edits/restart;
using that ID for another task returns 409. Concurrent retries create one run
and one set of stage/step records. As with other factory mutations, unknown
JSON fields are rejected.

## Skill resolution

`Factory:Skills` remains the deployment's exact explicit-file mapping. Optional
`Factory:SkillDiscovery:Roots` add bounded directory observations through the same
catalogue/resolver; see [installed skills](FACTORY-SKILLS.md). The default mapping
and roots are empty. No home/plugin locations or skill contents are guessed.

Every reference keeps its exact `(id, source)` identity. Explicit/discovered paths
to the same exact file deduplicate; conflicting files are ambiguous. Incomplete
coverage for the selected source, unresolved pairs, unreadable/non-UTF-8/oversized
files and symbolic links block preparation. Displaying a readable installed file
does not establish provider compatibility or successful execution.

Snapshots record the resolved path and actual SHA-256 byte hash. The dispatcher
rechecks current resolution and hash before use, includes the selected Markdown in
step instructions and supplies its actual directory for supporting references.
Changed installation contents or mappings require a new work order; refresh and
selection never rewrite previous snapshots, execute agents or accept tasks.

## Dispatch and evidence

Each run creates one retained detached worktree at the captured Git commit.
Moving the source branch or changing project model defaults cannot replace the
captured commit/settings. Ready stages execute serially according to explicit
dependencies; display groups and canvas positions have no scheduling meaning.
Steps retain their declared order within each stage.

Agent steps reuse the existing CLI worker, output handling and configured
limits. Factory children are associated with their step and kept out of the
standalone Agents history. Each step's `read-only` or `workspace-write`
permission controls CLI sandbox/tool settings; review requires read-only.
The [official Codex sandbox guidance](https://learn.chatgpt.com/docs/github-action#configure-codex-exec)
and installed CLI help were checked when reusing this integration.

Commands use executable/argument arrays without an implicit shell. They run in
the worktree with this local account; no additional OS sandbox is promised for
configured commands. Environment references resolve at invocation without
persisting their values as command configuration. Command output is retained as
emitted. Provider-specific CLI context cleanup does not strip
explicit command references. Command deadlines and the backend's configured
`Agents:MaxRunMinutes` active-step ceiling both apply. Output retention uses
`Agents:MaxOutputCharacters`; Git operations use the existing configured timeout.
Earlier output may be unavailable after the retention limit; no complete stdout
archive is fabricated.

Nonzero commands and timeouts fail the run. CLI completion records the actual
report and diff; it does not mean a review found no issues or that the task has
been accepted. Manual steps persist the explicit decision, note and timestamp.
Declining a gate stops the run. Run completion never changes task status.

Schema-1 artifact IDs identify **logical step evidence**, not physical files.
Successful producers record `kind: "step-evidence"` references with SHA-256
hashes of actual reports/diffs, command outcomes or approval records. Consumers
and artifact steps require intact successful producer evidence. A declared
artifact name does not prove a file exists or certify business correctness;
configure the actual verification command and review its evidence. Physical
file collection is not inferred from an artifact ID.

## Persistence and restart

Native DuckDB/EFCoreProvider stores `FactoryRuns`, `FactoryStageRuns`, and
`FactoryStepRuns` through `IWorkspaceStore`. The work order captures the exact
published version identity/provenance, task title/specification, overrides,
resolved agent settings, project instructions, repository/base commit, skill
hashes, and observed checks. Later task edits, connection changes, skill changes
or moving Git refs cannot rewrite an existing snapshot. Preparing does not touch
the task/project revision, status, hierarchy, dates, layout or standalone agent
history. Task/subtree/project deletion cleans up prepared run records in the
existing transaction.

The deployment remains single-user and single-backend-process. Local observations
can become stale. Starting rechecks captured prerequisites; active work blocks
conflicting task execution, attachment changes, connection changes and deletion.
Run state stays separate from ordinary task workflow state.

Cancellation terminates the active process tree and retains recorded output and
the worktree. Agent cancellation waits for child shutdown and final diff capture
before the parent becomes terminal. Waiting gates cancel immediately. Startup recovery runs before
HTTP traffic and workers, marking unfinished work interrupted without replaying
it, including work that was still stopping. Factory child sessions cannot be
continued through standalone agent dispatch. Inspect retained work/evidence before explicitly preparing a new run;
interrupted, failed and cancelled records are historical, not silently resumed.
Deleting terminal history does not automatically delete retained worktrees.

Run `python3 scripts/factory-run-regression.py` after building. It uses a temporary
repository/database and executable fixture, verifies immutable snapshots,
concurrent retries and restart persistence, and proves that neither fixture
agent nor verification command is invoked. The live database is never used.

`python3 scripts/factory-execution-regression.py` additionally runs actual Git
worktrees and verification commands, while Codex/Claude protocol responses come
from explicit executable fixtures. It checks gates, both permission paths,
commit/model pinning, cancellation, timeouts, evidence and restart without
contacting a model service.

## Execution workspace

Open a run from the project's **Software Factory → Runs** library, or choose
**Open execution view** in the task's factory history. The read-only React Flow
canvas uses the captured definition; status badges come from persisted stage and
step records. The step list provides the same inspection path on small screens.
**Focus step** centres the selected stage without changing executable dependencies.
Camera and arrangement preferences are scoped to the run in this browser.

The inspector shows actual activity, recorded timestamps/exit codes, operator
decisions, captured skill paths/hashes, reports and diffs. Downloads contain that
recorded text. Logical artifact links navigate to the producing step and expose
its evidence hash. **Review decision** brings a waiting gate into view; stopping
and leaving the view remain separate actions. Run completion never changes the
task's workflow state or certifies a model's conclusions.

Active reads form one polling chain at the configured backend interval. Older
revision responses cannot replace newer states; transient failures retain the
last saved state with an explicit warning. Refresh reloads terminal records;
workspace edits also refresh the current run. Historical titles come from the
captured work order, even after a task is renamed. Backend restart retains the
same history, reports, diffs and evidence. Interrupted work requires an explicit
new work order after review.

`GET /api/factory/projects/{id}/runs` returns this project's summaries newest
first, including the captured title and actual run revision. It is read-only and
uses the existing local-origin boundary. Records remain in native DuckDB; browser
preferences do not become a persistence source for run data.
