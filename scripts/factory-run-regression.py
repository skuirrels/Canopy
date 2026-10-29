#!/usr/bin/env python3
"""Real HTTP/native DuckDB preparation checks. Temporary inputs only; no agent or command runs."""
import concurrent.futures
import copy
import hashlib
import json
import os
from pathlib import Path
import socket
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.request
import uuid

ROOT = Path(__file__).resolve().parents[1]
API = ROOT / 'server/Planner.Api'
checks = 0


def check(condition, message):
    global checks
    assert condition, message
    checks += 1
    print(f'PASS {checks:02}: {message}', flush=True)


with tempfile.TemporaryDirectory(prefix='canopy-factory-runs-') as directory:
    scratch = Path(directory).resolve()
    repo = scratch / 'repository with spaces'
    repo.mkdir()
    (repo / 'SKILL.md').write_text('# Fixture skill\nExplicit test-only content.\n')
    for args in [['init', '-q'], ['config', 'user.name', 'Canopy regression'], ['config', 'user.email', 'regression@example.invalid'], ['add', 'SKILL.md'], ['commit', '-qm', 'Test baseline']]:
        subprocess.run(['git', '-C', str(repo), *args], check=True)
    commit = subprocess.check_output(['git', '-C', str(repo), 'rev-parse', 'HEAD'], text=True).strip()
    marker = scratch / 'executed'
    fake = scratch / 'fixture-agent'
    fake.write_text('#!/bin/sh\ntouch "' + str(marker) + '"\nexit 99\n')
    fake.chmod(0o755)
    with socket.socket() as sock:
        sock.bind(('127.0.0.1', 0)); base = f'http://127.0.0.1:{sock.getsockname()[1]}'
    env = os.environ | {'Database__Path': str(scratch / 'workspace.duckdb'), 'Agents__WorkspaceRoot': str(scratch / 'worktrees'),
        'Agents__CodexExecutable': str(fake), 'Factory__Skills__0__Id': 'fixture-skill', 'Factory__Skills__0__Source': 'test catalogue', 'Factory__Skills__0__Path': 'SKILL.md'}
    process = None
    log = (scratch / 'server.log').open('w+')

    def call(path, method='GET', body=None, expected=200, headers=None):
        request = urllib.request.Request(base + path, data=None if body is None else json.dumps(body).encode(),
            headers={'Content-Type': 'application/json'} | (headers or {}), method=method)
        try:
            with urllib.request.urlopen(request, timeout=20) as response:
                code, raw = response.status, response.read()
        except urllib.error.HTTPError as error:
            code, raw = error.code, error.read()
        value = json.loads(raw) if raw else None
        assert code == expected, (path, code, expected, value)
        return value

    def start():
        global process
        process = subprocess.Popen(['dotnet', 'bin/Debug/net10.0/Planner.Api.dll', '--urls', base], cwd=API, env=env, stdout=log, stderr=log)
        for _ in range(150):
            if process.poll() is not None:
                log.seek(0); raise AssertionError(log.read())
            try:
                call('/api/health'); return
            except OSError:
                time.sleep(.1)
        raise AssertionError('Backend did not start')

    def revision():
        return next(p['revision'] for p in call('/api/workspace')['projects'] if p['id'] == pid)

    def attach(version, overrides=None):
        return call(f'/api/factory/tasks/{tid}/configuration', 'PUT', dict(expectedRevision=revision(), pipelineVersionId=version['id'], overrides=overrides or {'steps': []}))

    try:
        start()
        workspace = call('/api/projects', 'POST', dict(name='Run preparation regression', description='Test only', color='#477fa1'))
        pid = workspace['projects'][0]['id']
        workspace = call(f'/api/projects/{pid}/items', 'POST', dict(expectedRevision=revision(), title='Prepared work order'))
        task = workspace['items'][0]; tid = task['id']
        ready_path = f'/api/factory/tasks/{tid}/readiness'; runs_path = f'/api/factory/tasks/{tid}/runs'
        check(not call(ready_path)['canPrepare'], 'unattached tasks report a concrete readiness error')
        call(runs_path, 'POST', dict(requestId=str(uuid.uuid4()), expectedRevision=revision()), 400)
        definition = {'stages': [{'id': 'build', 'name': 'Implementation', 'group': 'Build', 'dependsOn': [], 'steps': [dict(id='implement', name='Implement', kind='agent', instructions='Implement the saved specification.', role='implementation', permission='workspace-write', skills=[dict(id='fixture-skill', source='test catalogue')])]},
            {'id': 'verify', 'name': 'Verification', 'group': 'Check', 'dependsOn': ['build'], 'steps': [dict(id='test', name='Test', kind='command', command=dict(executable=sys.executable, arguments=['-c', f'open({str(marker)!r},"w").write("executed")'], workingDirectory='.', timeoutSeconds=10))]}]}
        pipeline = call(f'/api/factory/projects/{pid}/pipelines', 'POST', dict(expectedRevision=revision(), name='Fixture pipeline', description='Test only', source='Explicit regression input', definition=definition), 201)
        pipeline_id = pipeline['pipeline']['id']
        published = call(f'/api/factory/pipelines/{pipeline_id}/publish', 'POST', dict(expectedRevision=revision(), expectedDraftRevision=1))
        version = published['versions'][0]; attach(version)
        check(any(c['code'] == 'repository.required' for c in call(ready_path)['checks']), 'missing repository connection is exposed')
        call(f'/api/agents/projects/{pid}/connection', 'PUT', dict(expectedRevision=revision(), repositoryPath=str(repo), baseRef='HEAD', provider='codex', model='fixture-model', instructions='Fixture project instructions'))
        check(any(c['code'] == 'task.specification' for c in call(ready_path)['checks']), 'implementation preparation requires a real task specification')
        spec = json.dumps(dict(type='doc', content=[dict(type='paragraph', content=[dict(type='text', text='Acceptance criteria from test input')])]))
        call(f'/api/items/{tid}', 'PATCH', dict(expectedRevision=revision(), title=task['title'], specJson=spec, statusId=task['statusId'], priority='none', assigneeIds=[], startDate=None, dueDate=None, dependencyIds=[]))
        baseline = call('/api/workspace'); ready = call(ready_path)
        check(ready['canPrepare'] and ready['executionReadiness'] == 'notEvaluated', 'local prerequisites allow preparation without claiming execution readiness')
        check(any(c['code'] == 'agent.access' and c['severity'] == 'warning' for c in ready['checks']), 'executable availability does not certify provider or model access')
        check(ready['environment']['baseCommit'] == commit, 'repository revision comes from the configured Git ref')
        check(ready['environment']['skills'][0]['hash'] == hashlib.sha256((repo / 'SKILL.md').read_bytes()).hexdigest().upper(), 'skill content hash comes from the configured file')
        check(ready['environment']['definition']['stages'][0]['steps'][0]['model'] == 'fixture-model', 'provider/model defaults resolve from the real project connection')
        check(call('/api/workspace') == baseline, 'readiness is read-only')
        request = dict(requestId=str(uuid.uuid4()), expectedRevision=ready['projectRevision'])
        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
            attempts = list(pool.map(lambda _: call(runs_path, 'POST', request, 201), range(2)))
        prepared = attempts[0]; run_id = prepared['run']['id']; detail_path = f'/api/factory/runs/{run_id}'
        check(attempts[1] == prepared and len(call(runs_path)) == 1, 'concurrent duplicate requests create exactly one run and replay the original snapshot')
        check(prepared['run']['state'] == 'prepared' and all(x['state'] == 'pending' for x in prepared['stages'] + prepared['steps']), 'preparation records only pending stages and steps')
        check(len(prepared['stages']) == 2 and len(prepared['steps']) == 2, 'stage and step identities are persisted separately')
        snapshot = prepared['run']['snapshot']
        check(snapshot['specJson'] == spec and snapshot['pipelineVersionId'] == version['id'] and snapshot['source'] == version['source'], 'immutable work order captures exact task specification, published version and provenance')
        check(call('/api/workspace') == baseline and not marker.exists(), 'preparation does not execute commands/agents or change task status, hierarchy or workspace data')
        call(runs_path, 'POST', dict(requestId=str(uuid.uuid4()), expectedRevision=revision()-1), 409)
        call(runs_path, 'POST', dict(requestId=str(uuid.UUID(int=0)), expectedRevision=revision()), 400)
        call(runs_path, 'POST', dict(requestId=str(uuid.uuid4()), expectedRevision=revision(), state='running'), 400)
        check(len(call(runs_path)) == 1, 'stale writes, empty IDs and unknown fields cannot create execution records')
        second = call(f'/api/projects/{pid}/items', 'POST', dict(expectedRevision=revision(), title='Other task'))['items'][-1]['id']
        call(f'/api/factory/tasks/{second}/runs', 'POST', request, 409)
        check(len(call(runs_path)) == 1, 'request IDs cannot be reused for another task')
        overrides = {'steps': [dict(stageId='build', stepId='implement', model='', instructions='Later override')]}
        attach(version, overrides)
        check(call(detail_path) == prepared, 'later task overrides never modify an existing work order')
        changed = call(runs_path, 'POST', dict(requestId=str(uuid.uuid4()), expectedRevision=revision()), 201)
        check(changed['run']['snapshot']['overrides'] == call(f'/api/factory/tasks/{tid}/configuration')['configuration']['overrides'], 'new work orders capture exact explicit overrides')
        check(changed['run']['snapshot']['environment']['definition']['stages'][0]['steps'][0]['model'] is None, 'explicit empty model selects provider default instead of inheriting a project model')
        fake.chmod(0o644)
        check(not call(ready_path)['canPrepare'], 'non-executable provider files do not report readiness')
        fake.chmod(0o755)
        def publish_variant(variant):
            current = call(f'/api/factory/pipelines/{pipeline_id}')
            edited = call(f'/api/factory/pipelines/{pipeline_id}/draft', 'PUT', dict(expectedRevision=revision(), expectedDraftRevision=current['pipeline']['draftRevision'], name=current['pipeline']['name'], description=current['pipeline']['description'], definition=variant))
            return call(f'/api/factory/pipelines/{pipeline_id}/publish', 'POST', dict(expectedRevision=revision(), expectedDraftRevision=edited['pipeline']['draftRevision']))['versions'][-1]
        invalid_model = copy.deepcopy(definition); invalid_model['stages'][0]['steps'][0]['model'] = 'x' * 201
        attach(publish_variant(invalid_model))
        check(any(c['code'] == 'agent.settings' and c['severity'] == 'error' for c in call(ready_path)['checks']), 'invalid runtime model settings are structured readiness errors')
        unavailable = copy.deepcopy(definition); unavailable['stages'][1]['steps'][0]['command']['executable'] = str(scratch / 'unavailable-command')
        attach(publish_variant(unavailable))
        check(not call(ready_path)['canPrepare'], 'unavailable command executables block preparation without a fallback')
        missing_environment = copy.deepcopy(definition)
        missing_environment['stages'][1]['steps'][0]['command']['environmentReferences'] = {'INPUT': 'CANOPY_TEST_' + uuid.uuid4().hex.upper()}
        attach(publish_variant(missing_environment))
        check(not call(ready_path)['canPrepare'], 'missing external environment references block preparation without exposing values')
        unsafe_directory = copy.deepcopy(definition); (repo / 'redirect').symlink_to(scratch, target_is_directory=True)
        unsafe_directory['stages'][1]['steps'][0]['command']['workingDirectory'] = 'redirect'
        attach(publish_variant(unsafe_directory))
        check(not call(ready_path)['canPrepare'], 'symlink command working directories are rejected')
        attach(version)
        (repo / 'SKILL.md').unlink()
        check(not call(ready_path)['canPrepare'], 'missing configured skill files block preparation')
        external = scratch / 'external-skill.md'; external.write_text('# Outside fixture repository')
        (repo / 'SKILL.md').symlink_to(external)
        check(not call(ready_path)['canPrepare'], 'mapped skill files cannot redirect through symlinks')
        (repo / 'SKILL.md').unlink()
        (repo / 'SKILL.md').write_text('# Changed fixture skill\n')
        check(call(detail_path) == prepared, 'skill file changes cannot alter recorded hashes')
        subprocess.run(['git', '-C', str(repo), 'add', '.'], check=True); subprocess.run(['git', '-C', str(repo), 'commit', '-qm', 'Changed test skill'], check=True)
        check(call(ready_path)['environment']['baseCommit'] != commit and call(detail_path) == prepared, 'moving Git refs cannot change a prepared base commit')
        call(ready_path, headers={'Origin': 'https://example.invalid'}, expected=403)
        check(True, 'new factory controls retain the local same-origin restriction')
        process.terminate(); process.wait(timeout=20); start()
        check(call(detail_path) == prepared and len(call(runs_path)) == 2, 'native DuckDB run snapshots and stage/step records persist exactly across backend restart')
        check(call(runs_path, 'POST', request, 201) == prepared, 'request idempotency survives restart and later configuration edits')
        check(not marker.exists(), 'neither preparation nor restart invokes the configured agent or command')
        call(f'/api/items/{tid}?expectedRevision={revision()}', 'DELETE')
        call(detail_path, expected=404)
        check(not call(f'/api/factory/tasks/{second}/runs'), 'task deletion removes prepared run history')
        manual = {'stages': [dict(id='accept', name='Acceptance', group='Review', dependsOn=[], steps=[dict(id='approve', name='Approve', kind='manual', instructions='Inspect evidence before acceptance.')])]}
        manual_version = publish_variant(manual)
        call(f'/api/factory/tasks/{second}/configuration', 'PUT', dict(expectedRevision=revision(), pipelineVersionId=manual_version['id'], overrides={'steps': []}))
        final_run = call(f'/api/factory/tasks/{second}/runs', 'POST', dict(requestId=str(uuid.uuid4()), expectedRevision=revision()), 201)
        call(f'/api/projects/{pid}?expectedRevision={revision()}', 'DELETE')
        call(f"/api/factory/runs/{final_run['run']['id']}", expected=404)
        check(True, 'project deletion cleans up remaining prepared run history and pending records')
        check(not call('/api/workspace')['projects'], 'project cleanup leaves no test projects')
        print(f'{checks} run preparation checks passed', flush=True)
    finally:
        if process and process.poll() is None:
            process.terminate(); process.wait(timeout=20)
        log.close()
