#!/usr/bin/env python3
"""Controlled delivery using real HTTP/Git/commands/native DuckDB; provider protocol fixtures, no model calls."""
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
import threading
import http.server
import sqlite3
import signal
import re
from regression_processes import start_group, stop_group

ROOT=Path(__file__).resolve().parents[1]; API=ROOT/'server/Planner.Api'; checks=0

def check(value,message):
    global checks
    assert value,message
    checks+=1; print(f'PASS {checks:02}: {message}',flush=True)

with tempfile.TemporaryDirectory(prefix='canopy-delivery-') as directory:
    scratch=Path(directory).resolve();repo=scratch/'source';repo.mkdir()
    (repo/'.gitignore').write_text('__pycache__/\n')
    (repo/'counter.py').write_text('def increment(value):\n    return 0\n')
    (repo/'verify.py').write_text('from counter import increment\nassert increment(0) == 1\nprint("ZERO_ASSERTION_VERIFIED")\n')
    for args in [['init','-q','-b','main'],['config','user.name','Review regression'],['config','user.email','review@example.invalid'],['add','.'],['commit','-qm','Explicit regression baseline']]:subprocess.run(['git','-C',str(repo),*args],check=True)
    fake=scratch/'agent';trace=scratch/'trace.jsonl'
    fake.write_text('''#!/usr/bin/env python3
import json,sys,pathlib,subprocess
prompt=sys.stdin.read();purpose=next(x.split(': ',1)[1] for x in prompt.splitlines() if x.startswith('Purpose: '))
if purpose=='implementation':
 pathlib.Path('counter.py').write_text('def increment(value):\\n    return value + 1\\n')
 pathlib.Path('verify.py').write_text('from counter import increment\\nassert increment(0) == 1\\nassert increment(-1) == 0\\nprint("ZERO_ASSERTION_VERIFIED NEGATIVE_ASSERTION_VERIFIED")\\n')
 subprocess.run(['git','add','counter.py','verify.py'],check=True)
 subprocess.run(['git','commit','-qm','Committed candidate before verification'],check=True)
 report='BUILDER_BIAS_SENTINEL: candidate committed for actual verification.'
else:
 assert 'BUILDER_BIAS_SENTINEL' not in prompt
 report='Independent fixture assessment after inspecting actual code and tests.'
print(json.dumps({'type':'thread.started','thread_id':'a93c9b91-2011-4e22-ac90-626998c0ec50'}),flush=True)
print(json.dumps({'type':'item.completed','item':{'type':'agent_message','text':report}}),flush=True)
''');fake.chmod(0o755)
    with socket.socket() as sock:sock.bind(('127.0.0.1',0));base=f'http://127.0.0.1:{sock.getsockname()[1]}'
    env=os.environ|{'Database__Path':str(scratch/'workspace.duckdb'),'Agents__WorkspaceRoot':str(scratch/'worktrees'),'Agents__PollIntervalMs':'250','Agents__CodexExecutable':str(fake),'REVIEW_TRACE':str(trace),'DELIVERY_FIXTURE_TOKEN':'isolated-token','Delivery__GitHubHost':'127.0.0.1','Delivery__TokenEnvironmentVariable':'DELIVERY_FIXTURE_TOKEN','Delivery__RequestTimeoutSeconds':'2'}
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
    remotes=scratch/'remotes';bare=remotes/'regression/candidate.git';bare.parent.mkdir(parents=True)
    subprocess.run(['git','clone','--bare','-q',str(repo),str(bare)],check=True);subprocess.run(['git','--git-dir='+str(bare),'config','http.receivepack','true'],check=True)
    baseline_sha=subprocess.check_output(['git','-C',str(repo),'rev-parse','HEAD'],text=True).strip()
    class GitHandler(http.server.BaseHTTPRequestHandler):
        def do_GET(self):self.handle_git()
        def do_POST(self):self.handle_git()
        def log_message(self,*args):pass
        def handle_git(self):
            path,_,query=self.path.partition('?');body=self.rfile.read(int(self.headers.get('Content-Length','0')))
            environment=os.environ|{'GIT_PROJECT_ROOT':str(remotes),'GIT_HTTP_EXPORT_ALL':'1','PATH_INFO':urllib.parse.unquote(path),'REQUEST_METHOD':self.command,'QUERY_STRING':query,'CONTENT_TYPE':self.headers.get('Content-Type',''),'CONTENT_LENGTH':str(len(body)),'REMOTE_USER':'fixture'}
            result=subprocess.run(['git','http-backend'],input=body,capture_output=True,env=environment);head,_,data=result.stdout.partition(b'\r\n\r\n');headers=head.decode().splitlines();status=200
            for line in headers:
                if line.lower().startswith('status:'):status=int(line.split()[1])
            self.send_response(status)
            for line in headers:
                if ':' in line and not line.lower().startswith('status:'):key,value=line.split(':',1);self.send_header(key,value.strip())
            self.end_headers();self.wfile.write(data)
    git_server=http.server.ThreadingHTTPServer(('127.0.0.1',0),GitHandler);threading.Thread(target=git_server.serve_forever,daemon=True).start();git_url=f'http://127.0.0.1:{git_server.server_port}/regression/candidate.git'
    subprocess.run(['git','-C',str(repo),'remote','add','origin',git_url],check=True)
    fixture_path=scratch/'provider.json';fixture={'barePath':str(bare),'repository':'regression/candidate','gitUrl':git_url,'target':'main','checkName':'fixture-verify','appId':9001,'ready':True,'mode':'normal'}
    def configure(**values):
        fixture.update(values);temporary=fixture_path.with_suffix('.next');temporary.write_text(json.dumps(fixture));temporary.replace(fixture_path)
    configure()
    sim_log=(scratch/'simulation.ndjson').open('w+');sim_err=(scratch/'simulation.err').open('w+');simulation=start_group(['postman','simulation','run',str(ROOT/'postman/simulations/factory-delivery/factory-delivery.sim.yaml'),'--output','ndjson'],cwd=ROOT,env=os.environ|{'CANOPY_DELIVERY_FIXTURE':str(fixture_path)},stdout=sim_log,stderr=sim_err)
    provider_base=None
    for _ in range(150):
        sim_log.flush();lines=(scratch/'simulation.ndjson').read_text().splitlines()
        for line in lines:
            try:
                event=json.loads(line)
                if event.get('type')=='listening' or event.get('event')=='listening':
                    # Printed event format is authoritative; accept address fields from it.
                    strings=re.findall(r'http://[^"\s,}]+',line)
                    if strings:provider_base=strings[0].rstrip('/')
                    elif event.get('port'):provider_base='http://127.0.0.1:'+str(event['port'])
            except json.JSONDecodeError:pass
        if provider_base:break
        if simulation.poll() is not None:raise AssertionError((scratch/'simulation.err').read_text()+'\n'+(scratch/'simulation.ndjson').read_text())
        time.sleep(.1)
    assert provider_base,(scratch/'simulation.ndjson').read_text()
    if not provider_base.endswith('/github'):provider_base+='/github'
    env['Delivery__ApiBaseUrl']=provider_base
    app_script=scratch/'application.py';app_config=scratch/'application.json';app_db=scratch/'application.sqlite'
    with sqlite3.connect(app_db) as db:db.execute('create table records(id text primary key, value text)');db.execute('insert into records values (?,?)',('fixture-record','persisted input'))
    with socket.socket() as sock:sock.bind(('127.0.0.1',0));app_port=sock.getsockname()[1]
    app_script.write_text('''import http.server,json,os,sqlite3,sys,uuid
config=json.load(open(sys.argv[1]));instance=str(uuid.uuid4())
class Handler(http.server.BaseHTTPRequestHandler):
 def log_message(self,*args):pass
 def do_GET(self):
  if self.path=='/health':value=dict(status='ready',instance=instance,revision=os.environ['APP_REVISION'],pid=os.getpid())
  elif self.path=='/snapshot':
   with sqlite3.connect(config['database']) as db:value=dict(records=[dict(id=id,value=value) for id,value in db.execute('select id,value from records')])
  else:self.send_response(404);self.end_headers();return
  self.send_response(200);self.send_header('Content-Type','application/json');self.end_headers();self.wfile.write(json.dumps(value).encode())
http.server.HTTPServer(('127.0.0.1',config['port']),Handler).serve_forever()
''')
    app_configuration={'port':app_port,'database':str(app_db),'script':str(app_script),'count':0,'mode':'normal','baseline':baseline_sha};app_config.write_text(json.dumps(app_configuration))
    app_log=(scratch/'application.log').open('a');application=subprocess.Popen([sys.executable,str(app_script),str(app_config)],env=os.environ|{'APP_REVISION':baseline_sha},stdout=app_log,stderr=app_log)
    app_configuration['pid']=application.pid;app_config.write_text(json.dumps(app_configuration))
    hook=scratch/'restart.py';hook.write_text('''import json,os,signal,subprocess,sys,time,urllib.request,sqlite3
from pathlib import Path
p=Path(sys.argv[1]);c=json.loads(p.read_text());c['count']+=1;p.write_text(json.dumps(c))
if c['mode']=='noop':sys.exit(0)
os.kill(c['pid'],signal.SIGTERM)
for _ in range(100):
 try:urllib.request.urlopen('http://127.0.0.1:'+str(c['port'])+'/health',timeout=.1);time.sleep(.05)
 except OSError:break
if c['mode']=='persistenceDrift':
 with sqlite3.connect(c['database']) as db:db.execute("update records set value='explicit restart defect'")
revision=c['baseline'] if c['mode']=='wrongRevision' else os.environ['CANOPY_MERGE_SHA']
# A detached launcher exits, reparenting the new application before this hook waits.
launch='import subprocess,sys,os;subprocess.Popen([sys.executable,sys.argv[1],sys.argv[2]],env=os.environ,stdin=subprocess.DEVNULL,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL,start_new_session=True)'
subprocess.run([sys.executable,'-c',launch,c['script'],str(p)],env=os.environ|{'APP_REVISION':revision},check=True)
for _ in range(200):
 try:
  health=json.load(urllib.request.urlopen('http://127.0.0.1:'+str(c['port'])+'/health',timeout=.2));c['pid']=health['pid'];p.write_text(json.dumps(c));break
 except OSError:time.sleep(.05)
else:raise RuntimeError('Application restart timeout')
time.sleep(c.get('afterRestartWait',0))
print('Actual fixture application restarted',flush=True)
''')
    hook_configuration=[dict(id='fixture-restart',name='Restart isolated application',executable=sys.executable,arguments=[str(hook),str(app_config)],workingDirectory=str(scratch),timeoutSeconds=15,integrityFiles=[str(hook)],healthUrl=f'http://127.0.0.1:{app_port}/health',expectedHealthJson=json.dumps({'status':'ready'}),instanceJsonPointer='/instance',revisionJsonPointer='/revision',snapshotUrl=f'http://127.0.0.1:{app_port}/snapshot')]
    for k,v in hook_configuration[0].items():
        if isinstance(v,list):
            for i,entry in enumerate(v):env[f'Delivery__Hooks__0__{k}__{i}']=str(entry)
        else:env[f'Delivery__Hooks__0__{k}']=str(v)
    def provider_call(path):return json.load(urllib.request.urlopen(provider_base+path,timeout=10))
    def reset_target():subprocess.run(['git','--git-dir='+str(bare),'update-ref','refs/heads/main',baseline_sha],check=True)
    def source(without_checks=False):
        global tid,cid,task
        reset_target();configure(mode='normal',ready=True)
        old={x['id'] for x in call('/api/workspace')['items']};ws=call(f'/api/projects/{pid}/items','POST',dict(expectedRevision=revision(),title='Deliver committed counter'));task=next(x for x in ws['items'] if x['id'] not in old);tid=task['id']
        spec=json.dumps(dict(type='doc',content=[dict(type='paragraph',content=[dict(type='text',text='Implement increment and commit before verification.')])]))
        call(f'/api/items/{tid}','PATCH',dict(expectedRevision=revision(),title=task['title'],specJson=spec,statusId=task['statusId'],priority='none',assigneeIds=[],dependencyIds=[],startDate=None,dueDate=None))
        cid=str(uuid.uuid4());call(f'/api/factory/tasks/{tid}/criteria','PUT',dict(expectedRevision=revision(),criteria=[dict(id=cid,description='Increment including negative inputs',expectedResult='increment(-1) == 0',verificationMethod='Inspect zero and negative assertions')]))
        call(f'/api/factory/tasks/{tid}/configuration','PUT',dict(expectedRevision=revision(),pipelineVersionId=unchecked_version['id'] if without_checks else version['id'],overrides={'steps':[]}))
        run=execute(prepare());assert run['run']['state']=='succeeded',run
        if without_checks:
            q=call('/api/factory/runs/'+run['run']['id']+'/qa');call('/api/factory/runs/'+run['run']['id']+'/qa','POST',dict(requestId=str(uuid.uuid4()),expectedRevision=q['runRevision'],expectedContext=q['context']['fingerprint'],criterionId=cid,verdict='notApplicable',reason='Explicit regression waiver: no command was executed; delivery must still reject missing verification.',evidence=[]))
        else:qa_pass(run)
        r=assess(begin(run));submit(r,submit_body(r,'approve'));return call('/api/factory/runs/'+run['run']['id'])
    def adopt(hook_id=None,**changes):
        policy=dict(enabled=True,repository=fixture['repository'],remoteName='origin',targetBranch='main',branchPrefix='feature/',allowDraftPr=True,allowMerge=True,requiredChecks=[dict(name=fixture['checkName'],appId=fixture['appId'])],postMergeHookId=hook_id,environment='Isolated fixture',sensitivePaths=['counter.py'],migrationPaths=[])|changes
        return call(f'/api/factory/projects/{pid}/delivery-policy','PUT',dict(expectedRevision=revision(),policy=policy))
    def lib(run):return call('/api/factory/runs/'+run['run']['id']+'/delivery')
    def capture_body(run):return dict(requestId=str(uuid.uuid4()),expectedRevision=lib(run)['local']['runRevision'],branchName='feature/'+uuid.uuid4().hex)
    def capture(run,body=None):return call('/api/factory/runs/'+run['run']['id']+'/deliveries','POST',body or capture_body(run))
    def read(d):return call('/api/factory/deliveries/'+d['delivery']['id'])
    def act_body(d,kind,note='Explicit fixture approval; no sensitive changes or production migration.'):return dict(requestId=str(uuid.uuid4()),expectedRevision=d['delivery']['revision'],action=kind,approvalNote=note)
    def act(d,kind,body=None):return call('/api/factory/deliveries/'+d['delivery']['id']+'/actions','POST',body or act_body(d,kind))
    def settled(d):
        for _ in range(300):
            current=read(d)
            if current['delivery']['currentOperationId'] is None:return current
            time.sleep(.1)
        raise AssertionError(current)
    def close(d):return settled(act(read(d),'close'))
    def publish(d):return settled(act(d,'publish'))
    def approve(d):return settled(act(read(d),'approveMerge'))
    def merge(d):return settled(act(read(d),'merge'))
    def hook_mode(mode='normal',**values):
        c=json.loads(app_config.read_text());c.update(mode=mode,**values);app_config.write_text(json.dumps(c));return c
    try:
        start();pid=call('/api/projects','POST',dict(name='Factory delivery regression',description='Isolated contract simulation',color='#477fa1'))['projects'][0]['id']
        call(f'/api/agents/projects/{pid}/connection','PUT',dict(expectedRevision=revision(),repositoryPath=str(repo),baseRef='HEAD',provider='codex',instructions='Explicit protocol fixture; commit candidate before verification.'))
        definition={'stages':[dict(id='build',name='Build',group='Build',dependsOn=[],steps=[dict(id='implement',name='Implementation',kind='agent',role='implementation',permission='workspace-write',instructions='Implement and commit before checks.',produces=['changes'])]),dict(id='qa',name='QA',group='Check',dependsOn=['build'],steps=[dict(id='verify',name='Actual assertions',kind='command',command=dict(executable=sys.executable,arguments=['verify.py'],workingDirectory='.',timeoutSeconds=10),consumes=['changes'],produces=['verification'])]),dict(id='review',name='Independent reviewer',group='Check',dependsOn=['qa'],steps=[dict(id='review',name='Read-only reviewer',kind='agent',role='review',permission='read-only',instructions='Review code independently.',consumes=['changes','verification'])])]}
        p=call(f'/api/factory/projects/{pid}/pipelines','POST',dict(expectedRevision=revision(),name='Delivery fixture',description='Test only',source='Explicit regression fixture',definition=definition),201)
        version=call('/api/factory/pipelines/'+p['pipeline']['id']+'/publish','POST',dict(expectedRevision=revision(),expectedDraftRevision=1))['versions'][0]
        run=source();check(lib(run)['policy']['policy'] is None,'delivery permissions and repository policy are never silently adopted')
        adopt()
        stray=Path(run['run']['workspacePath'])/'uncommitted.txt';stray.write_text('Explicit unsupported publication input\n')
        call('/api/factory/runs/'+run['run']['id']+'/deliveries','POST',capture_body(run),409);stray.unlink();check(True,'uncommitted code cannot be captured as a reviewed publication candidate')
        body=capture_body(run);path='/api/factory/runs/'+run['run']['id']+'/deliveries';writes=len(provider_call('/__state')['writes'])
        call(path,'POST',body|{'unexpected':True},400);call(path,'POST',body,403,{'Origin':'https://example.invalid'})
        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:records=list(pool.map(lambda _:call(path,'POST',body),range(2)))
        d=records[0];check(d['delivery']['capture']['sensitiveFiles']==['counter.py'] and d['delivery']['policy']['environment']=='Isolated fixture','actual changed files and explicit environment bind sensitive-change approval scope');check(records[1]['delivery']['id']==d['delivery']['id'] and not d['operations'] and len(provider_call('/__state')['writes'])==writes,'concurrent exact capture retries retain one immutable candidate without external mutations')
        call(path,'POST',body|{'branchName':'feature/different'},409);call(path,'POST',capture_body(run),409)
        check(True,'request-ID content conflicts and multiple open task deliveries are rejected')
        call(f'/api/agents/tasks/{tid}/runs','POST',dict(requestId=str(uuid.uuid4()),expectedRevision=revision(),provider='codex',purpose='implementation',instructions='Bypass'),409)
        call(f'/api/items/{tid}?expectedRevision={revision()}','DELETE',None,409)
        check(True,'open delivery owns task execution and blocks deletion')
        d=settled(act(d,'merge'));check(d['operations'][-1]['state']=='failed' and not provider_call('/__state')['writes'],'merge without a PR, checks and explicit authority performs no remote write')
        publish_request=act_body(d,'publish');action_path='/api/factory/deliveries/'+d['delivery']['id']+'/actions'
        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:queued=list(pool.map(lambda _:call(action_path,'POST',publish_request),range(2)))
        d=settled(queued[0]);check(sum(x['kind']=='publish' for x in d['operations'])==1,'concurrent exact action retries create one persistent mutation intent')
        call(action_path,'POST',publish_request|{'approvalNote':'Different content'},409)
        check(d['delivery']['state']=='prCreated' and d['delivery']['remote']['draft'],'actual HTTP Git push publishes a new branch and records the draft PR')
        remote_head=subprocess.check_output(['git','--git-dir='+str(bare),'rev-parse','refs/heads/'+body['branchName']],text=True).strip();check(remote_head==d['delivery']['capture']['headSha'],'published remote branch equals the committed, reviewed head')
        # A draft cannot pass merge preflight even with successful checks.
        configure(ready=False);d=settled(act(d,'reconcile'));d=settled(act(d,'approveMerge'));check(d['operations'][-1]['state']=='failed' and not d['delivery']['approvals'],'draft PR cannot obtain merge authority')
        configure(ready=True);d=approve(d);assert d['operations'][-1]['state']=='succeeded',d
        before=call('/api/workspace');d=merge(d);check(d['delivery']['state']=='delivered' and d['delivery']['remote']['mergeSha'] and provider_call('/__state')['writes'][-1]['kind']=='merge','explicit approved merge reconciles actual merge commit/tree/parents and target ancestry')
        check(call('/api/workspace')==before,'delivery completion does not change task status, hierarchy, dates or layout')
        snap=read(d);process.terminate();process.wait(timeout=25);start();check(read(d)==snap,'native restart preserves delivery captures, approvals and provider operation receipts')
        unchecked=copy.deepcopy(definition);unchecked['stages']=[unchecked['stages'][0],unchecked['stages'][2]];unchecked['stages'][1]['dependsOn']=['build'];unchecked['stages'][1]['steps'][0]['consumes']=['changes']
        p=call(f'/api/factory/projects/{pid}/pipelines','POST',dict(expectedRevision=revision(),name='No verification fixture',description='Test only',source='Explicit missing-evidence regression',definition=unchecked),201)
        unchecked_version=call('/api/factory/pipelines/'+p['pipeline']['id']+'/publish','POST',dict(expectedRevision=revision(),expectedDraftRevision=1))['versions'][0]
        missing=source(without_checks=True);call('/api/factory/runs/'+missing['run']['id']+'/deliveries','POST',capture_body(missing),409);check('verification command' in ' '.join(lib(missing)['local']['errors']),'manual criterion waivers and review approval cannot replace actual current command verification')
        if '--hook-integrity-only' in sys.argv:
            run=source();adopt('fixture-restart');x=capture(run);original=hook.read_text();hook.write_text(original+'\n# Explicit changed hook source fixture\n')
            call('/api/factory/deliveries/'+x['delivery']['id']+'/actions','POST',act_body(x,'publish'),409);check(not read(x)['local']['ready'],'changing a declared hook source revokes captured action authority before any mutation')
            hook.write_text(original);x=merge(approve(publish(read(x))));x=settled(act(x,'approvePostMerge'));x=settled(act(x,'postMerge'));check(x['delivery']['state']=='delivered','restored exact executable/script binding permits an explicitly approved real restart')
            call(f'/api/projects/{pid}?expectedRevision={revision()}','DELETE');print('Targeted hook integrity and real restart validation passed',flush=True);raise SystemExit(0)
        if '--serve' in sys.argv:
            run=source();adopt();x=capture(run);print(json.dumps(dict(baseUrl=base,runId=run['run']['id'],projectId=pid,itemId=tid,deliveryId=x['delivery']['id'],providerBase=provider_base,scratch=str(scratch),providerFixture=str(fixture_path))),flush=True)
            while True:time.sleep(1)
        # Each failed/unknown observation must block mutation rather than fabricate green checks.
        for mode in ['failedCheck','missingCheck','wrongApp','ambiguous','truncated','loose','adminBypass','protectionMissing','unavailable','html','hanging']:
            run=source();adopt();x=publish(capture(run));configure(mode=mode);count=len(provider_call('/__state')['writes']);x=settled(act(x,'approveMerge'));check(x['operations'][-1]['state']=='failed' and len(provider_call('/__state')['writes'])==count,'provider '+mode+' blocks approval without external mutation');configure(mode='normal');close(x)
        # Lost create and merge responses are reconciled by reading actual provider state.
        run=source();adopt();x=capture(run);configure(mode='lostCreate');x=publish(x);check(x['operations'][-1]['state']=='uncertain','lost create response remains unknown with its durable mutation intent')
        count=len(provider_call('/__state')['writes'])
        configure(mode='normal');x=settled(act(x,'reconcile'));check(x['delivery']['remote'] is not None and len(provider_call('/__state')['writes'])==count,'reconcile finds the exact PR marker and never repeats a lost create')
        x=approve(x);configure(mode='lostMerge');x=merge(x);check(x['operations'][-1]['state']=='uncertain','lost merge response is retained as unknown rather than retried')
        count=len(provider_call('/__state')['writes']);configure(mode='normal');x=settled(act(x,'reconcile'));check(x['delivery']['state']=='delivered' and len(provider_call('/__state')['writes'])==count,'lost merge response resolves from actual merge ancestry without a duplicate merge')
        run=source();adopt();x=approve(publish(capture(run)));configure(mode='pending');x=merge(x);check(x['delivery']['state']=='mergePending' and x['delivery']['remote']['mergeSha'] is None,'accepted asynchronous merge is pending, never delivery success');configure(mode='normal');x=settled(act(x,'reconcile'));check(x['delivery']['state']=='delivered','explicit reconciliation verifies the asynchronous merge result')
        run=source();adopt();x=approve(publish(capture(run)));configure(mode='enqueued');x=merge(x);check(x['delivery']['state']=='needsAttention' and x['operations'][-1]['state']=='uncertain' and not x['delivery']['remote']['mergeSha'],'unexpected merge-queue acceptance remains unknown instead of completing or replaying');configure(mode='normal');close(x)
        for outcome in ['expired','failedResult']:
            run=source();adopt();x=approve(publish(capture(run)));configure(mode='pending');x=merge(x);configure(mode=outcome);count=len(provider_call('/__state')['writes']);x=settled(act(x,'reconcile'))
            if outcome=='expired':
                call('/api/factory/deliveries/'+x['delivery']['id']+'/actions','POST',act_body(x,'merge'),409);check(x['delivery']['remote']['mergeRequestId'] and len(provider_call('/__state')['writes'])==count,'expired async result retains its unknown request identity and blocks another merge');configure(mode='normal');close(x)
            else:
                check(x['delivery']['remote']['mergeRequestId'] is None,'proven terminal async rejection releases only the failed request identity');configure(mode='normal');x=merge(x);check(x['delivery']['state']=='delivered','a new explicit merge is possible after the previous request is proven terminal and preflight passes again')
        # Remote writers cannot bypass local approval or GitHub's final protection gate.
        for race in ['headChanged','baseChanged','baseRace','protectionRace']:
            run=source();adopt();x=approve(publish(capture(run)));count=len(provider_call('/__state')['writes'])
            if race in ['headChanged','baseChanged']:
                old=x['delivery']['capture']['headSha'] if race=='headChanged' else baseline_sha
                tree=subprocess.check_output(['git','--git-dir='+str(bare),'rev-parse',old+'^{tree}'],text=True).strip()
                newer=subprocess.check_output(['git','--git-dir='+str(bare),'commit-tree',tree,'-p',old],input='Explicit concurrent writer fixture\n',text=True,env=os.environ|{'GIT_AUTHOR_NAME':'Concurrent fixture','GIT_AUTHOR_EMAIL':'fixture@example.invalid','GIT_COMMITTER_NAME':'Concurrent fixture','GIT_COMMITTER_EMAIL':'fixture@example.invalid'}).strip()
                refname=x['delivery']['capture']['branchName'] if race=='headChanged' else 'main';subprocess.run(['git','--git-dir='+str(bare),'update-ref','refs/heads/'+refname,newer,old],check=True)
            else:configure(mode=race)
            x=merge(x);check(x['delivery']['state']=='needsAttention' and len(provider_call('/__state')['writes'])==count,'concurrent '+race+' cannot cause an unverified merge');configure(mode='normal');close(x)
        # Candidate change and policy revocation invalidate existing authority.
        run=source();adopt();x=approve(publish(capture(run)));count=len(provider_call('/__state')['writes']);(Path(run['run']['workspacePath'])/'counter.py').write_text('def increment(value):\n    return value\n');x=merge(x);check(x['operations'][-1]['state']=='failed' and len(provider_call('/__state')['writes'])==count,'changed reviewed source invalidates QA and blocks an approved merge');close(x)
        run=source();adopt();x=capture(run);adopt(allowMerge=False);call('/api/factory/deliveries/'+x['delivery']['id']+'/actions','POST',act_body(x,'publish'),409);check(True,'changed project authority revokes old captured permissions');close(x)
        # Real configured application restart, observed deployed revision and persisted data.
        run=source();adopt('fixture-restart');hook_mode();x=merge(approve(publish(capture(run))));assert x['delivery']['state']=='merged',x
        x=settled(act(x,'postMerge'));check(x['operations'][-1]['state']=='failed' and x['delivery']['hook'] is None,'post-merge process cannot run without separate revision-bound approval')
        x=settled(act(x,'approvePostMerge'));count=json.loads(app_config.read_text())['count'];x=settled(act(x,'postMerge'));check(x['delivery']['state']=='delivered' and json.loads(app_config.read_text())['count']==count+1 and x['delivery']['hook']['after']['revision']==x['delivery']['remote']['mergeSha'],'actual process restart verifies a new instance, merged revision, expected health and stable SQLite persistence')
        for mode in ['noop','wrongRevision','persistenceDrift']:
            run=source();adopt('fixture-restart');hook_mode(mode);x=merge(approve(publish(capture(run))));x=settled(act(x,'approvePostMerge'));count=json.loads(app_config.read_text())['count'];x=settled(act(x,'postMerge'));check(x['delivery']['state']=='needsAttention' and x['delivery']['hook']['error'] and json.loads(app_config.read_text())['count']==count+1,'post-merge '+mode+' remains an operational failure instead of false delivery');call('/api/factory/deliveries/'+x['delivery']['id']+'/actions','POST',act_body(x,'postMerge'),409);close(x)
        # Stop backend during a real hook after the new application is running.
        run=source();adopt('fixture-restart');hook_mode('normal',afterRestartWait=8);x=merge(approve(publish(capture(run))));x=settled(act(x,'approvePostMerge'));count=json.loads(app_config.read_text())['count'];act(x,'postMerge')
        for _ in range(100):
            c=json.loads(app_config.read_text())
            if c['count']>count:
                try:
                    health=json.load(urllib.request.urlopen(f'http://127.0.0.1:{app_port}/health'))
                    if health['revision']==x['delivery']['remote']['mergeSha']:break
                except OSError:pass
            time.sleep(.1)
        process.terminate();process.wait(timeout=25);start();x=read(x);check(x['operations'][-1]['state']=='uncertain' and json.loads(app_config.read_text())['count']==count+1,'backend interruption retains hook intent and never re-executes it on restart');x=settled(act(x,'reconcile'));check(x['delivery']['state']=='delivered' and json.loads(app_config.read_text())['count']==count+1,'interrupted hook reconciles actual running revision and intact persistence without repeating restart')
        hook_mode('normal',afterRestartWait=0)
        run=source();adopt();x=capture(run);configure(mode='hanging');act(x,'publish');process.terminate();process.wait(timeout=25);configure(mode='normal');count=len(provider_call('/__state')['writes']);start();x=read(x);check(x['operations'][-1]['state']=='uncertain' and len(provider_call('/__state')['writes'])==count,'queued/in-flight publication never replays after backend restart');close(x)
        assert any('request' in line and '/repos/' in line for line in (scratch/'simulation.ndjson').read_text().splitlines())
        check(True,'Postman simulation request log proves the real adapter used the contract provider')
        call(f'/api/projects/{pid}?expectedRevision={revision()}','DELETE');check(not call('/api/workspace')['projects'],'test-only tasks and native delivery journals clean up transactionally');print(f'{checks} controlled delivery checks passed')
    finally:
        if process and process.poll() is None:process.terminate();process.wait(timeout=25)
        stop_group(simulation);git_server.shutdown()
        try:os.kill(json.loads(app_config.read_text())['pid'],signal.SIGTERM)
        except ProcessLookupError:pass
        if application.poll() is None:application.terminate()
        application.wait(timeout=10);app_log.close();log.close();sim_log.close();sim_err.close()
