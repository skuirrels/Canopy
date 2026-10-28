# Linked Markdown specifications

Task details → Specification → **Link Markdown file** accepts an absolute path to an existing UTF-8 `.md` or `.markdown` file on the Canopy backend's machine. No agent connection is required. Preview the file, then **Link and load** to replace the saved specification. The path and last loaded file hash persist in DuckDB.

**Refresh from file** previews the latest disk content before replacing the specification. **Save to file** explicitly writes the saved specification as Markdown. Canopy edits still autosave to DuckDB; disk writes are explicit. Formatting may be normalized by the shared Markdown codec. **Unlink** keeps both the saved specification and disk file. Deleting a task or project removes its link, never its disk file.

If an external editor changed the file since the last load/save, saving returns 409 without changing the file. Refresh and review the disk version first. Missing, unreadable, invalid UTF-8, oversized, non-Markdown and symbolic-link files are rejected explicitly. Canopy does not create missing files or automatically run agents. Files are limited to 2 MB. Local file endpoints accept only loopback, same-origin callers.

## HTTP contract

- `POST /api/spec-files/preview` `{path}` → `{path, markdown, hash}`. Reads without changing workspace or file.
- `GET /api/spec-files/{itemId}` → the current disk preview for the saved link, or 404.
- `PUT /api/spec-files/{itemId}` `{expectedRevision, path, expectedHash, specJson}` → workspace. Checks the file still matches the preview, saves the parsed specification and link, and increments project revision. `specJson` uses the shared Markdown codec in the browser.
- `POST /api/spec-files/{itemId}/write` `{expectedRevision, expectedHash, markdown}` → workspace. Requires the saved link hash and current disk bytes to match `expectedHash`. Atomically replaces the file and updates the saved hash. No force-overwrite option.
- `DELETE /api/spec-files/{itemId}?expectedRevision=…` → workspace. Removes the link only.

Workspace responses add `specFiles`, containing `{id, projectId, path, hash}` records keyed by task ID. Ordinary task edits preserve these records. All mutations require the current project revision; stale revisions return 409. Invalid paths/content return 400, missing files/tasks/links return 404, inaccessible files return 400, cross-site/nonlocal requests return 403 (unconfigured Host headers may be rejected earlier with 400). File replacement and DuckDB commit cannot be one atomic transaction: if a database failure follows replacement, reload the file to reconcile the saved hash. External editors should use atomic saves; an edit concurrent with the final filesystem replacement cannot be serialized with Canopy's process lock.
