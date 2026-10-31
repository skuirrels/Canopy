# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Users

One person: a solo builder planning their own software projects and handing specified tasks to local coding agents, on their own machine. They are both the planner and the operator who accepts or rejects agent work. Team leads and members are a phase 2 audience only once authentication and membership exist; nothing is designed for them yet.

## Product Purpose

Canopy is a local project planner and Software Factory over one persistent task graph. The builder breaks a project into components, tasks and nested subtasks, writes specifications, sets dates, estimates, priorities, assignees and prerequisites, and sees the same work as a Kanban board, mind map, Gantt timeline and horizontal Components plan. The factory then takes a specified task through a configured pipeline: captured work order, agent implementation in a separate worktree, acceptance-criteria QA, independent review with persistent findings, bounded repair, controlled GitHub PR delivery and verified problem intake.

Success means the builder can plan a project and move specified work to reviewed, verified delivery without leaving their machine, and can always tell what has actually been verified from what has merely been reported.

## Positioning

Planning and the factory are equal halves of one product, not a planner with an agent add-on or a pipeline with a task list bolted on. Both read and write the same tasks in one native DuckDB file. The factory treats agent completion, a green unrelated suite or a written report as distinct from the operator's acceptance: verdicts are explicit, revision-bound and go stale when their inputs change.

## Operating Context

- Runs locally on loopback (`127.0.0.1`), one backend process owning one DuckDB file; opened in a desktop browser, with phone-width use supported for Components and task details.
- Agents are the builder's own installed CLIs (Codex, Claude, Gemini/Antigravity), which supply their own sign-in. Canopy binds a local Git repository and base ref per project.
- Claude Desktop and Codex can also reach the same data through the local MCP server (`mcp/`).
- Inputs and outputs are Markdown: task specifications, linked `.md` spec files, and imported Markdown plans parsed into a reviewable backlog.
- Gantt and mind map export to print/PDF and to self-contained static HTML that the builder emails to other people; those recipients see a read-only snapshot.
- Delivery goes to GitHub as pull requests under a configured merge policy; deployment environments and recovery are configured explicitly.

## Capabilities and Constraints

- Views: Overview (due today, overdue, due tomorrow), Board, Mind map (vertical and horizontal), Gantt, Components, Assignees, Software Factory (guided setup, pipeline designer, run inspection). Task details open as a side panel or expanded modal with autosave.
- Hierarchy, workflow status and map layout are independent. Domain rules live in Domain; persistence goes through `IWorkspaceStore` and the public EF Core DuckDB provider.
- Workflow defaults come from configuration (`WorkflowDefaults`, default To Do, In Progress, Blocked, In Testing, Done). Project, task and assignee data come only from user input or public APIs; no example data is inserted on startup.
- Nothing runs implicitly: saving settings, publishing a pipeline, assigning a task or changing status never starts an agent. Canopy does not merge, push, deploy or change a task's status on an agent's behalf.
- Missing executables, sign-in, repositories, refs, permissions and failed saves are surfaced explicitly; there are no fallback values or estimated completion percentages.
- Drag interactions have keyboard equivalents; build effects respect reduced motion and default off.
- Each project has a user-chosen colour that drives its accents.
- Terminology: project; component (a top-level task); task and subtask; workflow status (columns); prerequisite or dependency; assignee (a planning record, not an account); specification; pipeline, stage, step; work order; run; verdict; finding; intake.
- Internal names (`Planner.sln`, `Planner.Api`, `PLANNER_*`, storage keys) remain for compatibility; the product name is Canopy.
- Undecided: phase 2 authentication, team membership and multi-user access; a JSON import interface.

## Brand Commitments

The product name is Canopy. The in-app voice is warm and encouraging (confirmed), as in "Make room for a new idea." and "A little progress, one card at a time."; documentation stays plain, exact and explicit about what the product does not do. No other identity or asset commitments have been made.

## Evidence on Hand

- `examples/website-launch.json`: a fictional, editable example project (23 tasks, four fictional assignees), loaded only on request.
- `docs/images/phase1/` and `docs/images/factory-setup/`: screenshots of isolated training workspaces using local provider fixtures.
- Validation records in `docs/*-VALIDATION.md`.
- There are no real users, customers, testimonials, usage metrics, pricing or benchmarks. Future work must not invent them.

## Product Principles

1. **Built for one person on their own machine.** No team, account or sharing ceremony until phase 2 actually delivers it.
2. **One task graph, many views.** Every view edits the same tasks; no view owns data the others cannot see.
3. **Planning and delivery are peers.** Neither half is presented as subordinate to the other.
4. **Nothing happens implicitly.** Work starts, merges and is accepted only on an explicit operator action.
5. **Show real state, never a stand-in.** Unverified stays visibly unverified, missing stays visibly missing, and no value is invented to fill a gap.
