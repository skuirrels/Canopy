# Canopy factory skills

This checkout supplies three project-owned, self-contained skills. They support
the factory's existing captured work and operator decisions; registering or
selecting them starts no work.

| Skill | Purpose | Calibration edit target |
| --- | --- | --- |
| `canopy-calibrator` | Evaluate a verified delivery's bounded packet and propose evidence-backed improvements to the permitted mirrors | No |
| `canopy-qa` | Map criteria to actual assertions and runtime evidence, reporting meaningful gaps | Yes |
| `canopy-review` | Assess the raw change independently and recheck stable prior findings | Yes |

Their source files live in `.agents/skills/<name>/SKILL.md`. Discovery metadata
in `agents/openai.yaml` supplies labels and example prompts. Every instruction
needed by calibration remains in SKILL.md because the host copies only that file.
The QA/review skills follow the target repository and captured verification
configuration; they do not assume that every connected project uses Canopy's stack.

## Register this checkout

Run from the checkout that owns the canonical skills. For the live application,
use its normal main checkout after merging, rather than a temporary worktree:

```sh
python3 scripts/configure-canopy-skills.py
python3 scripts/configure-canopy-skills.py --apply
```

The first command previews the changes. The second writes machine-local
`server/Planner.Api/appsettings.Local.json`; back up existing local configuration
before applying. `--config` can select another file named `appsettings.Local.json`.
Restart the backend afterward, then refresh installed skills in the UI.

The source is **Canopy factory skills**. Registration uses the actual checkout's
absolute paths, preserves unrelated settings and existing skill sources, and
permits canonical edits only inside the `canopy-qa` and `canopy-review` folders.
The exact selected SKILL.md targets still come from project policy. Conflicting
registrations or different existing edit roots require explicit inspection;
the helper does not overwrite or widen them. Exact retries leave the file unchanged.
It rejects linked configuration destinations and ambiguous case-insensitive JSON
keys. It does not provide an OS-level cross-process compare-and-swap guarantee.

System/plugin skills, shared global guidance, rules, memory and the calibrator
remain outside this setup's editable scope. Global skill discovery stays available
for ordinary read-only selection.

## Use QA and review

In the pipeline designer, select `canopy-qa` for the relevant QA agent step and
`canopy-review` for the independent read-only reviewer. Select **Canopy factory
skills** as the source. Publish a version and explicitly attach it to a task;
existing attachments and historical work orders are unchanged.

The reports supply material for structured **Acceptance QA** and **Independent
review** decisions. They do not submit those decisions, accept tasks, start repairs
or grant PR/deployment authority. Choose safe, isolated verification commands and
follow the owning repository's requirements.

## Configure manual calibration

In **Software Factory → Skill calibration**, select:

- Installed calibrator: `canopy-calibrator` from **Canopy factory skills**.
- Existing targets: `canopy-qa` and `canopy-review` from that same source.
- Your explicit provider, model, optional effort and elapsed limit. The host
  permits one documentation-role invocation and no separately scheduled commands.
- **Automatically prepare and run**: off.
- **Automatically adopt**: off.

No project/model policy is invented by registration. Without an adopted enabled
policy, calibration remains disabled. Unsupported hard token/currency caps block
dispatch; execution without them requires explicit unmetered-cost consent.

After a verified delivered merge, open **Controlled PR delivery → Post-delivery
skill calibration**. Prepare, inspect the captured work and explicitly start.
The learner reads the selected evidence packet and permitted existing mirrors;
it does not scan history, access canonical destinations or modify the packet,
product, Git state, rules, memory or itself. Target indexes are not role identities.

Review the actual diff, evidence and declined lessons. A missing cause, unresolved
outcome or already-covered lesson should remain unchanged. Choose **Adopt reviewed
skill proposal** only when satisfied. Partial/conflicting file outcomes need
reconciliation; canonical edits do not commit or publish themselves.

## Validation and limits

Use the skill-creator validator for frontmatter/structure. The registration checks
run with `python3 scripts/canopy-skills-configuration-test.py` and in the core CI
group. They exercise the actual source files, preview/apply, setting preservation,
restricted scope, exact retries, conflicting mappings/roots and malformed or
linked configuration. These checks do not establish live-model lesson quality.
Judge that through an explicitly started delivery calibration and actual proposal.

Native verification with a disposable DuckDB workspace also resolved all three
actual files and byte hashes, observed the configured edit scope with no project
policy or factory runs, and preserved that catalogue/workspace across restart.
The test project was removed through the public API. No learner was dispatched.

See the [quick start](PHASE1-QUICKSTART.md) and
[calibration contract](FACTORY-CALIBRATION.md).
