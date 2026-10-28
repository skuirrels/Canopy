# Software Factory phase 1 validation

Validated on 28 October 2026 in an isolated worktree. The user's live database
and unrelated task-completion edits were not used or changed.

## Results

| Check | Result |
| --- | --- |
| Backend build | Passed, no warnings or errors |
| EF model/migration consistency | No pending model changes |
| Factory HTTP/DuckDB regression | 50 checks passed |
| Existing workspace HTTP/DuckDB regression | 57 checks passed |
| Existing agent process regression | 29 checks passed; fixture CLI protocols, no model calls |
| Existing linked Markdown regression | 18 checks passed |
| Client tests | 84 passed |
| Production frontend build | Passed; existing chunk-size warning |
| Factory Postman regression | 12 requests, 20 assertions passed |
| Existing Planner Postman regression | 32 requests, 47 assertions passed |
| OpenAPI and collection lint | Passed |
| Existing-database upgrade | Exact persisted workspace retained; no factory records seeded |

The factory suite runs a real loopback backend against temporary native DuckDB,
restarts the process, and checks version immutability, explicit task overrides,
archive behavior, invalid references/cycles/artifact wiring, concurrent publish
conflicts, rollback, cleanup, origin restrictions and configuration size limits.
It verifies that configuration never dispatches agents or changes task fields.

For the upgrade check, the previous backend created a project/task through its
public API in a temporary database. Its persisted GET snapshot was compared with
the new backend after automatic migration: the snapshots matched exactly. The
fixture project was deleted through the API. No live user database was migrated.

The new factory OpenAPI paths and their referenced schemas were taken from the
running endpoint generator. Existing saved API sections and generated requests
were preserved to avoid including unrelated historical contract drift. The
generated factory requests are documentation examples; the maintained regression
collection is the runnable lifecycle sequence. All Postman runs stayed local
with event reporting disabled.

## Code review

Reviewed domain invariants, API inputs/error contracts, transaction/revision
boundaries, published-version isolation, task/project deletion, migration shape
and execution separation. Review fixes reject unknown JSON fields (including
permission overrides), enforce the document limit on resolved overrides, and
document that structural validation does not certify execution readiness.

Startup/persistence testing also fixed dropped empty arrays during template
binding and UTC timestamp kind loss on factory record reload. The template
catalogue now uses typed JSON deserialization; factory timestamps preserve UTC
through EF conversion.

No blocking findings remained after the fixes and verification. New application
literals were checked: stage/template defaults are in the configured catalogue;
project/task data comes through public inputs; remaining constants describe the
schema, supported step/provider types and existing document-size boundary.

## Phase boundary

This is the configuration foundation/API. It adds no Factory UI or execution
dispatcher. Browser interaction testing of the React Flow editor belongs to the
next phase. Real-agent skill/tool readiness, command sandboxing, review evidence,
automatic scheduling and external publication remain unimplemented. Existing
agent controls retain their current behavior.
