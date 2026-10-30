#!/usr/bin/env python3
"""Native HTTP/Git/process routing, policy revocation and usage evidence. No model calls."""
import copy
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

def check(value, message):
    global checks
    assert value, message
    checks += 1
    print(f'PASS {checks:02}: {message}', flush=True)

with tempfile.TemporaryDirectory(prefix='canopy-routing-') as directory:
    scratch = Path(directory).resolve(); repo = scratch / 'fixture repository'; repo.mkdir()
    (repo / 'README.md').write_text('Caller supplied protocol fixture.\n')
    for args in (['init', '-q'], ['config','user.name','Canopy regression'], ['config','user.email','regression@example.invalid'], ['add','.'], ['commit','-qm','Fixture baseline']):
        subprocess.run(['git','-C',str(repo),*args],check=True)
    trace = scratch / 'trace.jsonl'; fake = scratch / 'agent-fixture'
    fake.write_text('''#!/usr/bin/env python3
import json,os,pathlib,sys,time
args=sys.argv[1:];prompt=sys.stdin.read();codex=args[0]=='exec'
assert not any('dangerously' in arg for arg in args)
with open(os.environ['CANOPY_ROUTE_TRACE'],'a') as f:f.write(json.dumps(dict(args=args,pid=os.getpid(),prompt=prompt))+'\\n')
if 'WAIT_FOR_REVOCATION' in prompt:time.sleep(120)
usage={'input_tokens':13,'output_tokens':5,'cached_input_tokens':8} if codex else {'input_tokens':7,'output_tokens':3,'cache_read_input_tokens':2}
if 'INVALID_USAGE' in prompt:usage={'input_tokens':'not a counter','output_tokens':-1}
if codex:print(json.dumps({'type':'item.completed','item':{'type':'agent_message','text':'Explicit fixture result'}}),flush=True)
for i in range(5 if 'OVERFLOW_USAGE' in prompt else 2 if 'MIXED_USAGE' in prompt else 1):
 event={'type':'turn.completed'} if codex else {'type':'result','result':'Explicit fixture result','is_error':False}
 if 'MISSING_USAGE' not in prompt and not ('MIXED_USAGE' in prompt and i==1):event['usage']=usage
 print(json.dumps(event),flush=True)
''');fake.chmod(0o755)
    with socket.socket() as sock:
        sock.bind(('127.0.0.1',0)); base=f'http://127.0.0.1:{sock.getsockname()[1]}'
    env=os.environ | {'Database__Path':str(scratch/'workspace.duckdb'),'Agents__WorkspaceRoot':str(scratch/'worktrees'), 'Agents__CodexExecutable':str(fake),'Agents__ClaudeExecutable':str(fake),'Agents__PollIntervalMs':'250','Agents__MaxUsageEvents':'3','CANOPY_ROUTE_TRACE':str(trace)}
    process=None; log=(scratch/'api.log').open('w+')
    def call(path,method='GET',body=None,expected=200,headers=None):
        request=urllib.request.Request(base+path,data=None if body is None else json.dumps(body).encode(),method=method,headers={'Content-Type':'application/json'} | (headers or {}))
        try:
            with urllib.request.urlopen(request,timeout=30) as response:code,raw=response.status,response.read()
        except urllib.error.HTTPError as error:code,raw=error.code,error.read()
        value=json.loads(raw) if raw else None
        assert code==expected,(path,code,expected,value)
        return value
    def start():
        global process
        process=subprocess.Popen(['dotnet','bin/Debug/net10.0/Planner.Api.dll','--urls',base],cwd=API,env=env,stdout=log,stderr=log)
        for _ in range(200):
            if process.poll() is not None:log.seek(0);raise AssertionError(log.read())
            try:call('/api/health');return
            except OSError:time.sleep(.1)
        raise TimeoutError('Isolated API startup')
    def revision():return next(p['revision'] for p in call('/api/workspace')['projects'] if p['id']==pid)
    def rows():return [json.loads(line) for line in trace.read_text().splitlines()] if trace.exists() else []
    def policy(**patch):return dict(enabled=True,routes=[dict(role='documentation',provider='codex',model='fixture-role-model',effort='high')],maxAgentInvocations=8,maxCommandInvocations=2,maxElapsedSeconds=120,allowUnmeteredCost=True,maxTokens=None,maxCost=None,currency=None)|patch
    def adopt(p,expected=200):return call(policy_path,'PUT',dict(expectedRevision=revision(),policy=p),expected)
    def agent(id='agent',**patch):return dict(id=id,name=id,kind='agent',role='documentation',permission='read-only',instructions='Use explicit fixture.')|patch
    def manual():return dict(id='manual',name='Manual',kind='manual',instructions='Explicit operator decision.')
    def definition(steps):return dict(stages=[dict(id='stage',name='Stage',group='Check',dependsOn=[],steps=steps)])
    def command(code):return dict(id='command',name='Command',kind='command',command=dict(executable=sys.executable,arguments=['-c',code],workingDirectory='.',timeoutSeconds=120))
    def attach(d,overrides=None):
        draft=call(f'/api/factory/projects/{pid}/pipelines','POST',dict(expectedRevision=revision(),name='Routing fixture',description='Test only',source='Regression caller',definition=d),201)
        published=call(f"/api/factory/pipelines/{draft['pipeline']['id']}/publish",'POST',dict(expectedRevision=revision(),expectedDraftRevision=1))
        version=published['versions'][0]
        call(f'/api/factory/tasks/{tid}/configuration','PUT',dict(expectedRevision=revision(),pipelineVersionId=version['id'],overrides=overrides or {'steps':[]}))
    def prepare(d=None,overrides=None):
        if d is not None:attach(d,overrides)
        return call(f'/api/factory/tasks/{tid}/runs','POST',dict(expectedRevision=revision(),requestId=str(uuid.uuid4())),201)
    def read(r):return call(f"/api/factory/runs/{r['run']['id']}")
    def dispatch(r,expected=200):return call(f"/api/factory/runs/{r['run']['id']}/start",'POST',dict(expectedRevision=r['run']['revision'],requestId=str(uuid.uuid4())),expected)
    def wait(r,state):
        until=time.monotonic()+35
        while time.monotonic()<until:
            value=read(r)
            if value['run']['state']==state:return value
            if value['run']['state'] in ('failed','succeeded','cancelled','interrupted'):raise AssertionError((state,value))
            time.sleep(.1)
        raise TimeoutError(read(r))
    def decide(r,expected=200):
        step=next(s for s in r['steps'] if s['state']=='waiting')
        return call(f"/api/factory/runs/{r['run']['id']}/steps/{step['id']}/decision",'POST',dict(expectedRevision=r['run']['revision'],requestId=str(uuid.uuid4()),approved=True),expected)
    def alive(pid):
        try:os.kill(pid,0);return True
        except ProcessLookupError:return False
    try:
        start();state=call('/api/projects','POST',dict(name='Routing regression',description='Temporary test input',color='#477fa1'));pid=state['projects'][0]['id']
        state=call(f'/api/projects/{pid}/items','POST',dict(expectedRevision=revision(),title='Explicit routing fixture'));tid=state['items'][0]['id']
        policy_path=f'/api/factory/projects/{pid}/execution-policy';ready_path=f'/api/factory/tasks/{tid}/readiness'
        initial=call(policy_path)
        check(initial['policy'] is None and not initial['capabilities']['hardTokenCap'] and not initial['capabilities']['hardCurrencyCap'],'no default routes or fictitious hard spend enforcement')
        call(policy_path,'PUT',dict(expectedRevision=revision(),policy=policy()),403,{'Origin':'https://example.invalid'})
        check(call(policy_path)['policy'] is None,'cross-origin adoption cannot change execution authority')
        invalid=[policy(routes=[dict(role='unknown',provider='codex',model='fixture')]),policy(routes=[dict(role='documentation',provider='unknown',model='fixture')]),policy(routes=[dict(role='documentation',provider='codex',model='')]),policy(routes=[dict(role='documentation',provider='codex',model='fixture',effort='unsupported')]),policy(routes=[dict(role='documentation',provider='codex',model='fixture')]*2),policy(maxElapsedSeconds=0),policy(maxCommandInvocations=-1),policy(maxCost=1,currency=None),policy(unrecognized='reject')]
        for p in invalid:adopt(p,400)
        check(call(policy_path)['policy'] is None,'invalid roles, providers, effort, limits and unknown fields are rejected atomically')
        call(f'/api/agents/projects/{pid}/connection','PUT',dict(expectedRevision=revision(),repositoryPath=str(repo),baseRef='HEAD',provider='codex',settings={'codex':{'model':'fixture-project-codex','effort':'low'},'claude':{'model':'fixture-project-claude','effort':'medium'}},instructions='Caller settings.'))
        stale=revision();saved=adopt(policy());call(policy_path,'PUT',dict(expectedRevision=stale,policy=policy()),409)
        check(saved['policy']['routes'][0]['model']=='fixture-role-model','policy persists caller model IDs with optimistic concurrency')
        steps=[manual(),agent('routed'),agent('explicit',provider='claude'),agent('effort',effort='medium'),agent('missing',instructions='MISSING_USAGE'),agent('invalid',instructions='INVALID_USAGE'),agent('overflow',instructions='OVERFLOW_USAGE'),agent('model',model='fixture-step-model'),agent('task-override'),command('print("Actual command fixture")')]
        prepared=prepare(definition(steps),{'steps':[dict(stageId='stage',stepId='task-override',provider='claude',model='fixture-task-model',effort='low')]});before=call('/api/workspace');capture=prepared['run']['snapshot']['execution'];routes=capture['routes']
        check(not rows() and prepared['budget']['reservedAgentInvocations']==0,'policy, readiness and preparation execute no provider process and reserve no calls')
        check(routes[0]['selectionSource']=='role' and routes[0]['model']=='fixture-role-model' and routes[0]['effort']=='high','role profile pins requested model and reasoning effort')
        check(routes[1]['selectionSource']=='step' and routes[1]['provider']=='claude' and routes[1]['model']=='fixture-project-claude','explicit provider isolates its own project model defaults')
        check(routes[2]['effortSource']=='step' and routes[2]['effort']=='medium','explicit effort overrides the role effort')
        dispatch(prepared);waiting=wait(prepared,'waiting');decide(waiting);done=wait(prepared,'succeeded');actual=rows()
        check(len(actual)==8 and done['budget']['reservedAgentInvocations']==8 and done['budget']['reservedCommandInvocations']==1,'persistent reservations equal actual agent and command invocations')
        check(actual[0]['args'][actual[0]['args'].index('--model')+1]=='fixture-role-model' and 'model_reasoning_effort="high"' in actual[0]['args'],'actual Codex CLI receives captured role model and effort')
        check(actual[1]['args'][actual[1]['args'].index('--model')+1]=='fixture-project-claude' and actual[1]['args'][actual[1]['args'].index('--effort')+1]=='medium','actual Claude CLI receives its own project settings')
        check(routes[6]['selectionSource']=='step' and routes[6]['effort']=='low' and 'model_reasoning_effort="low"' in actual[6]['args'] and actual[6]['args'][actual[6]['args'].index('--model')+1]=='fixture-step-model','explicit step model overrides a role profile without leaking its effort')
        check(routes[7]['selectionSource']=='task' and routes[7]['effortSource']=='task' and actual[7]['args'][actual[7]['args'].index('--model')+1]=='fixture-task-model','task model/provider overrides retain distinct provenance and actual CLI arguments')
        uses=[call(f"/api/agents/runs/{s['agentRunId']}")['usage'] for s in done['steps'] if s['agentRunId']]
        check(uses[0]['status']=='reported' and uses[0]['observations'][0]['inputTokens']==13 and uses[1]['observations'][0]['inputTokens']==7,'provider completion observations retain provider-specific counters')
        check(uses[3]['status']=='unknown' and uses[3]['observations'][0]['status']=='missing','missing usage remains unknown rather than zero')
        check(uses[4]['status']=='incomplete' and uses[4]['observations'][0]['status']=='invalid','malformed counters cannot become a complete usage report')
        check(uses[5]['status']=='incomplete' and uses[5]['retentionLimitReached'] and len(uses[5]['observations'])==3,'bounded usage retention exposes missing coverage')
        check(call('/api/workspace')['items']==before['items'],'factory routing and execution do not change task workflow or hierarchy')
        check(not call(f"/api/factory/runs/{done['run']['id']}/qa")['sourceChanged'],'captured execution policy is bound into fresh QA source evidence')
        adopt(policy(maxAgentInvocations=9));check(call(f"/api/factory/runs/{done['run']['id']}/qa")['sourceChanged'],'policy changes invalidate old QA and downstream delivery authority')
        snapshot=read(done);policy_saved=call(policy_path);process.terminate();process.wait(timeout=30);start()
        check(read(done)['run']==snapshot['run'] and call(policy_path)==policy_saved,'model routes, usage and completed native records persist across restart')
        check(call(f"/api/agents/runs/{done['steps'][1]['agentRunId']}")['usage']==uses[0],'provider usage payloads and provenance survive native restart')
        if '--serve' in sys.argv:
            context=dict(baseUrl=base,projectId=pid,runId=done['run']['id'],scratch=str(scratch))
            (Path('/private/tmp')/'routing-ui-context.json').write_text(json.dumps(context))
            print(json.dumps(context),flush=True)
            while not (scratch/'continue').exists():time.sleep(.25)
        # Run saved collection only against this explicit isolated project.
        if '--postman' in sys.argv:
            subprocess.run(['postman','collection','run',str(ROOT/'postman/collections/Factory routing regression'),'--env-var',f'baseUrl={base}','--env-var',f'projectId={pid}','--env-var','fixtureMode=isolated','--report-events=false'],cwd=ROOT,check=True)
        adopt(policy(maxElapsedSeconds=8));mixed=prepare(definition([agent(instructions='MIXED_USAGE')]));dispatch(mixed);mixed=wait(mixed,'succeeded')
        usage=call(f"/api/agents/runs/{mixed['steps'][0]['agentRunId']}")['usage']
        check(usage['status']=='incomplete' and len(usage['observations'])==2,'a missing completion report cannot hide behind an earlier valid observation')
        time.sleep(8.1)
        check(read(mixed)['budget']['blockedReason'] is None,'completed work is judged at its recorded finish time rather than expiring later')
        adopt(policy());shared=dict(stages=[dict(id='one',name='Agent stage',group='Build',dependsOn=[],steps=[agent('shared')]),dict(id='two',name='Command stage',group='Check',dependsOn=['one'],steps=[command('print("Same step ID in a separate stage")')|dict(id='shared')])])
        shared=prepare(shared);dispatch(shared);shared=wait(shared,'succeeded')
        check(shared['budget']['reservedAgentInvocations']==1 and shared['budget']['reservedCommandInvocations']==1,'stage-local repeated step IDs cannot corrupt invocation accounting')
        attach(definition([agent()]));count=len(rows())
        for p,code in [(policy(maxTokens=100),'execution.unsupported'),(policy(maxCost=1,currency='USD'),'execution.unsupported'),(policy(allowUnmeteredCost=False),'execution.unsupported'),(policy(maxAgentInvocations=0),'execution.agentBudget')]:
            adopt(p);ready=call(ready_path);check(not ready['canPrepare'] and any(c['code']==code for c in ready['checks']),'unsupported or insufficient budget blocks preparation: '+code)
        adopt(policy(routes=[]));
        call(f'/api/agents/projects/{pid}/connection','PUT',dict(expectedRevision=revision(),repositoryPath=str(repo),baseRef='HEAD',provider='codex',settings={'codex':{'model':None,'effort':None},'claude':{'model':None,'effort':None}},instructions='No requested model.'))
        check(any(c['code']=='execution.modelRequired' for c in call(ready_path)['checks']),'enabled policy cannot silently select an unpinned provider-default model')
        adopt(policy());prepared=prepare();adopt(policy(maxAgentInvocations=9));dispatch(prepared,409)
        check(len(rows())==count,'policy drift before start dispatches no model and cannot silently reroute')
        adopt(policy());prepared=prepare(definition([manual(),agent()]));dispatch(prepared);waiting=wait(prepared,'waiting');adopt(policy(maxElapsedSeconds=121));failed=wait(prepared,'failed')
        check(len(rows())==count and 'policy changed' in failed['run']['error'].lower(),'policy changes revoke manual waits before any subsequent step')
        adopt(policy(maxElapsedSeconds=3));prepared=prepare(definition([manual(),agent()]));dispatch(prepared);waiting=wait(prepared,'waiting');failed=wait(prepared,'failed');decide(waiting,409)
        check('elapsed' in failed['run']['error'] and len(rows())==count,'elapsed work-order budget includes manual waits and rejects late approval')
        adopt(policy(maxElapsedSeconds=5));pidfile=scratch/'command.pid';prepared=prepare(definition([command(f'import os,time,pathlib;pathlib.Path({str(pidfile)!r}).write_text(str(os.getpid()));time.sleep(120)')]))
        dispatch(prepared);failed=wait(prepared,'failed');check('elapsed' in failed['run']['error'] and pidfile.exists() and not alive(int(pidfile.read_text())),'elapsed deadline kills the owned command process and retains one reservation')
        check(failed['budget']['reservedCommandInvocations']==1,'failed command reservation is retained and never refunded into an implicit retry')
        adopt(policy(maxElapsedSeconds=5));timed_agent=prepare(definition([agent(instructions='WAIT_FOR_REVOCATION')]));dispatch(timed_agent);timed_agent=wait(timed_agent,'failed')
        check('elapsed' in timed_agent['run']['error'] and len(rows())==count+1 and not alive(rows()[-1]['pid']) and timed_agent['budget']['reservedAgentInvocations']==1,'elapsed budget stops the owned agent and retains its invocation')
        count=len(rows())
        adopt(policy());prepared=prepare(definition([agent(instructions='WAIT_FOR_REVOCATION')]));dispatch(prepared)
        until=time.monotonic()+25
        while len(rows())==count:
            assert time.monotonic()<until;time.sleep(.1)
        childpid=rows()[-1]['pid'];adopt(policy(maxAgentInvocations=9));failed=wait(prepared,'failed')
        check(not alive(childpid) and failed['steps'][0]['agentRunId'] and failed['steps'][0]['evidence'],'policy revocation stops an owned agent and retains its final evidence')
        adopt(policy());prepared=prepare(definition([agent(),manual(),agent('after-restart')]));dispatch(prepared);waiting=wait(prepared,'waiting');count=len(rows());reserved=waiting['budget']['reservedAgentInvocations']
        process.terminate();process.wait(timeout=30);start();interrupted=read(prepared)
        check(interrupted['run']['state']=='interrupted' and interrupted['budget']['reservedAgentInvocations']==reserved and len(rows())==count,'restart retains reservations and never replays waiting or interrupted work')
        dispatch(interrupted,409)
        adopt(policy(enabled=False,routes=[]));check(call(policy_path)['policy']['enabled'] is False,'disabled policy preserves existing opt-in behavior')
        state=call(f'/api/projects/{pid}?expectedRevision={revision()}','DELETE');check(not state['projects'],'temporary project and its execution policy can be removed without orphan records')
        print(f'Factory routing regression: {checks} checks passed.')
    except BaseException:
        log.flush();log.seek(0);print(log.read()[-20000:],file=sys.stderr);raise
    finally:
        if process is not None and process.poll() is None:
            process.terminate()
            try:process.wait(timeout=30)
            except subprocess.TimeoutExpired:process.kill();process.wait()
        log.close()
