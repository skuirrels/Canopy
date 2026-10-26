# Talk to Canopy from Claude and Codex

Canopy MCP is a local stdio server using the official MCP TypeScript SDK. It calls the running Canopy HTTP API. It never opens DuckDB, starts another backend, creates a browser-only store, or bypasses domain validation. Claude Desktop, Claude Code and local Codex clients can each launch their own MCP process while sharing the same single Canopy backend.

## Install and check

Requires Node.js 22 or later. Start Canopy normally, then:

```sh
cd /absolute/path/to/Canopy
npm --prefix mcp ci
node mcp/src/index.mjs --check
node mcp/src/index.mjs --print-config
```

`--print-config` generates the exact executable paths and connection settings for this checkout. It prints configuration; it does not change client settings. With no flags the process speaks MCP on stdin/stdout, so it normally waits silently.

## Codex

Use the `codexCommand` printed by `--print-config`, or add the generated `codexToml` table to your existing Codex config. A ready-to-copy example for this checkout is in [examples/codex.toml](examples/codex.toml).

The command has this shape:

```sh
codex mcp add canopy-planner --env PLANNER_URL=http://127.0.0.1:5274/ --env PLANNER_SPEC_ROOT=/absolute/path/to/Canopy -- /absolute/path/to/node /absolute/path/to/Canopy/mcp/src/index.mjs
```

