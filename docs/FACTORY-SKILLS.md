# Installed skill discovery and selection

The catalogue is a read-only observation of explicitly configured local sources.
It does not install skills, execute agents or establish model compatibility.
Selections retain the existing `(id, source)` contract; prepared work orders keep
resolved paths and actual byte hashes, and execution rechecks them before use.

## Contract

`GET /api/factory/projects/{id}/skills` returns the current project-context
catalogue: entries, source coverage, observation time and a content fingerprint.
It uses the existing loopback/same-origin/no-store factory boundary. It reads no
arbitrary paths supplied through HTTP, changes no workspace data and makes no
provider/model calls. Refresh repeats the observation; it never adopts references
or starts a run.

`Factory:Skills` remains the exact explicit-file mapping. Optional
`Factory:SkillDiscovery:Roots` entries declare `{Source, Path, MaximumDepth}`.
There are no guessed home/plugin directories. Relative paths resolve against the
project's connected repository; without a repository they remain unavailable.
Each real directory containing `SKILL.md` within the configured depth contributes
an ID equal to its relative directory path with `/` separators. A root that is
itself a skill directory uses its directory name. Sources remain exact configured
strings. Display titles come from the first Markdown heading after front matter;
YAML metadata is not interpreted as an identity or compatibility declaration.
Display headings are limited to 200 characters; execution reads the full validated file.

Explicit and discovered entries use one resolution path. The same pair and real
file deduplicate; different files for the same pair are ambiguous. Missing,
non-UTF-8, oversized and symlinked files are unavailable. Inaccessible directories,
symlinked directories or exhausted scan bounds report incomplete source coverage;
selected references in that source cannot be certified unique. `MaximumEntries`,
`MaximumDirectories` and `TimeoutSeconds` bound scanning. A successful empty source
is distinct from a source that was never configured or could not be scanned.

The picker searches IDs, sources, titles and paths. Adding/removing a selection
only edits the pipeline draft or task override. Existing/manual references stay
visible, including unresolved references. Published definitions remain immutable;
changing an installed file does not rewrite earlier work orders or approvals.

## Local deployment configuration

Machine paths may be declared in ignored `server/Planner.Api/appsettings.Local.json`.
It uses the same `Factory:Skills` / `Factory:SkillDiscovery` configuration shape.
Environment variables and command-line arguments retain higher precedence. The
file is optional and not reloadable during a captured run; restart the backend
after changing deployment configuration. No machine paths or demonstration
skills are committed as application defaults.
