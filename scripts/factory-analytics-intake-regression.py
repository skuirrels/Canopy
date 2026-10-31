#!/usr/bin/env python3
"""Configured real metric snapshots, public HTTP APIs and native DuckDB; no model calls."""
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

def uid():return str(uuid.uuid4())
def stamp(value):return value.isoformat().replace('+00:00','Z')
def check(value,message):
 global checks
 assert value,message
 checks+=1;print(f'PASS {checks:02}: {message}',flush=True)
with tempfile.TemporaryDirectory(prefix='canopy-analytics-') as directory:
 scratch=Path(directory).resolve();feed=scratch/'metrics.json';marker=scratch/'model-was-called';fake=scratch/'provider-fixture'
 fake.write_text('#!/bin/sh\ntouch "'+str(marker)+'"\nexit 99\n');fake.chmod(0o755)
 with socket.socket() as sock:sock.bind(('127.0.0.1',0));base='http://127.0.0.1:'+str(sock.getsockname()[1])
 env=os.environ|{'Database__Path':str(scratch/'workspace.duckdb'),'Agents__WorkspaceRoot':str(scratch/'worktrees'),'Agents__CodexExecutable':str(fake),'Agents__ClaudeExecutable':str(fake),'Intake__MaximumAnalyticsMetrics':'8','Intake__MaximumAnalyticsSignals':'2','Intake__MaximumCollectionBytes':'12000','Intake__CollectionTimeoutSeconds':'2','Intake__MaximumFutureSkewSeconds':'2'}
 threshold='9007199254740993.125'
 definition=dict(Id='metrics-fixture',Name='Caller metric source',SourceId=uid(),Application='Analytics fixture application',Environment='Isolated verification',Path=str(feed),MaxAgeSeconds=3600,MaxWindowSeconds=600,DimensionNames=['route'],Rules=[dict(Id='latency-rule',Name='Caller latency threshold',Condition=dict(MetricId='fixture.latency',Unit='milliseconds',Statistic='p95',Comparison='atLeast',Threshold=threshold,MinimumSamples='20')),dict(Id='conversion-rule',Name='Caller conversion threshold',Condition=dict(MetricId='fixture.conversion',Unit='ratio',Statistic='mean',Comparison='below',Threshold='0.1',MinimumSamples='10'))])
 process=None;log=(scratch/'api.log').open('w+')
 def configure(definitions):
  for key in list(env):
   if key.startswith('Intake__AnalyticsFeeds__'):del env[key]
  def fields(prefix,value):
   if isinstance(value,dict):
    for key,item in value.items():fields(prefix+'__'+key,item)
   elif isinstance(value,list):
    for i,item in enumerate(value):fields(prefix+'__'+str(i),item)
   else:env[prefix]=str(value)
  for i,value in enumerate(definitions):fields('Intake__AnalyticsFeeds__'+str(i),value)
 def call(path,method='GET',body=None,expected=200,headers=None):
  request=urllib.request.Request(base+path,method=method,data=None if body is None else json.dumps(body).encode(),headers={'Content-Type':'application/json'}|(headers or {}))
  try:
   with urllib.request.urlopen(request,timeout=25) as response:code,raw=response.status,response.read()
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
  raise TimeoutError('Isolated analytics API startup')
 def stop():
  if process and process.poll() is None:process.terminate();process.wait(timeout=30)
 def restart():stop();start()
 def revision():return next(p['revision'] for p in call('/api/workspace')['projects'] if p['id']==pid)
 def library():return call(f'/api/factory/projects/{pid}/intake')
 def source():return next(s for s in library()['sources'] if s['id']==sid)
 def config(**patch):return dict(name='Caller analytics source',kind='analytics',enabled=True,repository='',labels=[],categories=[],minimumOccurrences=0,analyticsFeedId=definition['Id'],analyticsRuleIds=['latency-rule'])|patch
 def save_source(**patch):return call('/api/factory/intake/sources/'+sid,'PUT',dict(expectedRevision=revision(),expectedSourceRevision=source()['revision'],configuration=source()['configuration']|patch))
 def collect(source_id=None,state='complete',body=None):
  s=source() if source_id is None else next(s for s in library()['sources'] if s['id']==source_id)
  batch=call('/api/factory/intake/sources/'+s['id']+'/collections','POST',body or dict(requestId=uid(),expectedSourceRevision=s['revision']))
  until=time.monotonic()+15
  while batch['state']=='running' and time.monotonic()<until:
   time.sleep(.05);batch=next(b for b in library()['collections'] if b['id']==batch['id'])
  assert batch['state']==state,batch
  return batch
 def detail(id=None):return call('/api/factory/intake/candidates/'+(id or candidate_id))
 def action(kind,id=None,expected=200,**patch):
  d=detail(id);body=dict(requestId=uid(),expectedRevision=d['preview']['candidate']['revision'],expectedProjectRevision=revision(),action=kind,reason='Inspected actual upstream aggregation and relevance.',taskId=None,statusId=None,taskTitle=None)|patch
  return call('/api/factory/intake/candidates/'+d['preview']['candidate']['id']+'/actions','POST',body,expected)
 def snapshot():
  now=dt.datetime.now(dt.timezone.utc);metric=dict(id='series-a',metricId='fixture.latency',name='Caller latency measurement',unit='milliseconds',statistic='p95',value=threshold,samples='20',dimensions=[dict(name='route',value='caller-route')])
  return dict(schemaVersion=1,reportId=uid(),sourceId=definition['SourceId'],application=definition['Application'],environment=definition['Environment'],generatedAt=stamp(now-dt.timedelta(seconds=1)),windowStart=stamp(now-dt.timedelta(seconds=120)),windowEnd=stamp(now-dt.timedelta(seconds=2)),complete=True,sanitized=True,metrics=[metric,metric|dict(id='low-samples',value='9007199254740994',samples='19'),metric|dict(id='missing-value',samples='0',value=None),metric|dict(id='conversion-series',name='Caller conversion measurement',metricId='fixture.conversion',unit='ratio',statistic='mean',value='0.05',samples='100')])
 def publish(value):
  if feed.exists() or feed.is_symlink():feed.unlink()
  path=feed.with_suffix('.next');path.write_text(json.dumps(value));path.replace(feed)
 def coverage(batch):return next(r['coverage'] for r in library()['analyticsReceipts'] if r['id']==batch['id'])
 def failed(value,error_contains=None):
  before=detail();candidates=[{k:v for k,v in p.items() if k!='current'} for p in library()['candidates']];receipts=library()['analyticsReceipts'];publish(value);batch=collect(state='failed');after=detail()
  check(before['observations']==after['observations'] and before['decisions']==after['decisions'] and library()['analyticsReceipts']==receipts and [{k:v for k,v in p.items() if k!='current'} for p in library()['candidates']]==candidates and not after['preview']['current'] and batch['error'] and (not error_contains or error_contains in batch['error']),'failed analytics coverage preserves native evidence and revokes current authority')
 try:
  configure([]);start();pid=call('/api/projects','POST',dict(name='Analytics intake regression',description='Test-only public API inputs',color='#477fa1'))['projects'][0]['id'];catalogue_path=f'/api/factory/projects/{pid}/intake/analytics-feeds';sources_path=f'/api/factory/projects/{pid}/intake/sources'
  check(call(catalogue_path)['feeds']==[] and not library()['sources'],'new deployment has no invented analytics source, threshold or runtime metric')
  call(sources_path,'POST',dict(expectedRevision=revision(),configuration=config()),503)
  configure([definition]);restart();catalogue=call(catalogue_path)
  check(catalogue['supported'] and catalogue['feeds'][0]['rules'][0]['condition']['threshold']==threshold and 'path' not in json.dumps(catalogue).lower(),'catalogue preserves exact configured decimal policy and scope without browser filesystem paths')
  call(sources_path,'POST',dict(expectedRevision=revision(),configuration=config()),403,{'Origin':'https://example.invalid'})
  for invalid in [config(analyticsFeedId=None),config(analyticsRuleIds=[]),config(analyticsRuleIds=['missing-rule']),config(analyticsRuleIds=[' latency-rule']),config(analyticsFeedId=' metrics-fixture'),config(minimumOccurrences=1),config(repository='unrelated/repository'),config(labels=['bug']),config(categories=['defect']),config(levels=['error']),config(path=str(feed))]:call(sources_path,'POST',dict(expectedRevision=revision(),configuration=invalid),400)
  check(not library()['sources'],'invalid filters, missing rules, unknown fields and foreign origins cannot create source authority')
  sid=call(sources_path,'POST',dict(expectedRevision=revision(),configuration=config()))['id'];call(sources_path,'POST',dict(expectedRevision=revision(),configuration=config(name='Duplicate scope')),409)
  missing=collect(state='failed');check(missing['error'] and not library()['candidates'],'missing metric file is unavailable coverage rather than zero measurements')
  good=snapshot();publish(good);body=dict(requestId=uid(),expectedSourceRevision=source()['revision'])
  with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:batches=list(pool.map(lambda _:collect(body=body),range(2)))
  c=coverage(batches[0]);candidate_id=library()['candidates'][0]['candidate']['id']
  check(batches[0]['id']==batches[1]['id'] and len(library()['candidates'])==1,'exact concurrent collection retries retain one result and stable candidate')
  check(c['reportedSeries']==4 and c['selectedSeries']==3 and c['availableSeries']==2 and c['qualifyingSignals']==1,'counts distinguish rule selection, insufficient samples and unavailable values')
  check(c['fileHash']==hashlib.sha256(feed.read_bytes()).hexdigest().upper() and c['bytes']==feed.stat().st_size and c['reportId']==good['reportId'],'native receipt retains actual complete file hash, bytes, scope, immutable report and window')
  evidence=json.loads(detail()['preview']['observation']['signal']['evidenceJson']);check(evidence['metric']['value']==threshold and evidence['rule']['condition']['threshold']==threshold and evidence['metric']['samples']=='20','measurement, samples and rule thresholds stay exact beyond JavaScript safe integers')
  status=next(s['id'] for s in call('/api/workspace')['statuses'] if s['projectId']==pid and not s['isCompleted']);action('file',expected=409,statusId=status,taskTitle='Unverified threshold must not file')
  check(not call('/api/workspace')['items'] and not marker.exists(),'crossing a metric threshold neither proves a defect nor files work or invokes an agent')
  if '--serve' in sys.argv:
   context=dict(baseUrl=base,projectId=pid,sourceId=sid,candidateId=candidate_id,scratch=str(scratch));Path('/private/tmp/analytics-ui-context.json').write_text(json.dumps(context));print(json.dumps(context),flush=True)
   while not (scratch/'continue').exists():time.sleep(.25)
   good=snapshot();publish(good);collect()
  action('verify');verified=detail()['preview']['candidate']['verifiedHash'];collect();check(detail()['preview']['candidate']['verifiedHash']==verified,'unchanged complete rereads preserve verification without multiplying measurements')
  same=copy.deepcopy(good);same['reportId']=uid();same['generatedAt']=stamp(dt.datetime.now(dt.timezone.utc)-dt.timedelta(milliseconds=100));publish(same);action('file',expected=409,statusId=status,taskTitle='New report needs collection');collect();check(detail()['preview']['candidate']['state']=='verified','new export identity needs collection while identical metric/window evidence remains verified')
  changed=copy.deepcopy(good);changed['reportId']=uid();changed['metrics'][0]['value']='9007199254740993.126';publish(changed);collect();check(detail()['preview']['candidate']['state']=='proposed','changed actual metric value revokes earlier verification')
  action('verify');d=detail();body=dict(requestId=uid(),expectedRevision=d['preview']['candidate']['revision'],expectedProjectRevision=revision(),action='file',reason='Verified aggregation provenance and application relevance.',statusId=status,taskId=None,taskTitle='Explicit analytics backlog task')
  with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:filed=list(pool.map(lambda _:call('/api/factory/intake/candidates/'+candidate_id+'/actions','POST',body),range(2)))
  task_id=filed[0]['preview']['candidate']['taskId'];task=next(t for t in call('/api/workspace')['items'] if t['id']==task_id)
  check(task_id==filed[1]['preview']['candidate']['taskId'] and len(call('/api/workspace')['items'])==1 and task['statusId']==status and candidate_id in task['specJson'] and 'fixture.latency' in task['specJson'],'concurrent exact filing creates one explicitly placed task with immutable analytic evidence references')
  check(not call('/api/agents/tasks/'+task_id+'/runs') and not call('/api/factory/tasks/'+task_id+'/runs'),'filing analytics grants no model, pipeline or acceptance authority')
  call('/api/items/'+task_id+'?expectedRevision='+str(revision()),'DELETE');action('file',expected=409,statusId=status,taskTitle='Duplicate after deletion');check(detail()['preview']['candidate']['taskId']==task_id and not detail()['preview']['linkedTaskExists'],'durable source link prevents refiling even after linked task deletion')
  saved=library();saved_detail=detail();restart();check(library()==saved and detail()==saved_detail,'native report identity, receipt, source, observations and decisions survive restart')
  conflict=copy.deepcopy(good);conflict['metrics'][0]['value']='9007199254740994';failed(conflict,'report ID')
  variants=[good|dict(complete=False),good|dict(sanitized=False),good|dict(schemaVersion=2),good|dict(sourceId='other-source'),good|dict(application='other-app'),good|dict(environment='other-environment'),good|dict(rawContext={'authorization':'NEVER_RETAIN_'+uid()}),good|dict(generatedAt=stamp(dt.datetime.now(dt.timezone.utc)-dt.timedelta(hours=2))),good|dict(generatedAt=stamp(dt.datetime.now(dt.timezone.utc)+dt.timedelta(hours=1))),good|dict(windowStart=good['windowEnd'],windowEnd=good['windowStart'])]
  for field,value in [('value',9007199254740993.125),('value','1e3'),('value','0.10000000000000000000000000001'),('value','79228162514264337593543950336'),('value',None),('samples','0'),('samples','-1'),('samples','01'),('samples',20),('samples','9223372036854775808'),('unit','unknown-unit'),('statistic','unknown-statistic'),('metricId','unknown-metric'),('id',' series-a')]:
   invalid=copy.deepcopy(good);invalid['metrics'][0][field]=value;variants.append(invalid)
  invalid=copy.deepcopy(good);invalid['metrics'][0]['dimensions']=[dict(name='raw-request',value='must not accept')];variants.append(invalid)
  invalid=copy.deepcopy(good);invalid['metrics'][0]['dimensions']*=2;variants.append(invalid)
  invalid=copy.deepcopy(good);invalid['metrics'].append(copy.deepcopy(invalid['metrics'][0]));variants.append(invalid)
  invalid=copy.deepcopy(good);invalid['metrics']=[invalid['metrics'][0]|dict(id='series-'+str(i)) for i in range(9)];variants.append(invalid)
  invalid=copy.deepcopy(good);del invalid['complete'];variants.append(invalid)
  invalid=copy.deepcopy(good);del invalid['metrics'][2]['value'];variants.append(invalid)
  invalid=copy.deepcopy(good);invalid['generatedAt']=invalid['generatedAt'].removesuffix('Z');variants.append(invalid)
  for invalid in variants:failed(invalid)
  publish(good);feed.write_text(json.dumps(good).replace('"complete": true','"complete": false, "complete": true'));collect(state='failed');check(not detail()['preview']['current'],'duplicate JSON completion fields cannot conceal incomplete coverage')
  feed.write_text('{ truncated');collect(state='failed');feed.write_text(' '*12001);collect(state='failed');check(not detail()['preview']['current'],'truncated and oversized files never become complete empty coverage')
  feed.unlink();os.mkfifo(feed);began=time.monotonic();collect(state='failed');check(time.monotonic()-began<3,'configured FIFO cannot block open or pretend to be a complete metric report');feed.unlink()
  target=scratch/'actual.json';target.write_text(json.dumps(good));feed.symlink_to(target);collect(state='failed');check(not detail()['preview']['current'],'snapshot symlinks are unavailable rather than followed');feed.unlink()
  empty=copy.deepcopy(good)|dict(reportId=uid(),metrics=[]);publish(empty);batch=collect();check(coverage(batch)['reportedSeries']==0 and not detail()['preview']['observation']['signal']['available'] and detail()['preview']['candidate']['state']=='filed','complete empty window retains coverage without marking disappeared problems fixed')
  unavailable=copy.deepcopy(good)|dict(reportId=uid());unavailable['metrics']=[unavailable['metrics'][0]|dict(value=None,samples='0')];publish(unavailable);batch=collect();check(coverage(batch)['selectedSeries']==1 and coverage(batch)['availableSeries']==0 and coverage(batch)['qualifyingSignals']==0,'explicit zero-sample measurement stays unavailable and cannot become a zero-valued signal')
  fanout=copy.deepcopy(definition);fanout['Rules']=[fanout['Rules'][0]|dict(Id='fanout-'+str(i)) for i in range(3)];configure([fanout]);restart();save_source(analyticsRuleIds=[r['Id'] for r in fanout['Rules']]);report=snapshot();report['metrics']=report['metrics'][:1];failed(report,'signal limit')
  byte_fanout=copy.deepcopy(definition);byte_fanout['Rules']=[byte_fanout['Rules'][0]|dict(Id='byte-fanout-'+str(i),Name='Explicit fixture rule '+('R'*170)) for i in range(2)];report=snapshot();report['metrics']=report['metrics'][:1];env['Intake__MaximumCollectionBytes']=str(len(json.dumps(report).encode())+1);configure([byte_fanout]);restart();save_source(analyticsRuleIds=[r['Id'] for r in byte_fanout['Rules']]);failed(report,'byte limit');env['Intake__MaximumCollectionBytes']='12000'
  # Threshold boundaries come from configured rules and real exact source values.
  for comparison,expected in [('above',[False,False,True]),('atLeast',[False,True,True]),('below',[True,False,False]),('atMost',[True,True,False])]:
   adjusted=copy.deepcopy(definition);adjusted['Rules'][1]['Condition']['Comparison']=comparison;configure([adjusted]);restart();save_source(analyticsRuleIds=['conversion-rule'])
   for value,qualifies in zip(['0.0999999999999999999999999999','0.1','0.1000000000000000000000000001'],expected):
    report=snapshot();report['metrics']=[report['metrics'][3]|dict(value=value)];publish(report);result=collect();check((coverage(result)['qualifyingSignals']==1)==qualifies,'exact threshold boundary '+comparison+' '+value)
  configure([definition]);restart();save_source(analyticsRuleIds=['latency-rule','conversion-rule']);good=snapshot();publish(good);collect();check(len([p for p in library()['candidates'] if p['current']])==2,'explicit rule subset changes eligibility while preserving distinct stable rule/series candidates')
  conversion_id=next(p['candidate']['id'] for p in library()['candidates'] if p['candidate']['id']!=candidate_id);action('verify',id=conversion_id)
  existing=call(f'/api/projects/{pid}/items','POST',dict(expectedRevision=revision(),title='Existing analytics fixture work',statusId=status));existing_task=next(t for t in existing['items'] if t['projectId']==pid)
  action('link',id=conversion_id,taskId=existing_task['id']);check(next(t for t in call('/api/workspace')['items'] if t['id']==existing_task['id'])==existing_task,'explicit analytic linking preserves existing task metadata')
  before_candidates={p['candidate']['id'] for p in library()['candidates']};rebound=copy.deepcopy(definition);rebound['Rules'][0]['Condition']['MetricId']='fixture.rebound';configure([rebound]);restart();save_source();report=snapshot();report['metrics']=[report['metrics'][0]|dict(metricId='fixture.rebound')];publish(report);collect();check(len(library()['candidates'])==len(before_candidates)+1 and detail()['preview']['candidate']['taskId']==task_id,'rebinding a retained rule ID to another metric cannot overwrite its prior source-linked problem')
  configure([definition]);restart();save_source();before_count=len(library()['candidates']);report=snapshot();report['metrics']=[report['metrics'][0]|dict(dimensions=[dict(name='route',value='another-caller-route')])];publish(report);collect();check(len(library()['candidates'])==before_count+1,'reused upstream series IDs with different canonical dimensions keep distinct problem identities')
  expiring=copy.deepcopy(definition)|dict(MaxAgeSeconds=5);configure([expiring]);restart();save_source();publish(snapshot());collect();time.sleep(5.2);check(not detail()['preview']['current'],'receipt age expiry revokes authority without needing a new collection')
  configure([definition]);restart();save_source();good=snapshot();publish(good);collect()
  # Same report ID is isolated by the actual configured application source scope.
  second=scratch/'second.json';second_definition=copy.deepcopy(definition)|dict(Id='second-feed',SourceId=uid(),Application='Second caller application',Environment='Another environment',Path=str(second));second_report=copy.deepcopy(good)|dict(sourceId=second_definition['SourceId'],application=second_definition['Application'],environment=second_definition['Environment']);second.write_text(json.dumps(second_report));configure([definition,second_definition]);restart()
  before_count=len(library()['candidates']);second_sid=call(sources_path,'POST',dict(expectedRevision=revision(),configuration=config(name='Second metric scope',analyticsFeedId='second-feed')))['id'];collect(source_id=second_sid);check(len(library()['candidates'])==before_count+1,'identical report and series IDs from distinct configured application scopes do not collide')
  if '--postman' in sys.argv:subprocess.run(['postman','collection','run',str(ROOT/'postman/collections/Factory analytics intake regression'),'--env-var',f'baseUrl={base}','--env-var',f'projectId={pid}','--env-var',f'sourceId={sid}','--env-var',f'candidateId={candidate_id}','--env-var','fixtureMode=isolated','--report-events=false'],cwd=ROOT,check=True)
  save_source(enabled=False);call('/api/factory/intake/sources/'+sid+'/collections','POST',dict(requestId=uid(),expectedSourceRevision=source()['revision']),409)
  # Unsupported retained selections remain disableable after deployment rule changes.
  adjusted=copy.deepcopy(definition);adjusted['Rules']=adjusted['Rules'][:1];configure([adjusted,second_definition]);restart();save_source(enabled=False);check(not source()['configuration']['enabled'],'removed rule selection can be disabled while preserving captured historical binding')
  configure([]);restart();save_source(enabled=False);check(not source()['configuration']['enabled'] and not call(catalogue_path)['feeds'],'removed feed can be disabled without losing native evidence')
  check(not marker.exists(),'analytics collection, evidence, threshold checks and triage never invoke a model')
  call(f'/api/projects/{pid}?expectedRevision={revision()}','DELETE');check(not call('/api/workspace')['projects'],'public project deletion cleans analytic receipts, report ledger, candidates and test work')
  print(f'Factory analytics intake regression: {checks} checks passed.',flush=True)
 except Exception:
  log.flush();log.seek(0);print(log.read()[-18000:],file=sys.stderr);raise
 finally:stop();log.close()
