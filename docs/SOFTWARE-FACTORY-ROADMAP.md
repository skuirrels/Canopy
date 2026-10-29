# Software Factory roadmap

Canopy's next Software Factory work connects acceptance criteria, verification,
review findings and repair attempts into an optional delivery loop. Quality gates
come first, followed by controlled PR delivery and verified problem intake.

## Delivered foundation

- [x] Persistent pipelines, configured templates, custom stages, immutable
  published versions and task overrides. [Configuration](SOFTWARE-FACTORY.md)
- [x] React Flow configuration editor and task attachment experience.
  [Designer](FACTORY-DESIGNER.md)
- [x] Captured work orders, explicit execution, retained worktrees, manual gates,
  cancellation, interruption recovery and actual runtime evidence.
  [Runs](FACTORY-RUNS.md) · [PR 13](https://github.com/skuirrels/Canopy/pull/13) ·
  [PR 14](https://github.com/skuirrels/Canopy/pull/14)
- [x] React Flow execution inspection, step lists, output, reports, diffs and
  logical evidence navigation. [PR 15](https://github.com/skuirrels/Canopy/pull/15)

## Planned priorities

All items below are pending. Order expresses priority and dependencies, with no
delivery dates or estimates assigned.

| Order | Item | Depends on | Completion outcome |
| --- | --- | --- | --- |
| 1 | Acceptance criteria and evidence based QA | Delivered foundation | Every criterion has an explicit verdict backed by relevant evidence |
| 2 | Independent review with persistent findings | Item 1 | Review findings survive revisions and require proof of resolution |
| 3 | Bounded repair and verification loop | Items 1 and 2 | Findings return to the responsible stage and are verified after repair |
| 4 | Controlled PR delivery and merge verification | Items 1 and 2; item 3 for automatic repairs | Only the reviewed and verified revision can pass the configured merge policy |
| 5 | Verified problem intake | Criteria and finding contracts from items 1 and 2 | Configured signals become deduplicated backlog candidates with provenance |

## Acceptance criteria and evidence based QA

- [ ] Give acceptance criteria stable identities within the captured work order.
  Flag missing, vague or unmeasurable criteria before unattended work; proposed
  clarifications require explicit adoption.
- [ ] Link each criterion to the asserting test and relevant runtime evidence,
  such as a command result, screenshot, log or persisted destination record.
  An uncovered criterion remains visible.
- [ ] Record explicit pass, fail, unverified or justified not-applicable verdicts.
  Agent process completion, a report, or a green unrelated suite cannot establish
  that a criterion passed.
- [ ] Capture the code revision, relevant configuration and verification context
  with each verdict. Changed inputs make affected evidence stale and require
  fresh verification.
- [ ] Check test quality: assertions must cover the requested behaviour and
  meaningful failure cases. Use targeted mutation checks for high-risk logic
  where they establish that the test can detect the defect.

Complete when a real fixture demonstrates passing, failing, uncovered and stale
criteria, with the React Flow inspector explaining each verdict and linking its
evidence. Existing logical artifacts retain their meaning; physical attachments
require explicit capture and provenance.

## Independent review with persistent findings

- [ ] Give the reviewer the acceptance criteria and raw change before the
  builder's explanation so it can form an independent assessment.
- [ ] Persist stable finding IDs, severity, responsible role, code/evidence
  references and status across review rounds.
- [ ] Recheck prior findings first. Resolution requires code and verification
  evidence, or an explicit reason the finding no longer applies; a resolved
  discussion or builder assertion alone is insufficient.
- [ ] Distinguish defects, missing behaviour, verification gaps, scope drift and
  optional improvements. Unrelated refactoring becomes separate backlog work.
- [ ] Keep review authority separate from implementation. Fixing code does not
  approve that code, and a completed reviewer process does not mean approval.

Complete when a second review retains finding identities, detects an unfixed
issue, verifies a repaired issue and records its verdict against the reviewed
revision without automatically accepting the task.

## Bounded repair and verification loop

- [ ] Route actionable findings to implementation or QA, retaining the finding
  and evidence that caused the transition.
- [ ] Record each repair and verification round as a distinct attempt. Preserve
  earlier specifications, outputs and review receipts.
- [ ] Configure retry, time and cost budgets through project policy. Halt on
  exhausted budgets, repeated lack of progress, scope changes or required human
  decisions rather than looping indefinitely.
- [ ] Re-run affected checks after repairs and invalidate approval for changed
  code. Earlier successful checks cannot approve a later revision.
- [ ] Preserve explicit stop, cancellation and interruption semantics. A backend
  restart must not silently replay work or repeat external actions.

Complete when a failing check returns work to its responsible stage, a repair
receives fresh QA/review, and an unrepaired or repeatedly failing case stops with
an actionable explanation. Task workflow status stays independent.

## Controlled PR delivery and merge verification

- [ ] Add explicit project policy for branch, draft PR and merge authority.
  Creating a PR, merging, releasing and deploying remain separate actions.
- [ ] Associate each PR with its captured work order, actual branch/head commit,
  verification evidence and review findings.
- [ ] Require the owning project's configured verification commands and required
  GitHub checks. Missing or unavailable evidence blocks the gate.
- [ ] Verify integration with the current target branch immediately before
  merging. Pin review and merge to the same head; a changed head or base requires
  renewed verification. Use server-side protection or a merge queue where needed
  rather than treating a local mutex as protection from every writer.
- [ ] Bind human approvals to the reviewed revision and requested action.
  Configure sensitive changes and migration environments explicitly.
- [ ] Reconcile the actual merge result and target-branch ancestry before marking
  delivery complete. Retries must not repeat a completed merge or external step.
  Configured post-merge restarts retain health and persistence receipts; failures
  remain visible as operational follow-up.

Complete when changed-head/base cases, failed checks, missing approvals and lost
responses cannot cause an unverified or duplicate merge, and the successful case
records the actual merge commit and any required post-merge checks.

## Verified problem intake

- [ ] Connect explicitly configured sources such as application errors, analytics,
  GitHub issues and recurring PR findings; retain each source's own identity and
  evidence. Integrations remain optional.
- [ ] Verify each candidate, assign a durable fingerprint and deduplicate against
  existing work. Preserve links to the original signal.
- [ ] Distinguish a successful empty read from unavailable or truncated coverage.
  Failed source reads must not imply that no problems exist.
- [ ] Preview proposed intake before filing unless the project explicitly enables
  automatic filing. Candidates enter the backlog for triage; intake grants no
  execution, Ready-state or merge authority.

Complete when repeated collection does not duplicate work, an unavailable source
is reported accurately, and every filed candidate links to verified source
evidence without starting implementation.

## Operating constraints

Planning, specification assistance and factory execution remain independent
explicit actions. Preserve native DuckDB through the public EFCoreProvider path,
the single-backend-process deployment and the separation of hierarchy, task
workflow status and graph layout.

QA uses explicitly configured test environments, test accounts and isolated
worktrees. It must not infer permission to read production credentials, create
or reset production users, or commit directly to a protected branch. Local command
execution retains its documented account permissions; guard hooks do not replace
an OS security boundary.

Installed-skill discovery and live-provider end-to-end validation remain existing
follow-up work. Authentication and team access remain a separate deployment
track. Model routing, parallel dispatch and automatic skill calibration are
deferred behind the quality and delivery priorities above.

## Design references

The selected patterns come from the
[Super Board project](https://erictechpro.github.io/super-board/) and its
[QA evidence checks](https://github.com/EricTechPro/super-board/blob/main/skills/super-qa/SKILL.md#test-gap-check-after-build),
[review memory and feedback loop](https://github.com/EricTechPro/super-board/blob/main/skills/super-review/SKILL.md#review-remembers),
[merge gate](https://github.com/EricTechPro/super-board/blob/main/scripts/super-board-merge-gate.sh)
and [verified intake](https://github.com/EricTechPro/super-board/blob/main/skills/super-collect/SKILL.md).
These guide Canopy's requirements; host-specific scripts and standalone QA
defaults are not adopted wholesale.
