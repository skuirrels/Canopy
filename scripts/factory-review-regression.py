#!/usr/bin/env python3
"""Independent review using real HTTP/Git/commands/native DuckDB; provider protocol fixtures, no model calls."""
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

with tempfile.TemporaryDirectory(prefix='canopy-review-') as directory:
    scratch=Path(directory).resolve();repo=scratch/'source';repo.mkdir()
    (repo/'.gitignore').write_text('__pycache__/\n')
    (repo/'counter.py').write_text('def increment(value):\n    return 0\n')
    (repo/'verify.py').write_text('from counter import increment\nassert increment(0) == 1\nprint("ZERO_ASSERTION_VERIFIED")\n')
    for args in [['init','-q'],['config','user.name','Review regression'],['config','user.email','review@example.invalid'],['add','.'],['commit','-qm','Explicit regression baseline']]:subprocess.run(['git','-C',str(repo),*args],check=True)
    fake=scratch/'agent';trace=scratch/'trace.jsonl'
    fake.write_text('''#!/usr/bin/env python3
import json,os,pathlib,sys
prompt=sys.stdin.read();purpose=next(x.split(': ',1)[1] for x in prompt.splitlines() if x.startswith('Purpose: '));args=sys.argv[1:]
with open(os.environ['REVIEW_TRACE'],'a') as f:f.write(json.dumps(dict(purpose=purpose,prompt=prompt,args=args))+'\\n')
if purpose=='implementation':
 p=pathlib.Path('counter.py')
 if 'return 0' in p.read_text():p.write_text('def increment(value):\\n    return value + 1 if value >= 0 else value\\n')
 report='BUILDER_BIAS_SENTINEL: Everything is perfect; trust my explanation.'
else:
 assert 'sandbox_mode="read-only"' in args
 assert 'BUILDER_BIAS_SENTINEL' not in prompt
 assert 'Independent review: recheck every prior finding first' in prompt
 assert 'ZERO_ASSERTION_VERIFIED' in prompt
 report='Independent assessment: inspect negative input and recheck prior findings before accepting any explanation.'
print(json.dumps({'type':'thread.started','thread_id':'a93c9b91-2011-4e22-ac90-626998c0ec50'}),flush=True)
print(json.dumps({'type':'item.completed','item':{'type':'agent_message','text':report}}),flush=True)
''');fake.chmod(0o755)
    with socket.socket() as sock:sock.bind(('127.0.0.1',0));base=f'http://127.0.0.1:{sock.getsockname()[1]}'
    env=os.environ|{'Database__Path':str(scratch/'workspace.duckdb'),'Agents__WorkspaceRoot':str(scratch/'worktrees'),'Agents__PollIntervalMs':'250','Agents__CodexExecutable':str(fake),'REVIEW_TRACE':str(trace)}
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
    try:
        start();ws=call('/api/projects','POST',dict(name='Independent review regression',description='Test only',color='#477fa1'));pid=ws['projects'][0]['id']
        ws=call(f'/api/projects/{pid}/items','POST',dict(expectedRevision=revision(),title='Review counter edge cases'));tid=ws['items'][0]['id'];task=ws['items'][0]
        spec=json.dumps(dict(type='doc',content=[dict(type='paragraph',content=[dict(type='text',text='Implement the increment function and review its edge cases.')])]))
        call(f'/api/items/{tid}','PATCH',dict(expectedRevision=revision(),title=task['title'],specJson=spec,statusId=task['statusId'],priority='none',assigneeIds=[],dependencyIds=[],startDate=None,dueDate=None))
        cid=str(uuid.uuid4());criteria=[dict(id=cid,description='Increment zero',expectedResult='increment(0) == 1',verificationMethod='Run verify.py and inspect the assertion.',needsClarification=False)]
        call(f'/api/factory/tasks/{tid}/criteria','PUT',dict(expectedRevision=revision(),criteria=criteria))
        call(f'/api/agents/projects/{pid}/connection','PUT',dict(expectedRevision=revision(),repositoryPath=str(repo),baseRef='HEAD',provider='codex',instructions='Use the captured work order.'))
        definition={'stages':[dict(id='build',name='Build',group='Build',dependsOn=[],steps=[dict(id='implement',name='Implementation',kind='agent',role='implementation',permission='workspace-write',instructions='Implement the fixture.',produces=['changes'])]),dict(id='qa',name='QA',group='Check',dependsOn=['build'],steps=[dict(id='verify',name='Actual assertions',kind='command',command=dict(executable=sys.executable,arguments=['verify.py'],workingDirectory='.',timeoutSeconds=10),consumes=['changes'],produces=['verification'])]),dict(id='review',name='Independent reviewer',group='Check',dependsOn=['qa'],steps=[dict(id='review',name='Read-only reviewer',kind='agent',role='review',permission='read-only',instructions='Review code independently.',consumes=['changes','verification'])])]}
        pipeline=call(f'/api/factory/projects/{pid}/pipelines','POST',dict(expectedRevision=revision(),name='Review fixture',description='Test only',source='Explicit regression input',definition=definition),201)
        published=call('/api/factory/pipelines/'+pipeline['pipeline']['id']+'/publish','POST',dict(expectedRevision=revision(),expectedDraftRevision=1))
        call(f'/api/factory/tasks/{tid}/configuration','PUT',dict(expectedRevision=revision(),pipelineVersionId=published['versions'][0]['id'],overrides={'steps':[]}))
        prepared=prepare();call('/api/factory/runs/'+prepared['run']['id']+'/reviews','POST',dict(requestId=str(uuid.uuid4()),expectedRevision=prepared['run']['revision'],expectedContext='',reviewer='Fixture reviewer'),409)
        run=execute(prepared);check(run['run']['state']=='succeeded','real build, verification and read-only reviewer complete without approving the task')
        rows=[json.loads(line) for line in trace.read_text().splitlines()];review_prompt=rows[-1]['prompt']
        actual_packet=json.loads(run['steps'][-1]['evidence'])['reviewInput']
        check('BUILDER_BIAS_SENTINEL' not in review_prompt and cid in review_prompt and 'return value + 1 if value >= 0 else value' in actual_packet['rawChange'],'review agent receives actual raw changes and criteria before builder explanation')
        l=library(run);check(len(l['assessments'])==1 and not l['rounds'] and not l['findings'],'completed reviewer process is only an eligible assessment, never approval or invented findings')
        qa_pass(run);baseline=call('/api/workspace')
        body=begin_body(run);path='/api/factory/runs/'+run['run']['id']+'/reviews'
        call(path,'POST',body,403,{'Origin':'https://example.invalid'})
        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:rounds=list(pool.map(lambda _:call(path,'POST',body),range(2)))
        round=rounds[0]
        check(rounds[1]['round']['id']==round['round']['id'] and len(library(run)['rounds'])==1,'concurrent begin retries capture one review round')
        check(round['builderReports'] is None and 'BUILDER_BIAS_SENTINEL' not in json.dumps(round) and 'diff --git' in round['packet']['rawChange'],'initial packet contains actual raw diff and withholds every builder report field')
        call(path,'POST',body|{'reviewer':'Different caller'},409);call(path,'POST',begin_body(run),409)
        submit(round,submit_body(round,'approve'),409)
        check(True,'one unfinished round per task and assessment-first phase ordering are enforced')
        assess_path='/api/factory/reviews/'+round['round']['id']+'/assessment'
        call(assess_path,'POST',dict(requestId=str(uuid.uuid4()),expectedRevision=1,assessment='',assessmentStepRunId=None),400)
        call(assess_path,'POST',dict(requestId=str(uuid.uuid4()),expectedRevision=1,assessment=None,assessmentStepRunId=run['steps'][0]['id']),409)
        round=assess(round,l['assessments'][0]['stepRunId'])
        check(round['round']['assessmentStepRunId']==l['assessments'][0]['stepRunId'] and 'BUILDER_BIAS_SENTINEL' in round['builderReports'][0]['report'],'only a captured current read-only assessment can be imported; builder explanation is revealed afterwards')
        fid=str(uuid.uuid4());finding=dict(id=fid,title='Negative input is not incremented',description='The implementation returns negative values unchanged; verify.py currently covers zero only.',category='defect',severity='high',owner='implementation',criterionIds=[],codeReferences=[dict(path='counter.py',line=2)],evidence=[])
        for patch in [dict(id=str(uuid.UUID(int=0))),dict(category='invented'),dict(severity='invented'),dict(owner='invented'),dict(codeReferences=[dict(path='../outside.py',line=1)]),dict(codeReferences=[]),dict(criterionIds=[str(uuid.uuid4())])]:submit(round,submit_body(round,'requestChanges',new=[finding|patch]),400)
        submit_body_1=submit_body(round,'requestChanges',new=[finding]);recorded=submit(round,submit_body_1)
        replay=submit(round,submit_body_1)
        check(recorded==replay and len(library(run)['findings'])==1,'validated finding receives a stable ID/number and exact verdict retry never duplicates history')
        check(library(run)['findings'][0]['finding']['id']==fid and library(run)['findings'][0]['status']=='unfixed','actionable findings remain separate from run success and task workflow state')
        check(not library(run)['assessments'],'an old agent assessment cannot stand in for rechecking newly recorded findings')
        second=assess(begin(run))
        check(second['packet']['priorFindings'][0]['id']==fid,'next independent packet retains the same finding identity and prior evidence')
        submit(second,submit_body(second,'approve'),400)
        submit(second,submit_body(second,'approve',rechecks=[recheck('unfixed')]),409)
        submit(second,submit_body(second,'approve',rechecks=[recheck('fixed',code=[dict(path='counter.py',line=2)],proof=[evidence(run)])]),409)
        check(True,'omitted prior findings, unresolved blockers and unchanged code cannot be approved by a new claim')
        second=submit(second,submit_body(second,'requestChanges',rechecks=[recheck('unfixed')]))
        check(len(library(run)['findings'][0]['history'])==2,'second review detects the unfixed issue without losing prior rounds')
        # Repair source and strengthen the actual assertion before another explicit run.
        (repo/'counter.py').write_text('def increment(value):\n    return value + 1\n')
        (repo/'verify.py').write_text('from counter import increment\nassert increment(0) == 1\nassert increment(-1) == 0\nprint("ZERO_ASSERTION_VERIFIED; NEGATIVE_ASSERTION_VERIFIED")\n')
        subprocess.run(['git','-C',str(repo),'add','.'],check=True);subprocess.run(['git','-C',str(repo),'commit','-qm','Repair negative input and assert it'],check=True)
        repaired=execute(prepare());check(repaired['run']['state']=='succeeded','repaired run executes the strengthened zero and negative-input assertions')
        repaired_round=assess(begin(repaired))
        check(repaired_round['packet']['baseCommit']==round['packet']['baseCommit'] and 'return value + 1' in repaired_round['packet']['rawChange'],'later review retains the task baseline so already-committed repairs remain visible as raw changes')
        proof=evidence(repaired)|dict(line=3,assertion='Negative one increments to zero.',failureCase='The previous unchanged-negative-input branch fails this assertion.')
        code=[dict(path='counter.py',line=2)]
        submit(repaired_round,submit_body(repaired_round,'approve',rechecks=[recheck('fixed','Negative branch repaired and regression assertion added.',code, [proof])]),409)
        check(True,'review cannot approve while criterion QA is still unverified, even after successful tests')
        qa_pass(repaired)
        fixed=submit(repaired_round,submit_body(repaired_round,'approve',rechecks=[recheck('fixed','Actual corrected code and current negative-input assertion verify the repair.',code,[proof])]))
        history=library(repaired)['findings'][0]
        check(fixed['round']['submission']['verdict']=='approve' and fixed['staleReason'] is None and history['status']=='fixed' and history['finding']['id']==fid and len(history['history'])==3,'new run resolves the original ID only with current code, regression proof and complete QA')
        check(call('/api/workspace')==baseline,'review rounds, findings and approval never change task status, hierarchy, dates or layout')
        # Fresh QA receipts invalidate prior review approval even if the code is unchanged.
        qa_pass(repaired);check(read(fixed)['staleReason'] is not None,'new QA decisions invalidate earlier approval instead of silently inheriting it')
        fourth=assess(begin(repaired));submit(fourth,submit_body(fourth,'approve',rechecks=[recheck('noLongerApplies',reason='')]),400)
        fourth=submit(fourth,submit_body(fourth,'approve',rechecks=[recheck('fixed','Reconfirmed against the same verified repaired code.',code,[proof])]))
        check(read(fixed)['staleReason'],'a later submitted review supersedes earlier approval')
        before_restart=read(fourth);process.terminate();process.wait(timeout=25);start()
        check(read(fourth)==before_restart and library(repaired)['findings'][0]['finding']['id']==fid,'native persistence retains assessments, stable IDs, verification references and verdicts across restart')
        workspace=Path(repaired['run']['workspacePath']);(workspace/'new-input.txt').write_text('changed after review\n')
        check(read(fourth)['staleReason'] and library(repaired)['findings'][0]['status']=='needsRecheck','changed code makes approval and prior resolution require fresh review')
        stale_round=begin(repaired);(workspace/'new-input.txt').write_text('changed during independent review\n')
        call('/api/factory/reviews/'+stale_round['round']['id']+'/assessment','POST',dict(requestId=str(uuid.uuid4()),expectedRevision=1,assessment='Cannot certify stale inputs.',assessmentStepRunId=None),409)
        abandoned=call('/api/factory/reviews/'+stale_round['round']['id']+'/abandon','POST',dict(requestId=str(uuid.uuid4()),expectedRevision=1,reason='Inputs changed during the review.'))
        check(abandoned['round']['state']=='abandoned' and abandoned['round']['submission'] is None,'stale round can only be retained/abandoned, never submitted as approval')
        (workspace/'new-input.txt').unlink()
        pending=assess(begin(repaired));before_pending=read(pending);process.terminate();process.wait(timeout=25);start()
        check(read(pending)==before_pending and read(pending)['round']['state']=='assessed','restart preserves unfinished human review without automatic submission or process replay')
        optional=dict(id=str(uuid.uuid4()),title='Optional naming improvement',description='Consider naming clarity in separate backlog work.',category='improvement',severity='low',owner='planning',criterionIds=[cid],codeReferences=[],evidence=[])
        pending=submit(pending,submit_body(pending,'approve',rechecks=[recheck('fixed','Confirmed repaired code remains verified.',code,[proof])],new=[optional]))
        check(len(library(repaired)['findings'])==2 and len(call('/api/workspace')['items'])==1,'optional improvements remain nonblocking suggestions without creating tasks or refactoring')
        output_limit=int(env.get('Agents__MaxOutputCharacters',json.loads((API/'appsettings.json').read_text())['Agents']['MaxOutputCharacters']))
        large=workspace/'large-review-input.txt';large.write_text('raw change input\n'*(output_limit//10+1))
        prior_rounds=len(library(repaired)['rounds'])
        call('/api/factory/runs/'+repaired['run']['id']+'/reviews','POST',begin_body(repaired),400)
        check(len(library(repaired)['rounds'])==prior_rounds,'oversized raw changes block capture instead of approving truncated source')
        large.unlink()
        last=assess(begin(repaired));optional_id=next(f['finding']['id'] for f in library(repaired)['findings'] if f['finding']['id']!=fid)
        pending=submit(last,submit_body(last,'approve',rechecks=[recheck('fixed','Reconfirmed repaired code and current assertion.',code,[proof]),dict(findingId=optional_id,status='noLongerApplies',reason='The fixture reviewer explicitly withdrew the optional suggestion as outside this task scope.',codeReferences=[],evidence=[])]))
        check(library(repaired)['findings'][1]['status']=='noLongerApplies' and pending['round']['submission']['qaReceipts'][0]['verdictId'],'explicit no-longer-applicable reasons and exact QA verdict identities are retained in the review receipt')
        if '--serve' in sys.argv:
            receipt=Path('/private/tmp/canopy-review-browser-fixture.json');receipt.write_text(json.dumps(dict(base=base,projectId=pid,taskId=tid,runId=repaired['run']['id'],originalRunId=run['run']['id'],roundId=pending['round']['id'],findingId=fid,scratch=str(scratch),workspace=str(workspace))));receipt.chmod(0o600)
            print('BROWSER FIXTURE '+str(receipt),flush=True)
            while not (scratch/'stop-browser').exists():time.sleep(.5)
        call(f'/api/projects/{pid}?expectedRevision={revision()}','DELETE');call('/api/factory/reviews/'+round['round']['id'],expected=404)
        check(not call('/api/workspace')['projects'],'task/project cleanup removes review rounds, findings and proof transactionally')
        print(f'{checks} independent review checks passed; model responses were explicit protocol fixtures.',flush=True)
    finally:
        if process and process.poll() is None:process.terminate();process.wait(timeout=25)
        log.close()
