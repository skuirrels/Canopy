#!/usr/bin/env python3
"""Exercise plan import transactions, identities and restart persistence using an isolated native database."""
import copy
import hashlib
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

with tempfile.TemporaryDirectory(prefix='canopy-plan-import-') as directory:
    scratch = Path(directory)
    with socket.socket() as sock:
        sock.bind(('127.0.0.1', 0)); base = f'http://127.0.0.1:{sock.getsockname()[1]}'
    env = os.environ | {'Database__Path': str(scratch / 'workspace.duckdb'), 'Agents__WorkspaceRoot': str(scratch / 'worktrees')}
    log = (scratch / 'server.log').open('w+'); process = None
    def call(path, method='GET', body=None, expected=200):
        request = urllib.request.Request(base + path, data=None if body is None else json.dumps(body).encode(), headers={'Content-Type': 'application/json'}, method=method)
        try:
            with urllib.request.urlopen(request, timeout=30) as response: code, raw = response.status, response.read()
        except urllib.error.HTTPError as error: code, raw = error.code, error.read()
        value = json.loads(raw) if raw else None
        assert code == expected, (path, code, expected, value)
        return value
    def start():
        global process
        process = subprocess.Popen(['dotnet', 'bin/Debug/net10.0/Planner.Api.dll', '--urls', base], cwd=API, env=env, stdout=log, stderr=log)
        for _ in range(200):
            if process.poll() is not None:
                log.seek(0); raise AssertionError(log.read())
            try: call('/api/health'); return
            except OSError: time.sleep(.1)
        raise AssertionError('Server did not become ready')
    def stop():
        if process and process.poll() is None: process.terminate(); process.wait(timeout=30)
    def workspace(): return call('/api/workspace')
    def revision(): return next(p['revision'] for p in workspace()['projects'] if p['id'] == pid)
    def item(key, parent=None, deps=None):
        return dict(key=key, title='Explicit ' + key, parentKey=parent, priority='none', specJson=json.dumps({'type':'doc','content':[{'type':'paragraph','content':[{'type':'text','text':'Source supplied ' + key}]}]}), dependencyKeys=deps or [], criteria=[] if parent is None else [dict(id=str(uuid.uuid4()), description='Response scenario', expectedResult='The source scenario succeeds', verificationMethod='Exercise the supplied scenario')])
    def payload(items): return dict(expectedRevision=revision(), source='explicit-plan.md', sourceHash=hashlib.sha256(b'explicit test plan').hexdigest(), items=items)
    def submit(body, expected=200): return call(f'/api/projects/{pid}/plan-imports', 'POST', body, expected)
    try:
        start()
        state = call('/api/projects','POST',dict(name='Plan import regression',description='Test only',color='#477fa1')); pid=state['projects'][0]['id']
        # Parent references can arrive in any order; dependencies can refer forward.
        draft = [item('T-2','component',['T-1']), item('component'), item('T-1','component')]
        original = payload(draft); state=submit(original)
        mapped = {r['key']:r['itemId'] for r in state['planImports']}
        check(len(state['items']) == 3 and len(state['dependencies']) == 1, 'creates hierarchy and forward dependencies atomically')
        criteria=call('/api/factory/tasks/'+mapped['T-1']+'/criteria')['criteria']
        check(criteria[0]['expectedResult']=='The source scenario succeeds' and criteria[0]['verificationMethod']=='Exercise the supplied scenario', 'persists native acceptance criteria')
        # A source change must never rewrite human edits or workflow progress.
        existing=next(i for i in state['items'] if i['id']==mapped['T-1']); edited=existing | dict(title='Human edit', expectedRevision=revision(), priority='high', dependencyIds=[])
        call('/api/items/'+existing['id'],'PATCH',edited)
        repeat=payload(draft); repeat['sourceHash']=hashlib.sha256(b'changed source').hexdigest(); state=submit(repeat)
        check(len(state['items'])==3 and next(i for i in state['items'] if i['id']==existing['id'])['title']=='Human edit', 're-import preserves existing tasks and specifications')
        check(call('/api/factory/tasks/'+mapped['T-1']+'/criteria')['criteria']==criteria, 're-import preserves criterion identity and content')
        check(all(r['sourceHash']==original['sourceHash'] for r in state['planImports']), 'retains original source hash so changed source remains visible')
        before=workspace()
        submit(original,409)
        check(workspace()==before, 'stale project revision rejects without writes')
        for invalid, message in [
            ([item('root'),item('bad','missing')], 'missing parent'),
            ([item('A','B'),item('B','A')], 'hierarchy cycle'),
            ([item('root'),item('A','root',['B']),item('B','root',['A'])], 'dependency cycle'),
            ([item('root'),item('A','root',['missing'])], 'missing dependency'),
            ([item('root'),item('root')], 'duplicate key'),
            ([item('root'),item('A','root',['A'])], 'self dependency'),
        ]:
            submit(payload(invalid),400);check(workspace()==before,message+' rolls back the whole import')
        invalid=[item('new-root'), item('bad-criterion','new-root')]; invalid[1]['criteria'][0]['id']=str(uuid.UUID(int=0))
        submit(payload(invalid),400);check(workspace()==before,'invalid criteria roll back tasks and receipts')
        foreign=call('/api/projects','POST',dict(name='Other import project',description='Test only',color='#477fa1')); other=next(p for p in foreign['projects'] if p['id']!=pid)
        foreign=call('/api/projects/'+other['id']+'/items','POST',dict(expectedRevision=other['revision'],title='Foreign task'))
        foreignId=next(i['id'] for i in foreign['items'] if i['projectId']==other['id'])
        invalid=[item('adopt')]; invalid[0]['existingItemId']=foreignId
        before=workspace();submit(payload(invalid),404);check(workspace()==before,'cross-project adoption rejected')
        new=[item('component'),item('T-1','component'),item('T-2','component',['T-1']),item('T-3','component',['T-2'])]
        state=submit(payload(new));check(sum(i['projectId']==pid for i in state['items'])==4,'changed plan can add work while keeping existing tasks')
        stop();start();after=workspace()
        check(after==state,'tasks, criteria, import identities and dependencies survive process restart')
        check(call('/api/factory/tasks/'+mapped['T-1']+'/criteria')['criteria']==criteria,'native criteria survive process restart')
        state=call('/api/items/'+mapped['component']+'?expectedRevision='+str(revision()),'DELETE')
        check(not any(r['projectId']==pid for r in state['planImports']), 'deleting a branch cleans its import identities')
        call('/api/projects/'+pid+'?expectedRevision='+str(revision()),'DELETE')
        otherRevision=next(p['revision'] for p in workspace()['projects'] if p['id']==other['id'])
        call('/api/projects/'+other['id']+'?expectedRevision='+str(otherRevision),'DELETE')
        check(workspace()['projects']==[],'all test projects cleaned up')
    finally:
        stop();log.close()
print(f'{checks} plan import checks passed.')
