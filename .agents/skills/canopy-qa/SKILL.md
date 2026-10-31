---
name: canopy-qa
description: Verify a captured Canopy factory work order against its acceptance criteria with actual test and runtime evidence. Use for a QA step or an explicit verification request; it does not approve delivery, accept tasks or start repair work.
---

# Canopy QA

Judge the selected work order against observable requirements. Follow its target
repository's instructions, configured verification commands, environment and
permissions. Do not assume the target has Canopy's stack, test command or data.

## Inspect coverage before claiming a result

Map each captured criterion to the assertion and observable result that would
establish it. Flag ambiguous requirements and uncovered criteria explicitly.
Compare the asserting line with the requested behavior: a test of a neighboring
case is not coverage. Check a relevant negative case; use a focused mutation check
when it materially tests whether an important assertion detects the defect.

Run only authorized checks within captured limits. Use isolated test data and
configured providers; derive domain values through real application paths.
Preserve unrelated work. Report missing credentials, tools or coverage as gaps.
For UI or persistence changes, inspect the running interaction and the saved or
reloaded result as appropriate, rather than relying on a build alone.

## Bind evidence to the tested inputs

Identify the actual revision and relevant configuration. Distinguish a native
command receipt from an agent report, a logical artifact name from an actual file,
and a protocol fixture from a live integration. Record command/exit outcome,
asserting path and line, what it proves, the failure case it detects and any
relevant runtime or destination evidence. Missing usage or external observations
remain unknown.

A passing assertion must concern the requested behavior and the current inputs.
Code, specification, skill or configuration changes require the affected checks
to be repeated; task input changes require a fresh captured work order. A failed
or unavailable check does not become successful because another suite is green.

## Report for the operator

Return criterion coverage with evidence references and distinguish pass, fail,
unverified and justified not applicable. Explain why each conclusion follows and
which environment/input boundaries remain untested. Retain original failures and
external changes. Stop at missing authority, exhausted limits or a need to expand
scope; do not silently rerun non-repeatable actions or start a repair loop.

This report supplies evidence for the operator's structured Acceptance QA verdict.
It does not submit that verdict through Canopy APIs, resolve review findings,
merge, deploy, edit skills or memory, or change task status. Keep verification
separate from independent review and acceptance.