Use the actual Node path printed by the generator. Start a new chat or reload MCP connections, then confirm `canopy-planner` is available. Local Codex clients share MCP configuration on the same host. See [official OpenAI MCP setup](https://learn.chatgpt.com/docs/extend/mcp?surface=cli).

## Claude Desktop and Claude Code

In Claude Desktop, open **Settings → Developer → Edit Config** and add the generated `claudeDesktop.mcpServers.canopy-planner` entry under your existing `mcpServers` object. Preserve your other servers. The complete example is [examples/claude-desktop.json](examples/claude-desktop.json). Fully quit and reopen Claude Desktop, then look for the Canopy tools under its connectors/local MCP servers.

Claude Code can register the same command directly:

```sh
claude mcp add --transport stdio canopy-planner --env PLANNER_URL=http://127.0.0.1:5274/ --env PLANNER_SPEC_ROOT=/absolute/path/to/Canopy -- /absolute/path/to/node /absolute/path/to/Canopy/mcp/src/index.mjs
```

See [Claude Code MCP setup](https://code.claude.com/docs/en/mcp) and the [official local MCP connection guide](https://modelcontextprotocol.io/docs/develop/connect-local-servers). This local connection is for desktop/CLI clients; a Claude web/cloud connector cannot reach a loopback-only backend.

Task effort is optional and independent of calendar dates. Use `update_task` with `estimatedHours` (a nonnegative number, including fractions); `null` clears it and omission preserves it. Markdown spec uploads preserve the estimate.

## Available tools

| Tool | Purpose |
| --- | --- |
| `list_projects`, `get_project` | Find projects, revisions, configured workflow statuses and assignees |
| `list_tasks`, `get_task` | Explore components and nested tasks; search titles; read Markdown specs, dates, assignments and dependencies |
| `create_project`, `create_task` | Build projects, components and nested tasks using configured workflow defaults |
| `update_task` | Change supplied task fields, estimated hours, dates, priority, assignee IDs or prerequisite IDs; omitted fields remain unchanged |
| `set_task_spec` | Send Markdown directly, choosing `append` or `replace` |
| `upload_task_spec` | Import a UTF-8 `.md`/`.markdown` file from the configured specification folder |
| `move_task` | Reparent a branch or move its workflow status, retaining descendants |
| `reorder_components` | Change the full sibling component sequence independently of Kanban order |
| `list_assignees`, `create_assignee` | Read/create planning assignee records |

Use `update_task` with `assigneeIds: [firstPersonId, secondPersonId]` to assign a task to two people. The list replaces all assignments; `[]` clears them. `list_tasks` filters by any assigned person using `assigneeId`. `get_task` returns all `assignees`, and Markdown spec uploads preserve the complete list. Legacy `assigneeId` updates remain supported as a single-person replacement (`null` clears the list); prefer `assigneeIds` for new clients.
| `delete_task` | Explicitly delete a task and its subtree; annotated as destructive |

Read tools paginate where appropriate (`offset`, `limit`, `nextOffset`); follow `nextOffset` to see all results. Every project mutation requires `expectedRevision`, obtained from a current read or the previous mutation result. Conflicts return `revision_conflict`; reread and reapply only the intended edit. No automatic write retries occur. Read the project after an interrupted write before deciding whether to retry.

## Markdown specifications

Headings, bold/italic/strike, links, bullet/numbered lists, nested task checklists, quotes, fenced code blocks, horizontal rules and tables convert into the same native rich-text JSON used by the editor. Specs remain editable in Canopy; `get_task` returns their current Markdown, including changes made in the UI. Formatting is normalized rather than retained byte-for-byte. `includeDocument: true` also returns native JSON.

Raw HTML, image embeds and unsafe link schemes reject without saving; use text/Markdown links instead. Files are imported as specification content, not stored as separate attachments. The backend's existing 2 MB spec limit also applies to the converted JSON. Only the spec changes: task hierarchy, dates, assignment and dependencies are preserved.

Example prompts:

- “Find the Website launch project and list its components.”
- “Create a nested task under Design, due next Friday, and assign it to Alex.”
- “Upload `specs/customer-portal.md` to the Customer portal task, replacing its current specification.”
- “Append these acceptance criteria to the task spec and preserve its existing content.”
- “Make this task depend on the API task, then move it to In progress.”

For uploads, paths may be absolute or relative to `PLANNER_SPEC_ROOT`. The root defaults to this Canopy checkout. Set it to your intended specs folder in the connection config to upload files from another project. Resolved paths, including symlinks, must stay inside that folder. Passing Markdown text to `set_task_spec` does not read the filesystem. Markdown links/includes are never fetched.

## Configuration

| Variable | Behavior |
| --- | --- |
| `PLANNER_URL` | Loopback HTTP(S) base URL; defaults to `Urls` in Canopy's `appsettings.json` |
| `PLANNER_SPEC_ROOT` | Folder the file-upload tool may read; defaults to this checkout |
| `PLANNER_MCP_READ_ONLY` | Set `true` to expose only the five read tools, with no uploads or mutations |
| `PLANNER_TIMEOUT_MS` | HTTP timeout, default 10000 ms; range 100–120000 |

The server listens on stdio and opens no network listener. Backend addresses are restricted to loopback; redirects are rejected. Existing phase-1 single-user/no-authentication limits still apply. Client approvals control tool execution; the server does not grant team access.

## Verification

```sh
npm --prefix mcp test
PLANNER_TEST_URL=http://127.0.0.1:5275 npm --prefix mcp run test:integration
```

Point the integration suite at an isolated backend with its own DuckDB file. It exercises the official MCP client over stdio, calls the actual API, creates its own fixture project/assignee, and removes them in `finally`. It checks Markdown upload/readback, metadata preservation, append/replace, pagination, revisions, invalid structural edits, component order, deletion and read-only mode. Unit checks cover legacy 2025 client negotiation, Markdown roundtrips, upload path confinement/symlinks, UTF-8 and size limits. Client application configuration and reload remain separate from protocol verification.

For the focused Postman HTTP contract simulation:

```sh
postman simulation run postman/simulations/planner-mcp.sim.yaml --port 5278 --no-history
PLANNER_SIM_URL=http://127.0.0.1:5278/planner node mcp/tests/simulation.mjs
```

The simulation models only first-use workspace reads; mutation/persistence claims come from the real backend tests.
