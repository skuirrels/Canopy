#!/usr/bin/env python3
"""Real Git/HTTP/DuckDB/process execution; provider CLIs are explicit fixtures, not model calls."""
import concurrent.futures
import copy
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
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


def check(value, message):
    global checks
    assert value, message
    checks += 1
    print(f'PASS {checks:02}: {message}', flush=True)


with tempfile.TemporaryDirectory(prefix='canopy-execution-') as directory:
    scratch = Path(directory).resolve(); repo = scratch / 'repository with spaces'; repo.mkdir()
    (repo / 'README.md').write_text('# Original pinned baseline\n')
    (repo / 'SKILL.md').write_text('# Explicit fixture skill\nUse the task acceptance criteria.\n')
    for args in [['init', '-q'], ['config', 'user.name', 'Canopy regression'], ['config', 'user.email', 'regression@example.invalid'], ['add', '.'], ['commit', '-qm', 'Fixture baseline']]:
        subprocess.run(['git', '-C', str(repo), *args], check=True)
    baseline_commit = subprocess.check_output(['git', '-C', str(repo), 'rev-parse', 'HEAD'], text=True).strip()
    trace = scratch / 'provider-trace.jsonl'; fake = scratch / 'fixture-agent'
    fake.write_text('''#!/usr/bin/env python3
import json, os, pathlib, sys, time
args=sys.argv[1:]; prompt=sys.stdin.read(); codex=args[0]=='exec'
purpose=next(line.split(': ',1)[1] for line in prompt.splitlines() if line.startswith('Purpose: '))
write=('sandbox_mode="workspace-write"' in args) if codex else args[args.index('--permission-mode')+1]=='acceptEdits'
assert not any('dangerously' in value for value in args)
assert 'CLAUDECODE' not in os.environ
if codex:
 assert '--ignore-user-config' in args and 'approval_policy="never"' in args
 assert 'sandbox_mode="'+('workspace-write' if write else 'read-only')+'"' in args
else:
 assert '--safe-mode' in args and '--restricted' in args and '--strict-mcp-config' in args
 assert ('Write' in args[args.index('--tools')+1])==write
if purpose=='implementation': assert 'Resolved file:' in prompt and 'Resolve supporting file references against:' in prompt
if purpose=='review':
 assert not write
 assert os.environ['CANOPY_EVIDENCE_TOKEN'] in prompt
assert 'Do not push, merge, deploy' in prompt
with open(os.environ['CANOPY_TRACE'],'a') as output:
 output.write(json.dumps(dict(cwd=str(pathlib.Path.cwd()),purpose=purpose,write=write,args=args,readme=pathlib.Path('README.md').read_text()))+'\\n')
session='8f3d9960-7b24-493a-8d85-c153b054df66'
print(json.dumps({'type':'thread.started','thread_id':session} if codex else {'type':'system','session_id':session}),flush=True)
if write: pathlib.Path('implemented.txt').write_text('Fixture implementation\\n')
if 'REGRESSION_STOP_RECOVERY' in prompt: pathlib.Path('pause-stop-recovery').touch()
if 'REGRESSION_WAIT' in prompt:
 print(json.dumps({'type':'item.completed','item':{'type':'command_execution','command':'fixture protocol activity','aggregated_output':'Actual fixture is waiting pid='+str(os.getpid())}}),flush=True)
 time.sleep(120)
result='Fixture review report' if purpose=='review' else 'Fixture implementation report'
print(json.dumps({'type':'item.completed','item':{'type':'agent_message','text':result}} if codex else {'type':'result','is_error':False,'result':result,'permission_denials':[]}),flush=True)
''')
    fake.chmod(0o755)
    with socket.socket() as sock:
        sock.bind(('127.0.0.1', 0)); base = f'http://127.0.0.1:{sock.getsockname()[1]}'
    token = str(uuid.uuid4()); secret = str(uuid.uuid4())
    env = os.environ | {'Database__Path': str(scratch / 'workspace.duckdb'), 'Agents__WorkspaceRoot': str(scratch / 'worktrees'),
        'Agents__CodexExecutable': str(fake), 'Agents__ClaudeExecutable': str(fake), 'Agents__PollIntervalMs': '250',
        'Factory__Skills__0__Id': 'fixture-skill', 'Factory__Skills__0__Source': 'regression catalogue', 'Factory__Skills__0__Path': 'SKILL.md',
        'CLAUDECODE': 'fixture-outer-client', 'CANOPY_TRACE': str(trace), 'CANOPY_EVIDENCE_TOKEN': token, 'CANOPY_TEST_INPUT': secret}
    log = (scratch / 'server.log').open('w+'); process = None; cleanup_pid = None

    def call(path, method='GET', body=None, expected=200, headers=None):
        request = urllib.request.Request(base + path, data=None if body is None else json.dumps(body).encode(), headers={'Content-Type': 'application/json'} | (headers or {}), method=method)
        try:
            with urllib.request.urlopen(request, timeout=20) as response: code, raw = response.status, response.read()
        except urllib.error.HTTPError as error: code, raw = error.code, error.read()
        value = json.loads(raw) if raw else None
        assert code == expected, (path, code, expected, value)
        return value

    def start():
        global process
        process = subprocess.Popen(['dotnet', 'bin/Debug/net10.0/Planner.Api.dll', '--urls', base], cwd=API, env=env, stdout=log, stderr=log)
        for _ in range(200):
            if process.poll() is not None: log.seek(0); raise AssertionError(log.read())
            try: call('/api/health'); return
            except OSError: time.sleep(.1)
        raise AssertionError('Backend startup timed out')

    def revision(): return next(p['revision'] for p in call('/api/workspace')['projects'] if p['id'] == pid)
    def read(run): return call(f"/api/factory/runs/{run['run']['id']}")
    def wait(run, predicate):
        deadline = time.monotonic() + 25
        while time.monotonic() < deadline:
            value = read(run)
            if predicate(value): return value
            time.sleep(.1)
        raise AssertionError(('Run did not reach expected state', read(run)))
    def prepare(definition, name='Execution fixture'):
        draft = call(f'/api/factory/projects/{pid}/pipelines', 'POST', dict(expectedRevision=revision(), name=name, description='Explicit test input', source='Regression fixture', definition=definition), 201)
        detail = call(f"/api/factory/pipelines/{draft['pipeline']['id']}/publish", 'POST', dict(expectedRevision=revision(), expectedDraftRevision=1))
        version = detail['versions'][0]
        call(f'/api/factory/tasks/{tid}/configuration', 'PUT', dict(expectedRevision=revision(), pipelineVersionId=version['id'], overrides={'steps': []}))
        return call(f'/api/factory/tasks/{tid}/runs', 'POST', dict(expectedRevision=revision(), requestId=str(uuid.uuid4())), 201)
    def dispatch(run): return call(f"/api/factory/runs/{run['run']['id']}/start", 'POST', dict(requestId=str(uuid.uuid4()), expectedRevision=run['run']['revision']))
    def decide(run, approved=True, note='Operator reviewed fixture evidence'):
        step = next(s for s in run['steps'] if s['state'] == 'waiting')
        body = dict(requestId=str(uuid.uuid4()), expectedRevision=run['run']['revision'], approved=approved, note=note)
        return call(f"/api/factory/runs/{run['run']['id']}/steps/{step['id']}/decision", 'POST', body), step, body
    def trace_rows(): return [json.loads(line) for line in trace.read_text().splitlines()] if trace.exists() else []
    def command_definition(code, timeout=10):
        return {'stages': [dict(id='verify', name='Actual command', group='Check', dependsOn=[], steps=[dict(id='command', name='Verification command', kind='command', command=dict(executable=sys.executable, arguments=['-c', code], workingDirectory='.', timeoutSeconds=timeout), produces=['command-result'])])]}

    try:
        start()
        state = call('/api/projects', 'POST', dict(name='Factory execution regression', description='Test only', color='#477fa1')); pid = state['projects'][0]['id']
        state = call(f'/api/projects/{pid}/items', 'POST', dict(expectedRevision=revision(), title='Execute captured work order')); task = state['items'][0]; tid = task['id']
        spec = json.dumps(dict(type='doc', content=[dict(type='paragraph', content=[dict(type='text', text='Implement and verify the fixture in a detached worktree.')])]))
        call(f'/api/items/{tid}', 'PATCH', dict(expectedRevision=revision(), title=task['title'], specJson=spec, statusId=task['statusId'], priority='none', assigneeIds=[], dependencyIds=[], startDate=None, dueDate=None))
        connection = dict(expectedRevision=revision(), repositoryPath=str(repo), baseRef='HEAD', provider='codex', model='fixture-model', instructions='Use the captured specification.')
        call(f'/api/agents/projects/{pid}/connection', 'PUT', connection)
        verify = command_definition("from pathlib import Path;import os;assert Path('implemented.txt').read_text()=='Fixture implementation\\n';assert os.environ['CHECK_INPUT']==os.environ['CANOPY_TEST_INPUT'];assert os.environ['CLAUDECODE']==os.environ['CANOPY_TEST_INPUT'];print(os.environ['CANOPY_EVIDENCE_TOKEN'])")['stages'][0]['steps'][0]
        verify.update(consumes=['changes'], produces=['verification']); verify['command']['environmentReferences']={'CHECK_INPUT': 'CANOPY_TEST_INPUT', 'CLAUDECODE': 'CANOPY_TEST_INPUT'}
        definition = {'stages': [
            dict(id='prepare', name='Prepare', group='Prepare', dependsOn=[], steps=[dict(id='approve', name='Approve specification', kind='manual', instructions='Review the captured specification.', produces=['approved-specification'])]),
            dict(id='build', name='Implementation', group='Build', dependsOn=['prepare'], steps=[dict(id='implement', name='Implement', kind='agent', role='implementation', permission='workspace-write', instructions='Implement the fixture.', consumes=['approved-specification'], produces=['changes'], skills=[dict(id='fixture-skill', source='regression catalogue')])]),
            dict(id='verify', name='Verify', group='Check', dependsOn=['build'], steps=[verify]),
            dict(id='review', name='Review', group='Check', dependsOn=['verify'], steps=[dict(id='artifacts', name='Inspect evidence', kind='artifact', consumes=['verification']),dict(id='review', name='Review changes', kind='agent', role='review', permission='read-only', instructions='Review the actual changes and verification evidence.', consumes=['changes','verification'], produces=['review'])]),
            dict(id='accept', name='Accept', group='Accept', dependsOn=['review'], steps=[dict(id='approve', name='Accept evidence', kind='manual', instructions='Review the evidence before acceptance.', consumes=['review'])]) ]}
        prepared = prepare(definition); baseline = call('/api/workspace')
        start_body = dict(requestId=str(uuid.uuid4()), expectedRevision=prepared['run']['revision']); start_path=f"/api/factory/runs/{prepared['run']['id']}/start"
        call(start_path, 'POST', start_body, 403, {'Origin': 'https://example.invalid'})
        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool: starts = list(pool.map(lambda _: call(start_path, 'POST', start_body), range(2)))
        check(starts[0]['run']['id'] == starts[1]['run']['id'], 'concurrent start retries dispatch one captured run')
        waiting = wait(prepared, lambda d:d['run']['state']=='waiting')
        check(not trace_rows() and not (Path(waiting['run']['workspacePath'])/'implemented.txt').exists(), 'initial manual gate blocks actual agent/command execution')
        check(Path(waiting['run']['workspacePath']).is_dir() and waiting['run']['snapshot']['environment']['baseCommit'] == baseline_commit, 'explicit start creates a worktree at the captured commit')
        call(f'/api/items/{tid}?expectedRevision={revision()}', 'DELETE', expected=409)
        call(f'/api/factory/tasks/{tid}/configuration?expectedRevision={revision()}', 'DELETE', expected=409)
        call(f'/api/agents/tasks/{tid}/runs', 'POST', dict(requestId=str(uuid.uuid4()), expectedRevision=revision(), provider='codex', purpose='documentation', instructions='Do not run'), 409)
        check(not call(f'/api/factory/tasks/{tid}/readiness')['canPrepare'], 'active factory work locks conflicting task execution/configuration/deletion')
        first_manual=next(s for s in waiting['steps'] if s['state']=='waiting')
        call(f"/api/factory/runs/{prepared['run']['id']}/steps/{first_manual['id']}/decision",'POST',dict(requestId=str(uuid.uuid4()),expectedRevision=waiting['run']['revision']),400)
        check(read(prepared)['run']['state']=='waiting','manual decisions require an explicit approved value; omission cannot decline or advance work')
        advanced, manual, decision = decide(waiting)
        replay = call(f"/api/factory/runs/{prepared['run']['id']}/steps/{manual['id']}/decision", 'POST', decision)
        check(sum(a['name']=='approved-specification' for a in replay['artifacts']) == 1, 'human decisions are explicit, audited and idempotent')
        final_gate = wait(prepared, lambda d:d['run']['state']=='waiting' and next(x for x in d['stages'] if x['stageId']=='accept')['state']=='waiting')
        rows = trace_rows()
        check([r['purpose'] for r in rows] == ['implementation','review'] and rows[0]['write'] and not rows[1]['write'], 'implementation and review reach the CLI with the configured write/read-only permissions')
        check(len({r['cwd'] for r in rows}) == 1 and rows[0]['cwd'] != str(repo), 'all agent stages share the retained run worktree')
        verify_step = next(x for x in final_gate['steps'] if x['stepId']=='command')
        check(verify_step['exitCode'] == 0 and token in verify_step['output'], 'verification executes the actual typed command and records its real output/exit code')
        for artifact in final_gate['artifacts']:
            producer = next(x for x in final_gate['steps'] if x['id']==artifact['stepRunId'])
            assert artifact['evidenceHash'] == hashlib.sha256(producer['evidence'].encode()).hexdigest().upper()
            assert artifact['kind']=='step-evidence'
        check(True, 'logical artifacts hash actual successful step evidence, without inventing physical files')
        check(not call(f'/api/agents/tasks/{tid}/runs'), 'factory children remain separate from standalone agent history')
        check(call('/api/workspace') == baseline and secret not in json.dumps(final_gate), 'factory activity leaves task workflow/layout unchanged and stores no environment-reference values')
        decide(final_gate); complete = wait(prepared, lambda d:d['run']['state']=='succeeded')
        check(all(x['state']=='succeeded' for x in complete['stages']+complete['steps']), 'final acceptance completes the graph only after all real steps and human gates')
        check(subprocess.check_output(['git','-C',str(repo),'status','--porcelain'],text=True)=='', 'execution does not modify the connected source checkout')
        check(call(start_path, 'POST', start_body)['run']['state']=='succeeded' and len(trace_rows())==2, 'start idempotency never replays completed work')
        child=next(step for step in complete['steps'] if step['agentRunId'])
        call(f'/api/agents/tasks/{tid}/runs','POST',dict(requestId=str(uuid.uuid4()),expectedRevision=revision(),provider='codex',purpose='implementation',instructions='Continue outside the factory',previousRunId=child['agentRunId']),409)
        check(True,'factory child sessions cannot be resumed through standalone agent dispatch')
        pinned_definition = {'stages': [dict(id='inspect', name='Inspect pinned source', group='Check', dependsOn=[], steps=[dict(id='inspect', name='Read source', kind='agent', role='verification', permission='read-only', instructions='Inspect the pinned baseline.')])]}
        pinned = prepare(pinned_definition)
        expected_readme = (repo / 'README.md').read_text()
        (repo / 'README.md').write_text('# Later source commit\n')
        subprocess.run(['git','-C',str(repo),'add','.'],check=True); subprocess.run(['git','-C',str(repo),'commit','-qm','Later source input'],check=True)
        connection.update(expectedRevision=revision(), model='later-project-model'); call(f'/api/agents/projects/{pid}/connection','PUT',connection)
        dispatch(pinned); pinned_result = wait(pinned,lambda d:d['run']['state']=='succeeded'); pinned_trace=trace_rows()[-1]
        check(pinned_trace['readme']==expected_readme and pinned_result['run']['snapshot']['environment']['baseCommit']==baseline_commit,'moving source refs does not replace the captured base commit')
        check(pinned_trace['args'][pinned_trace['args'].index('--model')+1]==pinned['run']['snapshot']['environment']['definition']['stages'][0]['steps'][0]['model'],'later project model changes do not replace captured execution settings')
        claude_definition=copy.deepcopy(pinned_definition); claude_definition['stages'][0]['steps'][0].update(provider='claude',model='fixture-claude')
        claude=prepare(claude_definition);dispatch(claude);wait(claude,lambda d:d['run']['state']=='succeeded')
        check('--safe-mode' in trace_rows()[-1]['args'] and not trace_rows()[-1]['write'],'Claude factory steps reuse the restricted read-only CLI path')
        failing_definition=command_definition("print('real failure');raise SystemExit(7)")
        unreachable=command_definition("from pathlib import Path;Path('must-not-run.txt').write_text('unexpected execution')")['stages'][0]
        unreachable.update(id='unreachable',dependsOn=['verify']);unreachable['steps'][0]['produces']=[]
        failing_definition['stages'].append(unreachable)
        failing = prepare(failing_definition); dispatch(failing); failure=wait(failing,lambda d:d['run']['state']=='failed')
        check(failure['steps'][0]['exitCode']==7 and not failure['artifacts'], 'nonzero commands fail the run and cannot publish success evidence')
        check(failure['steps'][1]['state']=='blocked' and not (Path(failure['run']['workspacePath'])/'must-not-run.txt').exists(),'failed stages prevent real execution of dependent commands')
        large_deadline=prepare(command_definition("print('real bounded command')",2**31-1));dispatch(large_deadline);bounded=wait(large_deadline,lambda d:d['run']['state']=='succeeded')
        check(bounded['steps'][0]['exitCode']==0,'large positive command deadlines obey the configured active-step ceiling without timer overflow')
        timed = prepare(command_definition("import os,time;print('pid='+str(os.getpid()),flush=True);time.sleep(60)",1)); dispatch(timed); timeout=wait(timed,lambda d:d['run']['state']=='failed')
        check('timeout' in timeout['steps'][0]['error'] and timeout['steps'][0]['exitCode'] is None, 'command timeout is a recorded failure, not a fabricated exit code')
        cancelling = prepare(command_definition("import os,time;print('pid='+str(os.getpid()),flush=True);time.sleep(60)",60)); dispatch(cancelling)
        running=wait(cancelling,lambda d:'pid=' in d['steps'][0]['output']); pid_text=int(re.search(r'pid=(\d+)',running['steps'][0]['output']).group(1))
        call(f"/api/factory/runs/{cancelling['run']['id']}/cancel", 'POST', dict(expectedRevision=running['run']['revision']))
        cancelled=wait(cancelling,lambda d:d['run']['state']=='cancelled')
        try:os.kill(pid_text,0);alive=True
        except ProcessLookupError:alive=False
        check(not alive and cancelled['steps'][0]['state']=='cancelled', 'cancellation stops the actual process and records cancellation')
        slow_agent=copy.deepcopy(definition);slow_agent['stages']=slow_agent['stages'][1:2];slow_agent['stages'][0]['dependsOn']=[];slow_agent['stages'][0]['steps'][0]['consumes']=[];slow_agent['stages'][0]['steps'][0]['instructions']='REGRESSION_WAIT implement fixture'
        cancelling_agent=prepare(slow_agent);dispatch(cancelling_agent)
        active_agent=wait(cancelling_agent,lambda d:'waiting pid=' in d['steps'][0]['output'])
        agent_pid=int(re.search(r'pid=(\d+)',active_agent['steps'][0]['output']).group(1))
        call(f"/api/factory/runs/{cancelling_agent['run']['id']}/cancel",'POST',dict(expectedRevision=active_agent['run']['revision']))
        settled_agent=wait(cancelling_agent,lambda d:d['run']['state']=='cancelled');child=call('/api/agents/runs/'+settled_agent['steps'][0]['agentRunId'])
        try:os.kill(agent_pid,0);agent_alive=True
        except ProcessLookupError:agent_alive=False
        check(not agent_alive and not child['active'],'agent cancellation settles its real child process before the parent becomes terminal')
        check('implemented.txt' in json.loads(settled_agent['steps'][0]['evidence'])['diff'],'cancellation preserves the child diff captured during shutdown')
        manual_def={'stages':[dict(id='approval',name='Approval',group='Review',dependsOn=[],steps=[dict(id='approve',name='Human approval',kind='manual',instructions='Inspect the evidence.')])]}
        declining=prepare(manual_def);dispatch(declining);pending=wait(declining,lambda d:d['run']['state']=='waiting');declined,step,body=decide(pending,False,'Operator declined')
        check(declined['run']['state']=='failed' and step['state']=='waiting' and declined['steps'][0]['decision'] is False, 'declining a manual gate durably stops the run')
        call(f"/api/factory/runs/{declining['run']['id']}/steps/{step['id']}/decision",'POST',body)
        call(f"/api/factory/runs/{declining['run']['id']}/steps/{step['id']}/decision",'POST',body|{'approved':True},409)
        check(True,'a replayed decision cannot silently change an earlier human decision')
        stopped_gate=prepare(manual_def);dispatch(stopped_gate);gate=wait(stopped_gate,lambda d:d['run']['state']=='waiting')
        stopped=call(f"/api/factory/runs/{stopped_gate['run']['id']}/cancel",'POST',dict(expectedRevision=gate['run']['revision']))
        check(stopped['run']['state']=='cancelled','cancelling a waiting manual gate settles immediately without dispatch')
        skill_change=prepare(definition);old_skill=(repo/'SKILL.md').read_bytes();(repo/'SKILL.md').write_text('# Changed after preparation\n')
        call(f"/api/factory/runs/{skill_change['run']['id']}/start",'POST',dict(requestId=str(uuid.uuid4()),expectedRevision=skill_change['run']['revision']),400)
        check(read(skill_change)['run']['state']=='prepared','changed skill contents block dispatch rather than replacing the captured skill')
        (repo/'SKILL.md').write_bytes(old_skill)
        call(f"/api/factory/runs/{skill_change['run']['id']}/cancel",'POST',dict(expectedRevision=skill_change['run']['revision']))
        slow=copy.deepcopy(definition);slow['stages']=slow['stages'][1:2];slow['stages'][0]['dependsOn']=[];slow['stages'][0]['steps'][0]['consumes']=[];slow['stages'][0]['steps'][0]['instructions']='REGRESSION_WAIT implement fixture'
        interrupted=prepare(slow);dispatch(interrupted);active=wait(interrupted,lambda d:d['steps'][0]['state']=='running' and 'waiting' in d['steps'][0]['output'])
        before_restart=len(trace_rows());process.terminate();process.wait(timeout=25);start();recovered=read(interrupted)
        time.sleep(.6)
        check(recovered['run']['state']=='interrupted' and not recovered['run']['active'] and len(trace_rows())==before_restart,'backend restart marks work interrupted before traffic and never automatically replays it')
        check((Path(recovered['run']['workspacePath'])/'implemented.txt').exists(),'interrupted execution retains its actual worktree for review')
        # Pause real Git diff collection to persist an observable stopping state,
        # then crash this isolated backend. No live application data is used.
        process.terminate();process.wait(timeout=25)
        delayed_git=scratch/'delayed-git'
        delayed_git.write_text("#!/usr/bin/env python3\nimport os,pathlib,sys,time\nif 'diff' in sys.argv[1:] and pathlib.Path('pause-stop-recovery').exists():\n pathlib.Path('git-cleanup-pid.txt').write_text(str(os.getpid()))\n time.sleep(60)\nos.execv("+repr(shutil.which('git'))+",['git',*sys.argv[1:]])\n")
        delayed_git.chmod(0o755);env['Agents__GitExecutable']=str(delayed_git);start()
        crash_definition=copy.deepcopy(slow_agent);crash_definition['stages'][0]['steps'][0]['instructions']='REGRESSION_WAIT REGRESSION_STOP_RECOVERY implement fixture'
        stopping=prepare(crash_definition);dispatch(stopping);running_stop=wait(stopping,lambda d:'waiting pid=' in d['steps'][0]['output'])
        call(f"/api/factory/runs/{stopping['run']['id']}/cancel",'POST',dict(expectedRevision=running_stop['run']['revision']))
        cleanup_file=Path(running_stop['run']['workspacePath'])/'git-cleanup-pid.txt'
        wait(stopping,lambda d:d['run']['state']=='cancelling' and cleanup_file.exists())
        cleanup_pid=int(cleanup_file.read_text());before_crash=len(trace_rows());process.kill();process.wait(timeout=25)
        try:os.kill(cleanup_pid,9)
        except ProcessLookupError:pass  # The backend owner lease may already have stopped it.
        cleanup_pid=None;start();recovered_stop=read(stopping)
        stopped_child=call('/api/agents/runs/'+recovered_stop['steps'][0]['agentRunId'])
        check(recovered_stop['run']['state']=='interrupted' and stopped_child['state']=='interrupted','a crash during child cleanup recovers both stopping records as interrupted, never confirmed cancelled')
        time.sleep(.6)
        check(len(trace_rows())==before_crash and (Path(recovered_stop['run']['workspacePath'])/'implemented.txt').exists(),'recovery after an interrupted stop retains work and never restarts the child')
        check(next(t for t in call('/api/workspace')['items'] if t['id']==tid)==next(t for t in baseline['items'] if t['id']==tid),'all task metadata remains unchanged after execution, failure, cancellation and restart')
        call(f'/api/projects/{pid}?expectedRevision={revision()}', 'DELETE')
        call(f"/api/factory/runs/{prepared['run']['id']}",expected=404)
        check(not call('/api/workspace')['projects'],'terminal run history and test project clean up transactionally')
        print(f'{checks} execution checks passed; provider responses were protocol fixtures, not live model calls.',flush=True)
    finally:
        if cleanup_pid:
            try: os.kill(cleanup_pid,9)
            except ProcessLookupError: pass
        if process and process.poll() is None:process.terminate();process.wait(timeout=25)
        log.close()
