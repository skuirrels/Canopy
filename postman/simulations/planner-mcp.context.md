# Planner MCP client simulation

## Purpose

Exercise the real stdio MCP process against its HTTP workspace dependency. The handler in `../mocks/planner-mcp-read/default.js` models the empty first-use workspace; it intentionally does not implement writes. Real mutation and persistence checks live in `../../mcp/tests/integration.mjs`.

## Execution

Start `postman simulation run postman/simulations/planner-mcp.sim.yaml --port 5278 --no-history --output ndjson`, then use its advertised member address as `PLANNER_SIM_URL` for `node mcp/tests/simulation.mjs`. The adapter supports a base path, so path routing needs no proxy.

## Assumptions

- 2026-10-26: Empty arrays are the actual first-use workspace contract; no synthetic task/domain records are injected by this mock. Confirmed against the isolated empty backend before fixture creation.
- 2026-10-26: This simulation proves stdio → HTTP → MCP read handling only. It cannot establish persistence, revisions or structural rules; the real-backend integration run supplies that evidence.

## Observations

<!-- begin observations -->
- 2026-10-26: After estimated-hours support, the actual stdio MCP process again called GET /api/workspace on the advertised planner member with 200. Effort writes and spec-preservation assertions were verified separately against native DuckDB; this simulation remains read-only.
- 2026-10-26: After the multi-assignee adapter update, the `planner` member again served `GET /api/workspace` from the actual stdio MCP process with 200. No member was silent. This read-only simulation does not establish assignment writes; the real DuckDB/MCP integration suite verifies those separately.
- 2026-10-26: The `planner` member served `GET /api/workspace` from the actual MCP process and returned 200. No member was silent. Confirmed from the simulation request log and the passing MCP result assertion; no cloud history was recorded.
<!-- end observations -->
