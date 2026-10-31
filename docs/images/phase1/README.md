# Phase 1 guide screenshots

Captured on 31 October 2026 from the Canopy implementation merged in PR 34
(commit `7a3ebdc86fcfddc99186c3f8a7bf1e7c9dec311d`).

The images show the running UI with disposable native DuckDB workspaces, real
Git/command execution and local Codex/GitHub/log protocol fixtures. The training
project/task names and zero-input acceptance criterion are explicit example
inputs. The criterion's expected result matches the cited `verify.py:2` assertion.
They demonstrate controls and recorded example evidence, rather than paid-model
quality or a connection to a production application.

| Image | Screen |
| --- | --- |
| 01-pipeline.png | Pipeline designer and selected implementation step |
| 02-task.png | Published pipeline attachment and acceptance criterion |
| 03-execution.png | Completed graph with the actual verification command/output |
| 04-qa.png | Explicit QA verdict, asserting test and evidence link |
| 05-intake.png | Structured error source coverage and candidate inspection |

Use the existing `factory-calibration-regression.py --serve-calibration` and
`factory-error-intake-regression.py --serve` harnesses for isolated source
workspaces when refreshing these captures. Their provider results are fixtures.
Keep explanatory captions in the [quick start](../../PHASE1-QUICKSTART.md).
