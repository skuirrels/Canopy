---
name: canopy-review
description: Independently assess a captured Canopy change and recheck persistent review findings against current code and verification evidence. Use for a read-only review step or explicit review request; it does not implement fixes or grant merge or deployment authority.
---

# Canopy Review

Review the requested scope under the target repository's instructions and captured
read-only permissions. Read its acceptance criteria, raw change and previous
findings before the builder's explanation. Keep that explanation as claims to
check after forming your own assessment.

## Establish current evidence

Identify the reviewed revision and context. Separate actual code, test assertions,
command/runtime receipts and destination observations from reassuring reports.
If required inputs are missing or stale, explain the precise verification gap;
do not infer approval from a completed process or another project's green suite.
Use only authorized local inspection/checks and configured sources. Do not assume
a stack or provider that the target project has not selected.

## Recheck and assess

Recheck each prior stable finding before adding new ones. Keep its identity and
explain whether it is fixed, unfixed, unverified or no longer applicable. A fixed
finding needs current code and relevant verification evidence; a changed rationale
or resolved discussion is insufficient. Preserve an earlier failure when recording
later recovery. If a finding no longer applies, state the actual scope/context
change rather than claiming its code was repaired.

Assess correctness, requested behavior, meaningful test coverage, domain-data
provenance and effects on the relevant user interaction, persistence or external
action. Test fixtures cannot become runtime sources for desired domain results.
Classify actionable issues as defects, missing behavior, verification gaps or
scope drift; keep optional improvements distinct. Give a concrete consequence,
severity, responsible role and tight code/criterion/evidence references. Avoid
speculative issues, style-only blockers, duplicate findings and unrelated cleanup.

## Report an independent recommendation

Report your assessment, every prior finding's recheck, new actionable findings,
remaining evidence gaps and an approve/request-changes/inconclusive recommendation.
Recommend approval only when current acceptance QA supports the captured criteria
and no blocking finding remains unresolved. Fresh code, inputs or QA decisions
require renewed review; earlier approval does not cover a later revision.

The operator submits the structured Independent review decision. Your report
neither authenticates role separation nor grants approval. Do not use workflow
control APIs to submit decisions or change task state. Application API checks
require captured test authority and isolated targets. Do not implement fixes, edit
skills/rules/memory, start repairs, commit, push, merge or deploy. Route a useful
out-of-scope idea as a separate proposal and stop when captured authority or
budget is exhausted.
