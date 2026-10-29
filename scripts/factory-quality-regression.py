#!/usr/bin/env python3
"""Criterion QA against actual Git, HTTP, verification commands and native DuckDB; isolated data only."""
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

ROOT = Path(__file__).resolve().parents[1]
API = ROOT / 'server/Planner.Api'
checks = 0


def check(condition, message):
    global checks
    assert condition, message
    checks += 1
    print(f'PASS {checks:02}: {message}', flush=True)


with tempfile.TemporaryDirectory(prefix='canopy-criterion-qa-') as directory:
    scratch = Path(directory).resolve(); repo = scratch / 'source'; repo.mkdir()
    (repo / '.gitignore').write_text('ignored-test.py\n__pycache__/\n')
    (repo / 'counter.py').write_text('def increment(value):\n    return value + 1\n')
    (repo / 'verify.py').write_text('from counter import increment\nassert increment(0) == 1\nassert increment(-1) == 0\nprint("Assertions passed; zero and negative inputs verified")\n')
    for args in [['init', '-q'], ['config', 'user.name', 'Canopy QA regression'], ['config', 'user.email', 'qa@example.invalid'], ['add', '.'], ['commit', '-qm', 'Explicit test source']]:
        subprocess.run(['git', '-C', str(repo), *args], check=True)
    with socket.socket() as sock:
        sock.bind(('127.0.0.1', 0)); base = f'http://127.0.0.1:{sock.getsockname()[1]}'
    env = os.environ | {'Database__Path': str(scratch / 'workspace.duckdb'), 'Agents__WorkspaceRoot': str(scratch / 'worktrees'), 'Agents__PollIntervalMs': '250', 'QA_CHECK_INPUT': 'explicit-first-environment'}
    process = None; log = (scratch / 'server.log').open('w+')

    def call(path, method='GET', body=None, expected=200, headers=None):
        request = urllib.request.Request(base + path, data=None if body is None else json.dumps(body).encode(), headers={'Content-Type': 'application/json'} | (headers or {}), method=method)
        try:
            with urllib.request.urlopen(request, timeout=30) as response: code, raw = response.status, response.read()
        except urllib.error.HTTPError as error: code, raw = error.code, error.read()
        try: value = json.loads(raw) if raw else None
        except json.JSONDecodeError: raise AssertionError((path, code, raw.decode()[:3000]))
        assert code == expected, (path, code, expected, value)
        return value

    def start():
        global process
        process = subprocess.Popen(['dotnet', 'bin/Debug/net10.0/Planner.Api.dll', '--urls', base], cwd=API, env=env, stdout=log, stderr=log)
        for _ in range(180):
            if process.poll() is not None:
                log.seek(0); raise AssertionError(log.read())
            try: call('/api/health'); return
            except OSError: time.sleep(.1)
        raise AssertionError('Backend did not start')

    def revision(): return next(p['revision'] for p in call('/api/workspace')['projects'] if p['id'] == pid)
    def criteria(values): return call(f'/api/factory/tasks/{tid}/criteria', 'PUT', dict(expectedRevision=revision(), criteria=values))
    def prepare(): return call(f'/api/factory/tasks/{tid}/runs', 'POST', dict(requestId=str(uuid.uuid4()), expectedRevision=revision()), 201)
    def execute(run):
        call(f"/api/factory/runs/{run['run']['id']}/start", 'POST', dict(requestId=str(uuid.uuid4()), expectedRevision=run['run']['revision']))
        for _ in range(200):
            result = call('/api/factory/runs/' + run['run']['id'])
            if not result['run']['active']: return result
            time.sleep(.1)
        raise AssertionError('Run did not settle')
    def quality(run): return call('/api/factory/runs/' + run['run']['id'] + '/qa')
    def body(run, criterion, verdict='pass', **kwargs):
        q = quality(run)
        return dict(requestId=str(uuid.uuid4()), expectedRevision=q['runRevision'], expectedContext=q['context']['fingerprint'] if q['context'] else None, criterionId=criterion['id'], verdict=verdict,
            reason='Explicit operator review of the zero and negative assertions.', evidence=[dict(stepRunId=run['steps'][0]['id'], testPath='./verify.py', line=2, assertion='increment(0) is exactly one', failureCase='Returning unchanged input fails this assertion')] if verdict in ['pass', 'fail'] else []) | kwargs
    def record(run, payload, expected=200): return call('/api/factory/runs/' + run['run']['id'] + '/qa', 'POST', payload, expected)

    try:
        start()
        state = call('/api/projects', 'POST', dict(name='Acceptance QA regression', description='Test only', color='#477fa1')); pid = state['projects'][0]['id']
        state = call(f'/api/projects/{pid}/items', 'POST', dict(expectedRevision=revision(), title='Counter acceptance QA')); tid = state['items'][0]['id']
        path = f'/api/factory/tasks/{tid}/criteria'
        check(any(x['code']=='criteria.missing' for x in call(path)['checks']), 'missing criteria remain explicit; no synthetic defaults')
        ids = [str(uuid.uuid4()) for _ in range(5)]
        inputs = [dict(id=ids[i], description=description, expectedResult='increment(0) == 1', verificationMethod='Run verify.py and inspect assertion line 2', needsClarification=False) for i, description in enumerate(['Increment zero', 'Negative input review', 'Uncovered requirement', 'Platform-specific requirement', 'Needs clarification'])]
        inputs[-1]['needsClarification'] = True
        before = revision(); adopted = criteria(inputs)
        check(adopted['criteria']==inputs and adopted['projectRevision']==before+1, 'adoption preserves caller-supplied stable criterion IDs and increments project revision')
        check(any(x['code']=='criteria.incomplete' for x in adopted['checks']), 'explicitly vague or unmeasurable criteria are flagged before work')
        call(path, 'PUT', dict(expectedRevision=before, criteria=inputs), 409)
        call(path, 'PUT', dict(expectedRevision=revision(), criteria=[inputs[0],inputs[0]]), 400)
        call(path, 'PUT', dict(expectedRevision=revision(), criteria=[inputs[0]|{'id':str(uuid.UUID(int=0))}]),400)
        call(path, 'PUT', dict(expectedRevision=revision(), criteria=None),400)
        call(path, 'PUT', dict(expectedRevision=revision(), criteria=inputs, fake=True),400)
        check(call(path)['criteria']==inputs, 'stale, duplicate-ID, empty-ID, null and unknown-field edits cannot replace criteria')
        connection = dict(expectedRevision=revision(), repositoryPath=str(repo), baseRef='HEAD', provider='codex', instructions='Use explicit fixture input.')
        call(f'/api/agents/projects/{pid}/connection', 'PUT', connection)
        definition = {'stages':[dict(id='verify',name='Verification',group='QA',dependsOn=[],steps=[dict(id='test',name='Asserting counter test',kind='command',command=dict(executable=sys.executable,arguments=['verify.py'],workingDirectory='.',timeoutSeconds=10,environmentReferences={'CHECK_INPUT':'QA_CHECK_INPUT'}))])]}
        pipeline = call(f'/api/factory/projects/{pid}/pipelines','POST',dict(expectedRevision=revision(), name='QA fixture', description='Test only', source='Explicit test input', definition=definition),201)
        published = call(f"/api/factory/pipelines/{pipeline['pipeline']['id']}/publish",'POST',dict(expectedRevision=revision(),expectedDraftRevision=1))
        version = published['versions'][0]
        call(f'/api/factory/tasks/{tid}/configuration','PUT',dict(expectedRevision=revision(),pipelineVersionId=version['id'],overrides={'steps':[]}))
        prepared=prepare(); check(prepared['run']['snapshot']['criteria']==inputs,'work order captures immutable criterion identities, meaning and clarification flags')
        record(prepared,body(prepared,inputs[0],verdict='unverified'),409)
        run=execute(prepared);q=quality(run); baseline=call('/api/workspace')
        check(run['run']['state']=='succeeded' and all(c['status']=='unverified' for c in q['criteria']),'successful execution never fabricates criterion acceptance')
        receipt=q['receipts'][0]
        check(receipt['stable'] and receipt['after']==q['context'] and run['steps'][0]['exitCode']==0,'real command captures matching code, HEAD and configuration before/after')
        pass_body=body(run,inputs[0])
        call('/api/factory/runs/'+run['run']['id']+'/qa','POST',pass_body,403,{'Origin':'https://example.invalid'})
        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool: saved=list(pool.map(lambda _:record(run,pass_body),range(2)))
        check(all(result['criteria'][0]['status']=='pass' and len(result['criteria'][0]['history'])==1 for result in saved),'concurrent identical verdict retries append exactly one audited decision')
        record(run,pass_body|{'reason':'Changed intent'},409)
        record(run,body(run,inputs[1],expectedRevision=q['runRevision']),409)
        check(True,'request-ID reuse with different content and stale run revisions are rejected')
        for evidence_patch in [dict(testPath='../outside.py'),dict(testPath='/etc/passwd'),dict(testPath='missing.py'),dict(testPath='invalid\x00path'),dict(testPath='.GIT'),dict(line=0),dict(line=900),dict(assertion=''),dict(failureCase=''),dict(stepRunId=str(uuid.uuid4()))]:
            invalid=body(run,inputs[1]);invalid['evidence'][0].update(evidence_patch);record(run,invalid,400)
        check(True,'foreign evidence, path traversal, missing files/lines and absent test-quality explanations cannot support a verdict')
        workspace=Path(run['run']['workspacePath']);(workspace/'ignored-test.py').write_text('assert True\n')
        invalid=body(run,inputs[1]);invalid['evidence'][0]['testPath']='ignored-test.py';invalid['evidence'][0]['line']=1;record(run,invalid,400)
        check(True,'ignored asserting files cannot bypass code freshness tracking')
        invalid=body(run,inputs[1]);invalid['evidence']=[];record(run,invalid,400)
        record(run,body(run,inputs[-1]),400)
        record(run,body(run,inputs[3],verdict='notApplicable',reason=''),400)
        record(run,body(run,inputs[3],verdict='notApplicable',reason='This fixture has no platform-specific implementation.'))
        record(run,body(run,inputs[1],verdict='fail',reason='Operator identifies a deliberately unmet behavioural requirement despite process success.'))
        q=quality(run)
        check([c['status'] for c in q['criteria']]==['pass','fail','unverified','notApplicable','unverified'],'passing, failing, uncovered, justified exclusion and vague criteria remain distinct')
        check(call('/api/workspace')==baseline,'QA evidence and verdicts leave task workflow, hierarchy, dates and layout unchanged')
        record(run,body(run,inputs[1],verdict='unverified',reason='Further review needed; prior failure remains in history.'))
        check(len(quality(run)['criteria'][1]['history'])==2,'new verdicts retain prior failure and evidence without rewriting history')
        stable=quality(run);process.terminate();process.wait(timeout=25);start()
        check(quality(run)==stable,'native DuckDB retains criteria, receipts and ordered verdict history across restart')
        # Worktree code changes, including untracked files, invalidate old evidence.
        stale_body=body(run,inputs[2]);(workspace/'new-input.txt').write_text('new non-ignored input\n')
        stale=quality(run)
        check(stale['criteria'][0]['status']=='stale' and stale['criteria'][3]['status']=='stale','non-ignored code changes invalidate pass and not-applicable decisions')
        record(run,stale_body,409);record(run,body(run,inputs[2]),409)
        check(True,'stale client context and stale successful command evidence cannot approve changed code')
        (workspace/'new-input.txt').unlink();check(quality(run)['criteria'][0]['status']=='pass','return to the identical code/config fingerprint restores the recorded evidence context')
        subprocess.run(['git','-C',str(workspace),'commit','--allow-empty','-qm','Changed verified HEAD'],check=True)
        check(quality(run)['criteria'][0]['status']=='stale','a changed HEAD commit invalidates approval even when file contents match')
        subprocess.run(['git','-C',str(workspace),'reset','--soft',receipt['after']['headCommit']],check=True)
        (workspace/'linked-test.py').symlink_to(workspace/'verify.py')
        unavailable=quality(run)
        check(unavailable['context'] is None and unavailable['criteria'][0]['status']=='stale','symlink inputs expose unavailable context rather than partial verification coverage')
        (workspace/'linked-test.py').unlink()
        env['QA_CHECK_INPUT']='explicit-second-environment';process.terminate();process.wait(timeout=25);start()
        changed=quality(run)
        check(changed['criteria'][0]['status']=='stale' and 'explicit-second-environment' not in json.dumps(changed),'changed referenced environment invalidates evidence without returning its value')
        env['QA_CHECK_INPUT']='explicit-first-environment';process.terminate();process.wait(timeout=25);start()
        changed_inputs=copy.deepcopy(inputs);changed_inputs[0]['expectedResult']='Adopted changed expectation';criteria(changed_inputs)
        check(quality(run)['sourceChanged'] and quality(run)['criteria'][0]['status']=='stale','adopting changed criteria invalidates historical QA while preserving captured criteria')
        check(call('/api/factory/runs/'+run['run']['id'])['run']['snapshot']['criteria']==inputs,'task edits cannot rewrite the old work order')
        record(run,body(run,inputs[2]),409)
        criteria(inputs)
        # New execution really fails its assertion; zero exit code cannot be invented.
        (repo/'counter.py').write_text('def increment(value):\n    return value\n')
        subprocess.run(['git','-C',str(repo),'add','.'],check=True);subprocess.run(['git','-C',str(repo),'commit','-qm','Deliberate test defect'],check=True)
        failing=execute(prepare());check(failing['run']['state']=='failed' and failing['steps'][0]['exitCode']!=0,'real assertion defect produces a failing command receipt')
        record(failing,body(failing,inputs[0]),400);record(failing,body(failing,inputs[0],verdict='fail'))
        check(quality(failing)['criteria'][0]['status']=='fail','failed command supports an explicit failing verdict and cannot support pass')
        # Commands that alter source while verifying cannot certify a stable revision.
        mutable=copy.deepcopy(definition);mutable['stages'][0]['steps'][0]['command']['arguments']=['-c',"from pathlib import Path;Path('changed-by-check.txt').write_text('changed');print('exit zero')"]
        current=call('/api/factory/pipelines/'+pipeline['pipeline']['id'])
        updated=call('/api/factory/pipelines/'+pipeline['pipeline']['id']+'/draft','PUT',dict(expectedRevision=revision(),expectedDraftRevision=current['pipeline']['draftRevision'],name='QA fixture',description='Test only',definition=mutable))
        published=call('/api/factory/pipelines/'+pipeline['pipeline']['id']+'/publish','POST',dict(expectedRevision=revision(),expectedDraftRevision=updated['pipeline']['draftRevision']))
        call(f'/api/factory/tasks/{tid}/configuration','PUT',dict(expectedRevision=revision(),pipelineVersionId=published['versions'][-1]['id'],overrides={'steps':[]}))
        unstable=execute(prepare());check(not quality(unstable)['receipts'][0]['stable'],'source-mutating verification command remains unstable despite zero exit')
        record(unstable,body(unstable,inputs[0]),409)
        # Foreign-run references never satisfy local criteria.
        invalid=body(unstable,inputs[0]);invalid['evidence'][0]['stepRunId']=run['steps'][0]['id'];record(unstable,invalid,400)
        check(True,'unstable and foreign-run evidence are rejected')
        # Missing worktrees expose unavailable context and retain history.
        retained=workspace.with_name(workspace.name+'-unavailable');workspace.rename(retained)
        missing=quality(run);check(missing['context'] is None and missing['contextError'] and missing['criteria'][0]['status']=='stale','missing verification worktree is a visible gap, never a pass')
        retained.rename(workspace)
        if '--serve' in sys.argv:
            receipt_path=Path('/private/tmp/canopy-qa-browser-fixture.json');receipt_path.write_text(json.dumps(dict(base=base,projectId=pid,taskId=tid,runId=run['run']['id'],scratch=str(scratch),workspace=str(workspace),pipelineId=pipeline['pipeline']['id'])));receipt_path.chmod(0o600)
            print('BROWSER FIXTURE '+str(receipt_path),flush=True)
            while not (scratch/'stop-browser').exists():time.sleep(.5)
        call(f'/api/projects/{pid}?expectedRevision={revision()}','DELETE')
        call('/api/factory/runs/'+run['run']['id']+'/qa',expected=404)
        check(not call('/api/workspace')['projects'],'project cleanup removes criteria, receipts and verdict history transactionally')
        print(f'{checks} acceptance QA checks passed; no live data or provider calls.',flush=True)
    finally:
        if process and process.poll() is None:process.terminate();process.wait(timeout=25)
        log.close()
