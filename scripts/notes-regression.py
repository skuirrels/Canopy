#!/usr/bin/env python3
"""Real HTTP/DuckDB checks for component notes, isolated from the user's workspace."""
import json
import os
from pathlib import Path
import socket
import subprocess
import tempfile
import time
import urllib.error
import urllib.request
import uuid

ROOT = Path(__file__).resolve().parents[1]
API = ROOT / 'server/Planner.Api'
checks = 0


def check(value, message):
    global checks
    assert value, message
    checks += 1
    print(f'PASS {checks:02}: {message}', flush=True)


with tempfile.TemporaryDirectory(prefix='canopy-notes-') as directory:
    scratch = Path(directory).resolve()
    with socket.socket() as sock:
        sock.bind(('127.0.0.1', 0))
        port = sock.getsockname()[1]
    base = f'http://127.0.0.1:{port}'
    env = os.environ | {'Database__Path': str(scratch / 'workspace.duckdb'), 'Agents__WorkspaceRoot': str(scratch / 'worktrees')}
    log = open(scratch / 'server.log', 'w+')
    process = None

    def call(path, method='GET', body=None, expected=200):
        request = urllib.request.Request(base + path, data=None if body is None else json.dumps(body).encode(),
                                         headers={'Content-Type': 'application/json'}, method=method)
        try:
            with urllib.request.urlopen(request, timeout=15) as response:
                code, raw = response.status, response.read()
        except urllib.error.HTTPError as error:
            code, raw = error.code, error.read()
        value = json.loads(raw) if raw else None
        assert code == expected, (path, code, expected, value)
        return value

    def start():
        global process
        process = subprocess.Popen(['dotnet', 'bin/Debug/net10.0/Planner.Api.dll', '--urls', base], cwd=API, env=env, stdout=log, stderr=log)
        for _ in range(300):
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

    def notes(item_id=None):
        return [n for n in state()['notes'] if n['projectId'] == pid and (item_id is None or n['itemId'] == item_id)]

    def add_item(title, parent=None):
        workspace = call(f'/api/projects/{pid}/items', 'POST', dict(expectedRevision=revision(), title=title, parentId=parent))
        return next(i for i in workspace['items'] if i['projectId'] == pid and i['title'] == title)

    try:
        start()
        workspace = call('/api/projects', 'POST', dict(name='Notes regression ' + uuid.uuid4().hex[:6], description='Test only', color='#376881'))
        pid = workspace['projects'][-1]['id']
        component = add_item('Notes component')
        task = add_item('Notes child task', component['id'])
        before_item = next(i for i in state()['items'] if i['id'] == component['id'])
        markdown = '## Attendees\n\n- Aleks\n\n## Action items\n\n- [ ] Send the schedule\n\n<raw html stays as text>\n'
        rev = revision()
        workspace = call(f"/api/items/{component['id']}/notes", 'POST', dict(expectedRevision=rev, title='  Call with Aleks  ', heldOn='2026-11-05', markdown=markdown))
        note = next(n for n in workspace['notes'] if n['itemId'] == component['id'])
        check(note['title'] == 'Call with Aleks' and note['heldOn'] == '2026-11-05' and note['markdown'] == markdown,
              'a component note stores a trimmed title, the call date and raw Markdown exactly')
        check(next(p for p in workspace['projects'] if p['id'] == pid)['revision'] == rev + 1, 'adding a note advances the project revision once')
        after_item = next(i for i in workspace['items'] if i['id'] == component['id'])
        check({k: after_item[k] for k in ('specJson', 'statusId', 'parentId', 'startDate', 'dueDate', 'siblingPosition')} ==
              {k: before_item[k] for k in ('specJson', 'statusId', 'parentId', 'startDate', 'dueDate', 'siblingPosition')},
              'a note leaves the component specification, status, dates and hierarchy unchanged')

        call(f"/api/items/{component['id']}/notes", 'POST', dict(expectedRevision=revision(), title='', heldOn=None, markdown=''))
        check(len(notes(component['id'])) == 2, 'a blank, undated note is allowed so writing can start immediately')

        current = revision(); count = len(notes())
        call(f"/api/items/{component['id']}/notes", 'POST', dict(expectedRevision=current, title='x' * 201, markdown=''), 400)
        call(f"/api/items/{component['id']}/notes", 'POST', dict(expectedRevision=current, title='No body'), 400)
        call(f"/api/items/{component['id']}/notes", 'POST', dict(expectedRevision=current, title='Too large', markdown='x' * 2_000_001), 400)
        call(f"/api/items/{component['id']}/notes", 'POST', dict(expectedRevision=current, title='Bad date', heldOn='2026-13-40', markdown=''), 400)
        call(f'/api/items/{uuid.uuid4()}/notes', 'POST', dict(expectedRevision=current, title='Missing', markdown=''), 404)
        call(f"/api/items/{component['id']}/notes", 'POST', dict(expectedRevision=current - 1, title='Stale', markdown=''), 409)
        check(len(notes()) == count and revision() == current, 'invalid, missing-item and stale note creation is rejected without changing data')

        edited = markdown + '\n- [x] Agreed the contract\n'
        workspace = call(f"/api/notes/{note['id']}", 'PATCH', dict(expectedRevision=revision(), title='Call with Aleks (C&C)', heldOn='2026-11-06', markdown=edited))
        changed = next(n for n in workspace['notes'] if n['id'] == note['id'])
        check(changed['title'] == 'Call with Aleks (C&C)' and changed['heldOn'] == '2026-11-06' and changed['markdown'] == edited and changed['createdAt'] == note['createdAt'],
              'editing a note replaces its title, date and Markdown and keeps its creation time')
        current = revision()
        call(f"/api/notes/{note['id']}", 'PATCH', dict(expectedRevision=current - 1, title='Stale edit', heldOn=None, markdown=''), 409)
        call(f"/api/notes/{note['id']}", 'PATCH', dict(expectedRevision=current, title='x' * 201, heldOn=None, markdown=''), 400)
        call(f'/api/notes/{uuid.uuid4()}', 'PATCH', dict(expectedRevision=current, title='Missing', heldOn=None, markdown=''), 404)
        check(next(n for n in notes() if n['id'] == note['id'])['markdown'] == edited, 'stale, invalid and missing edits leave the saved note intact')

        persisted = sorted(notes(), key=lambda n: n['id'])
        stop(); start()
        check(sorted(notes(), key=lambda n: n['id']) == persisted, 'notes persist identically across a backend restart')

        blank = next(n for n in notes(component['id']) if n['id'] != note['id'])
        call(f"/api/notes/{blank['id']}?expectedRevision={revision() - 1}", 'DELETE', expected=409)
        call(f"/api/notes/{blank['id']}?expectedRevision={revision()}", 'DELETE')
        check([n['id'] for n in notes()] == [note['id']], 'deleting a note removes only that note')

        call(f"/api/items/{task['id']}/notes", 'POST', dict(expectedRevision=revision(), title='Child call', heldOn='2026-11-01', markdown='child'))
        call(f"/api/items/{component['id']}?expectedRevision={revision()}", 'DELETE')
        check(notes() == [], 'deleting a component removes notes on it and its subitems')

        component = add_item('Second component')
        call(f"/api/items/{component['id']}/notes", 'POST', dict(expectedRevision=revision(), title='Kept until project deletion', markdown='text'))
        call(f'/api/projects/{pid}?expectedRevision={revision()}', 'DELETE')
        check(not any(n['projectId'] == pid for n in state()['notes']), 'deleting the project removes its notes')

        paths = call('/openapi/v1.json')['paths']
        check({'/api/items/{id}/notes', '/api/notes/{id}'} <= set(paths) and {'patch', 'delete'} <= set(paths['/api/notes/{id}']),
              'OpenAPI documents the note endpoints')
    except BaseException:
        log.flush(); log.seek(0); print(log.read()[-4000:])
        raise
    finally:
        if process and process.poll() is None:
            stop()
        log.close()
print(f'Notes regression passed: {checks} checks.')
