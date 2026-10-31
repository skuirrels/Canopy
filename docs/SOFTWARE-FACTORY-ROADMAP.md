# Software Factory roadmap

Canopy records acceptance criteria, revision-bound verification evidence and
explicit operator QA verdicts, independent review rounds and persistent findings.
It also supports bounded repair, controlled PR delivery and verified problem intake.

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

Items 1–5 are delivered with explicit operator decisions, independent review,
bounded repair, controlled GitHub delivery and manual verified intake. Order
expresses priority and dependencies, with no delivery dates or estimates assigned.

| Order | Item | Depends on | Completion outcome |
| --- | --- | --- | --- |
| 1 | [Delivered: acceptance criteria and evidence based QA](FACTORY-QUALITY.md) | Delivered foundation | Captured criteria, explicit operator verdicts, test/command references and stale-evidence detection |
| 2 | [Delivered: independent review with persistent findings](FACTORY-REVIEW.md) | Item 1 | Stable finding IDs, assessment before builder comparison, verified rechecks and revision-bound approval |
| 3 | [Delivered: bounded repair and verification loop](FACTORY-REPAIR.md) | Items 1 and 2 | Findings return to the responsible stage and are verified after repair |
| 4 | [Delivered: controlled PR delivery and merge verification](FACTORY-DELIVERY.md) | Items 1 and 2; item 3 for automatic repairs | Only the reviewed and verified revision can pass the configured merge policy |
| 5 | [Delivered: verified problem intake](FACTORY-INTAKE.md) | Criteria and finding contracts from items 1 and 2 | Configured signals become deduplicated backlog candidates with provenance |

## Acceptance criteria and evidence based QA

- [x] Give acceptance criteria stable identities within the captured work order.
  Flag missing, vague or unmeasurable criteria before unattended work; proposed
  clarifications require explicit adoption.
- [x] Link each criterion to the asserting test and relevant runtime evidence,
  such as a command result, screenshot, log or persisted destination record.
  An uncovered criterion remains visible.
- [x] Record explicit pass, fail, unverified or justified not-applicable verdicts.
  Agent process completion, a report, or a green unrelated suite cannot establish
  that a criterion passed.
- [x] Capture the code revision, relevant configuration and verification context
  with each verdict. Changed inputs make affected evidence stale and require
  fresh verification.
- [x] Check test quality: assertions must cover the requested behaviour and
  meaningful failure cases. Use targeted mutation checks for high-risk logic
  where they establish that the test can detect the defect.

Validated with an isolated real fixture demonstrating passing, failing, uncovered
and stale criteria. The React Flow inspector explains each verdict and links its
evidence. The operator judges semantic test relevance and assertion quality;
nonempty fields and a green command do not automatically establish a verdict.
See [validation evidence](FACTORY-QUALITY-VALIDATION.md). Existing logical
artifacts retain their meaning; physical attachments require explicit capture
and provenance.

## Independent review with persistent findings

- [x] Give the reviewer the acceptance criteria and raw change before the
  builder's explanation so it can form an independent assessment.
- [x] Persist stable finding IDs, severity, responsible role, code/evidence
  references and status across review rounds.
- [x] Recheck prior findings first. Resolution requires code and verification
  evidence, or an explicit reason the finding no longer applies; a resolved
  discussion or builder assertion alone is insufficient.
- [x] Distinguish defects, missing behaviour, verification gaps, scope drift and
  optional improvements. Unrelated refactoring becomes separate backlog work.
- [x] Keep review authority separate from implementation. Fixing code does not
  approve that code, and a completed reviewer process does not mean approval.

Validated with repeated reviews retaining the same finding ID, detecting an
unfixed issue and verifying its later repair with actual code and a strengthened
assertion. Approval binds the reviewed code and exact QA verdict identities; it
does not accept the task. See [review validation](FACTORY-REVIEW-VALIDATION.md).

## Bounded repair and verification loop

- [x] Route actionable findings to implementation or QA, retaining the finding
  and evidence that caused the transition.
- [x] Record each repair and verification round as a distinct attempt. Preserve
  earlier specifications, outputs and review receipts.
- [x] Configure retry, time and cost budgets through project policy. Halt on
  exhausted budgets, repeated lack of progress, scope changes or required human
  decisions rather than looping indefinitely.
- [x] Re-run affected checks after repairs and invalidate approval for changed
  code. Earlier successful checks cannot approve a later revision.
