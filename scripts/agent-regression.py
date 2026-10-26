#!/usr/bin/env python3
"""Isolated real HTTP/DuckDB/CLI-boundary checks. No model service is contacted."""
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


def check(condition, message):
    global checks
    assert condition, message
    checks += 1
    print(f'PASS {checks:02}: {message}', flush=True)


with tempfile.TemporaryDirectory(prefix='canopy-agents-') as directory:
    scratch = Path(directory)
    repo = scratch / 'repository with spaces'
    repo.mkdir()
    subprocess.run(['git', 'init', '-q', str(repo)], check=True)
    subprocess.run(['git', '-C', str(repo), 'config', 'user.name', 'Canopy regression'], check=True)
    subprocess.run(['git', '-C', str(repo), 'config', 'user.email', 'regression@example.invalid'], check=True)
    (repo / 'README.md').write_text('# Regression fixture\n')
    subprocess.run(['git', '-C', str(repo), 'add', 'README.md'], check=True)
    subprocess.run(['git', '-C', str(repo), 'commit', '-qm', 'Fixture baseline'], check=True)
    fake = scratch / 'fixture-agent'
    fake.write_text('''#!/usr/bin/env python3
import json, pathlib, sys, time, uuid
args=sys.argv[1:]; prompt=sys.stdin.read(); codex=args[0]=='exec'
assert not any('dangerously' in value for value in args)
implementation='Purpose: implementation' in prompt
if codex:
 assert '--ignore-user-config' in args
 assert 'sandbox_mode="'+('workspace-write' if implementation else 'read-only')+'"' in args
 if 'Refine this result' in prompt: assert 'resume' in args
else:
 assert '--safe-mode' in args and '--restricted' in args and '--strict-mcp-config' in args
 tools=args[args.index('--tools')+1]
 assert ('Bash' in tools) == implementation
 if implementation: assert '"allowUnsandboxedCommands":false' in args[args.index('--settings')+1]
session='19799227-aec9-4f79-aac2-98b42488b8a0'
print(json.dumps({'type':'thread.started','thread_id':session} if codex else {'type':'system','session_id':session}),flush=True)
if 'REGRESSION_WAIT' in prompt: time.sleep(120)
if 'EXPECT_SETTINGS: ' in prompt:
 expected=json.loads(prompt.split('EXPECT_SETTINGS: ',1)[1].splitlines()[0])
 if expected['model'] is None: assert '--model' not in args, args
 else: assert args[args.index('--model')+1] == expected['model'], args
 if codex:
  effort_args=[value for value in args if value.startswith('model_reasoning_effort=')]
  assert effort_args == ([] if expected['effort'] is None else ['model_reasoning_effort='+json.dumps(expected['effort'])]), args
 elif expected['effort'] is None: assert '--effort' not in args, args
 else: assert args[args.index('--effort')+1] == expected['effort'], args
if 'Purpose: implementation' in prompt:
 assert pathlib.Path.cwd().name != 'repository with spaces'
 pathlib.Path('implemented.md').write_text('# Implemented in isolated worktree\\n')
result='# Fixture result\\n\\n- [x] Retained Markdown formatting'
if 'REGRESSION_FAILURE' in prompt:
 print('Fixture provider failed',file=sys.stderr,flush=True); sys.exit(7)
if codex: print(json.dumps({'type':'item.completed','item':{'type':'agent_message','text':result}}),flush=True)
else: print(json.dumps({'type':'result','session_id':session,'is_error':False,'result':result,'permission_denials':[]}),flush=True)
''')
    fake.chmod(0o755)
    with socket.socket() as sock:
        sock.bind(('127.0.0.1', 0))
        port = sock.getsockname()[1]
    base = f'http://127.0.0.1:{port}'
    env = os.environ | {
        'Database__Path': str(scratch / 'workspace.duckdb'), 'Agents__WorkspaceRoot': str(scratch / 'worktrees'),
        'Agents__CodexExecutable': str(fake), 'Agents__ClaudeExecutable': str(fake), 'Agents__PollIntervalMs': '250',
    }
    log = open(scratch / 'server.log', 'w+')
    process = None

    def call(path, method='GET', body=None, expected=200, headers=None):
        request = urllib.request.Request(base + path, data=None if body is None else json.dumps(body).encode(),
                                        headers={'Content-Type': 'application/json'} | (headers or {}), method=method)
        try:
            with urllib.request.urlopen(request, timeout=15) as response:
                code = response.status
                raw = response.read()
        except urllib.error.HTTPError as error:
            code = error.code
            raw = error.read()
        value = json.loads(raw) if raw else None
        assert code == expected, (path, code, expected, value)
        return value

    def start_server():
        global process
        process = subprocess.Popen(['dotnet', 'bin/Debug/net10.0/Planner.Api.dll', '--urls', base], cwd=API, env=env, stdout=log, stderr=log)
        for _ in range(150):
            if process.poll() is not None:
                log.seek(0)
                raise AssertionError(log.read())
            try:
                call('/api/health')
                return
            except (OSError, AssertionError):
                time.sleep(.1)
        raise AssertionError('Backend did not start')

    def stop_server():
        process.terminate()
        process.wait(timeout=20)

    def state():
        return call('/api/workspace')

    def revision():
        return next(p['revision'] for p in state()['projects'] if p['id'] == pid)

    def start_run(purpose='spec', provider='codex', instructions='Write the regression spec', previous=None, expected=200, **overrides):
        payload = dict(requestId=str(uuid.uuid4()), expectedRevision=revision(), provider=provider, purpose=purpose, instructions=instructions, previousRunId=previous)
        payload.update(overrides)
        return call(f'/api/agents/tasks/{tid}/runs', 'POST', payload, expected), payload

    def wait_run(run_id, terminal=True):
        for _ in range(120):
            value = call(f'/api/agents/runs/{run_id}')
            if not value['active'] if terminal else value['state'] == 'running':
                return value
            time.sleep(.1)
        raise AssertionError(('Run did not reach expected state', value))

    try:
        start_server()
        project_name = 'Agent regression ' + uuid.uuid4().hex
        workspace = call('/api/projects', 'POST', dict(name=project_name, description='Test-only fixture', color='#446655'))
        project = next(p for p in workspace['projects'] if p['name'] == project_name)
        pid = project['id']
        workspace = call(f'/api/projects/{pid}/items', 'POST', dict(expectedRevision=revision(), title='Fixture task', parentId=None, statusId=None))
        task = next(i for i in state()['items'] if i['projectId'] == pid)
        tid = task['id']
        check(call(f'/api/agents/projects/{pid}/connection') is None, 'Ordinary projects have no agent connection')
        start_run(expected=400)
        binding = dict(expectedRevision=revision(), repositoryPath=str(repo), baseRef='HEAD', provider='codex', model=None, instructions='Test fixture instructions')
        call(f'/api/agents/projects/{pid}/connection', 'PUT', binding | {'repositoryPath': str(scratch)}, 400)
        call(f'/api/agents/projects/{pid}/connection', 'PUT', binding | {'baseRef': 'missing-ref'}, 400)
        call(f'/api/agents/projects/{pid}/connection', 'PUT', binding, headers={'Origin': 'https://example.invalid'}, expected=403)
        connection = call(f'/api/agents/projects/{pid}/connection', 'PUT', binding)
        check(connection['repositoryPath'] == str(repo.resolve()), 'Connection validates repository, Git ref and browser origin')
        check(call(f'/api/agents/tasks/{tid}/runs') == [], 'Saving a connection never starts a run')
        start_run(provider='unknown', expected=400)
        call(f'/api/agents/tasks/{tid}/runs', 'POST', dict(requestId=str(uuid.uuid4()), expectedRevision=revision()-1, provider='codex', purpose='spec', instructions='Write a spec', previousRunId=None), 409)
        start_run('implementation', instructions='', expected=400)
        started, payload = start_run()
        duplicate = call(f'/api/agents/tasks/{tid}/runs', 'POST', payload)
        check(duplicate['id'] == started['id'], 'Dispatch retries return the same persisted run')
        call(f'/api/agents/tasks/{tid}/runs', 'POST', payload | {'instructions': 'Different request'}, 409)
        finished = wait_run(started['id'])
        check(finished['state'] == 'succeeded' and finished['sessionId'] and '# Fixture result' in finished['result'], 'Codex protocol captures completion, session and Markdown result')
        check(next(i for i in state()['items'] if i['id'] == tid) == task, 'Drafting changes neither task fields nor workflow status')
        followed, _ = start_run(instructions='Refine this result', previous=finished['id'])
        check(wait_run(followed['id'])['previousRunId'] == finished['id'], 'Follow-up resumes the exact saved task session')
        start_run(provider='claude', previous=finished['id'], expected=400)
        documentation, _ = start_run('documentation', 'claude', 'Write documentation')
        check(wait_run(documentation['id'])['state'] == 'succeeded', 'Claude protocol returns documentation independently of task specs')
        fields = {k: task[k] for k in ['title', 'specJson', 'statusId', 'priority', 'assigneeIds', 'estimatedHours', 'startDate', 'dueDate']}
        fields['specJson'] = json.dumps({'type': 'doc', 'content': [{'type': 'paragraph', 'content': [{'type': 'text', 'text': 'Implement the fixture document.'}]}]})
        workspace = call(f'/api/items/{tid}', 'PATCH', fields | {'expectedRevision': revision(), 'dependencyIds': []})
        implementation, _ = start_run('implementation', instructions='')
        completed = wait_run(implementation['id'])
        check(completed['state'] == 'succeeded' and 'implemented.md' in completed['diff'] and not (repo / 'implemented.md').exists(), 'Implementation retains a real diff in an isolated worktree')
        workspace = call(f'/api/projects/{pid}/items', 'POST', dict(expectedRevision=revision(), title='Prerequisite fixture', parentId=tid, statusId=None))
        prerequisite = next(i for i in workspace['items'] if i['title'] == 'Prerequisite fixture')
        call(f'/api/items/{tid}', 'PATCH', fields | {'expectedRevision': revision(), 'dependencyIds': [prerequisite['id']]})
        start_run('implementation', instructions='', expected=409)
        check(True, 'Incomplete configured prerequisites block implementation dispatch')
        call(f"/api/items/{prerequisite['id']}?expectedRevision={revision()}", 'DELETE')
        snapshot = completed['specJson']
        fields['title'] = 'Edited while preserving captured work'
        call(f'/api/items/{tid}', 'PATCH', fields | {'expectedRevision': revision(), 'dependencyIds': []})
        check(call(f"/api/agents/runs/{completed['id']}")['specJson'] == snapshot, 'Task edits preserve immutable historical work orders')
        failure, _ = start_run(instructions='REGRESSION_FAILURE')
        failed = wait_run(failure['id'])
        check(failed['state'] == 'failed' and 'code 7' in failed['error'] and 'Fixture provider failed' in failed['output'], 'CLI failures remain visible and never become success')
        waiting, _ = start_run(instructions='REGRESSION_WAIT')
        wait_run(waiting['id'], terminal=False)
        workspace = call(f'/api/projects/{pid}/items', 'POST', dict(expectedRevision=revision(), title='Queued cancellation fixture', parentId=tid, statusId=None))
        queued_task = next(i for i in workspace['items'] if i['title'] == 'Queued cancellation fixture')
        queued_run = call(f"/api/agents/tasks/{queued_task['id']}/runs", 'POST', dict(requestId=str(uuid.uuid4()), expectedRevision=revision(), provider='codex', purpose='spec', instructions='This queued request will be cancelled', previousRunId=None))
        call(f"/api/agents/runs/{queued_run['id']}/cancel", 'POST')
        call(f"/api/items/{queued_task['id']}?expectedRevision={revision()}", 'DELETE')
        check(call(f"/api/agents/runs/{waiting['id']}")['state'] == 'running', 'Cancelled queued work can be deleted without disturbing the active run')
        start_run(expected=409)
        call(f'/api/projects/{pid}?expectedRevision={revision()}', 'DELETE', expected=409)
        call(f'/api/items/{tid}?expectedRevision={revision()}', 'DELETE', expected=409)
        call(f'/api/agents/projects/{pid}/connection?expectedRevision={revision()}', 'DELETE', expected=409)
        call(f"/api/agents/runs/{waiting['id']}/cancel", 'POST')
        check(wait_run(waiting['id'])['state'] == 'cancelled', 'Cancellation terminates a live process; active tasks cannot be deleted or disconnected')
        capabilities = {p['id']: p for p in call('/api/agents/providers')['providers']}
        codex_levels = capabilities['codex']['effortLevels']; claude_levels = capabilities['claude']['effortLevels']
        settings = {'codex': {'model': 'fixture-codex-default', 'effort': codex_levels[0]}, 'claude': {'model': 'fixture-claude-default', 'effort': claude_levels[0]}}

        def save_settings(values):
            return call(f'/api/agents/projects/{pid}/connection', 'PUT', binding | {'expectedRevision': revision(), 'settings': values})

        connection = save_settings(settings)
        check(connection['settings'] == settings and connection['model'] == settings['codex']['model'], 'Project defaults are independent for each agent with the legacy model alias preserved')

        def expect_settings(provider, resolved, **overrides):
            run, payload = start_run(provider=provider, instructions='EXPECT_SETTINGS: ' + json.dumps(resolved), **overrides)
            value = wait_run(run['id'])
            assert value['state'] == 'succeeded', (value['error'], value['output'])
            assert value['model'] == resolved['model'] and value['effort'] == resolved['effort'], value
            return value, payload

        inherited, _ = expect_settings('codex', settings['codex'])
        check(inherited['requestedModel'] is None and inherited['requestedEffort'] is None, 'Inherited settings are captured and forwarded using the Codex config argument')
        changed = dict(settings, codex={'model': 'fixture-new-project-default', 'effort': codex_levels[-1]})
        save_settings(changed)
        check(call(f"/api/agents/runs/{inherited['id']}")['model'] == inherited['model'], 'Changing project defaults never changes an existing run snapshot')
        continued, _ = expect_settings('codex', settings['codex'], previous=inherited['id'])
        check(continued['model'] == inherited['model'] and continued['effort'] == inherited['effort'], 'Follow-ups retain previous-run settings instead of changed project defaults')
        explicit = {'model': 'fixture-task-override', 'effort': codex_levels[1]}
        overridden, override_payload = expect_settings('codex', explicit, **explicit)
        duplicate = call(f'/api/agents/tasks/{tid}/runs', 'POST', override_payload)
        call(f'/api/agents/tasks/{tid}/runs', 'POST', override_payload | {'effort': codex_levels[0]}, 409)
        check(overridden['requestedModel'] == explicit['model'] and duplicate['id'] == overridden['id'], 'Idempotent retries retain settings and changed settings require a new request ID')
        expect_settings('codex', {'model': None, 'effort': None}, model='', effort='')
        check(True, 'Explicit agent-default choices omit both model and effort CLI arguments')
        claude_run, _ = expect_settings('claude', settings['claude'])
        check(claude_run['model'] != inherited['model'], 'Claude uses its own defaults and receives the --effort flag')
        expect_settings('claude', {'model': None, 'effort': None}, model='', effort='')
        invalid = dict(changed, claude={'model': 'fixture-claude-default', 'effort': 'unsupported-effort'})
        saved_connection = call(f'/api/agents/projects/{pid}/connection')
        call(f'/api/agents/projects/{pid}/connection', 'PUT', binding | {'expectedRevision': revision(), 'settings': invalid}, 400)
        start_run(effort='unsupported-effort', expected=400)
        check(call(f'/api/agents/projects/{pid}/connection') == saved_connection, 'Invalid effort settings reject atomically before dispatch or saving defaults')
        legacy = call(f'/api/agents/projects/{pid}/connection', 'PUT', binding | {'expectedRevision': revision(), 'model': 'fixture-legacy-edit'})
        check(legacy['settings']['claude'] == settings['claude'] and legacy['settings']['codex']['effort'] == changed['codex']['effort'], 'Legacy connection saves preserve the other provider and the current effort')
        before_restart = call(f'/api/agents/tasks/{tid}/runs')
        stop_server()
        start_server()
        check(call(f'/api/agents/tasks/{tid}/runs') == before_restart, 'Connection and completed runs survive a real backend restart')
        check(call(f'/api/agents/projects/{pid}/connection')['id'] == connection['id'], 'Project binding retains identity after restart')
        waiting, _ = start_run(instructions='REGRESSION_WAIT')
        wait_run(waiting['id'], terminal=False)
        stop_server()
        start_server()
        check(call(f"/api/agents/runs/{waiting['id']}")['state'] == 'interrupted', 'Unfinished runs stay interrupted across restart and are never replayed')
        call(f'/api/agents/projects/{pid}/connection?expectedRevision={revision()}', 'DELETE', expected=200)
        check(call(f'/api/agents/tasks/{tid}/runs'), 'Disconnecting preserves historical runs')
        call(f'/api/projects/{pid}?expectedRevision={revision()}', 'DELETE')
        check(not state()['projects'], 'Explicit cleanup removes all fixture project data')
        print(f'Passed {checks} agent checks. Provider responses were protocol fixtures, not live model calls.', flush=True)
    finally:
        if process is not None and process.poll() is None:
            stop_server()
        log.close()
