# Markdown task specification

A reusable example of an editable task specification uploaded through MCP.

## Acceptance criteria

- [x] Import a Markdown specification
- [ ] Verify the task in Planner
  - [ ] Check nested acceptance criteria
- [ ] Confirm dependencies, dates and assignee remain unchanged

| Requirement | Expected outcome |
| --- | --- |
| Source | Caller-supplied Markdown |
| Storage | Existing Planner API and DuckDB |
| Editing | Rich-text editor with checklists and tables |

## Implementation notes

Use **explicit revisions** and preserve *unrelated task fields*.

> Review the stored task before retrying a failed write.

```json
{ "mode": "append" }
```

[Markdown reference](https://commonmark.org/)