- [x] Preserve explicit stop, cancellation and interruption semantics. A backend
  restart must not silently replay work or repeat external actions.

Delivered with distinct retained attempts, captured project limits, explicit start/stop,
fresh QA and independent rechecks. Monetary usage is unavailable: a configured
currency cap blocks dispatch, and uncapped execution requires explicit consent.
Task workflow status stays independent. See [repair validation](FACTORY-REPAIR-VALIDATION.md).

## Controlled PR delivery and merge verification

- [x] Add explicit project policy for branch, draft PR and merge authority.
  Creating a PR, merging, releasing and deploying remain separate actions.
- [x] Associate each PR with its captured work order, actual branch/head commit,
  verification evidence and review findings.
- [x] Require the owning project's configured verification commands and required
  GitHub checks. Missing or unavailable evidence blocks the gate.
- [x] Verify integration with the current target branch immediately before
  merging. Pin review and merge to the same head; a changed head or base requires
  renewed verification. Use server-side protection or a merge queue where needed
  rather than treating a local mutex as protection from every writer.
- [x] Bind human approvals to the reviewed revision and requested action.
  Configure sensitive changes and migration environments explicitly.
- [x] Reconcile the actual merge result and target-branch ancestry before marking
  delivery complete. Retries must not repeat a completed merge or external step.
  Configured post-merge restarts retain health and persistence receipts; failures
  remain visible as operational follow-up.

Delivered for GitHub direct merge commits with strict, non-bypassable branch
protection and configured check App IDs. Unsupported protection, queue/method or
provider evidence blocks the gate. Project policy and each requested action remain
explicit. Configured post-merge hooks verify instance, deployed revision, health
and persistence. See [delivery validation](FACTORY-DELIVERY-VALIDATION.md).

## Verified problem intake

- [x] Connect explicitly configured GitHub issue and recurring review-finding
  sources; retain each source's own identity and evidence. Integrations remain
  optional. Configured application-error snapshots are delivered as the source
  extensions below. External connections remain explicitly configured.
- [x] Verify each candidate, assign a durable fingerprint and deduplicate against
  existing work. Preserve links to the original signal.
- [x] Distinguish a successful empty read from unavailable or truncated coverage.
  Failed source reads must not imply that no problems exist.
- [x] Preview proposed intake before explicit filing or linking. Automatic filing
  is deferred. Candidates enter an explicitly chosen incomplete backlog column;
  intake grants no execution, Ready-state or merge authority.

Delivered with durable fingerprints, complete/failed/interrupted collection
receipts, immutable observations and revision-bound triage decisions. Recurrence
counts distinct tasks, not repeated rechecks. Current source evidence is rechecked
before filing, and exact retries retain one task link even after deletion.
Operator verification judges relevance and reproduction; source collection alone
does not establish a defect. See [intake validation](FACTORY-INTAKE-VALIDATION.md).

## Model routing and execution budgets

- [x] Opt-in project routes for existing agent roles, with explicit provider,
  model and optional effort. Task and step choices retain precedence/provenance.
- [x] Immutable resolved settings, policy hashes, CLI invocation reservations
  and elapsed deadlines, including manual waits and owned process cancellation.
- [x] Apply the same captured policy to repair plans alongside loop-wide limits;
  policy changes revoke old dispatch and invalidate QA/review/delivery authority.
- [x] Retain provider completion observations without fabricating missing usage,
  cache equivalence or billing. Unsupported token/currency caps block execution.
- [x] Persist policy, reservations and observations across native restart, with
  no implicit reroute or replay. Inspect settings and evidence in the running UI.

See [execution policy](FACTORY-EXECUTION-POLICY.md) and its
[validation](FACTORY-EXECUTION-POLICY-VALIDATION.md). This controls CLI invocations;
provider-internal calls and hard spend enforcement remain outside its guarantees.

## Application-error intake

- [x] Explicitly configured structured log snapshots, with source/application/
  environment identity, selected levels and a distinct-event threshold.
- [x] Closed producer-sanitized schema, complete bounded windows, native receipts
  for empty coverage and fail-closed malformed, stale or conflicting evidence.
- [x] Persistent occurrence identity checks across windows and restart, durable
  group fingerprints, immutable observations and explicit verification/file/link.
- [x] UI feed selection, scope/coverage history, code frames and occurrence evidence;
  source saves revoke current authority until collection.

