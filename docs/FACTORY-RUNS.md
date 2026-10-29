# Factory run preparation

The first execution slice adds local readiness observations and durable prepared
work orders. Open a task's **Factory** tab, apply a published pipeline, choose
**Check readiness**, then **Prepare run**. Neither action executes an agent,
command or approval. A prepared run has state `prepared`; its stage/step records
have state `pending`. Pipeline execution is the next slice.

## Contract

All endpoints use the existing `/api/factory` loopback/same-origin boundary and
`Cache-Control: no-store`.

| Method | Path | Result / side effects |
| --- | --- | --- |
| GET | `/tasks/{id}/readiness` | `{projectRevision,canPrepare,executionReadiness,checkedAt,checks,environment}`; local inspection only |
| POST | `/tasks/{id}/runs` | `{requestId,expectedRevision}` → 201 with `{run,stages,steps}` and Location; prepares only |
| GET | `/tasks/{id}/runs` | Run summaries ordered newest first; no full specification in the list |
| GET | `/runs/{id}` | Full immutable work order and ordered stage/step records |

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

`Factory:Skills` is the deployment's authoritative mapping from a recorded skill's
exact `(id, source)` pair to its actual Markdown file. It is separate from the
template catalogue because it describes this machine's installation paths.
The default is empty. Each configured entry is `{Id, Source, Path}`; paths may
be absolute or relative to the connected repository root. Environment overrides
use the ordinary configuration index syntax, e.g. `Factory__Skills__0__Path`.

Unmapped or ambiguous pairs block preparation. Files use the existing local
Markdown reader: readable UTF-8, at most 2 MB, with symbolic links rejected in
every path component. Snapshots record the resolved path and SHA-256 hash, not
assumed installation or compatibility. A future dispatcher must recheck the
hash before use. No fallback skill contents or guessed package locations exist.

## Persistence and boundaries

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
can become stale; prepared records are evidence of an observation and are not
permission to execute. Dispatcher, runtime rechecks and manual approval gates
will be added separately.

Run `python3 scripts/factory-run-regression.py` after building. It uses a temporary
repository/database and executable fixture, verifies immutable snapshots,
concurrent retries and restart persistence, and proves that neither fixture
agent nor verification command is invoked. The live database is never used.
