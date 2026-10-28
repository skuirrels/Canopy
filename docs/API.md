# Canopy API contract

Phase 1 is a local, single-user workspace. Authentication is deliberately deferred. Bind to loopback; do not expose it publicly.

GET /api/workspace returns all projects, work items, workflow statuses and assignee records, sourced from DuckDB. No demonstration data is inserted on startup.

Mutation responses return the authoritative workspace snapshot. Project changes require expectedRevision; stale requests return 409 without changing data. Validation returns 400 Problem Details, missing resources 404. All mutations are serialized by the sole backend process, run in a database transaction, and only return success after commit. Client cancellation never implies a successful save.

POST /api/projects: name, description, color. Creates workflow columns from the configured WorkflowDefaults.
PATCH /api/projects/{id}: expectedRevision, name, description, color.
DELETE /api/projects/{id}?expectedRevision=: deletes project and its tasks, dependencies, statuses.
POST /api/projects/{id}/items: expectedRevision, title, parentId?, statusId?.
PATCH /api/items/{id}: expectedRevision, title, specJson, statusId, priority (none/low/medium/high/urgent), assigneeIds[] (legacy assigneeId?), estimatedHours?, startDate?, dueDate?, dependencyIds[]. estimatedHours is an optional finite non-negative number of hours, including fractions: null clears the estimate, omission preserves it. It is independent of dates, workflow status and assignee count; editing one never calculates or changes the other. Dates are ISO dates; start must not exceed due date. Dependencies must be in the same project and cannot form a cycle. Specs are Tiptap JSON documents.
POST /api/items/{id}/move: expectedRevision, parentId?, statusId, beforeId?. Reparents and reorders an item atomically; moving a parent preserves descendants. beforeId must be a sibling in the destination status. Hierarchy cycles are rejected.
DELETE /api/items/{id}?expectedRevision=: deletes the entire subtree, removing incoming dependency links.
PUT /api/projects/{id}/layout: expectedRevision, positions[{id,x,y}]. Saves mind-map positions independently of hierarchy.
POST /api/projects/{id}/status-order: expectedRevision, statusIds[]. Must include every project column exactly once.
POST /api/projects/{id}/sibling-order: expectedRevision, parentId?, itemIds[]. Must include every immediate child of this parent exactly once, across all workflow statuses. A null parent orders root components. Missing, duplicated, foreign, or non-sibling IDs are rejected atomically. Changes only siblingPosition; task contents, parent, workflow status/order, map layout and descendants remain unchanged.
POST /api/projects/{id}/statuses: expectedRevision, name, color.
PATCH /api/statuses/{id}: expectedRevision, name, color, isCompleted.
DELETE /api/statuses/{id}?expectedRevision=: only empty columns can be removed; one column must remain.
POST /api/people: name. Creates an assignee record, not an authenticated account.
DELETE /api/people/{id}: removes an assignee record, clears assignments atomically, and increments revisions of affected projects.
GET /api/health: readiness probe.
GET /openapi/v1.json: generated OpenAPI from these implemented endpoint contracts.

Work items expose position for Kanban ordering within a parent/status, and siblingPosition for Components ordering within a parent across statuses. Both are persisted independently of map layout. Completion is explicitly configured on a column. Parent progress is calculated from descendant completion, never from fixed example values. IDs are client-opaque UUIDs.

The database uses logical relationships validated in the domain aggregate and committed transactionally. Scalar UUID references deliberately avoid DuckDB's restrictions on updating referenced principal rows; no alternate runtime persistence path exists.

Linked local Markdown files use `/api/spec-files` endpoints and add `specFiles` to workspace snapshots. See [linked specification contract](LINKED-SPECIFICATIONS.md) for preview/load/write/unlink payloads, local access rules and filesystem conflict semantics.