See [structured feed setup and contract](FACTORY-ERROR-INTAKE.md) and
[validation](FACTORY-ERROR-INTAKE-VALIDATION.md). No real error producer is configured
by default. Exporter installation and automatic filing remain
separate work; intake still grants no execution authority.

## Analytics signal intake

- [x] Configured metric snapshots with explicit source/application/environment,
  metric identity, unit/statistic, dimension allowlist and threshold/sample rules.
- [x] Exact decimal/count strings, complete bounded windows, immutable native
  report identities and distinct unavailable/zero-sample/empty coverage.
- [x] Semantic rule/series fingerprints, native coverage receipts and immutable
  observations, fresh explicit verification and duplicate-safe filing/linking.
- [x] UI rule selection, exact quantities, dimensions and coverage history;
  source edits revoke authority and unavailable selections remain visible.

See [analytics setup and contract](FACTORY-ANALYTICS-INTAKE.md) and
[validation](FACTORY-ANALYTICS-INTAKE-VALIDATION.md). Reported aggregates and sample
counts belong to the configured producer. Threshold crossings require operator
verification; no production source, business rule or automatic filing is enabled
by default.

## Parallel stage execution

- [x] Explicit captured concurrency on the enabled project execution policy;
  legacy/absent settings retain serial execution and existing policy hashes.
- [x] Retained stage worktrees, ordered steps, dependency-only source joins and
  native input/output/index/HEAD checkpoints. Conflicts fail visibly.
- [x] Stage-local approvals, bounded reservations and elapsed time, owned child
  cancellation, revocation and restart interruption without replay.
- [x] Final integrated QA/review/repair context, stage inspection in React Flow and
  the step list, compatibility and native migration regressions.

See [parallel execution](FACTORY-PARALLEL-EXECUTION.md) and
[validation](FACTORY-PARALLEL-VALIDATION.md). Concurrency is opt-in; ignored build
outputs and physical artifacts are not distributed as source.

## Post-delivery skill calibration

- [x] Explicit installed calibrator, selected existing skill targets, administrator
  edit roots and a separate bounded model policy; disabled by default.
- [x] Fresh verified merge/check/QA/review evidence, bounded selected-delivery
  packets, duplicate-safe capture and future-delivery automatic observations.
- [x] Retained isolated repositories, real file/path/permission and diff audits,
  owned process cancellation, policy revocation and restart without replay.
- [x] Exact proposal review, guarded host adoption, per-file write journals and
  explicit reconciliation/acknowledgment of partial or uncertain outcomes.

See [calibration setup and contract](FACTORY-CALIBRATION.md) and
[validation](FACTORY-CALIBRATION-VALIDATION.md). Path and hash verification does not
judge semantic lesson quality. No real calibrator or canonical edit scope is
installed or enabled by this phase; private skills, memory and publication remain
under separate authority.

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

Installed-skill discovery and a bounded real-provider implementation/check/review
pilot are delivered. Discovery uses explicitly configured roots, a searchable
picker and the same canonical file/hash resolver as execution. See
[skills](FACTORY-SKILLS.md) and [validation](FACTORY-SKILLS-VALIDATION.md). Broader
live-provider validation is now exercised by the protected GitHub pilot; its
failed/recovered attempts and completed deliveries must be judged from retained
operational receipts, not PR state alone. See [live delivery](FACTORY-LIVE-DELIVERY.md).
Named deployment targets, separate revision-bound approvals and explicit recovery
are delivered as an extension of controlled delivery. See
[deployment environments](FACTORY-DEPLOYMENTS.md) and
[validation](FACTORY-DEPLOYMENTS-VALIDATION.md). Authentication and team access remain a separate deployment
track.

## Design references

The selected patterns come from the
[Super Board project](https://erictechpro.github.io/super-board/) and its
[QA evidence checks](https://github.com/EricTechPro/super-board/blob/main/skills/super-qa/SKILL.md#test-gap-check-after-build),
[review memory and feedback loop](https://github.com/EricTechPro/super-board/blob/main/skills/super-review/SKILL.md#review-remembers),
[merge gate](https://github.com/EricTechPro/super-board/blob/main/scripts/super-board-merge-gate.sh)
and [verified intake](https://github.com/EricTechPro/super-board/blob/main/skills/super-collect/SKILL.md).
These guide Canopy's requirements; host-specific scripts and standalone QA
defaults are not adopted wholesale.
