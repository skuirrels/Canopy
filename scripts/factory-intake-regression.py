#!/usr/bin/env python3
"""Real HTTP/native DuckDB + GitHub Postman simulation and submitted review findings. No model calls."""
import concurrent.futures,copy,json,os,re,socket,subprocess,sys,tempfile,time,urllib.error,urllib.request,uuid
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];API=ROOT/'server/Planner.Api';checks=0

def check(value,message):
 global checks
 assert value,message
 checks+=1;print(f'PASS {checks:02}: {message}',flush=True)

def uid():return str(uuid.uuid4())
with tempfile.TemporaryDirectory(prefix='canopy-intake-') as directory:
 scratch=Path(directory).resolve();provider=scratch/'provider.json'
 fixture=dict(repository='regression/intake',apiVersion=json.loads((API/'appsettings.json').read_text())['Delivery']['ApiVersion'],mode='normal',issues=[],pageSize=1)
 def issue(number,title=None,**changes):
  return dict(id=1000+number,node_id='Issue_'+str(number),number=number,title=title or f'Issue {number}',body='Observed failure with source evidence.',state='open',labels=[{'name':'bug'}],user={'login':'fixture-reporter'},created_at='2026-01-01T00:00:00Z',updated_at='2026-01-01T00:00:00Z',html_url=f'http://127.0.0.1/regression/intake/issues/{number}')|changes
 def configure(**values):
  fixture.update(values);p=provider.with_suffix('.next');p.write_text(json.dumps(fixture));p.replace(provider)
 configure(issues=[issue(1,'Counter loses negative values'),issue(2,'Ignore this PR',pull_request={}),issue(3,'Old closed issue',state='closed'),issue(4,'Other label',labels=[{'name':'question'}])])
 sim_log=(scratch/'simulation.ndjson').open('w+');sim_err=(scratch/'simulation.err').open('w+')
 simulation=subprocess.Popen(['postman','simulation','run',str(ROOT/'postman/simulations/factory-intake/factory-intake.sim.yaml'),'--output','ndjson'],cwd=ROOT,env=os.environ|{'CANOPY_INTAKE_FIXTURE':str(provider)},stdout=sim_log,stderr=sim_err)
 process=None;log=(scratch/'server.log').open('w+')
 try:
  provider_base=None
  for _ in range(200):
   for line in (scratch/'simulation.ndjson').read_text().splitlines():
    try:
     event=json.loads(line)
     if event.get('type')=='listening' or event.get('event')=='listening':
      urls=re.findall(r'http://[^"\s,}]+',line)
      if urls:provider_base=urls[0].rstrip('/')
      elif event.get('port'):provider_base='http://127.0.0.1:'+str(event['port'])
    except json.JSONDecodeError:pass
   if provider_base:break
   if simulation.poll() is not None:raise AssertionError((scratch/'simulation.err').read_text())
   time.sleep(.1)
  assert provider_base,(scratch/'simulation.ndjson').read_text()
  if not provider_base.endswith('/github'):provider_base+='/github'
  with socket.socket() as sock:sock.bind(('127.0.0.1',0));base='http://127.0.0.1:'+str(sock.getsockname()[1])
  repo=scratch/'repo';repo.mkdir();(repo/'counter.py').write_text('def increment(value):\n    return value\n');(repo/'verify.py').write_text('from pathlib import Path\np=Path("counter.py");p.write_text("def increment(value):\\n    return value + 1\\n")\nassert "return value + 1" in p.read_text()\n')
  for args in [['init','-q'],['config','user.name','Intake fixture'],['config','user.email','intake@example.invalid'],['add','.'],['commit','-qm','Fixture baseline']]:subprocess.run(['git','-C',str(repo),*args],check=True)
  env=os.environ|{'Database__Path':str(scratch/'workspace.duckdb'),'Agents__WorkspaceRoot':str(scratch/'worktrees'),'Agents__PollIntervalMs':'250','Delivery__ApiBaseUrl':provider_base,'Delivery__GitHubHost':'127.0.0.1','Delivery__TokenEnvironmentVariable':'INTAKE_FIXTURE_TOKEN','INTAKE_FIXTURE_TOKEN':'isolated-intake-token','Delivery__MaximumPages':'5','Delivery__RequestTimeoutSeconds':'1','Delivery__MaxResponseBytes':'10000','Intake__MaximumCollectionBytes':'20000','Intake__CollectionTimeoutSeconds':'2'}
  def call(path,method='GET',body=None,expected=200,headers=None):
   req=urllib.request.Request(base+path,method=method,data=None if body is None else json.dumps(body).encode(),headers={'Content-Type':'application/json'}|(headers or {}))
   try:
    with urllib.request.urlopen(req,timeout=30) as r:code,raw=r.status,r.read()
   except urllib.error.HTTPError as e:code,raw=e.code,e.read()
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
   raise AssertionError('API startup timeout')
  start();pid=call('/api/projects','POST',dict(name='Verified intake regression',description='Isolated fixture only',color='#477fa1'))['projects'][0]['id']
  def library():return call('/api/factory/projects/'+pid+'/intake')
  def revision():return next(x['revision'] for x in call('/api/workspace')['projects'] if x['id']==pid)
  def config(kind='githubIssues',**values):return dict(name='GitHub bug intake' if kind=='githubIssues' else 'Recurring defects',kind=kind,enabled=True,repository=fixture['repository'] if kind=='githubIssues' else '',labels=['bug'] if kind=='githubIssues' else [],minimumOccurrences=0 if kind=='githubIssues' else 2,categories=[] if kind=='githubIssues' else ['defect'])|values
  def add(c,expected=200):return call('/api/factory/projects/'+pid+'/intake/sources','POST',dict(expectedRevision=revision(),configuration=c),expected)
  def edit(source,c):return call('/api/factory/intake/sources/'+source['id'],'PUT',dict(expectedRevision=revision(),expectedSourceRevision=source['revision'],configuration=c))
  def collect(source,body=None,expected=200):return call('/api/factory/intake/sources/'+source['id']+'/collections','POST',body or dict(requestId=uid(),expectedSourceRevision=source['revision']),expected)
  def read(c):return call('/api/factory/intake/candidates/'+(c if isinstance(c,str) else c['candidate']['id']))
  def preview(c):return read(c)['preview']
  def body(c,action,**values):return dict(requestId=uid(),expectedRevision=preview(c)['candidate']['revision'],expectedProjectRevision=revision(),action=action,reason='Inspected original evidence and reproduced its described behavior.',taskId=None,statusId=None,taskTitle=None)|values
  def act(c,action,request=None,expected=200,**values):return call('/api/factory/intake/candidates/'+preview(c)['candidate']['id']+'/actions','POST',request or body(c,action,**values),expected)
  check(not library()['sources'] and not library()['candidates'],'fresh projects have no sources or automatic collection')
  add(config(kind='invented'),400);add(config(repository='../outside'),400);add(config('reviewFindings',minimumOccurrences=1),400);add(config('reviewFindings',categories=[]),400)
  source=add(config());add(config(),409)
  call('/api/factory/projects/'+pid+'/intake/sources','POST',dict(expectedRevision=revision(),configuration=config(repository='other/repo'),extra='unknown'),400)
  call('/api/factory/projects/'+pid+'/intake',headers={'Origin':'https://example.invalid'},expected=403)
  check(True,'invalid scopes, recurrence, duplicate configuration, unknown fields and foreign origins are rejected')
  request=dict(requestId=uid(),expectedSourceRevision=source['revision'])
  with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:results=list(pool.map(lambda _:collect(source,request),range(2)))
  collect(source,request|{'expectedSourceRevision':999},409)
  candidates=library()['candidates'];c=candidates[0];cid=c['candidate']['id'];baseline=call('/api/workspace')
  check(results[0]['id']==results[1]['id'] and len(candidates)==1 and c['observation']['signal']['title']=='Counter loses negative values','real paginated adapter excludes PRs, closed issues and wrong labels; exact collection retries share one receipt')
  collect(source);check(len(library()['candidates'])==1 and len(read(c)['observations'])==2,'repeated collection preserves one durable candidate and immutable observations')
  status=next(x['id'] for x in baseline['statuses'] if x['projectId']==pid and not x['isCompleted'])
  act(c,'file',expected=409,statusId=status,taskTitle='Counter loses negative values');act(c,'verify',expected=400,reason='')
  verified=act(c,'verify');check(verified['preview']['candidate']['state']=='verified' and call('/api/workspace')['items']==baseline['items'],'explicit verification records rationale without creating tasks or starting work')
  for mode in ['outage','forbidden','html','hanging','oversized','malformed','invalidIssue','truncated','duplicate']:
   configure(mode=mode);receipt=collect(source)
   check(receipt['state']=='failed' and receipt['error'] and len(library()['candidates'])==1 and not preview(c)['current'],'provider '+mode+' reports unavailable/incomplete coverage without replacing candidate evidence')
  saved_issues=copy.deepcopy(fixture['issues']);configure(mode='normal',issues=[issue(20+n,body='x'*5000) for n in range(5)]);limited=collect(source)
  check(limited['state']=='failed' and 'byte limit' in limited['error'] and len(library()['candidates'])==1,'aggregate evidence byte limit rejects the complete result before any partial candidate mutation')
  configure(mode='slowPages',issues=[issue(30+n) for n in range(5)]);limited=collect(source)
  check(limited['state']=='failed' and len(library()['candidates'])==1,'whole-collection deadline bounds multiple individually responsive provider pages')
  configure(mode='normal',issues=saved_issues);collect(source);check(preview(c)['candidate']['state']=='verified','unchanged fresh evidence retains explicit verification after a failed collection')
  original_version=fixture['apiVersion'];process.terminate();process.wait(timeout=25);env['Delivery__ApiVersion']='2022-11-28';configure(apiVersion='2022-11-28');start()
  collect(source,expected=409);source=edit(source,config());collect(source)
  check(preview(c)['current'],'changed provider version blocks old source authority until explicit source save and fresh collection')
  process.terminate();process.wait(timeout=25);env['Delivery__ApiVersion']=original_version;configure(apiVersion=original_version);start();source=edit(source,config());collect(source)
  changed=issue(1,'Counter loses negative values',body='Updated reproduction evidence',updated_at='2026-01-02T00:00:00Z');configure(issues=[changed])
  act(c,'file',expected=409,statusId=status,taskTitle='Counter loses negative values');collect(source)
  check(preview(c)['candidate']['state']=='proposed','source changes before filing are rechecked and changed evidence revokes verification')
  act(c,'verify');request=body(c,'file',statusId=status,taskTitle='Counter loses negative values')
  with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:files=list(pool.map(lambda _:act(c,'file',request),range(2)))
  taskid=files[0]['preview']['candidate']['taskId'];ws=call('/api/workspace');task=next(x for x in ws['items'] if x['id']==taskid)
  check(files[1]['preview']['candidate']['taskId']==taskid and len(ws['items'])==1 and 'Updated reproduction evidence' in task['specJson'] and task['statusId']==status and task['assigneeIds']==[] and not call('/api/factory/tasks/'+taskid+'/runs'),'concurrent exact filing creates one public-API backlog task with provenance and no execution')
  act(c,'file',request|{'reason':'Different input'},409)
  call('/api/items/'+taskid+'?expectedRevision='+str(revision()),'DELETE');collect(source);act(c,'file',expected=409,statusId=status,taskTitle='Counter loses negative values')
  check(not preview(c)['linkedTaskExists'] and preview(c)['candidate']['taskId']==taskid,'deleting a filed task retains its durable source link and cannot silently recreate it')
  configure(issues=[issue(5,'Existing backlog item'),issue(6,'Needs new triage')]);collect(source);cs=library()['candidates'];c5=next(x for x in cs if x['observation']['signal']['origin'].endswith('#5'));c6=next(x for x in cs if x['observation']['signal']['origin'].endswith('#6'))
  existing=call('/api/projects/'+pid+'/items','POST',dict(expectedRevision=revision(),title='Existing backlog item'))['items'][0];act(c5,'verify');act(c5,'file',expected=409,statusId=status,taskTitle=existing['title'])
  before_item=next(x for x in call('/api/workspace')['items'] if x['id']==existing['id']);act(c5,'link',taskId=existing['id']);check(next(x for x in call('/api/workspace')['items'] if x['id']==existing['id'])==before_item,'exact-title existing work blocks duplicate filing; explicit linking changes no task fields')
  act(c6,'dismiss');collect(source);check(preview(c6)['candidate']['state']=='dismissed','dismissal survives repeated collection');act(c6,'reopen');act(c6,'verify')
  source=edit(source,config(enabled=False));act(c6,'file',expected=409,statusId=status,taskTitle='Needs new triage');collect(source,expected=409)
  source=edit(source,config());act(c6,'file',expected=409,statusId=status,taskTitle='Needs new triage');collect(source)
  check(preview(c6)['current'],'disabled or edited source authority invalidates old coverage until explicit new collection')
  configure(issues=[]);empty=collect(source);check(empty['state']=='complete' and empty['observed']==0 and not preview(c6)['observation']['signal']['available'],'successful empty reads are distinct from failures and mark disappeared candidates unavailable')
  configure(mode='slow',issues=[issue(8,'Scope race')]);request=dict(requestId=uid(),expectedSourceRevision=source['revision'])
  with concurrent.futures.ThreadPoolExecutor(max_workers=1) as pool:
   future=pool.submit(collect,source,request)
   for _ in range(100):
    if any(x['state']=='running' for x in library()['collections']):break
    time.sleep(.01)
   source=edit(source,config(labels=['different-label']));race=future.result()
  check(race['state']=='failed' and not any(x['observation']['signal']['title']=='Scope race' for x in library()['candidates']),'editing source authority during collection rejects the complete result before candidate mutation')
  source=edit(source,config());configure(mode='normal',issues=[issue(9,'Null author and empty body',user=None,body=None)]);collect(source)
  c9=next(x for x in library()['candidates'] if x['observation']['signal']['origin'].endswith('#9'))
  act(c9,'verify');act(c9,'file',expected=400,statusId=None,taskTitle='Null author and empty body')
  completed=call('/api/projects/'+pid+'/statuses','POST',dict(expectedRevision=revision(),name='Completed intake fixture',color='#477fa1'))['statuses'][-1]
  call('/api/statuses/'+completed['id'],'PATCH',dict(expectedRevision=revision(),name=completed['name'],color=completed['color'],isCompleted=True))
  act(c9,'file',expected=400,statusId=completed['id'],taskTitle='Null author and empty body')
  act(c9,'verify',expected=400,taskId=uid());act(c9,'invented',expected=400)
  check(json.loads(preview(c9)['observation']['signal']['evidenceJson'])['author'] is None,'nullable provider authors/bodies are retained accurately; missing/completed columns and unrelated action fields cannot file')
  act(c9,'file',statusId=status,taskTitle='Null author and empty body');filed9=next(x for x in call('/api/workspace')['items'] if x['title']=='Null author and empty body')
  check('"text": ""' not in filed9['specJson'],'empty source body produces a valid rich-text provenance document without empty text nodes')
  # Build actual work orders and submit persisted findings through existing public APIs.
  call('/api/agents/projects/'+pid+'/connection','PUT',dict(expectedRevision=revision(),repositoryPath=str(repo),baseRef='HEAD',provider='codex',instructions='Explicit local fixture; no agent step is used.'))
  definition={'stages':[dict(id='verify',name='Verify',group='Check',dependsOn=[],steps=[dict(id='verify',name='Actual fixture change and assertion',kind='command',command=dict(executable=sys.executable,arguments=['verify.py'],workingDirectory='.',timeoutSeconds=10))])]}
  pipe=call('/api/factory/projects/'+pid+'/pipelines','POST',dict(expectedRevision=revision(),name='Intake evidence fixture',description='Local command only',source='Explicit regression',definition=definition),201)
  version=call('/api/factory/pipelines/'+pipe['pipeline']['id']+'/publish','POST',dict(expectedRevision=revision(),expectedDraftRevision=1))['versions'][0]
  def finding_task(title):
   old={x['id'] for x in call('/api/workspace')['items']};items=call('/api/projects/'+pid+'/items','POST',dict(expectedRevision=revision(),title=title))['items'];tid=next(x['id'] for x in items if x['id'] not in old)
   call('/api/factory/tasks/'+tid+'/configuration','PUT',dict(expectedRevision=revision(),pipelineVersionId=version['id'],overrides={'steps':[]}))
   run=call('/api/factory/tasks/'+tid+'/runs','POST',dict(requestId=uid(),expectedRevision=revision()),201)
   call('/api/factory/runs/'+run['run']['id']+'/start','POST',dict(requestId=uid(),expectedRevision=run['run']['revision']))
   for _ in range(300):
    run=call('/api/factory/runs/'+run['run']['id'])
    if not run['run']['active']:break
    time.sleep(.1)
   assert run['run']['state']=='succeeded',run
   return tid,run
  def review(run,new=None,rechecks=None,verdict='requestChanges'):
   lib=call('/api/factory/runs/'+run['run']['id']+'/reviews');round=call('/api/factory/runs/'+run['run']['id']+'/reviews','POST',dict(requestId=uid(),expectedRevision=lib['runRevision'],expectedContext=lib['context']['fingerprint'],reviewer='Intake fixture reviewer'))
   round=call('/api/factory/reviews/'+round['round']['id']+'/assessment','POST',dict(requestId=uid(),expectedRevision=round['round']['revision'],assessment='Inspected actual code and prior findings independently.',assessmentStepRunId=None))
   return call('/api/factory/reviews/'+round['round']['id']+'/submit','POST',dict(requestId=uid(),expectedRevision=round['round']['revision'],expectedContext=round['packet']['context']['fingerprint'],verdict=verdict,summary='The explicit fixture finding remains actionable.',rechecks=rechecks or [],newFindings=new or []))
  review_source=add(config('reviewFindings'));t1,run1=finding_task('Review fixture one');f1=uid()
  def finding(fid):return dict(id=fid,title='Repeated counter edge case',description='Repeated negative-input assertion gap.',category='defect',severity='high',owner='verification',criterionIds=[],codeReferences=[dict(path='counter.py',line=2)],evidence=[])
  review(run1,[finding(f1)]);review(run1,rechecks=[dict(findingId=f1,status='unfixed',reason='The same finding remains; this is no new occurrence.',codeReferences=[],evidence=[])])
  first=collect(review_source);check(first['observed']==0,'two rounds rechecking one stable finding do not count as recurring problems')
  t2,run2=finding_task('Review fixture two');f2=uid();review(run2,[finding(f2)]);receipt=collect(review_source)
  recurring=next(x for x in library()['candidates'] if x['observation']['signal']['origin'].startswith('Recurring'))
  evidence=json.loads(recurring['observation']['signal']['evidenceJson']);check(receipt['observed']==1 and len(evidence)==2 and {x['findingId'] for x in evidence}=={f1,f2},'distinct submitted findings across two tasks form one recurring candidate with original code/context evidence')
  review(run2,rechecks=[dict(findingId=f2,status='unfixed',reason='Rechecked persistent finding.',codeReferences=[],evidence=[])]);collect(review_source)
  check(len([x for x in library()['candidates'] if x['observation']['signal']['origin'].startswith('Recurring')])==1,'more review rounds preserve recurring identity instead of duplicating candidates')
  act(recurring,'verify');old_request=body(recurring,'dismiss');collect(review_source);act(recurring,'dismiss',old_request,409)
  check(True,'changed observation revision rejects a stale operator decision')
  before_restart=library();detail_before=read(recurring);process.terminate();process.wait(timeout=25);start()
  check(library()==before_restart and read(recurring)==detail_before,'native restart preserves sources, observations, verified state, triage history and tombstone links')
  review(run2,rechecks=[dict(findingId=f2,status='noLongerApplies',reason='Explicitly withdrawn after reviewing this task scope; retain original source history.',codeReferences=[],evidence=[])],verdict='inconclusive')
  act(recurring,'file',expected=409,statusId=status,taskTitle='Recurring counter follow-up');withdrawn=collect(review_source)
  check(withdrawn['observed']==0 and not preview(recurring)['observation']['signal']['available'] and len(read(recurring)['observations'])>1,'withdrawn recurrence cannot be filed from old verification and retains all original observations')
  # Force interruption during a real provider read; startup retains the receipt without replay.
  configure(mode='hanging');request=dict(requestId=uid(),expectedSourceRevision=source['revision'])
  with concurrent.futures.ThreadPoolExecutor(max_workers=1) as pool:
   future=pool.submit(collect,source,request)
   for _ in range(50):
    running=[x for x in library()['collections'] if x['state']=='running']
    if running:break
    time.sleep(.02)
   assert running;process.kill();process.wait(timeout=25)
   try:future.result()
   except (OSError,AssertionError):pass
  configure(mode='normal');start();interrupted=collect(source,request)
  check(interrupted['state']=='interrupted' and not any(x['state']=='running' for x in library()['collections']),'interrupted collection retains unavailable coverage and exact retry never replays provider reads')
  sim_text=(scratch/'simulation.ndjson').read_text();check('request' in sim_text and '/issues' in sim_text,'Postman simulation log proves the real configured HTTP adapter called the consumed dependency')
  if '--serve' in sys.argv:
   configure(issues=[issue(7,'Browser triage example')]);collect(source)
   receipt=Path('/private/tmp/canopy-intake-browser-fixture.json');receipt.write_text(json.dumps(dict(baseUrl=base,projectId=pid,sourceId=source['id'],reviewSourceId=review_source['id'],scratch=str(scratch),providerFixture=str(provider))));receipt.chmod(0o600)
   print('BROWSER FIXTURE '+str(receipt),flush=True)
   while not (scratch/'stop-browser').exists():time.sleep(.5)
  candidate_ids=[x['candidate']['id'] for x in library()['candidates']];call('/api/projects/'+pid+'?expectedRevision='+str(revision()),'DELETE')
  for id in candidate_ids:call('/api/factory/intake/candidates/'+id,expected=404)
  check(not call('/api/workspace')['projects'],'project cleanup removes all intake sources, candidates, receipts and histories through public APIs')
  print(f'{checks} verified intake checks passed',flush=True)
 finally:
  if process and process.poll() is None:process.terminate();process.wait(timeout=25)
  simulation.terminate()
  try:simulation.wait(timeout=10)
  except subprocess.TimeoutExpired:simulation.kill();simulation.wait()
  log.close();sim_log.close();sim_err.close()
