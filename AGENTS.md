<API_TASKS>
Before responding to any non-trivial API engineering task — designing, implementing, mocking, testing, monitoring, documenting, or deploying an API or Postman Flow — read `postman/skills/api-engineer/SKILL.md` and follow it. It is the default entry point for API work in this repository. Pure questions and trivial one-line edits do not need it.
</API_TASKS>

# Planner implementation notes

- Phase 1 is explicitly a single-user, single-backend-process native DuckDB deployment. Authentication and team access are deferred to phase 2.
- Preserve the public EFCoreProvider path; do not replace it with a JSON store, browser-only storage, or a different database.
- Keep hierarchy, workflow status, and map layout independent. Domain rules belong in Domain; transaction coordination and persistence belong behind IWorkspaceStore.
- Never hide missing data with hard-coded domain values. Workflow defaults belong in configuration; project, task and assignee data come from public API inputs. Test projects must remain test-only and be cleaned up.
- Test the running interface, persistence across restart, and invalid structural edits. Do not treat a build alone as interaction verification.
