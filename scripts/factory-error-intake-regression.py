#!/usr/bin/env python3
"""Public HTTP + real structured files + native DuckDB. No model/provider calls."""
import concurrent.futures
import copy
import datetime as dt
import hashlib
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

def check(value,message):
 global checks
 assert value,message
 checks+=1;print(f'PASS {checks:02}: {message}',flush=True)
def uid():return str(uuid.uuid4())
def stamp(value):return value.isoformat().replace('+00:00','Z')
with tempfile.TemporaryDirectory(prefix='canopy-error-feed-') as directory:
 scratch=Path(directory).resolve();feed=scratch/'application-errors.json';marker=scratch/'model-was-called'
 fake=scratch/'provider-fixture';fake.write_text('#!/bin/sh\ntouch "'+str(marker)+'"\nexit 99\n');fake.chmod(0o755)
 with socket.socket() as sock:sock.bind(('127.0.0.1',0));base='http://127.0.0.1:'+str(sock.getsockname()[1])
 env=os.environ|{'Database__Path':str(scratch/'workspace.duckdb'),'Agents__WorkspaceRoot':str(scratch/'worktrees'),'Agents__CodexExecutable':str(fake),'Agents__ClaudeExecutable':str(fake),'Agents__PollIntervalMs':'250','Intake__MaximumErrorEvents':'20','Intake__MaximumCollectionBytes':'20000','Intake__CollectionTimeoutSeconds':'2','Intake__MaximumFutureSkewSeconds':'2'}
 process=None;log=(scratch/'api.log').open('w+')
 source_identity=uid();feed_definition=dict(Id='fixture-feed',Name='Fixture error snapshot',SourceId=source_identity,Application='Caller fixture application',Environment='Isolated regression',Path=str(feed),Levels=['error','critical','warning'],MaxAgeSeconds=3600,MaxWindowSeconds=600)
 def configure_feeds(definitions):
  for key in list(env):
   if key.startswith('Intake__ErrorFeeds__'):del env[key]
  for i,definition in enumerate(definitions):
   for k,v in definition.items():
    if isinstance(v,list):
     for j,value in enumerate(v):env[f'Intake__ErrorFeeds__{i}__{k}__{j}']=str(value)
    else:env[f'Intake__ErrorFeeds__{i}__{k}']=str(v)
 def call(path,method='GET',body=None,expected=200,headers=None):
  req=urllib.request.Request(base+path,method=method,data=None if body is None else json.dumps(body).encode(),headers={'Content-Type':'application/json'}|(headers or {}))
  try:
   with urllib.request.urlopen(req,timeout=25) as response:code,raw=response.status,response.read()
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
 def stop():
  if process and process.poll() is None:process.terminate();process.wait(timeout=30)
 def restart():stop();start()
 def revision():return next(p['revision'] for p in call('/api/workspace')['projects'] if p['id']==pid)
 def library():return call(f'/api/factory/projects/{pid}/intake')
 def source():return next(s for s in library()['sources'] if s['id']==sid)
 def config(**changes):return dict(name='Application error fixture',kind='applicationErrors',enabled=True,repository='',labels=[],categories=[],minimumOccurrences=2,errorFeedId='fixture-feed',levels=['error'])|changes
 def save_source(**changes):
  s=source();return call('/api/factory/intake/sources/'+sid,'PUT',dict(expectedRevision=revision(),expectedSourceRevision=s['revision'],configuration=s['configuration']|changes))
 def collect(source_id=None,expected_state='complete',body=None):
  s=source() if source_id is None else next(x for x in library()['sources'] if x['id']==source_id)
  result=call('/api/factory/intake/sources/'+s['id']+'/collections','POST',body or dict(requestId=uid(),expectedSourceRevision=s['revision']))
  until=time.monotonic()+15
  while result['state']=='running' and time.monotonic()<until:
   time.sleep(.05);result=next(x for x in library()['collections'] if x['id']==result['id'])
  assert result['state']==expected_state,result
  return result
 def detail(id=None):return call('/api/factory/intake/candidates/'+(id or candidate_id))
 def action(kind,id=None,expected=200,**patch):
  d=detail(id);body=dict(requestId=uid(),expectedRevision=d['preview']['candidate']['revision'],expectedProjectRevision=revision(),action=kind,reason='Verified the actual source reproduction and intended scope.',taskId=None,statusId=None,taskTitle=None)|patch
  return call('/api/factory/intake/candidates/'+d['preview']['candidate']['id']+'/actions','POST',body,expected)
 def snapshot():
  now=dt.datetime.now(dt.timezone.utc);generation=uid();event=dict(id='event-a-'+generation,occurredAt=stamp(now-dt.timedelta(seconds=30)),level='error',release='fixture-release')
  group=dict(id='error-group-a',title='Explicit fixture failure',exceptionType='FixtureFailure',messageTemplate='The configured fixture operation failed.',frames=[dict(path='src/handler.py',symbol='handle',line=7)],occurrences=[event,copy.deepcopy(event),event|dict(id='event-b-'+generation)],code='FIXTURE_CODE')
  return dict(schemaVersion=1,sourceId=source_identity,application=feed_definition['Application'],environment=feed_definition['Environment'],generatedAt=stamp(now-dt.timedelta(seconds=1)),windowStart=stamp(now-dt.timedelta(seconds=120)),windowEnd=stamp(now-dt.timedelta(seconds=2)),complete=True,sanitized=True,groups=[group,group|dict(id='warning-group',title='Unselected warning',occurrences=[event|dict(id='warning-a-'+generation,level='warning')])])
 def publish(value):
  if feed.exists() or feed.is_symlink():feed.unlink()
  next_file=feed.with_suffix('.next');next_file.write_text(json.dumps(value));next_file.replace(feed)
 def coverage(batch):return next(x['coverage'] for x in library()['errorFeedReceipts'] if x['id']==batch['id'])
 def failed(value):
  persisted=lambda values:[{k:v for k,v in item.items() if k!='current'} for item in values]
  before=persisted(library()['candidates']);receipts=library()['errorFeedReceipts'];observations=detail()['observations'];decisions=detail()['decisions'];publish(value);batch=collect(expected_state='failed');after=library()
  check(persisted(after['candidates'])==before and after['errorFeedReceipts']==receipts and detail()['observations']==observations and detail()['decisions']==decisions and batch['error'] and all(not item['current'] for item in after['candidates']),'failed coverage preserves persisted candidates/receipts and revokes current triage authority')
  return batch
 try:
  configure_feeds([]);start();pid=call('/api/projects','POST',dict(name='Structured error intake regression',description='Test-only API inputs',color='#477fa1'))['projects'][0]['id']
  catalogue_path=f'/api/factory/projects/{pid}/intake/error-feeds';sources_path=f'/api/factory/projects/{pid}/intake/sources'
  check(call(catalogue_path)['feeds']==[] and not library()['sources'],'no error feed or runtime event is fabricated for a new deployment')
  call(sources_path,'POST',dict(expectedRevision=revision(),configuration=config()),503)
  configure_feeds([feed_definition]);restart();cat=call(catalogue_path)
  check(cat['supported'] and cat['feeds'][0]['identity']['sourceId']==source_identity and 'path' not in json.dumps(cat).lower(),'catalogue supplies configured scope and levels without accepting or exposing a browser file path')
  call(sources_path,'POST',dict(expectedRevision=revision(),configuration=config()),403,{'Origin':'https://example.invalid'})
  invalid=[config(errorFeedId=None),config(levels=[]),config(levels=['unknown']),config(minimumOccurrences=0),config(repository='unrelated/repo'),config(categories=['defect']),config(labels=['bug']),config(arbitraryPath=str(feed))]
  for value in invalid:call(sources_path,'POST',dict(expectedRevision=revision(),configuration=value),400)
  check(not library()['sources'],'invalid scope, filters, thresholds and unknown fields create no source authority')
  sid=call(sources_path,'POST',dict(expectedRevision=revision(),configuration=config()))['id'];call(sources_path,'POST',dict(expectedRevision=revision(),configuration=config(name='Duplicate scope')),409)
  s=source();call('/api/factory/intake/sources/'+sid,'PUT',dict(expectedRevision=revision(),expectedSourceRevision=s['revision'],configuration=config(errorFeedId='another-feed')),503)
  check(len(library()['sources'])==1 and not marker.exists(),'one source per configured scope; adding it performs no model or collection work')
  batch=collect(expected_state='failed');check(not library()['candidates'] and batch['error'],'missing file is unavailable coverage, not a successful empty collection')
  good=snapshot();publish(good);body=dict(requestId=uid(),expectedSourceRevision=source()['revision'])
  with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:results=list(pool.map(lambda _:collect(body=body),range(2)))
  check(results[0]['id']==results[1]['id'] and len(library()['candidates'])==1,'exact concurrent collection retries capture one complete result and candidate')
  batch=results[0];c=coverage(batch);candidate_id=library()['candidates'][0]['candidate']['id'];d=detail()
  check(c['rawEvents']==4 and c['distinctEvents']==3 and c['selectedEvents']==2 and c['eligibleGroups']==1,'counts derive from distinct event IDs and explicit selected levels')
  check(c['fileHash']==hashlib.sha256(feed.read_bytes()).hexdigest().upper() and c['bytes']==feed.stat().st_size and c['identity']['environment']==feed_definition['Environment'],'native coverage retains actual file bytes/hash, window and configured source identity')
  e=json.loads(d['preview']['observation']['signal']['evidenceJson']);check(len(e['group']['occurrences'])==2 and e['group']['frames'][0]['path']=='src/handler.py','candidate evidence retains the actual group, frame, event and release identities')
  action('file',expected=409,statusId=next(s['id'] for s in call('/api/workspace')['statuses'] if s['projectId']==pid and not s['isCompleted']),taskTitle='Do not file unverified evidence')
  check(not call('/api/workspace')['items'],'complete source collection is not defect verification or automatic task filing')
  if '--serve' in sys.argv:
   context=dict(baseUrl=base,projectId=pid,sourceId=sid,candidateId=candidate_id,scratch=str(scratch));Path('/private/tmp/error-intake-ui-context.json').write_text(json.dumps(context));print(json.dumps(context),flush=True)
   while not (scratch/'continue').exists():time.sleep(.25)
   good=snapshot();publish(good);collect()
  action('verify');verified=detail()['preview']['candidate'];collect();check(detail()['preview']['candidate']['state']=='verified' and detail()['preview']['candidate']['verifiedHash']==verified['verifiedHash'],'unchanged rereads preserve verified evidence and do not add error occurrences')
  same=copy.deepcopy(good);same['generatedAt']=stamp(dt.datetime.now(dt.timezone.utc)-dt.timedelta(milliseconds=100));publish(same);action('file',expected=409,statusId=next(s['id'] for s in call('/api/workspace')['statuses'] if s['projectId']==pid and not s['isCompleted']),taskTitle='Changed snapshot needs collection')
  collect();check(detail()['preview']['candidate']['state']=='verified','fresh export metadata requires collection but does not invent a changed error or revoke unchanged facts')
  changed=copy.deepcopy(good);changed['groups'][0]['occurrences'].append(changed['groups'][0]['occurrences'][0]|dict(id='event-c'));publish(changed);collect();check(detail()['preview']['candidate']['state']=='proposed' and len(json.loads(detail()['preview']['observation']['signal']['evidenceJson'])['group']['occurrences'])==3,'a distinct new error event invalidates previous verification')
  action('verify');d=detail();status=next(s for s in call('/api/workspace')['statuses'] if s['projectId']==pid and not s['isCompleted']);body=dict(requestId=uid(),expectedRevision=d['preview']['candidate']['revision'],expectedProjectRevision=revision(),action='file',reason='Confirmed this source-backed fixture defect needs repair.',statusId=status['id'],taskId=None,taskTitle='Explicit verified error task')
  with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:filed=list(pool.map(lambda _:call('/api/factory/intake/candidates/'+candidate_id+'/actions','POST',body),range(2)))
  task_id=filed[0]['preview']['candidate']['taskId'];task=next(t for t in call('/api/workspace')['items'] if t['id']==task_id)
  check(task_id==filed[1]['preview']['candidate']['taskId'] and len(call('/api/workspace')['items'])==1 and task['statusId']==status['id'],'concurrent exact filing retries create one task in the explicitly selected incomplete column')
  check(candidate_id in task['specJson'] and 'error-group-a' in task['specJson'] and 'signal SHA-256' in task['specJson'] and 'full producer message template' in task['specJson'] and not call('/api/agents/tasks/'+task_id+'/runs') and not call('/api/factory/tasks/'+task_id+'/runs'),'filed task retains source and immutable evidence references without any agent or factory execution')
  call('/api/factory/intake/candidates/'+candidate_id+'/actions','POST',body|dict(reason='Conflicting replay rationale'),409)
  call('/api/items/'+task_id+'?expectedRevision='+str(revision()),'DELETE');action('file',expected=409,statusId=status['id'],taskTitle='Duplicate after deletion')
  check(not detail()['preview']['linkedTaskExists'] and detail()['preview']['candidate']['taskId']==task_id,'a deleted task keeps its durable source link and cannot be filed again')
  saved=library();saved_detail=detail();restart();check(library()==saved and detail()==saved_detail,'native source, coverage, observations and filing history survive backend restart intact')
  # Lifetime identity conflicts must fail even after a native restart and when
  # the later snapshot is internally consistent (no within-file collision).
  history=copy.deepcopy(good)
  for event in history['groups'][0]['occurrences']:
   if event['id']==good['groups'][0]['occurrences'][0]['id']:event['release']='rewritten-release'
  failed(history)
  reassigned=copy.deepcopy(good);moved=copy.deepcopy(reassigned['groups'][0]['occurrences'][0]);reassigned['groups'][0]['occurrences']=[e for e in reassigned['groups'][0]['occurrences'] if e['id']!=moved['id']];reassigned['groups'].append(reassigned['groups'][0]|dict(id='another-owner',occurrences=[moved]));failed(reassigned)
  # Every invalid snapshot must preserve all existing observations and receipts.
  variants=[good|dict(complete=False),good|dict(sanitized=False),good|dict(schemaVersion=999),good|dict(sourceId='another-source'),good|dict(application='another-application'),good|dict(environment='another-environment'),good|dict(rawContext={'authorization':'NEVER_PERSIST_'+uid()}),good|dict(generatedAt=stamp(dt.datetime.now(dt.timezone.utc)+dt.timedelta(hours=1))),good|dict(windowStart=good['windowEnd'],windowEnd=good['windowStart'])]
  old=copy.deepcopy(good);old['generatedAt']=stamp(dt.datetime.now(dt.timezone.utc)-dt.timedelta(hours=2));variants.append(old)
  collision=copy.deepcopy(good);collision['groups'][0]['occurrences'][1]['release']='conflicting-event-content';variants.append(collision)
  collision=copy.deepcopy(good);collision['groups'][1]['occurrences'][0]['id']=good['groups'][0]['occurrences'][0]['id'];variants.append(collision)
  variants.append(good|dict(groups=[good['groups'][0],good['groups'][0]]))
  bad_frame=copy.deepcopy(good);bad_frame['groups'][0]['frames'][0]['path']='../outside.py';variants.append(bad_frame)
  too_many=copy.deepcopy(good);too_many['groups'][0]['occurrences']=[good['groups'][0]['occurrences'][0]|dict(id='event-'+str(i)) for i in range(21)];variants.append(too_many)
  missing=copy.deepcopy(good);del missing['complete'];variants.append(missing)
  missing=copy.deepcopy(good);del missing['groups'][0]['occurrences'][0]['occurredAt'];variants.append(missing)
  offsetless=copy.deepcopy(good);offsetless['generatedAt']=offsetless['generatedAt'].removesuffix('Z');variants.append(offsetless)
  variants.append(good|dict(schemaVersion='1'))
  ambiguous=copy.deepcopy(good);ambiguous['groups'][0]['occurrences'][0]['id']=' '+good['groups'][0]['occurrences'][0]['id'];variants.append(ambiguous)
  variants.append(good|dict(windowStart=stamp(dt.datetime.now(dt.timezone.utc)-dt.timedelta(hours=1))))
  for value in variants:failed(value)
  publish(good);raw=json.dumps(good);feed.write_text(raw.replace('\"complete\": true','\"complete\": false, \"complete\": true'));collect(expected_state='failed');check(not detail()['preview']['current'],'duplicate JSON fields cannot conceal conflicting completion authority')
  publish(good);feed.write_text('{ incomplete');collect(expected_state='failed');check(not detail()['preview']['current'],'malformed/truncated JSON revokes usable coverage without pretending the group disappeared')
  feed.write_text(' ' * 20001);collect(expected_state='failed');check(not detail()['preview']['current'],'raw byte bounds reject oversized coverage without truncation')
  feed.unlink();os.mkfifo(feed);began=time.monotonic();collect(expected_state='failed');check(time.monotonic()-began<3,'a configured FIFO cannot block file open or masquerade as a complete snapshot');feed.unlink()
  target=scratch/'real-feed.json';target.write_text(json.dumps(good));feed.symlink_to(target);collect(expected_state='failed');check(not detail()['preview']['current'],'symbolic-link feed files cannot bypass configured snapshot ownership');feed.unlink()
  empty=copy.deepcopy(good);empty['groups']=[];publish(empty);empty_batch=collect();check(coverage(empty_batch)['eligibleGroups']==0 and not detail()['preview']['observation']['signal']['available'] and detail()['preview']['candidate']['state']=='filed','complete empty windows retain coverage and make old groups ineligible without marking work fixed')
  publish(good);collect();check(len(library()['candidates'])==1,'reappearing stable group IDs reuse the durable candidate rather than duplicating work')
  save_source(enabled=False)
  call('/api/factory/intake/sources/'+sid+'/collections','POST',dict(requestId=uid(),expectedSourceRevision=source()['revision']),409)
  check(not marker.exists() and not call('/api/workspace')['items'],'collection, triage, failures and restart never trigger a model or hidden task execution')
  save_source(enabled=True)
  # Real wall-clock receipt expiry affects previews before any further triage.
  configure_feeds([feed_definition|dict(MaxAgeSeconds=5)]);restart();call('/api/factory/intake/sources/'+sid+'/collections','POST',dict(requestId=uid(),expectedSourceRevision=source()['revision']),409)
  save_source();publish(snapshot());collect();time.sleep(5.2);check(not detail()['preview']['current'],'complete receipt expiry blocks triage even without a new collection')
  configure_feeds([feed_definition]);restart();save_source();publish(snapshot());collect()
  second_file=scratch/'second-errors.json';second_definition=feed_definition|dict(Id='fixture-second-feed',Name='Second configured feed',SourceId=uid(),Application='Second caller application',Environment='Another explicit environment',Path=str(second_file))
  configure_feeds([feed_definition,second_definition]);restart()
  second=json.loads(feed.read_text())|dict(sourceId=second_definition['SourceId'],application=second_definition['Application'],environment=second_definition['Environment']);second_file.write_text(json.dumps(second))
  second_sid=call(sources_path,'POST',dict(expectedRevision=revision(),configuration=config(name='Second error scope',errorFeedId=second_definition['Id'])))['id'];collect(source_id=second_sid)
  second_candidate=next(x for x in library()['candidates'] if x['candidate']['id']!=candidate_id)
  second_id=second_candidate['candidate']['id'];check(len(library()['candidates'])==2 and second_candidate['candidate']['fingerprint']!=detail()['preview']['candidate']['fingerprint'],'same upstream group/event IDs in different configured application scopes cannot collide')
  action('verify',id=second_id)
  existing_state=call(f'/api/projects/{pid}/items','POST',dict(expectedRevision=revision(),title='Existing fixture work',statusId=status['id']));existing_id=next(t['id'] for t in existing_state['items'] if t['projectId']==pid)
  prior_projects={p['id'] for p in call('/api/workspace')['projects']};other=call('/api/projects','POST',dict(name='Other isolated project',description='Cross-project authority test',color='#477fa1'));other_id=next(p['id'] for p in other['projects'] if p['id'] not in prior_projects)
  other_revision=next(p['revision'] for p in other['projects'] if p['id']==other_id);other_status=next(s['id'] for s in other['statuses'] if s['projectId']==other_id and not s['isCompleted']);other=call(f'/api/projects/{other_id}/items','POST',dict(expectedRevision=other_revision,title='Different project task',statusId=other_status));other_task=next(t['id'] for t in other['items'] if t['projectId']==other_id)
  action('link',id=second_id,expected=404,taskId=other_task)
  check(detail(second_id)['preview']['candidate']['taskId'] is None,'error intake cannot link work from a different project')
  before_task=next(t for t in call('/api/workspace')['items'] if t['id']==existing_id);action('link',id=second_id,taskId=existing_id)
  check(detail(second_id)['preview']['candidate']['taskId']==existing_id and next(t for t in call('/api/workspace')['items'] if t['id']==existing_id)==before_task,'explicit linking retains source evidence without changing existing task metadata')
  other_revision=next(p['revision'] for p in call('/api/workspace')['projects'] if p['id']==other_id);call(f'/api/projects/{other_id}?expectedRevision={other_revision}','DELETE')
  if '--postman' in sys.argv:subprocess.run(['postman','collection','run',str(ROOT/'postman/collections/Factory error intake regression'),'--env-var',f'baseUrl={base}','--env-var',f'projectId={pid}','--env-var',f'sourceId={sid}','--env-var',f'candidateId={candidate_id}','--env-var','fixtureMode=isolated','--report-events=false'],cwd=ROOT,check=True)
  configure_feeds([feed_definition|dict(Levels=['warning']),second_definition]);restart();save_source(enabled=False);check(not source()['configuration']['enabled'],'a source with a removed selected level remains disableable without adopting unsupported authority')
  configure_feeds([]);restart();check(not detail()['preview']['current'] and not call(catalogue_path)['feeds'],'removing a configured feed exposes unavailable authority without erasing its history')
  save_source(enabled=False);check(not source()['configuration']['enabled'],'a removed feed can be disabled while preserving its captured binding and evidence')
  call(f'/api/projects/{pid}?expectedRevision={revision()}','DELETE');check(not call('/api/workspace')['projects'],'public project deletion cleans test sources, receipts, observations and tasks')
  print(f'Factory error intake regression: {checks} checks passed.')
 except Exception:
  log.flush();log.seek(0);print(log.read()[-18000:],file=sys.stderr);raise
 finally:
  stop();log.close()
