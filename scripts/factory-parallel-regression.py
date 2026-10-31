#!/usr/bin/env python3
"""Isolated public HTTP/native DuckDB + real overlapping processes and Git joins."""
import concurrent.futures
import copy
import signal
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
ROOT=Path(__file__).resolve().parents[1];API=ROOT/'server/Planner.Api';checks=0

def uid():return str(uuid.uuid4())
def check(value,message):
 global checks
 assert value,message
 checks+=1;print(f'PASS {checks:02}: {message}',flush=True)
with tempfile.TemporaryDirectory(prefix='canopy-parallel-') as directory:
 scratch=Path(directory).resolve();repo=scratch/'repo';repo.mkdir();trace=scratch/'trace';trace.mkdir()
 def git(*args):return subprocess.check_output(['git','-C',str(repo),*args],text=True).strip()
 git('init','-q');git('config','user.name','Parallel test caller');git('config','user.email','parallel-fixture@example.invalid')
 (repo/'work.py').write_text('''import json,os,pathlib,sys,time
name=sys.argv[1];trace=pathlib.Path(os.environ['TRACE_DIRECTORY'])
(trace/(name+'-start')).write_text(str(time.time()))
if name in ['a','b']:
 other='b' if name=='a' else 'a'
 assert not pathlib.Path(other+'.txt').exists(),'unrelated sibling code leaked into branch'
 time.sleep(1.5)
 pathlib.Path(name+'.txt').write_text(name+' actual branch output')
else:
 assert pathlib.Path('a.txt').read_text()=='a actual branch output'
 assert pathlib.Path('b.txt').read_text()=='b actual branch output'
(trace/(name+'-end')).write_text(str(time.time()))
print(name+' actual command verified')
''');(repo/'shared.txt').write_text(''.join(f'base {n}\n' for n in range(30)))
 (repo/'remove.bin').write_bytes(bytes(range(256)))
 (repo/'mode.txt').write_text('Caller mode fixture\n')
 git('add','.');git('commit','-qm','Caller fixture base');head=git('rev-parse','HEAD')
 fake=scratch/'fixture-agent'
 fake.write_text('''#!/usr/bin/env python3
import json,os,pathlib,re,sys,time
prompt=sys.stdin.read();name=re.search(r'PARALLEL_AGENT=(\\w+)',prompt).group(1);trace=pathlib.Path(os.environ['PARALLEL_FIXTURE_TRACE'])
(trace/(name+'-pid')).write_text(str(os.getpid()));(trace/(name+'-start')).write_text(str(time.time()))
assert not pathlib.Path(('agenta' if name=='agentb' else 'agentb')+'.txt').exists()
if 'COMMIT_SPLIT' in prompt:
 import subprocess
 pathlib.Path('staged.txt').write_text('caller staged bytes');subprocess.run(['git','add','staged.txt'],check=True)
 subprocess.run(['git','-c','user.name=Caller','-c','user.email=caller@example.invalid','commit','-qm','Actual stage checkpoint'],check=True)
 subprocess.run(['git','update-index','--split-index'],check=True)
 pathlib.Path('staged.txt').write_text('caller unstaged bytes');subprocess.run(['git','add','staged.txt'],check=True)
time.sleep(120 if 'WAIT_OWNED' in prompt else 2)
if 'READ_ONLY_REVIEW' not in prompt:pathlib.Path(name+'.txt').write_text('Actual '+name+' output')
(trace/(name+'-end')).write_text(str(time.time()))
print(json.dumps({'type':'item.completed','item':{'type':'agent_message','text':'Actual fixture result'}}) if sys.argv[1]=='exec' else json.dumps({'type':'result','result':'Actual fixture result','is_error':False}),flush=True)
''');fake.chmod(0o755)
 with socket.socket() as sock:sock.bind(('127.0.0.1',0));base='http://127.0.0.1:'+str(sock.getsockname()[1])
 env=os.environ|{'Database__Path':str(scratch/'workspace.duckdb'),'Agents__WorkspaceRoot':str(scratch/'worktrees'),'Agents__PollIntervalMs':'250','PARALLEL_FIXTURE_TRACE':str(trace),'Agents__CodexExecutable':str(fake),'Agents__ClaudeExecutable':str(fake)}
 process=None;log=(scratch/'api.log').open('w+')
 def call(path,method='GET',body=None,expected=200):
  request=urllib.request.Request(base+path,method=method,data=None if body is None else json.dumps(body).encode(),headers={'Content-Type':'application/json'})
  try:
   with urllib.request.urlopen(request,timeout=30) as response:code,raw=response.status,response.read()
  except urllib.error.HTTPError as error:code,raw=error.code,error.read()
  value=json.loads(raw) if raw else None
  assert code==expected,(path,code,expected,value)
  return value
 def start():
  global process
  process=subprocess.Popen(['dotnet','bin/Debug/net10.0/Planner.Api.dll','--urls',base],cwd=API,env=env,stdout=log,stderr=log)
  for _ in range(250):
   if process.poll() is not None:log.seek(0);raise AssertionError(log.read())
   try:call('/api/health');return
   except OSError:time.sleep(.1)
  raise TimeoutError('Parallel API startup')
 def stop():
  if process and process.poll() is None:process.terminate();process.wait(timeout=30)
 def revision():return next(p['revision'] for p in call('/api/workspace')['projects'] if p['id']==pid)
 def read(id):return call('/api/factory/runs/'+id)
 def wait(id,states=('succeeded','failed','interrupted','cancelled','waiting')):
  until=time.monotonic()+45
  while time.monotonic()<until:
   value=read(id)
   if value['run']['state'] in states:return value
   time.sleep(.1)
  raise AssertionError(read(id))
 def command(id):return dict(id=id,name='Actual '+id,kind='command',command=dict(executable=sys.executable,arguments=['work.py',id],workingDirectory='.',timeoutSeconds=15,environmentReferences={'TRACE_DIRECTORY':'PARALLEL_FIXTURE_TRACE'}))
 def cstep(id,code):return dict(id=id,name=id,kind='command',command=dict(executable=sys.executable,arguments=['-c',code],workingDirectory='.',timeoutSeconds=120))
 def stage(id,steps,deps=None):return dict(id=id,name=id,group='Check',dependsOn=deps or [],steps=steps)
 def manual(id='gate'):return dict(id=id,name=id,kind='manual',instructions='Explicit caller approval of this stage.')
 def agent(name,provider='codex',extra=''):return dict(id=name,name=name,kind='agent',role='implementation',permission='workspace-write',provider=provider,model='caller-protocol-fixture',instructions='PARALLEL_AGENT='+name+' '+extra)
 def adopt(**patch):
  value=dict(enabled=True,routes=[],maxAgentInvocations=10,maxCommandInvocations=20,maxElapsedSeconds=120,allowUnmeteredCost=True,maxConcurrentStages=2)|patch
  return call('/api/factory/projects/'+pid+'/execution-policy','PUT',dict(expectedRevision=revision(),policy=value))
 def prepare(stages):
  draft=call('/api/factory/projects/'+pid+'/pipelines','POST',dict(expectedRevision=revision(),name='Caller parallel case',description='Test only',source='Regression',definition=dict(stages=stages)),201)
  published=call('/api/factory/pipelines/'+draft['pipeline']['id']+'/publish','POST',dict(expectedRevision=revision(),expectedDraftRevision=draft['pipeline']['draftRevision']))
  call('/api/factory/tasks/'+tid+'/configuration','PUT',dict(expectedRevision=revision(),pipelineVersionId=published['versions'][-1]['id'],overrides=dict(steps=[])))
  readiness=call('/api/factory/tasks/'+tid+'/readiness');assert readiness['canPrepare'],readiness['checks']
  return call('/api/factory/tasks/'+tid+'/runs','POST',dict(requestId=uid(),expectedRevision=revision()),201)
 def dispatch(r):return call('/api/factory/runs/'+r['run']['id']+'/start','POST',dict(requestId=uid(),expectedRevision=r['run']['revision']))
 def until(id,predicate):
  deadline=time.monotonic()+45
  while time.monotonic()<deadline:
   r=read(id)
   if predicate(r):return r
   if r['run']['state'] in ('succeeded','failed','cancelled','interrupted'):raise AssertionError(r)
   time.sleep(.1)
  raise TimeoutError(read(id))
 def approve(r,stageid,approved=True):
  sid=next(s['id'] for s in r['stages'] if s['stageId']==stageid);step=next(s for s in r['steps'] if s['stageRunId']==sid and s['state']=='waiting')
  return call('/api/factory/runs/'+r['run']['id']+'/steps/'+step['id']+'/decision','POST',dict(requestId=uid(),expectedRevision=r['run']['revision'],approved=approved,note='Caller reviewed actual fixture evidence'))
 def execute(stages):
  run=prepare(stages);dispatch(run);return wait(run['run']['id'])
 def stopped(pids):
  deadline=time.monotonic()+5
  while any(alive(p) for p in pids) and time.monotonic()<deadline:time.sleep(.1)
  return not any(alive(p) for p in pids)
 def alive(pid):
  result=subprocess.run(['ps','-p',str(pid),'-o','stat='],capture_output=True,text=True)
  return result.returncode==0 and not result.stdout.strip().startswith('Z')
 try:
  start();pid=call('/api/projects','POST',dict(name='Parallel execution regression',description='Isolated public API inputs',color='#477fa1'))['projects'][0]['id']
  call('/api/agents/projects/'+pid+'/connection','PUT',dict(expectedRevision=revision(),repositoryPath=str(repo),baseRef=head,provider='codex',model=None,instructions='Caller fixture scope.'))
  task_state=call('/api/projects/'+pid+'/items','POST',dict(expectedRevision=revision(),title='Caller parallel fixture task'));tid=task_state['items'][0]['id'];task=task_state['items'][0]
  spec=json.dumps(dict(type='doc',content=[dict(type='paragraph',content=[dict(type='text',text='Caller acceptance: verify isolated writes, joins and failures.')])]))
  call('/api/items/'+tid,'PATCH',dict(expectedRevision=revision(),title=task['title'],specJson=spec,statusId=task['statusId'],priority='none',assigneeIds=[],dependencyIds=[],startDate=None,dueDate=None))
  policy=dict(enabled=True,routes=[],maxAgentInvocations=0,maxCommandInvocations=3,maxElapsedSeconds=90,allowUnmeteredCost=False,maxConcurrentStages=2)
  call('/api/factory/projects/'+pid+'/execution-policy','PUT',dict(expectedRevision=revision(),policy=policy))
  definition=dict(stages=[dict(id='a',name='Independent a',group='Build',dependsOn=[],steps=[command('a')]),dict(id='b',name='Independent b',group='Build',dependsOn=[],steps=[command('b')]),dict(id='join',name='Actual dependency join',group='QA',dependsOn=['a','b'],steps=[command('join')])])
  pipeline=call('/api/factory/projects/'+pid+'/pipelines','POST',dict(expectedRevision=revision(),name='Caller parallel pipeline',description='Actual command/worktree branches',source='Caller fixture',definition=definition),201)
  version=call('/api/factory/pipelines/'+pipeline['pipeline']['id']+'/publish','POST',dict(expectedRevision=revision(),expectedDraftRevision=pipeline['pipeline']['draftRevision']))
  call('/api/factory/tasks/'+tid+'/configuration','PUT',dict(expectedRevision=revision(),pipelineVersionId=version['versions'][-1]['id'],overrides=dict(steps=[])))
  ready=call('/api/factory/tasks/'+tid+'/readiness');check(ready['canPrepare'] and not list(trace.iterdir()),'parallel policy/readiness execute nothing')
  run=call('/api/factory/tasks/'+tid+'/runs','POST',dict(requestId=uid(),expectedRevision=revision()),201);id=run['run']['id'];check(not run.get('parallel') and not list(trace.iterdir()),'preparation creates no worktree, snapshot or command')
  call('/api/factory/runs/'+id+'/start','POST',dict(requestId=uid(),expectedRevision=run['run']['revision']));done=wait(id)
  assert done['run']['state']=='succeeded',(done['run']['state'],done['run']['error'],done.get('parallelStages'));check(True,'real parallel branch execution and final integration succeed')
  starts={name:float((trace/(name+'-start')).read_text()) for name in ['a','b','join']};ends={name:float((trace/(name+'-end')).read_text()) for name in ['a','b','join']}
  check(max(starts['a'],starts['b'])<min(ends['a'],ends['b']),'real branch command processes overlap')
  check(starts['join']>max(ends['a'],ends['b']),'dependency join waits for both real branch outputs')
  root=Path(done['run']['workspacePath']);check((root/'a.txt').read_text()=='a actual branch output' and (root/'b.txt').read_text()=='b actual branch output','final root contains both actual branch outputs')
  check(len({p['workspacePath'] for p in done['parallelStages']})==3 and all(Path(p['workspacePath'])!=root for p in done['parallelStages']),'each stage uses its own retained isolated worktree')
  qa=call('/api/factory/runs/'+id+'/qa');join_step=next(s for s in done['steps'] if s['stepId']=='join');receipt=next(r for r in qa['receipts'] if r['stepRunId']==join_step['id']);check(receipt['stable'] and receipt['after']['fingerprint']==qa['context']['fingerprint'],'joined command evidence matches the actual final QA code/index/HEAD context')
  saved=read(id);stop();start();check(read(id)==saved,'parallel input/output snapshots and final context survive native restart without replay')
  adopt()
  d=execute([stage('agenta',[agent('agenta')]),stage('agentb',[agent('agentb','claude')]),stage('join',[cstep('verify',"from pathlib import Path;assert Path('agenta.txt').read_text()=='Actual agenta output';assert Path('agentb.txt').read_text()=='Actual agentb output'")],['agenta','agentb'])])
  check(d['run']['state']=='succeeded' and d['budget']['reservedAgentInvocations']==2,'Codex and Claude protocol fixtures execute real isolated writes under atomic invocation reservations')
  check(max(float((trace/(n+'-start')).read_text()) for n in ['agenta','agentb'])<min(float((trace/(n+'-end')).read_text()) for n in ['agenta','agentb']),'real provider child processes overlap under the shared agent worker')
  d=execute([stage('commit',[agent('commit',extra='COMMIT_SPLIT')])]);branch=d['parallelStages'][0];cp=branch['stepCheckpoints'][0]
  check(d['run']['state']=='succeeded' and cp['context']['headCommit']!=head and Path(d['run']['workspacePath'],'staged.txt').read_text()=='caller unstaged bytes','stage commits, split indexes and staged changes are checkpointed before final HEAD/index normalization')
  check(subprocess.check_output(['git','-C',d['run']['workspacePath'],'diff','--cached'],text=True)=='','final QA worktree retains the initial logical index instead of stage-local staging')

  def edit(line,value):return "from pathlib import Path;p=Path('shared.txt');s=p.read_text().splitlines(True);s["+str(line)+"]="+repr(value+'\n')+";p.write_text(''.join(s))"
  d=execute([stage('left',[cstep('left',edit(0,'left actual'))]),stage('right',[cstep('right',edit(29,'right actual'))])])
  joined=Path(d['run']['workspacePath'],'shared.txt').read_text()
  check(d['run']['state']=='succeeded' and joined.startswith('left actual\n') and joined.endswith('right actual\n'),'nonoverlapping edits of the same tracked file merge from actual Git deltas')
  d=execute([stage('left',[cstep('left',edit(0,'left conflicting'))]),stage('right',[cstep('right',edit(0,'right conflicting'))])])
  check(d['run']['state']=='failed' and 'join failed' in d['run']['error'] and Path(d['run']['workspacePath'],'shared.txt').read_text()==(repo/'shared.txt').read_text(),'content conflict retains completed branches and leaves final QA source unchanged')
  check(len(d['parallelStages'])==2 and all(x['state']=='complete' for x in d['parallelStages']),'successful branch evidence survives an integration failure')
  d=execute([stage('left',[cstep('left',"from pathlib import Path;Path('mode.txt').chmod(0o600)")]),stage('right',[cstep('right',"from pathlib import Path;Path('mode.txt').chmod(0o640)")])])
  check(d['run']['state']=='failed' and 'file-mode conflict' in d['run']['error'],'incompatible full file-mode changes fail visibly without an arbitrary winner')
  d=execute([stage('files',[cstep('files',"from pathlib import Path;Path('remove.bin').unlink();Path('added.bin').write_bytes(bytes(range(255,-1,-1)));Path('mode.txt').rename('renamed.txt');Path('renamed.txt').chmod(0o750)")])])
  root=Path(d['run']['workspacePath'])
  check(d['run']['state']=='succeeded' and not (root/'remove.bin').exists() and not (root/'mode.txt').exists() and (root/'added.bin').read_bytes()==bytes(range(255,-1,-1)) and ((root/'renamed.txt').stat().st_mode&0o777)==0o750,'binary bytes, deletion, rename and full executable permissions integrate intact')

  sibling_started=trace/'gate-sibling-started';sibling_release=trace/'gate-sibling-release'
  slow="import time;from pathlib import Path;Path("+repr(str(sibling_started))+").touch();deadline=time.monotonic()+30\nwhile not Path("+repr(str(sibling_release))+").exists():\n assert time.monotonic()<deadline,'Caller never released the approval fixture'\n time.sleep(.02)\nPath('slow.txt').write_text('sibling completed')"
  r=prepare([stage('gated',[manual(),cstep('after',"from pathlib import Path;Path('approved.txt').write_text('approved')")]),stage('slow',[cstep('slow',slow)])]);dispatch(r)
  r=until(r['run']['id'],lambda d:any(x['state']=='waiting' for x in d['steps']) and d['run']['state']=='running' and sibling_started.exists())
  gate=next(x for x in r['steps'] if x['state']=='waiting');call('/api/factory/runs/'+r['run']['id']+'/steps/'+gate['id']+'/decision','POST',dict(requestId=uid(),expectedRevision=r['run']['revision']-1,approved=True),409)
  check(read(r['run']['id'])==r,'stale parallel approval is rejected without changing the run or gate')
  approve(r,'gated');sibling_release.touch();d=wait(r['run']['id'])
  check(d['run']['state']=='succeeded','stage-local approval while a sibling runs continues without stranding the parent')
  r=prepare([stage('one',[manual()]),stage('two',[manual()])]);dispatch(r);r=wait(r['run']['id']);check(r['run']['state']=='waiting' and len([s for s in r['steps'] if s['state']=='waiting'])==2,'multiple stage gates pause only when no runnable work remains')
  approve(r,'two');r=until(r['run']['id'],lambda d:d['run']['state']=='waiting' and next(s for s in d['stages'] if s['stageId']=='two')['state']=='succeeded');approve(r,'one');d=wait(r['run']['id']);check(d['run']['state']=='succeeded','independent decisions resume only their own stages and completed steps never replay')
  r=prepare([stage('gate',[manual(),cstep('after',"raise Exception('must not run')")])]);dispatch(r);r=wait(r['run']['id']);Path(r['parallelStages'][0]['workspacePath'],'external.txt').write_text('external gate edit');approve(r,'gate');d=wait(r['run']['id']);check(d['run']['state']=='failed' and 'changed while waiting' in d['run']['error'],'worktree mutation while a human gate waits blocks continuation')
  r=prepare([stage('gate',[manual()])]);dispatch(r);r=wait(r['run']['id']);Path(r['run']['workspacePath'],'external.txt').write_text('caller external work');approve(r,'gate');d=wait(r['run']['id']);check(d['run']['state']=='failed' and 'No branch will overwrite' in d['run']['error'] and Path(d['run']['workspacePath'],'external.txt').read_text()=='caller external work','external final-root edits prevent integration and retain caller data')

  child_trace=trace/'command-child-pid'
  child_code="import pathlib,time,os;pathlib.Path("+repr(str(child_trace))+").write_text(str(os.getpid()));time.sleep(120)"
  command_wait="import subprocess,sys,time;p=subprocess.Popen([sys.executable,'-c',"+repr(child_code)+"]);p.wait()"
  r=prepare([stage('gate',[manual()]),stage('ownedcommand',[cstep('ownedcommand',command_wait)])]);dispatch(r)
  r=until(r['run']['id'],lambda d:any(s['state']=='waiting' for s in d['steps']) and child_trace.exists());approve(r,'gate',False);d=wait(r['run']['id'])
  childpid=int(child_trace.read_text());check(d['run']['state']=='failed' and stopped([childpid]),'declined stage gate settles a running sibling command and its descendant before terminal failure')

  r=prepare([stage('output',[cstep('output',"from pathlib import Path;Path('output.txt').write_text('captured')")]),stage('gate',[manual()],['output'])]);dispatch(r);r=wait(r['run']['id']);output=next(x for x in r['stages'] if x['stageId']=='output')
  git('update-ref','refs/canopy/factory/'+r['run']['id'].replace('-','')+'/'+output['id'].replace('-','')+'/output',git('rev-parse','HEAD^{tree}'))
  approve(r,'gate');d=wait(r['run']['id']);check(d['run']['state']=='failed' and 'tree reference changed' in d['run']['error'],'changed private output references block final integration')

  reviewer=agent('review',extra='READ_ONLY_REVIEW');reviewer.update(role='review',permission='read-only')
  d=execute([stage('build',[cstep('write',"from pathlib import Path;Path('reviewed.txt').write_text('actual reviewed change')")]),stage('verify',[cstep('assert',"from pathlib import Path;assert Path('reviewed.txt').read_text()=='actual reviewed change'")],['build']),stage('review',[reviewer],['verify'])])
  evidence=json.loads(next(s['evidence'] for s in d['steps'] if s['stepId']=='review'));library=call('/api/factory/runs/'+d['run']['id']+'/reviews')
  check(d['run']['state']=='succeeded' and evidence['reviewContextStable'] and library['assessments'],'dependency-bound independent review evidence remains eligible only when the actual final context matches')

  # One stage inherits the root output, while an unrelated root remains invisible.
  d=execute([stage('base',[cstep('base',"from pathlib import Path;Path('base.txt').write_text('base')")]),stage('unrelated',[cstep('unrelated',"from pathlib import Path;Path('unrelated.txt').write_text('unrelated')")]),stage('child',[cstep('child',"from pathlib import Path;assert Path('base.txt').exists();assert not Path('unrelated.txt').exists();Path('child.txt').write_text('child')")],['base']),stage('join',[cstep('check',"from pathlib import Path;assert all(Path(n+'.txt').exists() for n in ['base','unrelated','child'])")],['base','child','unrelated'])])
  check(d['run']['state']=='succeeded','transitive joins apply each unique dependency once and exclude earlier unrelated output')

  def waiting_agents():
   r=prepare([stage('waita',[agent('waita',extra='WAIT_OWNED')]),stage('waitb',[agent('waitb','claude','WAIT_OWNED')])]);dispatch(r)
   r=until(r['run']['id'],lambda d:all(s['state']=='running' for s in d['steps']))
   deadline=time.monotonic()+10
   while not all((trace/(n+'-pid')).exists() for n in ['waita','waitb']):
    assert time.monotonic()<deadline;time.sleep(.1)
   return r,[int((trace/(n+'-pid')).read_text()) for n in ['waita','waitb']]
  r,pids=waiting_agents();r=read(r['run']['id']);call('/api/factory/runs/'+r['run']['id']+'/cancel','POST',dict(expectedRevision=r['run']['revision']));d=wait(r['run']['id'])
  check(d['run']['state']=='cancelled' and stopped(pids) and all(s['state']=='cancelled' for s in d['steps']),'cancellation settles both owned provider processes before the parent is terminal')
  for n in ['waita','waitb']:(trace/(n+'-pid')).unlink()
  r,pids=waiting_agents();adopt(enabled=False);d=wait(r['run']['id']);check(d['run']['state']=='failed' and stopped(pids) and 'policy changed' in d['run']['error'].lower(),'policy revocation stops all parallel children and retains failure evidence')
  adopt()
  for n in ['waita','waitb']:(trace/(n+'-pid')).unlink()
  r,pids=waiting_agents();process.kill();process.wait(timeout=30);check(stopped(pids),'abrupt backend death closes both owned process leases');start();d=read(r['run']['id']);check(d['run']['state']=='interrupted' and d['parallel']['state']=='interrupted' and all(not s['state'] in ['running','queued','waiting'] for s in d['steps']),'native startup recovery marks parallel execution interrupted without replay')

  adopt(maxElapsedSeconds=2);r=prepare([stage('gate',[manual()])]);dispatch(r);r=wait(r['run']['id']);d=until(r['run']['id'],lambda d:d['run']['state']=='failed');check('elapsed-time budget' in d['run']['error'],'elapsed budget includes stage-local human approval waits')
  adopt(maxConcurrentStages=5);draft=call('/api/factory/tasks/'+tid+'/readiness');check(not draft['canPrepare'] and any(c['code']=='execution.parallelCapacity' for c in draft['checks']),'unavailable host capacity is exposed in readiness without execution')
  adopt();stop();env['Factory__Parallel__MaximumPatchBytes']='1';start()
  d=execute([stage('bounded',[cstep('bounded',"from pathlib import Path;Path('bounded.txt').write_text('actual oversized patch')")])]);check(d['run']['state']=='failed' and 'configured byte limit' in d['run']['error'] and not Path(d['run']['workspacePath'],'bounded.txt').exists(),'configured patch bound prevents applying an oversized branch delta to the final root')
  stop();del env['Factory__Parallel__MaximumPatchBytes'];env['Factory__Parallel__MaximumIndexBytes']='1';start()
  d=execute([stage('bounded',[cstep('bounded',"raise Exception('must not dispatch')")])]);check(d['run']['state']=='failed' and 'configured byte limit' in d['run']['error'] and d['budget']['reservedCommandInvocations']==0,'configured index bound fails visibly before any command dispatch')
  stop();del env['Factory__Parallel__MaximumIndexBytes'];start()
  adopt(maxConcurrentStages=None);d=execute([stage('serial',[cstep('serial',"print('actual serial command')")])]);check(d['run']['state']=='succeeded' and 'parallel' not in d and 'maxConcurrentStages' not in d['run']['snapshot']['execution']['policy'],'absent concurrency retains serial contract and omits new policy fields')
  if '--postman' in sys.argv:subprocess.run(['postman','collection','run',str(ROOT/'postman/collections/Factory parallel regression'),'--env-var',f'baseUrl={base}','--env-var',f'projectId={pid}','--env-var',f'parallelRunId={id}','--env-var','fixtureMode=isolated','--report-events=false'],cwd=ROOT,check=True)
  call('/api/projects/'+pid+'?expectedRevision='+str(revision()),'DELETE');check(not call('/api/workspace')['projects'],'test-only project and native histories clean up through public APIs')
  print(f'Parallel execution regression: {checks} checks passed.',flush=True)
 except Exception:
  log.flush();log.seek(0);print(log.read()[-18000:],file=sys.stderr);raise
 finally:stop();log.close()
