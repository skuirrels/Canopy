#!/usr/bin/env python3
"""Native API/file catalogue and execution-bound skill regression. Isolated, no real model calls."""
import copy,hashlib,json,os,socket,subprocess,sys,tempfile,time,urllib.request,urllib.error,uuid
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];API=ROOT/'server/Planner.Api';checks=0

def check(value,message):
 global checks
 assert value,message
 checks+=1;print(f'PASS {checks:02}: {message}',flush=True)
def uid():return str(uuid.uuid4())
with tempfile.TemporaryDirectory(prefix='canopy-skills-') as directory:
 scratch=Path(directory).resolve();repo=scratch/'repo';repo.mkdir();(repo/'.gitignore').write_text('__pycache__/\n');skills=repo/'skills';skills.mkdir();skill=skills/'counter-style';skill.mkdir();file=skill/'SKILL.md';file.write_text('---\nname: ignored-metadata-identity\n---\n# Counter verification\nUse actual assertions for zero and negative inputs.\n')
 (repo/'counter.py').write_text('def increment(value):\n    return value\n');(repo/'test_counter.py').write_text('from counter import increment\nassert increment(0)==1\nassert increment(-1)==0\n')
 for args in [['init','-q'],['config','user.name','Skill fixture'],['config','user.email','skills@example.invalid'],['add','.'],['commit','-qm','Explicit skill fixture']]:subprocess.run(['git','-C',str(repo),*args],check=True)
 trace=scratch/'trace';fake=scratch/'agent';fake.write_text('''#!/usr/bin/env python3
import json,pathlib,sys
prompt=sys.stdin.read()
assert 'Use actual assertions for zero and negative inputs.' in prompt
assert 'SHA-256:' in prompt and 'Resolve supporting file references against:' in prompt
pathlib.Path('counter.py').write_text('def increment(value):\\n    return value + 1\\n')
print(json.dumps({'type':'thread.started','thread_id':'a93c9b91-2011-4e22-ac90-626998c0ec50'}))
print(json.dumps({'type':'item.completed','item':{'type':'agent_message','text':'Implemented increment; run the actual assertions.'}}))
''');fake.chmod(0o755)
 with socket.socket() as sock:sock.bind(('127.0.0.1',0));base='http://127.0.0.1:'+str(sock.getsockname()[1])
 env=os.environ|{'Database__Path':str(scratch/'workspace.duckdb'),'Agents__WorkspaceRoot':str(scratch/'worktrees'),'Agents__CodexExecutable':str(fake),'Agents__PollIntervalMs':'250','Factory__SkillDiscovery__Roots__0__Source':'Project skill library','Factory__SkillDiscovery__Roots__0__Path':'skills','Factory__SkillDiscovery__Roots__0__MaximumDepth':'2'}
 log=(scratch/'server.log').open('w+');process=None
 def call(path,method='GET',body=None,expected=200,headers=None):
  request=urllib.request.Request(base+path,method=method,data=None if body is None else json.dumps(body).encode(),headers={'Content-Type':'application/json'}|(headers or {}))
  try:
   with urllib.request.urlopen(request,timeout=25) as r:code,raw=r.status,r.read()
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
  raise AssertionError('Startup timeout')
 def restart():
  process.terminate();process.wait(timeout=25);start()
 def revision():return next(x['revision'] for x in call('/api/workspace')['projects'] if x['id']==pid)
 def catalogue():return call('/api/factory/projects/'+pid+'/skills')
 def readiness():return call('/api/factory/tasks/'+tid+'/readiness')
 def prepare():return call('/api/factory/tasks/'+tid+'/runs','POST',dict(requestId=uid(),expectedRevision=revision()),201)
 def execute(run):
  call('/api/factory/runs/'+run['run']['id']+'/start','POST',dict(requestId=uid(),expectedRevision=run['run']['revision']))
  for _ in range(300):
   detail=call('/api/factory/runs/'+run['run']['id'])
   if not detail['run']['active']:return detail
   time.sleep(.1)
  raise AssertionError('Execution timeout')
 try:
  start();pid=call('/api/projects','POST',dict(name='Installed skills regression',description='Isolated fixture',color='#477fa1'))['projects'][0]['id']
  initial=catalogue();check(not initial['complete'] and not initial['entries'] and 'repository' in initial['scopes'][0]['error'],'relative roots require the actual connected repository, not backend cwd fallback')
  call('/api/factory/projects/'+pid+'/skills',headers={'Origin':'https://example.invalid'},expected=403);call('/api/factory/projects/'+uid()+'/skills',expected=404)
  call('/api/agents/projects/'+pid+'/connection','PUT',dict(expectedRevision=revision(),repositoryPath=str(repo),baseRef='HEAD',provider='codex',instructions='Use only captured explicit instructions.'))
  observed=catalogue();entry=observed['entries'][0];original_hash=hashlib.sha256(file.read_bytes()).hexdigest().upper()
  check(observed['complete'] and entry['id']=='counter-style' and entry['source']=='Project skill library' and entry['paths']==[str(file)] and entry['hash']==original_hash and entry['title']=='Counter verification','catalogue derives stable IDs, actual file paths, heading and byte hashes from the configured root')
  before=call('/api/workspace');again=catalogue();check(again['fingerprint']==observed['fingerprint'] and call('/api/workspace')==before,'refresh is read-only and stable content has a deterministic catalogue fingerprint')
  tid=call('/api/projects/'+pid+'/items','POST',dict(expectedRevision=revision(),title='Use discovered skill'))['items'][0]['id'];task=call('/api/workspace')['items'][0]
  spec=json.dumps(dict(type='doc',content=[dict(type='paragraph',content=[dict(type='text',text='Increment every integer. Verify zero and negative inputs with the actual test file.')])]))
  call('/api/items/'+tid,'PATCH',dict(expectedRevision=revision(),title=task['title'],specJson=spec,statusId=task['statusId'],priority='none',assigneeIds=[],dependencyIds=[],startDate=None,dueDate=None))
  definition={'stages':[dict(id='build',name='Build',group='Build',dependsOn=[],steps=[dict(id='implement',name='Implement with selected skill',kind='agent',role='implementation',permission='workspace-write',instructions='Implement the captured task and follow the selected skill.',skills=[dict(id=entry['id'],source=entry['source'])])]),dict(id='check',name='Check',group='QA',dependsOn=['build'],steps=[dict(id='assert',name='Zero and negative assertions',kind='command',command=dict(executable=sys.executable,arguments=['test_counter.py'],workingDirectory='.',timeoutSeconds=10))])]}
  p=call('/api/factory/projects/'+pid+'/pipelines','POST',dict(expectedRevision=revision(),name='Discovered skill fixture',description='Explicit fixture',source='Public catalogue selection',definition=definition),201)
  version=call('/api/factory/pipelines/'+p['pipeline']['id']+'/publish','POST',dict(expectedRevision=revision(),expectedDraftRevision=1))['versions'][0]
  call('/api/factory/tasks/'+tid+'/configuration','PUT',dict(expectedRevision=revision(),pipelineVersionId=version['id'],overrides={'steps':[]}))
  check(readiness()['canPrepare'] and not call('/api/factory/tasks/'+tid+'/runs'),'selecting and publishing a discovered reference prepares no runs and uses the same authoritative resolver')
  run=prepare();check(run['run']['snapshot']['environment']['skills'][0]['hash']==original_hash,'prepared work order captures the same actual selected file hash')
  frozen=call('/api/factory/runs/'+run['run']['id']);file.write_text('# Changed skill\nChanged after capture.\n');changed=catalogue()
  check(changed['entries'][0]['hash']!=original_hash and changed['fingerprint']!=observed['fingerprint'] and call('/api/factory/runs/'+run['run']['id'])==frozen,'changed files refresh observations without rewriting immutable work-order hashes')
  call('/api/factory/runs/'+run['run']['id']+'/start','POST',dict(requestId=uid(),expectedRevision=run['run']['revision']),400)
  check(not call('/api/agents/tasks/'+tid+'/runs'),'changed captured skill blocks dispatch before any agent execution')
  file.write_text('---\nname: ignored-metadata-identity\n---\n# Counter verification\nUse actual assertions for zero and negative inputs.\n')
  # New uncommitted files do not become runtime sources; commit only the explicit fixture repository.
  result=execute(prepare());check(result['run']['state']=='succeeded' and all(x['state']=='succeeded' for x in result['steps']),'actual runtime receives selected skill text/path/hash and executes real zero/negative assertions')
  workspace_before=call('/api/workspace');saved=catalogue();restart();check(catalogue()['fingerprint']==saved['fingerprint'] and call('/api/workspace')==workspace_before,'restart retains task/version state and re-observes the same installed catalogue')
  # Two declarations of the same real file are one location; conflicting paths are never silently overridden.
  env.update(Factory__Skills__0__Id='counter-style',Factory__Skills__0__Source='Project skill library',Factory__Skills__0__Path=str(file));restart();check(len(catalogue()['entries'])==1 and catalogue()['entries'][0]['state']=='available','explicit and discovered references to the same exact file deduplicate')
  other=scratch/'other.md';other.write_text('# Different installed bytes\n');env['Factory__Skills__0__Path']=str(other);restart()
  check(catalogue()['entries'][0]['state']=='ambiguous' and not readiness()['canPrepare'],'conflicting explicit/root files for one ID/source block preparation instead of choosing precedence')
  for key in ['Factory__Skills__0__Id','Factory__Skills__0__Source','Factory__Skills__0__Path']:env.pop(key)
  restart();file.unlink();check(not catalogue()['entries'] and catalogue()['complete'] and not readiness()['canPrepare'],'complete empty root is distinct from missing selected reference and cannot invent skill content')
  file.symlink_to(other);check(catalogue()['entries'][0]['state']=='unavailable' and not readiness()['canPrepare'],'symlinked skill files remain visible but cannot be consumed')
  file.unlink();file.write_bytes(b'\xff\xfe');check(catalogue()['entries'][0]['state']=='unavailable' and not readiness()['canPrepare'],'invalid UTF-8 files cannot be certified installed')
  file.write_text('x'*2000001);check(catalogue()['entries'][0]['state']=='unavailable','oversized skill files use the canonical Markdown limit')
  file.write_text('# Restored skill\nUse actual assertions for zero and negative inputs.\n');link=skills/'redirect';link.symlink_to(scratch,target_is_directory=True)
  check(not catalogue()['complete'] and not readiness()['canPrepare'],'a symlinked directory reports incomplete source coverage and blocks uniqueness claims')
  link.unlink();nested=skill/'nested'/'deep';nested.mkdir(parents=True);(nested/'SKILL.md').write_text('# Nested scope\n')
  check({x['id'] for x in catalogue()['entries']}=={'counter-style'},'declared maximum depth confines the discovery scope without traversing deeper directories')
  env['Factory__SkillDiscovery__Roots__0__Path']='missing-root';restart();missing=catalogue();check(not missing['complete'] and not missing['entries'] and missing['scopes'][0]['error'],'a missing root is unavailable coverage, not a successful empty scan');env['Factory__SkillDiscovery__Roots__0__Path']='skills'
  env['Factory__SkillDiscovery__MaximumDirectories']='1';restart();check(not catalogue()['complete'] and not readiness()['canPrepare'],'configured directory bound reports incomplete coverage without fallback resolution')
  env['Factory__SkillDiscovery__MaximumDirectories']='5000';env['Factory__SkillDiscovery__MaximumEntries']='1';(skills/'second').mkdir();(skills/'second'/'SKILL.md').write_text('# Second skill\n');restart()
  check(not catalogue()['complete'] and not readiness()['canPrepare'],'configured entry bound cannot certify a partial catalogue as unique')
  env['Factory__SkillDiscovery__MaximumEntries']='1000';restart()
  if '--serve' in sys.argv:
   receipt=Path('/private/tmp/canopy-skills-browser-fixture.json');receipt.write_text(json.dumps(dict(baseUrl=base,projectId=pid,taskId=tid,pipelineId=p['pipeline']['id'],versionId=version['id'],scratch=str(scratch),repository=str(repo),skillFile=str(file))));receipt.chmod(0o600);print('BROWSER FIXTURE '+str(receipt),flush=True)
   while not (scratch/'stop-browser').exists():time.sleep(.5)
  call('/api/projects/'+pid+'?expectedRevision='+str(revision()),'DELETE');check(not call('/api/workspace')['projects'],'isolated tasks, versions and work orders clean up through public APIs')
  print(f'{checks} skill discovery checks passed',flush=True)
 finally:
  if process and process.poll() is None:process.terminate();process.wait(timeout=25)
  log.close()
