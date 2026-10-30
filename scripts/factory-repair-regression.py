#!/usr/bin/env python3
"""Bounded repair using real HTTP/Git/commands/native DuckDB; provider protocol fixtures, no model calls."""
import concurrent.futures
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

ROOT=Path(__file__).resolve().parents[1]; API=ROOT/'server/Planner.Api'; checks=0

def check(value,message):
    global checks
    assert value,message
    checks+=1; print(f'PASS {checks:02}: {message}',flush=True)

with tempfile.TemporaryDirectory(prefix='canopy-repair-') as directory:
    scratch=Path(directory).resolve();repo=scratch/'source';repo.mkdir()
    (repo/'.gitignore').write_text('__pycache__/\n')
    (repo/'obsolete.txt').write_text('tracked deletion fixture\n')
    (repo/'tool.sh').write_text('#!/bin/sh\nexit 0\n')
    (repo/'counter.py').write_text('def increment(value):\n    return 0\n')
    (repo/'verify.py').write_text('from counter import increment\nassert increment(0) == 1\nprint("ZERO_ASSERTION_VERIFIED")\n')
    for args in [['init','-q'],['config','user.name','Review regression'],['config','user.email','review@example.invalid'],['add','.'],['commit','-qm','Explicit regression baseline']]:subprocess.run(['git','-C',str(repo),*args],check=True)
    fake=scratch/'agent';trace=scratch/'trace.jsonl'
    mode=scratch/'mode';mode.write_text('good')
    fake.write_text('''#!/usr/bin/env python3
import json,os,pathlib,sys,time
prompt=sys.stdin.read();purpose=next(x.split(': ',1)[1] for x in prompt.splitlines() if x.startswith('Purpose: '));args=sys.argv[1:]
repair='Bounded repair scope' in prompt
with open(os.environ['REVIEW_TRACE'],'a') as f:f.write(json.dumps(dict(purpose=purpose,prompt=prompt,args=args,cwd=os.getcwd()))+'\\n')
if purpose in ('implementation','verification'):
 if repair:
  mode=pathlib.Path(os.environ['REPAIR_MODE']).read_text()
  if mode=='slow':
   pathlib.Path('active.pid').write_text(str(os.getpid()))
   time.sleep(180)
  if mode=='commandFail' or purpose=='verification':
   pathlib.Path('verify.py').write_text('from counter import increment\\nassert increment(0) == 1\\nassert increment(-1) == 0\\n')
  if mode=='good' and purpose=='implementation':
   pathlib.Path('counter.py').write_text('def increment(value):\\n    return value + 1\\n')
   pathlib.Path('verify.py').write_text('from counter import increment\\nassert increment(0) == 1\\nassert increment(-1) == 0\\nprint("ZERO_ASSERTION_VERIFIED NEGATIVE_ASSERTION_VERIFIED")\\n')
 else:
  pathlib.Path('counter.py').write_text('def increment(value):\\n    return value + 1 if value >= 0 else value\\n')
  pathlib.Path('staged.txt').write_text('index content\\n')
  import subprocess
  subprocess.run(['git','add','staged.txt'],check=True)
  pathlib.Path('staged.txt').write_text('working content\\n')
  pathlib.Path('obsolete.txt').unlink()
  pathlib.Path('binary.dat').write_bytes(bytes(range(256)))
  pathlib.Path('tool.sh').chmod(0o755)
  pathlib.Path('__pycache__').mkdir(exist_ok=True);pathlib.Path('__pycache__/ignored').write_text('ignored output')
 report='BUILDER_BIAS_SENTINEL: Everything is perfect.'
else:
 assert 'sandbox_mode="read-only"' in args
 assert 'BUILDER_BIAS_SENTINEL' not in prompt
 assert 'Independent review: recheck every prior finding first' in prompt
 assert 'ZERO_ASSERTION_VERIFIED' in prompt
 report='Independent assessment: inspect negative input against real command evidence.'
print(json.dumps({'type':'thread.started','thread_id':'a93c9b91-2011-4e22-ac90-626998c0ec50'}),flush=True)
print(json.dumps({'type':'item.completed','item':{'type':'agent_message','text':report}}),flush=True)
''');fake.chmod(0o755)
    with socket.socket() as sock:sock.bind(('127.0.0.1',0));base=f'http://127.0.0.1:{sock.getsockname()[1]}'
    env=os.environ|{'Database__Path':str(scratch/'workspace.duckdb'),'Agents__WorkspaceRoot':str(scratch/'worktrees'),'Agents__PollIntervalMs':'250','Agents__CodexExecutable':str(fake),'REVIEW_TRACE':str(trace),'REPAIR_MODE':str(mode)}
    log=(scratch/'server.log').open('w+');process=None
    def call(path,method='GET',body=None,expected=200,headers=None):
        request=urllib.request.Request(base+path,method=method,data=None if body is None else json.dumps(body).encode(),headers={'Content-Type':'application/json'}|(headers or {}))
        try:
            with urllib.request.urlopen(request,timeout=30) as r:code,raw=r.status,r.read()
        except urllib.error.HTTPError as e:code,raw=e.code,e.read()
        try:value=json.loads(raw) if raw else None
        except json.JSONDecodeError:raise AssertionError((path,code,raw.decode()[:3000]))
        assert code==expected,(path,code,expected,value)
        return value
    def start():
        global process
        process=subprocess.Popen(['dotnet','bin/Debug/net10.0/Planner.Api.dll','--urls',base],cwd=API,env=env,stdout=log,stderr=log)
        for _ in range(200):
            if process.poll() is not None:log.seek(0);raise AssertionError(log.read())
            try:call('/api/health');return
            except OSError:time.sleep(.1)
        raise AssertionError('Startup timeout')
    def revision():return next(p['revision'] for p in call('/api/workspace')['projects'] if p['id']==pid)
    def prepare():return call(f'/api/factory/tasks/{tid}/runs','POST',dict(requestId=str(uuid.uuid4()),expectedRevision=revision()),201)
    def execute(run):
        call('/api/factory/runs/'+run['run']['id']+'/start','POST',dict(requestId=str(uuid.uuid4()),expectedRevision=run['run']['revision']))
        for _ in range(200):
            run=call('/api/factory/runs/'+run['run']['id'])
            if not run['run']['active']:return run
            time.sleep(.1)
        raise AssertionError('Run timeout')
    def library(run):return call('/api/factory/runs/'+run['run']['id']+'/reviews')
    def read(round):return call('/api/factory/reviews/'+round['round']['id'])
    def begin_body(run):
        l=library(run);return dict(requestId=str(uuid.uuid4()),expectedRevision=l['runRevision'],expectedContext=l['context']['fingerprint'] if l['context'] else '',reviewer='Independent fixture reviewer')
    def begin(run):return call('/api/factory/runs/'+run['run']['id']+'/reviews','POST',begin_body(run))
    def assess(round,step=None):return call('/api/factory/reviews/'+round['round']['id']+'/assessment','POST',dict(requestId=str(uuid.uuid4()),expectedRevision=round['round']['revision'],assessment=None if step else 'Rechecked prior findings against actual code and tests before reading builder explanations.',assessmentStepRunId=step))
    def evidence(run):return dict(stepRunId=next(s['id'] for s in run['steps'] if s['stepId']=='verify'),testPath='verify.py',line=2,assertion='Zero increments to one.',failureCase='Returning unchanged input fails the assertion.')
    def qa_pass(run):
        q=call('/api/factory/runs/'+run['run']['id']+'/qa')
        return call('/api/factory/runs/'+run['run']['id']+'/qa','POST',dict(requestId=str(uuid.uuid4()),expectedRevision=q['runRevision'],expectedContext=q['context']['fingerprint'],criterionId=cid,verdict='pass',reason='Reviewed the actual zero-input assertion.',evidence=[evidence(run)]))
    def submit_body(round,verdict='inconclusive',rechecks=None,new=None):return dict(requestId=str(uuid.uuid4()),expectedRevision=round['round']['revision'],expectedContext=round['packet']['context']['fingerprint'],verdict=verdict,summary='Fixture reviewer examined the actual change and prior findings.',rechecks=rechecks or [],newFindings=new or [])
    def submit(round,body,expected=200):return call('/api/factory/reviews/'+round['round']['id']+'/submit','POST',body,expected)
    def recheck(status,reason='The negative-input bug remains visible in code.',code=None,proof=None):return dict(findingId=fid,status=status,reason=reason,codeReferences=code or [],evidence=proof or [])
    def publish(definition,name):
        p=call(f'/api/factory/projects/{pid}/pipelines','POST',dict(expectedRevision=revision(),name=name,description='Test only',source='Explicit regression fixture',definition=definition),201)
        return call('/api/factory/pipelines/'+p['pipeline']['id']+'/publish','POST',dict(expectedRevision=revision(),expectedDraftRevision=1))['versions'][0]
    def source(with_finding=True):
        global tid,cid,fid,task
        ws=call(f'/api/projects/{pid}/items','POST',dict(expectedRevision=revision(),title='Repair counter edge case'));task=ws['items'][-1];tid=task['id']
        spec=json.dumps(dict(type='doc',content=[dict(type='paragraph',content=[dict(type='text',text='Implement increment including negative values.')])]))
        call(f'/api/items/{tid}','PATCH',dict(expectedRevision=revision(),title=task['title'],specJson=spec,statusId=task['statusId'],priority='none',assigneeIds=[],dependencyIds=[],startDate=None,dueDate=None))
        cid=str(uuid.uuid4());call(f'/api/factory/tasks/{tid}/criteria','PUT',dict(expectedRevision=revision(),criteria=[dict(id=cid,description='Increment',expectedResult='Increment input by one',verificationMethod='Run and inspect assertions')]))
        call(f'/api/factory/tasks/{tid}/configuration','PUT',dict(expectedRevision=revision(),pipelineVersionId=version['id'],overrides={'steps':[]}))
        run=execute(prepare());assert run['run']['state']=='succeeded',run
        qa_pass(run);r=assess(begin(run));fid=str(uuid.uuid4())
        finding=dict(id=fid,title='Negative input does not increment',description='The negative-input branch returns unchanged input.',category='defect',severity='high',owner='implementation',criterionIds=[cid],codeReferences=[dict(path='counter.py',line=2)],evidence=[])
        if with_finding:submit(r,submit_body(r,'requestChanges',new=[finding]))
        else:call('/api/factory/reviews/'+r['round']['id']+'/abandon','POST',dict(requestId=str(uuid.uuid4()),expectedRevision=r['round']['revision'],reason='QA-only repair fixture'))
        return call('/api/factory/runs/'+run['run']['id'])
    def adopt(**changes):
        p=dict(enabled=True,implementationPlanVersionId=version['id'],verificationPlanVersionId=None,maxAttempts=3,maxElapsedMinutes=5,maxNoProgressRounds=2,maxAgentInvocations=6,autoContinue=False,commandsSafeToRepeat=True,allowUnmeteredCost=True,maxCost=None,currency=None)|changes
        return call(f'/api/factory/projects/{pid}/repair-policy','PUT',dict(expectedRevision=revision(),policy=p))
    def repair_library(run):return call('/api/factory/runs/'+run['run']['id']+'/repair-loops')
    def capture_body(run):
        l=repair_library(run);return dict(requestId=str(uuid.uuid4()),expectedRevision=l['runRevision'],expectedContext=l['context']['fingerprint'],owner='implementation',findingIds=[fid],criterionIds=[])
    def capture(run):return call('/api/factory/runs/'+run['run']['id']+'/repair-loops','POST',capture_body(run))
    def loop_read(loop):return call('/api/factory/repair-loops/'+loop['loop']['id'])
    def advance_body(loop):return dict(requestId=str(uuid.uuid4()),expectedRevision=loop['loop']['revision'])
    def advance(loop,body=None):return call('/api/factory/repair-loops/'+loop['loop']['id']+'/advance','POST',body or advance_body(loop))
    def stop(loop):
        loop=loop_read(loop);return call('/api/factory/repair-loops/'+loop['loop']['id']+'/stop','POST',dict(requestId=str(uuid.uuid4()),expectedRevision=loop['loop']['revision'],reason='Explicit fixture stop'))
    def wait_loop(loop,states):
        for _ in range(400):
            current=loop_read(loop)
            if current['loop']['state'] in states:return current
            time.sleep(.1)
        raise AssertionError(current)
    def child(loop):return call('/api/factory/runs/'+loop['attempts'][-1]['run']['id'])
    def finish(loop,fixed):
        run=child(loop);qa_pass(run);r=assess(begin(run))
        proof=evidence(run)|dict(line=3,assertion='Negative one increments to zero.',failureCase='Unchanged negative branch fails this assertion.')
        submit(r,submit_body(r,'approve' if fixed else 'requestChanges',rechecks=[recheck('fixed' if fixed else 'unfixed','Actual negative assertion and corrected implementation.' if fixed else 'The negative branch remains unfixed.',[dict(path='counter.py',line=2)] if fixed else [],[proof] if fixed else [])]))
        return wait_loop(loop,{'completed'} if fixed else {'needsDecision','stopped'})
    try:
        start();pid=call('/api/projects','POST',dict(name='Bounded repair regression',description='Isolated test only',color='#477fa1'))['projects'][0]['id']
        call(f'/api/agents/projects/{pid}/connection','PUT',dict(expectedRevision=revision(),repositoryPath=str(repo),baseRef='HEAD',provider='codex',instructions='Explicit protocol fixture.'))
        if '--execution-policy' in sys.argv:
            execution_policy=dict(enabled=True,routes=[dict(role=role,provider='codex',model='fixture-repair-model',effort='high') for role in ['implementation','review','verification']],maxAgentInvocations=2,maxCommandInvocations=1,maxElapsedSeconds=120,allowUnmeteredCost=True)
            call(f'/api/factory/projects/{pid}/execution-policy','PUT',dict(expectedRevision=revision(),policy=execution_policy))
        definition={'stages':[dict(id='build',name='Build',group='Build',dependsOn=[],steps=[dict(id='implement',name='Implementation',kind='agent',role='implementation',permission='workspace-write',instructions='Implement the fixture.',produces=['changes'])]),dict(id='qa',name='QA',group='Check',dependsOn=['build'],steps=[dict(id='verify',name='Actual assertions',kind='command',command=dict(executable=sys.executable,arguments=['verify.py'],workingDirectory='.',timeoutSeconds=10),consumes=['changes'],produces=['verification'])]),dict(id='review',name='Independent reviewer',group='Check',dependsOn=['qa'],steps=[dict(id='review',name='Read-only reviewer',kind='agent',role='review',permission='read-only',instructions='Review code independently.',consumes=['changes','verification'])])]}
        version=publish(definition,'Repair fixture');run=source()
        check(repair_library(run)['policy']['policy'] is None,'no repair policy or limits are fabricated for a new project')
        adopt();body=capture_body(run);path='/api/factory/runs/'+run['run']['id']+'/repair-loops'
        call(path,'POST',body,403,{'Origin':'https://example.invalid'})
        call(path,'POST',body|{'ignoredField':True},400)
        call(path,'POST',body|{'owner':'verification'},409)
        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:prepared=list(pool.map(lambda _:call(path,'POST',body),range(2)))
        loop=prepared[0];check(prepared[1]['loop']['id']==loop['loop']['id'] and not loop['attempts'],'concurrent preparation retries capture exactly one nonexecuting scope')
        call(path,'POST',body|{'criterionIds':[cid]},409)
        call(path,'POST',capture_body(run),409)
        check(True,'request-ID conflicts, invalid ownership and multiple open loops are rejected')
        call(f'/api/factory/tasks/{tid}/runs','POST',dict(requestId=str(uuid.uuid4()),expectedRevision=revision()),400)
        call(f'/api/agents/tasks/{tid}/runs','POST',dict(requestId=str(uuid.uuid4()),expectedRevision=revision(),provider='codex',purpose='implementation',instructions='bypass'),409)
        call('/api/factory/runs/'+run['run']['id']+'/reviews','POST',begin_body(run),409)
        check(True,'open loop owns execution and review of its task; alternate dispatch cannot bypass budgets')
        parent=Path(run['run']['workspacePath']);parent_hash=call('/api/factory/runs/'+run['run']['id']+'/qa')['context'];parent_index=subprocess.check_output(['git','-C',str(parent),'ls-files','--stage','-z'])
        adopt(maxAttempts=1,maxAgentInvocations=2)
        check(loop_read(loop)['loop']['policy']['maxAttempts']==3,'later project policy edits never replace captured loop limits')
        body=advance_body(loop);path='/api/factory/repair-loops/'+loop['loop']['id']+'/advance'
        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:advanced=list(pool.map(lambda _:call(path,'POST',body),range(2)))
        check(len(advanced[0]['attempts'])==len(advanced[1]['attempts'])==1,'concurrent advance retries queue one child and reserve its agents once')
        loop=wait_loop(loop,{'awaitingQa','stopped'});assert loop['loop']['state']=='awaitingQa',loop
        repaired=child(loop);workspace=Path(repaired['run']['workspacePath'])
        check(workspace!=parent and (workspace/'staged.txt').read_bytes()==(parent/'staged.txt').read_bytes() and subprocess.check_output(['git','-C',str(workspace),'ls-files','--stage','-z'])==parent_index and (workspace/'binary.dat').read_bytes()==bytes(range(256)) and not (workspace/'obsolete.txt').exists() and (workspace/'tool.sh').stat().st_mode&0o111 and not (workspace/'__pycache__/ignored').exists(),'repair forks staged/index, unstaged, binary, untracked, deletion and executable state while excluding ignored output')
        check(call('/api/factory/runs/'+run['run']['id']+'/qa')['context']==parent_hash and (parent/'counter.py').read_text().endswith('else value\n'),'parent code, index and evidence remain unchanged after repair')
        lineage=repaired['run']['snapshot']['repair'];check(lineage['parentRunId']==run['run']['id'] and lineage['feedback']['remaining'][0]['id']==fid and loop['loop']['reservedAgentInvocations']==2,'child records durable parent, original finding ID, role scope and reserved invocation budget')
        check(not library(repaired)['rounds'][0]['runId']==repaired['run']['id'] and call('/api/factory/runs/'+repaired['run']['id']+'/qa')['criteria'][0]['status']=='unverified','parent QA and review are never inherited as current child approval')
        loop=finish(loop,True);check(loop['loop']['state']=='completed' and library(repaired)['findings'][0]['finding']['id']==fid,'current asserting QA plus independent recheck resolves the same original finding and completes the loop')
        check(call('/api/workspace')['items'][0]['statusId']==task['statusId'],'completed repair does not accept the task or change workflow status')
        snapshot=loop_read(loop);process.terminate();process.wait(timeout=25);start();check(loop_read(loop)==snapshot,'completed policy, attempt lineage and feedback persist across native DuckDB restart')
        if '--execution-policy' in sys.argv:
            check(repaired['run']['snapshot']['execution']['policy']['maxAgentInvocations']==2 and repaired['budget']['reservedAgentInvocations']==2,'repair plans capture project routes and retain per-work-order invocation reservations')
            check(all('--model' in row['args'] and row['args'][row['args'].index('--model')+1]=='fixture-repair-model' for row in [json.loads(line) for line in trace.read_text().splitlines()]),'repair and source runners dispatch the same captured role model')
            run=source();adopt();loop=capture(run);count=len(trace.read_text().splitlines())
            call(f'/api/factory/projects/{pid}/execution-policy','PUT',dict(expectedRevision=revision(),policy=execution_policy|dict(maxCommandInvocations=2)))
            call('/api/factory/repair-loops/'+loop['loop']['id']+'/advance','POST',advance_body(loop),409)
            check(not loop_read(loop)['attempts'] and len(trace.read_text().splitlines())==count,'execution-policy drift cannot dispatch an old captured repair plan')
            stop(loop)
            call(f'/api/projects/{pid}?expectedRevision={revision()}','DELETE')
            print(f'{checks} execution-policy repair checks passed')
            sys.exit(0)
        if '--serve' in sys.argv:
            mode.write_text('good');run=source();adopt();loop=capture(run)
            print(json.dumps(dict(baseUrl=base,runId=run['run']['id'],projectId=pid,itemId=tid,criterionId=cid,findingId=fid,loopId=loop['loop']['id'],scratch=str(scratch))),flush=True)
            while True:time.sleep(1)
        # Repeat failure stops at attempts even when further dispatch is explicitly requested.
        mode.write_text('unchanged');run=source();adopt(maxAttempts=1,maxAgentInvocations=2,maxNoProgressRounds=5);loop=capture(run);advance(loop);loop=wait_loop(loop,{'awaitingQa'});loop=finish(loop,False)
        check(loop['loop']['state']=='stopped' and 'attempt' in loop['loop']['reason'] and len(loop['attempts'])==1,'maximum attempts halt repeated failure after real review')
        call('/api/factory/repair-loops/'+loop['loop']['id']+'/advance','POST',advance_body(loop),409)
        run=source();adopt(maxNoProgressRounds=1);loop=capture(run);advance(loop);loop=wait_loop(loop,{'awaitingQa'});loop=finish(loop,False)
        check(loop['loop']['state']=='stopped' and 'no progress' in loop['loop']['reason'],'no-progress ceiling uses unresolved selected issues, not successful agent reports')
        run=source();adopt(maxAgentInvocations=1);loop=capture(run);check(not loop['canAdvance'] and 'invocation' in loop['blockedReason'],'agent-call reservation blocks an unaffordable plan before any agent starts');stop(loop)
        run=source();adopt(maxCost=1,currency='USD');loop=capture(run);call('/api/factory/repair-loops/'+loop['loop']['id']+'/advance','POST',advance_body(loop),409)
        check(not loop_read(loop)['attempts'] and 'unavailable' in loop['blockedReason'],'unknown monetary cost blocks a configured currency cap without inventing zero usage');stop(loop)
        run=source();adopt(allowUnmeteredCost=False);loop=capture(run);check(not loop['canAdvance'] and 'unmetered' in loop['blockedReason'],'missing explicit unmetered-cost consent blocks execution');stop(loop)
        # Current review feedback remains explicit; auto continuation queues only after that feedback.
        run=source();adopt(autoContinue=True,maxAttempts=2,maxNoProgressRounds=5);loop=capture(run);advance(loop);loop=wait_loop(loop,{'awaitingQa'});time.sleep(.6)
        check(len(loop_read(loop)['attempts'])==1,'automatic continuation cannot skip fresh human QA and review')
        repaired=child(loop);qa_pass(repaired);r=assess(begin(repaired));submit(r,submit_body(r,'requestChanges',rechecks=[recheck('unfixed')]))
        for _ in range(100):
            loop=loop_read(loop)
            if len(loop['attempts'])==2 and loop['loop']['state']=='awaitingQa':break
            time.sleep(.1)
        check(len(loop['attempts'])==2 and loop['loop']['state']=='awaitingQa','explicit fresh request-changes feedback can autoqueue the next bounded attempt')
        loop=stop(loop);check(wait_loop(loop,{'stopped'})['loop']['state']=='stopped','operator stop ends an awaiting-feedback loop without replay')
        mode.write_text('commandFail');run=source();adopt(autoContinue=True,maxAttempts=2,maxNoProgressRounds=5);loop=capture(run);advance(loop);loop=wait_loop(loop,{'stopped'})
        check(len(loop['attempts'])==2 and all(x['attempt']['feedback']['kind']=='commandFailed' for x in loop['attempts']) and all(x['run']['state']=='failed' for x in loop['attempts']),'actual negative-case command failures drive bounded automatic repair and stop at the attempt ceiling')
        mode.write_text('unchanged');run=source(with_finding=False)
        q=call('/api/factory/runs/'+run['run']['id']+'/qa');call('/api/factory/runs/'+run['run']['id']+'/qa','POST',dict(requestId=str(uuid.uuid4()),expectedRevision=q['runRevision'],expectedContext=q['context']['fingerprint'],criterionId=cid,verdict='fail',reason='Verification omits the negative-input assertion.',evidence=[evidence(run)]))
        verification_definition=copy.deepcopy(definition);verification_definition['stages'][0]['steps'][0]['role']='verification'
        verification_version=publish(verification_definition,'Verification repair fixture');adopt(implementationPlanVersionId=None,verificationPlanVersionId=verification_version['id'],maxAttempts=1)
        body=capture_body(run)|dict(owner='verification',findingIds=[],criterionIds=[cid]);loop=call('/api/factory/runs/'+run['run']['id']+'/repair-loops','POST',body);advance(loop);loop=wait_loop(loop,{'stopped'});r=child(loop)
        check(r['run']['snapshot']['repair']['feedback']['remaining'][0]['owner']=='verification' and r['run']['snapshot']['environment']['definition']['stages'][0]['steps'][0]['role']=='verification' and (Path(r['run']['workspacePath'])/'counter.py').read_bytes()==(Path(run['run']['workspacePath'])/'counter.py').read_bytes(),'QA-only failure requires explicit verification ownership and dispatches its captured test-repair plan')
        mode.write_text('unchanged');run=source();adopt();loop=capture(run);advance(loop);loop=wait_loop(loop,{'awaitingQa'});r=child(loop);qa_pass(r);review=assess(begin(r))
        new_finding=dict(id=str(uuid.uuid4()),title='A newly discovered planning gap',description='A broader behavior needs human scope clarification.',category='missingBehavior',severity='medium',owner='planning',criterionIds=[cid],codeReferences=[],evidence=[])
        submit(review,submit_body(review,'requestChanges',rechecks=[recheck('unfixed')],new=[new_finding]))
        check('New or unselected' in wait_loop(loop,{'stopped'})['loop']['reason'],'new blocking findings stop repair for a human scope decision')
        # Scope changes require a new human decision, including during manual waiting time.
        run=source();adopt();loop=capture(run);advance(loop);loop=wait_loop(loop,{'awaitingQa'})
        call(f'/api/factory/tasks/{tid}/criteria','PUT',dict(expectedRevision=revision(),criteria=[dict(id=cid,description='Changed criterion',expectedResult='New expected behavior',verificationMethod='New method')]))
        check('changed' in wait_loop(loop,{'stopped'})['loop']['reason'],'changed task acceptance scope halts the loop instead of repairing outside captured scope')
        # Cancel a real child process, not only its database status.
        run=source();mode.write_text('slow');adopt();loop=capture(run);advance(loop)
        for _ in range(200):
            loop=loop_read(loop);r=child(loop);work=Path(r['run']['workspacePath']) if r['run']['workspacePath'] else None
            if work and (work/'active.pid').exists():break
            time.sleep(.1)
        child_pid=int((work/'active.pid').read_text());loop=stop(loop);loop=wait_loop(loop,{'stopped'})
        try:os.kill(child_pid,0);running=True
        except ProcessLookupError:running=False
        check(not running and child(loop)['run']['state']=='cancelled','stop waits for the actual child process to die and retains a cancelled attempt')
        mode.write_text('unchanged');run=source();adopt();loop=capture(run);advance(loop);loop=wait_loop(loop,{'awaitingQa'});count=len(trace.read_text().splitlines());process.terminate();process.wait(timeout=25);start()
        check(loop_read(loop)['loop']['state']=='stopped' and len(trace.read_text().splitlines())==count,'restart stops an unfinished QA-waiting loop without redispatch')
        run=source();adopt();loop=capture(run);process.terminate();process.wait(timeout=25);start();check(loop_read(loop)['loop']['state']=='stopped' and not loop_read(loop)['attempts'],'restart also stops prepared loops without implicit execution')
        # Real elapsed ceiling covers manual feedback waits. No database time manipulation.
        run=source();adopt(maxElapsedMinutes=1);loop=capture(run);advance(loop);loop=wait_loop(loop,{'awaitingQa'});deadline=time.time()+70
        while time.time()<deadline:
            loop=loop_read(loop)
            if loop['loop']['state']=='stopped':break
            time.sleep(.25)
        check(loop['loop']['state']=='stopped' and 'elapsed' in loop['loop']['reason'],'real elapsed budget includes waiting for criterion QA and stops without a new attempt')
        call(f'/api/projects/{pid}?expectedRevision={revision()}','DELETE')
        check(not call('/api/workspace')['projects'],'test-only project and native histories clean up through public deletion')
        print(f'{checks} bounded repair checks passed')
    finally:
        if process and process.poll() is None:process.terminate();process.wait(timeout=25)
        log.close()
