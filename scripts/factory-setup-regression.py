#!/usr/bin/env python3
"""Real native setup, attachment, revision and persistence checks. No model calls."""
import copy, json, os, socket, subprocess, sys, tempfile, time, urllib.error, urllib.request, uuid
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];API=ROOT/'server/Planner.Api';checks=0
def check(value,message):
 global checks
 assert value,message
 checks+=1;print(f'PASS {checks:02}: {message}',flush=True)
with tempfile.TemporaryDirectory(prefix='canopy-setup-') as temporary:
 scratch=Path(temporary).resolve();repo=scratch/'repo';repo.mkdir()
 (repo/'skills').mkdir()
 (repo/'verify.py').write_text('assert 2 + 2 == 4\n')
 for args in [('init','-q'),('config','user.name','Setup fixture'),('config','user.email','setup@example.invalid'),('add','.'),('commit','-qm','Isolated verification inputs')]:subprocess.run(['git','-C',str(repo),*args],check=True)
 with socket.socket() as sock:sock.bind(('127.0.0.1',0));base='http://127.0.0.1:'+str(sock.getsockname()[1])
 env={k:v for k,v in os.environ.items() if not k.startswith(('Database__','Agents__','Factory__','Delivery__','Intake__'))}
 templates_file=scratch/'templates.json';templates_file.write_bytes((API/'factory-templates.json').read_bytes());env['Factory__TemplatesPath']=str(templates_file)
 env.update(Database__Path=str(scratch/'workspace.duckdb'),Agents__WorkspaceRoot=str(scratch/'worktrees'),Agents__CodexExecutable=sys.executable)
 for i,name in enumerate(('canopy-qa','canopy-review')):
  for key,value in {'Id':name,'Source':'Canopy factory skills','Path':str(ROOT/'.agents/skills'/name/'SKILL.md')}.items():env[f'Factory__Skills__{i}__{key}']=value
 env.update(Factory__SkillDiscovery__Roots__0__Source='Repository skills',Factory__SkillDiscovery__Roots__0__Path='skills',Factory__SkillDiscovery__Roots__0__MaximumDepth='1')
 log=(scratch/'api.log').open('w+');process=None
 def call(path,method='GET',body=None,status=200,headers=None):
  request=urllib.request.Request(base+path,method=method,data=None if body is None else json.dumps(body).encode(),headers={'Content-Type':'application/json'}|(headers or {}))
  try:
   with urllib.request.urlopen(request,timeout=25) as response:code,raw=response.status,response.read()
  except urllib.error.HTTPError as error:code,raw=error.code,error.read()
  value=json.loads(raw) if raw else None
  assert code==status,(path,code,status,value)
  return value
 def start():
  global process
  process=subprocess.Popen(['dotnet','bin/Debug/net10.0/Planner.Api.dll','--urls',base],cwd=API,env=env,stdout=log,stderr=log)
  for _ in range(300):
   if process.poll() is not None:log.seek(0);raise AssertionError(log.read())
   try:
    if call('/api/health')['status']=='ready':return
   except OSError:pass
   time.sleep(.1)
  raise AssertionError('Native startup timeout')
 def revision():return next(p['revision'] for p in call('/api/workspace')['projects'] if p['id']==pid)
 def setup():return call(f'/api/factory/projects/{pid}/setup')
 def body(**changes):return request|{'expectedRevision':revision()}|changes
 def pipelines():return call(f'/api/factory/projects/{pid}/pipelines')['pipelines']
 def config(item):return call(f'/api/factory/tasks/{item}/configuration')
 try:
  start();project=call('/api/projects','POST',{'name':'Factory setup verification','description':'Disposable project','color':'#477fa1'})['projects'][0];pid=project['id'];route=f'/api/factory/projects/{pid}/setup'
  tid=call(f'/api/projects/{pid}/items','POST',{'expectedRevision':revision(),'title':'First explicit task'})['items'][0]['id']
  check(not call(f'/api/factory/projects/{pid}/skills')['complete'],'relative project skill roots expose missing connection before setup')
  initial=call('/api/workspace');detail=setup();template=next(t for t in detail['templates'] if t['id']==detail['defaults']['defaultTemplateId'])
  request={'templateId':template['id'],'name':'Project verification workflow','commands':[{'executable':sys.executable,'arguments':['verify.py'],'workingDirectory':'.','timeoutSeconds':17,'environmentReferences':None}],'skills':{'review':[{'id':'canopy-review','source':'Canopy factory skills'}]},'itemId':tid}
  check(detail['setup'] is None and call('/api/workspace')==initial and not pipelines(),'observing setup changes no planner state and creates no default workflow')
  call(route,'GET',status=403,headers={'Origin':'https://example.invalid'})
  call(route,'PUT',body(),400);check(not pipelines(),'missing connection blocks setup without guessed repository or agent')
  call(f'/api/agents/projects/{pid}/connection','PUT',{'expectedRevision':revision(),'repositoryPath':str(repo),'baseRef':'HEAD','provider':'codex','instructions':'Only explicitly requested work.'})
  check(call(f'/api/factory/projects/{pid}/skills')['complete'],'connecting the actual repository resolves its configured relative skill source')
  for change in [{'commands':[]},{'commands':[request['commands'][0]|{'timeoutSeconds':0}]},{'skills':{'review':[{'id':'unavailable','source':'Canopy factory skills'}]}},{'skills':{'implementation':[{'id':'canopy-review','source':'Wrong source'}]}},{'templateId':'missing'}]:
   call(route,'PUT',body(**change),400)
  check(not pipelines(),'invalid checks, missing skills and unknown templates leave no partial setup')
  preview=call(route+'/preview','POST',body());check(len(preview['stages'])==len(template['definition']['stages']) and not pipelines(),'preview derives the real configured workflow and saves nothing')
  check(any(step.get('command')==request['commands'][0] for stage in preview['stages'] for step in stage['steps']),'preview uses the explicit caller executable, arguments, directory and time bound')
  before=call('/api/workspace');call(route,'PUT',body(itemId=str(uuid.uuid4())),404)
  check(not pipelines() and call('/api/workspace')==before,'failed task attachment rolls back the entire workflow publication transaction')
  first=call(route,'PUT',body());version1=first['version'];check(first['available'] and config(tid)['version']['id']==version1['id'] and len(pipelines())==1,'setup publishes and attaches one reusable project workflow atomically')
  check(first['version']['source']==template['source'],'generated workflow retains actual configured template provenance')
  check(not call(f'/api/factory/tasks/{tid}/runs') and not call(f'/api/agents/tasks/{tid}/runs'),'setup dispatches no command or model work')
  check(call('/api/workspace')['items']==before['items'],'setup preserves task specifications, status, hierarchy, estimates and layout')
  call(route,'PUT',request|{'expectedRevision':before['projects'][0]['revision']},409)
  saved=call(route,'PUT',body());check(saved['version']['id']==version1['id'] and len(pipelines())==1,'same configured inputs reuse the existing version and pipeline')
  tid2=next(i['id'] for i in call(f'/api/projects/{pid}/items','POST',{'expectedRevision':revision(),'title':'Second explicit task'})['items'] if i['id']!=tid)
  call(f'/api/factory/tasks/{tid2}/use-project-setup','POST',{'expectedRevision':revision(),'pipelineVersionId':version1['id']})
  check(config(tid2)['version']['id']==version1['id'] and len(pipelines())==1,'later tasks reuse the project workflow without duplicate pipelines')
  changed=[request['commands'][0],request['commands'][0]|{'arguments':['-m','py_compile','verify.py']}]
  call(route,'PUT',body(commands=changed),409)
  check(setup()['version']['id']==version1['id'],'reconfiguring an already attached task cannot publish a partial replacement')
  updated=call(route,'PUT',body(commands=changed,itemId=None));version2=updated['version']
  check(version2['number']==2 and version2['id']!=version1['id'] and len(pipelines())==1,'changed project checks publish a new immutable version in the same managed pipeline')
  check(config(tid)['version']['id']==version1['id'] and config(tid2)['version']['id']==version1['id'],'updating project setup does not silently move existing task attachments')
  call(f'/api/factory/tasks/{tid2}/use-project-setup','POST',{'expectedRevision':revision(),'pipelineVersionId':version1['id']},409)
  steps=[step for stage in version2['definition']['stages'] for step in stage['steps'] if step['kind']=='command']
  check([step['command'] for step in steps]==changed and len({step['id'] for step in steps})==2,'multiple actual checks preserve order, unique IDs and valid artifact production')
  state=call('/api/workspace');frozen=setup();process.terminate();process.wait(timeout=25);start()
  check(setup()['setup']==frozen['setup'] and setup()['version']==frozen['version'] and call('/api/workspace')==state,'native DuckDB restart retains project setup and exact planner state')
  pipeline=call('/api/factory/pipelines/'+version2['pipelineId'])
  call('/api/factory/pipelines/'+version2['pipelineId']+'/draft','PUT',{'expectedRevision':revision(),'expectedDraftRevision':pipeline['pipeline']['draftRevision'],'name':'Unpublished advanced edit','description':'Owned draft','definition':pipeline['pipeline']['definition']})
  call(route,'PUT',body(itemId=None),409)
  check(call('/api/factory/pipelines/'+version2['pipelineId'])['pipeline']['name']=='Unpublished advanced edit','guided setup cannot overwrite an unpublished advanced draft')
  writing=next(t for t in setup()['templates'] if all(step['kind']!='command' for stage in t['definition']['stages'] for step in stage['steps']))
  docs=call(route,'PUT',body(templateId=writing['id'],commands=[],skills={},itemId=None))
  check(all(step.get('permission')=='read-only' for stage in docs['version']['definition']['stages'] for step in stage['steps'] if step['kind']=='agent'),'documentation/specification workflows require no invented verification commands and retain read-only permissions')
  template_document=json.loads(templates_file.read_text())
  revised=next(t for t in template_document['FactoryTemplates'] if t['id']==writing['id']);revised['source']+=' / explicit fixture revision'
  templates_file.write_text(json.dumps(template_document));process.terminate();process.wait(timeout=25);start()
  revised_setup=call(route,'PUT',body(templateId=writing['id'],commands=[],skills={},itemId=None))
  check(revised_setup['version']['source']==revised['source'] and revised_setup['version']['pipelineId']!=docs['version']['pipelineId'],'configured template source changes create correctly attributed workflows without rewriting historical provenance')
  if '--write-openapi' in sys.argv:
   spec_path=ROOT/'postman/specs/planner.openapi.json';prior=json.loads(spec_path.read_text());current=call('/openapi/v1.json')
   def ordered(prior,current):
    if isinstance(prior,dict) and isinstance(current,dict):return {key:ordered(prior.get(key),current[key]) if key in current else prior[key] for key in list(prior)+[k for k in current if k not in prior] if key in current or key in {'description','summary','example','examples','deprecated','externalDocs'}}
    return current
   current['servers']=[{'url':json.loads((API/'appsettings.json').read_text())['Urls'].rstrip('/')+'/'}]
   spec_path.write_text(json.dumps(ordered(prior,current),indent=2)+'\n')
  if '--serve' in sys.argv:
   # Restore the last published draft for browser setup while retaining test evidence.
   current=call('/api/factory/pipelines/'+version2['pipelineId'])
   call('/api/factory/pipelines/'+version2['pipelineId']+'/draft','PUT',{'expectedRevision':revision(),'expectedDraftRevision':current['pipeline']['draftRevision'],'name':version2['name'],'description':version2['description'],'definition':version2['definition']})
   call('/api/factory/pipelines/'+version2['pipelineId']+'/publish','POST',{'expectedRevision':revision(),'expectedDraftRevision':current['pipeline']['draftRevision']+1})
   browser=call('/api/projects','POST',{'name':'Factory UX browser verification','description':'Disposable browser workspace','color':'#477fa1'});browser_pid=next(p['id'] for p in browser['projects'] if p['id']!=pid)
   revision_browser=next(p['revision'] for p in browser['projects'] if p['id']==browser_pid)
   call(f'/api/agents/projects/{browser_pid}/connection','PUT',{'expectedRevision':revision_browser,'repositoryPath':str(repo),'baseRef':'HEAD','provider':'codex','instructions':'Only explicit work.'})
   browser_workspace=call('/api/workspace');revision_browser=next(p['revision'] for p in browser_workspace['projects'] if p['id']==browser_pid)
   result=call(f'/api/projects/{browser_pid}/items','POST',{'expectedRevision':revision_browser,'title':'Verify guided factory setup'})
   browser_tid=next(i['id'] for i in result['items'] if i['projectId']==browser_pid)
   browser_workspace=call('/api/workspace');revision_browser=next(p['revision'] for p in browser_workspace['projects'] if p['id']==browser_pid);browser_task=next(i for i in browser_workspace['items'] if i['id']==browser_tid)
   call('/api/items/'+browser_tid,'PATCH',{'expectedRevision':revision_browser,'title':browser_task['title'],'specJson':json.dumps({'type':'doc','content':[{'type':'paragraph','content':[{'type':'text','text':'Verify the tracked repository assertion in verify.py and preserve the observed evidence.'}]}]}),'statusId':browser_task['statusId'],'priority':browser_task['priority'],'assigneeIds':[],'dependencyIds':[],'startDate':None,'dueDate':None})
   disconnected=call('/api/projects','POST',{'name':'First connection browser verification','description':'Disposable connection UX','color':'#477fa1'});disconnected_pid=next(p['id'] for p in disconnected['projects'] if p['id'] not in (pid,browser_pid))
   output=Path('/private/tmp/canopy-factory-setup-browser.json');output.write_text(json.dumps({'baseUrl':base,'projectId':browser_pid,'taskId':browser_tid,'repository':str(repo),'scratch':str(scratch),'executable':sys.executable,'disconnectedProjectId':disconnected_pid}));print('BROWSER FIXTURE '+str(output),flush=True)
   while not (scratch/'stop-browser').exists():time.sleep(.5)
   latest=call('/api/workspace');disconnected_rev=next(p['revision'] for p in latest['projects'] if p['id']==disconnected_pid);call(f'/api/projects/{disconnected_pid}?expectedRevision={disconnected_rev}','DELETE')
   latest=call('/api/workspace');rev=next(p['revision'] for p in latest['projects'] if p['id']==browser_pid);call(f'/api/projects/{browser_pid}?expectedRevision={rev}','DELETE')
  call(f'/api/projects/{pid}?expectedRevision={revision()}','DELETE');check(not call('/api/workspace')['projects'],'public project deletion cleans up reusable setup and test-only data')
  call(route,status=404)
  print(str(checks)+' factory setup checks passed',flush=True)
 finally:
  if process and process.poll() is None:process.terminate();process.wait(timeout=25)
  log.close()
