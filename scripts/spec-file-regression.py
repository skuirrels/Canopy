#!/usr/bin/env python3
"""Real HTTP/filesystem/DuckDB checks, isolated from the user's workspace."""
import json
import os
from pathlib import Path
import socket
import subprocess
import tempfile
import time
import urllib.error
import urllib.request

ROOT = Path(__file__).resolve().parents[1]
API = ROOT / 'server/Planner.Api'
checks = 0


def check(value, message):
    global checks
    assert value, message
    checks += 1
    print(f'PASS {checks:02}: {message}', flush=True)


with tempfile.TemporaryDirectory(prefix='canopy-spec-files-') as directory:
    scratch = Path(directory).resolve()
    with socket.socket() as sock:
        sock.bind(('127.0.0.1', 0))
        port = sock.getsockname()[1]
    base = f'http://127.0.0.1:{port}'
    env = os.environ | {'Database__Path': str(scratch / 'workspace.duckdb'), 'Agents__WorkspaceRoot': str(scratch / 'worktrees')}
    log = open(scratch / 'server.log', 'w+')
    process = None

    def call(path, method='GET', body=None, expected=200, headers=None):
        request = urllib.request.Request(base + path, data=None if body is None else json.dumps(body).encode(),
                                        headers={'Content-Type': 'application/json'} | (headers or {}), method=method)
        try:
            with urllib.request.urlopen(request, timeout=15) as response:
                code, raw = response.status, response.read()
        except urllib.error.HTTPError as error:
            code, raw = error.code, error.read()
        try:
            value = json.loads(raw) if raw else None
        except json.JSONDecodeError:
            value = raw.decode(errors='replace')
        assert code == expected, (path, code, expected, value)
        return value

    def start():
        global process
        process = subprocess.Popen(['dotnet', 'bin/Debug/net10.0/Planner.Api.dll', '--urls', base], cwd=API, env=env, stdout=log, stderr=log)
        for _ in range(150):
            if process.poll() is not None:
                log.seek(0)
                raise AssertionError(log.read())
            try:
                call('/api/health')
                return
            except OSError:
                time.sleep(.1)
        raise AssertionError('Backend did not start')

    def stop():
        process.terminate()
        process.wait(timeout=20)

    def state():
        return call('/api/workspace')

    def revision():
        return next(p['revision'] for p in state()['projects'] if p['id'] == pid)

    def document(text):
        return json.dumps({'type': 'doc', 'content': [{'type': 'paragraph', 'content': [{'type': 'text', 'text': text}]}]})

    def link(preview, text, expected=200):
        return call(f'/api/spec-files/{tid}', 'PUT', dict(expectedRevision=revision(), path=preview['path'], expectedHash=preview['hash'], specJson=document(text)), expected)

    try:
        start()
        workspace = call('/api/projects', 'POST', dict(name='Linked file regression', description='Test only', color='#376881'))
        pid = workspace['projects'][-1]['id']
        workspace = call(f'/api/projects/{pid}/items', 'POST', dict(expectedRevision=revision(), title='Shared spec'))
        tid = next(i['id'] for i in workspace['items'] if i['projectId'] == pid)
        endpoint = f'/api/spec-files/{tid}'
        file = scratch / 'shared specification.md'
        original = '# Shared specification\n\n- [ ] Codex and Claude can read this.\n'
        file.write_text(original)
        preview = call('/api/spec-files/preview', 'POST', dict(path=str(file)))
        check(preview['markdown'] == original and len(preview['hash']) == 64, 'preview reads actual UTF-8 file with content hash and spaced path')
        check(state()['specFiles'] == [] and file.read_text() == original, 'preview changes neither workspace nor file')
        linked = link(preview, 'Loaded from disk')
        check(linked['specFiles'][0]['path'] == str(file) and 'Loaded from disk' in linked['items'][0]['specJson'], 'link saves path and specification through public API')
        check(file.read_text() == original, 'link never rewrites original Markdown')
        old_revision = revision()
        written = 'Saved by Canopy\n'
        workspace = call(endpoint + '/write', 'POST', dict(expectedRevision=old_revision, expectedHash=preview['hash'], markdown=written))
        saved_link = workspace['specFiles'][0]
        check(file.read_text() == written and saved_link['hash'] != preview['hash'], 'explicit save updates file and saved byte hash')
        call(endpoint + '/write', 'POST', dict(expectedRevision=old_revision, expectedHash=saved_link['hash'], markdown='stale'), 409)
        check(file.read_text() == written, 'stale project revision cannot write disk')
        external = '# Changed by Claude\n'
        file.write_text(external)
        call(endpoint + '/write', 'POST', dict(expectedRevision=revision(), expectedHash=saved_link['hash'], markdown='overwrite'), 409)
        check(file.read_text() == external, 'external edit rejects overwrite and preserves disk')
        latest = call(endpoint)
        file.write_text('# Changed again after preview\n')
        link(latest, 'stale preview', 409)
        check('Loaded from disk' in state()['items'][0]['specJson'], 'stale preview cannot replace specification')
        latest = call(endpoint)
        workspace = link(latest, 'Changed again after preview')
        check(workspace['specFiles'][0]['hash'] == latest['hash'], 'refresh records newly reviewed external content')
        stop()
        start()
        restarted = state()
        check(restarted['specFiles'] == workspace['specFiles'] and restarted['items'][0]['specJson'] == workspace['items'][0]['specJson'], 'file link and specification survive backend restart')
        call(f'/api/items/{tid}/move', 'POST', dict(expectedRevision=revision(), parentId=tid, statusId=state()['items'][0]['statusId']), 400)
        check(state()['specFiles'] == workspace['specFiles'], 'invalid hierarchy edit leaves link and spec intact')
        for bad in ['relative.md', str(scratch / 'not-markdown.txt'), str(scratch / 'missing.md')]:
            call('/api/spec-files/preview', 'POST', dict(path=bad), 404 if bad.endswith('missing.md') else 400)
        check(True, 'relative, non-Markdown and missing paths return explicit errors')
        invalid = scratch / 'invalid.md'
        invalid.write_bytes(b'\xff\xfe')
        call('/api/spec-files/preview', 'POST', dict(path=str(invalid)), 400)
        invalid.write_bytes(b'x' * 2_000_001)
        call('/api/spec-files/preview', 'POST', dict(path=str(invalid)), 400)
        check(True, 'invalid UTF-8 and oversized Markdown rejected')
        symlink = scratch / 'alias.md'
        symlink.symlink_to(file)
        call('/api/spec-files/preview', 'POST', dict(path=str(symlink)), 400)
        parent_alias = scratch / 'alias-folder'
        parent_alias.symlink_to(scratch, target_is_directory=True)
        call('/api/spec-files/preview', 'POST', dict(path=str(parent_alias / file.name)), 400)
        check(True, 'symlink file and symlink parent rejected')
        for headers in [{'Origin': 'https://example.invalid'}, {'Sec-Fetch-Site': 'cross-site'}, {'Host': 'example.invalid'}]:
            call('/api/spec-files/preview', 'POST', dict(path=str(file)), 400 if 'Host' in headers else 403, headers)
        check(True, 'cross-origin, cross-site and nonlocal Host requests rejected')
        retained = file.read_text()
        spec = state()['items'][0]['specJson']
        workspace = call(endpoint + f'?expectedRevision={revision()}', 'DELETE')
        check(workspace['specFiles'] == [] and workspace['items'][0]['specJson'] == spec and file.read_text() == retained, 'unlink retains task specification and file')
        call(endpoint, expected=404)
        link(call('/api/spec-files/preview', 'POST', dict(path=str(file))), 'Relinked')
        call(f'/api/items/{tid}?expectedRevision={revision()}', 'DELETE')
        check(state()['specFiles'] == [] and file.read_text() == retained, 'task deletion removes link without deleting file')
        call(f'/api/projects/{pid}?expectedRevision={revision()}', 'DELETE')
        check(state()['projects'] == [], 'isolated regression leaves no test projects')
        print(f'{checks} checks passed')
    finally:
        if process and process.poll() is None:
            stop()
        log.close()
