"""Additional real native/API/CLI calibration cases using the existing delivery simulation."""
import concurrent.futures
import json
import os
from pathlib import Path
import subprocess
import sys
import time
import uuid


def install_fixture(env, scratch, executable):
    root = scratch / 'canonical skills'; root.mkdir()
    calibrator = root / 'calibrator' / 'SKILL.md'; calibrator.parent.mkdir(); calibrator.write_text('# Caller installed calibrator\nUse only current delivery evidence and the supplied mirror targets.\n')
    targets = []
    for index in range(2):
        path = root / ('target-' + str(index)) / 'SKILL.md'; path.parent.mkdir(); path.write_text('# Caller target ' + str(index) + '\nKeep actual verification evidence.\n'); targets.append(path)
    case = scratch / 'calibration-case.json'; case.write_text(json.dumps({'mode': 'write', 'marker': 'Initial caller fixture lesson'}))
    trace = scratch / 'calibration-trace.jsonl'
    env.update({'CANOPY_CALIBRATION_CASE': str(case), 'CANOPY_CALIBRATION_TRACE': str(trace), 'Agents__ClaudeExecutable': str(executable), 'Factory__Calibration__EditableRoots__0': str(root)})
    for index, path in enumerate([calibrator, *targets]):
        env.update({f'Factory__Skills__{index}__Id': 'calibrator' if index == 0 else 'target-' + str(index - 1), f'Factory__Skills__{index}__Source': 'Calibration fixture', f'Factory__Skills__{index}__Path': str(path)})
    source = executable.read_text()
    code = '''
if 'Task: Post-delivery skill calibration' in prompt:
 import os,time
 config=json.load(open(os.environ['CANOPY_CALIBRATION_CASE']));mode=config['mode'];cwd=pathlib.Path.cwd()
 packet=json.loads(pathlib.Path('.canopy-calibration/evidence.json').read_text())
 with open(os.environ['CANOPY_CALIBRATION_TRACE'],'a') as trace:trace.write(json.dumps(dict(pid=os.getpid(),cwd=str(cwd),mode=mode,args=sys.argv[1:],packet=packet['id']))+'\\n')
 if mode=='wait':
  child=subprocess.Popen([sys.executable,'-c','import time;time.sleep(120)'])
  pathlib.Path(config['childPid']).write_text(str(child.pid));time.sleep(120)
 if mode!='noChange' and mode!='wait':
  target=pathlib.Path('.canopy-calibration/targets/0/SKILL.md');target.write_text(target.read_text()+'\\n'+config['marker']+'\\n')
 if mode=='both':
  target=pathlib.Path('.canopy-calibration/targets/1/SKILL.md');target.write_text(target.read_text()+'\\n'+config['marker']+'\\n')
 if mode=='product':pathlib.Path('counter.py').write_text('Unexpected product edit')
 if mode=='ignored':pathlib.Path('__pycache__').mkdir(exist_ok=True);pathlib.Path('__pycache__/unexpected').write_text('Unexpected ignored file')
 if mode=='directory':pathlib.Path('unexpected-empty-directory').mkdir()
 if mode=='fifo':pathlib.Path('__pycache__').mkdir(exist_ok=True);os.mkfifo('__pycache__/unexpected-pipe')
 if mode=='mirrorFifo':pathlib.Path('.canopy-calibration/targets/0/SKILL.md').unlink();os.mkfifo('.canopy-calibration/targets/0/SKILL.md')
 if mode=='gitconfig':subprocess.run(['git','config','core.hooksPath','unexpected-hooks'],check=True)
 if mode=='packet':pathlib.Path('.canopy-calibration/evidence.json').write_text('{}')
 if mode=='delete':pathlib.Path('.canopy-calibration/targets/0/SKILL.md').unlink()
 if mode=='permission':pathlib.Path('.canopy-calibration/targets/0/SKILL.md').chmod(0o600)
 if mode=='canonical':pathlib.Path(config['canonical']).write_text('Unexpected canonical escape')
 if mode=='commit':subprocess.run(['git','add','.'],check=True);subprocess.run(['git','-c','user.name=Caller calibration fixture','-c','user.email=calibration@example.invalid','commit','-qm','Unpermitted calibration commit'],check=True)
 if mode=='reference':subprocess.run(['git','update-ref','refs/canopy/calibration/'+cwd.name.split('.')[0].replace('-','')+'/input','HEAD^{tree}'],check=True)
 if mode=='huge':pathlib.Path('.canopy-calibration/targets/0/SKILL.md').write_text('x'*2100000)
 report='Actual isolated calibration fixture report'
 if sys.argv[1]=='exec':print(json.dumps({'type':'item.completed','item':{'type':'agent_message','text':report}}),flush=True)
 else:print(json.dumps({'type':'result','result':report,'is_error':False}),flush=True)
 sys.exit(0)
'''
    source = source.replace("prompt=sys.stdin.read();purpose=", "prompt=sys.stdin.read()\n" + code + "\npurpose=")
    executable.write_text(source)
    return dict(root=root, calibrator=calibrator, targets=targets, case=case, trace=trace)


def run(ns, delivery):
    call, check = ns['call'], ns['check']; fixture = ns['calibration_fixture']; pid = ns['pid']; revision = ns['revision']; did = delivery['delivery']['id']; policy_path = f'/api/factory/projects/{pid}/calibration-policy'; library_path = f'/api/factory/deliveries/{did}/calibrations'; serial = 0
    def uid(): return str(uuid.uuid4())
    def rows(): return [json.loads(line) for line in fixture['trace'].read_text().splitlines()] if fixture['trace'].exists() else []
    def configure(mode='write', **extra): fixture['case'].write_text(json.dumps(dict(mode=mode, marker='Caller evidence-based lesson '+uid()) | extra))
    def policy(**changes):
        nonlocal serial
        serial += 1
        return dict(enabled=True, autoRun=False, autoApply=False, calibrator=dict(id='calibrator', source='Calibration fixture'), targets=[dict(id='target-0', source='Calibration fixture'), dict(id='target-1', source='Calibration fixture')], execution=dict(enabled=True, routes=[dict(role='documentation', provider='codex', model='caller-calibration-model-'+str(serial), effort=None)], maxAgentInvocations=1, maxCommandInvocations=0, maxElapsedSeconds=90, allowUnmeteredCost=True)) | changes
    def adopt(value): return call(policy_path, 'PUT', dict(expectedRevision=revision(), policy=value))
    def prepare(expected=201): return call(library_path, 'POST', dict(requestId=uid(), expectedRevision=delivery['delivery']['revision']), expected)
    def read(job): return call('/api/factory/calibrations/'+job['id'])
    def act(job, action, expected=200, **patch): return call('/api/factory/calibrations/'+job['id']+'/actions', 'POST', dict(requestId=uid(), expectedRevision=job['revision'], action=action, proposalHash=job['proposalHash']) | patch, expected)
    def wait(job, states=None):
        deadline=time.monotonic()+45
        while time.monotonic()<deadline:
            value=read(job)
            if states and value['state'] in states or not states and not value['active']: return value
            time.sleep(.1)
        raise AssertionError(read(job))
    def execute(mode='write', provider='codex', **extra):
        configure(mode, **extra); p=policy();p['execution']['routes'][0]['provider']=provider;adopt(p);job=prepare();act(job,'start');return wait(job)
    def restart():
        ns['process'].terminate();ns['process'].wait(timeout=30);ns['start']()
    def alive(child):
        result=subprocess.run(['ps','-p',str(child),'-o','stat='],capture_output=True,text=True);return result.returncode==0 and not result.stdout.strip().startswith('Z')
    def stopped(children):
        deadline=time.monotonic()+5
        while any(alive(child) for child in children) and time.monotonic()<deadline:time.sleep(.1)
        return not any(alive(child) for child in children)
    initial=call(policy_path);before=rows();workspace=call('/api/workspace')
    check(initial['policy'] is None and initial['editableScopeConfigured'] and initial['processLifetimeSupported'], 'calibration has no default policy or invented installed calibrator')
    disabled=call(library_path);check(not disabled['readiness']['ready'] and not disabled['jobs'] and rows()==before, 'disabled calibration reads execute nothing and expose the actual gap')
    p=policy();adopt(p);ready=call(library_path)
    assert ready['readiness']['ready'],ready
    check(rows()==before and call('/api/workspace')['items']==workspace['items'], 'saving explicit scope and reading verified delivery eligibility preserve tasks and invoke no model')
    # Publishing branch cleanup must not erase immutable reviewed-head check evidence.
    subprocess.run(['git','--git-dir='+str(ns['bare']),'update-ref','-d','refs/heads/'+delivery['delivery']['capture']['branchName']],check=True)
    cleaned=call(library_path);assert cleaned['readiness']['ready'],cleaned
    check(True, 'deleted merged publication branch retains fresh check and merge evidence by reviewed commit')
    subprocess.run(['git','--git-dir='+str(ns['bare']),'update-ref','refs/heads/main',ns['baseline_sha']],check=True)
    check(not call(library_path)['readiness']['ready'],'a historical delivered state cannot substitute for current target ancestry')
    subprocess.run(['git','--git-dir='+str(ns['bare']),'update-ref','refs/heads/main',delivery['delivery']['remote']['mergeSha']],check=True)
    ns['configure'](mode='missingCheck');gap=call(library_path);check(not gap['readiness']['ready'] and not gap['jobs'] and rows()==before,'missing live check evidence blocks calibration without source fabrication or dispatch');ns['configure'](mode='normal')
    body=dict(requestId=uid(), expectedRevision=delivery['delivery']['revision']);call(library_path,'POST',body|{'unexpected':True},400);call(library_path,'POST',body,403,{'Origin':'https://example.invalid'})
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:jobs=list(pool.map(lambda _:call(library_path,'POST',body,201),range(2)))
    job=jobs[0];check(job['id']==jobs[1]['id'] and job['state']=='prepared' and not job['workspacePath'] and rows()==before,'concurrent public preparation retries capture one native bounded work item without executing')
    call(library_path,'POST',body|{'expectedRevision':0},409)
    originals=[path.read_bytes() for path in fixture['targets']];configure()
    act(job,'start',409,expectedRevision=0);job=wait(act(job,'start'))
    assert job['state']=='proposed',job
    check(not subprocess.check_output(['git','-C',str(ns['repo']),'for-each-ref','refs/canopy/calibration'],text=True).strip(),'private skill snapshots and refs stay in a separate owned repository instead of the product object store')
    check(len(job['edits'])==1 and 'Caller evidence-based lesson' in job['edits'][0]['diff'] and [path.read_bytes() for path in fixture['targets']]==originals,'actual model-fixture mirror writes produce an exact proposal without canonical changes')
    child=call('/api/agents/runs/'+job['agentRunId']);check(child['state']=='succeeded' and child['calibrationJobId']==job['id'] and child['usage']['status']=='unknown','calibration uses the shared owned CLI worker with native association and honest missing usage')
    call(f"/api/agents/tasks/{job['itemId']}/runs",'POST',dict(requestId=uid(),expectedRevision=revision(),provider='codex',purpose='documentation',instructions='Unauthorized continuation',previousRunId=child['id']),409)
    check(True,'standalone dispatch cannot resume a calibration session')
    saved=read(job);count=len(rows());restart();check(read(job)==saved and len(rows())==count,'proposal, evidence, policy and actual byte hashes survive native restart without replay')
    if '--serve-calibration' in sys.argv:
        metadata=dict(baseUrl=ns['base'],projectId=pid,itemId=job['itemId'],runId=job['runId'],deliveryId=did,calibrationId=job['id'],scratch=str(ns['scratch']))
        Path('/private/tmp/calibration-ui.json').write_text(json.dumps(metadata));Path('/private/tmp/calibration-openapi-generated.json').write_text(json.dumps(call('/openapi/v1.json'),indent=2)+'\n')
        print(json.dumps(metadata),flush=True)
        while not Path('/private/tmp/calibration-ui-stop').exists():time.sleep(.5)
        job=read(job)
        if job['active']:job=wait(act(job,'cancel'))
        if job['state']=='proposed':act(job,'cancel')
        if job['state']=='needsAttention':act(job,'close',note='Temporary browser fixture cleanup preserves recorded outcomes.')
        adopt(policy(enabled=False));call(f'/api/projects/{pid}?expectedRevision={revision()}','DELETE');return
    act(job,'apply',409,proposalHash='changed');job=wait(act(job,'apply'));check(job['state']=='applied' and all(w['state']=='applied' for w in job['writes']) and fixture['targets'][0].read_text()==job['edits'][0]['markdown'],'explicit proposal-bound adoption writes actual canonical bytes and journals observed hashes')
    saved=read(job);restart();check(read(job)==saved and len(rows())==count,'completed canonical adoption persists across restart without another invocation or file write')
    if '--postman' in sys.argv:subprocess.run(['postman','collection','run',str(ns['ROOT']/'postman/collections/Factory calibration regression'),'--env-var',f"baseUrl={ns['base']}",'--env-var',f'projectId={pid}','--env-var',f"calibrationId={job['id']}",'--env-var','fixtureMode=isolated','--report-events=false'],cwd=ns['ROOT'],check=True)
    job=execute('noChange',provider='claude');check(job['state']=='noChange' and not job['edits'],'a real read/no-change Claude protocol invocation remains distinct from unavailable or failed calibration')
    for mode in ['product','ignored','directory','fifo','mirrorFifo','gitconfig','packet','delete','permission','reference','commit','huge']:
        before=[path.read_bytes() for path in fixture['targets']];job=execute(mode)
        assert job['state']=='failed',(mode,job)
        if mode in ['fifo','mirrorFifo']:assert 'regular files only' in job['error'],(mode,job['error'])
        check([path.read_bytes() for path in fixture['targets']]==before and job['error'],mode+' violation fails actual proposal audit and preserves canonical skills')
    before=fixture['targets'][0].read_bytes();job=execute('canonical',canonical=str(fixture['targets'][0]));check(job['state']=='failed' and fixture['targets'][0].read_text()=='Unexpected canonical escape','an executable bypassing its CLI sandbox is detected by actual canonical hashes; external edits are preserved for review');fixture['targets'][0].write_bytes(before)
    job=execute();before=fixture['targets'][0].read_bytes();fixture['targets'][0].write_bytes(before+b'\nCaller external edit\n');job=wait(act(job,'apply'));check(job['state']=='needsAttention' and fixture['targets'][0].read_bytes().endswith(b'Caller external edit\n'),'canonical hash conflict prevents overwriting external edits and records a reconciliation state')
    job=act(job,'reconcile');check(job['state']=='needsAttention' and job['writes'][0]['state']=='conflict','reconciliation observes actual conflicting canonical bytes without replaying the writer');job=act(job,'close',note='Caller preserves external change and closes this proposal.');check(job['state']=='closed','explicit acknowledgment closes an unresolved proposal without certifying or undoing external changes');fixture['targets'][0].write_bytes(before)
    job=execute('both');assert len(job['edits'])==2,job
    before_second=fixture['targets'][1].read_bytes();directory=fixture['targets'][1].parent;mode=directory.stat().st_mode&0o777;directory.chmod(0o500)
    try:job=wait(act(job,'apply'))
    finally:directory.chmod(mode)
    assert job['state']=='needsAttention',job
    check(fixture['targets'][0].read_text()==job['edits'][0]['markdown'] and fixture['targets'][1].read_bytes()==before_second,'real second-file write failure preserves the completed first write and the untouched second target')
    before=[path.read_bytes() for path in fixture['targets']];job=act(job,'reconcile');check(job['state']=='needsAttention' and [x['state'] for x in job['writes']]==['applied','pending'] and [path.read_bytes() for path in fixture['targets']]==before,'partial reconciliation observes each canonical hash and neither replays nor rolls back a file')
    job=act(job,'close',note='Caller accepts first file only; remaining proposal stays unapplied.');check(job['state']=='partiallyApplied','explicit partial acknowledgment releases tracking while retaining exact applied and pending outcomes')
    job=execute();fixture['targets'][0].write_text(job['edits'][0]['markdown']);job=wait(act(job,'apply'));assert job['state']=='needsAttention',job
    before=[path.read_bytes() for path in fixture['targets']];job=act(job,'reconcile');check(job['state']=='applied' and job['error'] is None and [path.read_bytes() for path in fixture['targets']]==before,'reconciliation can prove the desired canonical outcome without repeating an externally completed write')
    child_pid=ns['scratch']/'calibration-child-pid';configure('wait',childPid=str(child_pid));adopt(policy());job=prepare();act(job,'start')
    job=wait(job,{'running'});deadline=time.monotonic()+15
    while not child_pid.exists():assert time.monotonic()<deadline;time.sleep(.1)
    direct=rows()[-1]['pid'];descendant=int(child_pid.read_text());act(job,'cancel');job=wait(job)
    check(job['state']=='cancelled' and stopped([direct,descendant]),'cancellation settles the owned calibrator and descendant before the native job becomes terminal')
    child_pid.unlink();configure('wait',childPid=str(child_pid));adopt(policy());job=prepare();act(job,'start');job=wait(job,{'running'});deadline=time.monotonic()+15
    while not child_pid.exists():assert time.monotonic()<deadline;time.sleep(.1)
    direct=rows()[-1]['pid'];descendant=int(child_pid.read_text());ns['process'].kill();ns['process'].wait(timeout=30);check(stopped([direct,descendant]),'abrupt backend death closes calibration process ownership leases');count=len(rows());ns['start']();job=read(job);check(job['state']=='interrupted' and len(rows())==count,'native recovery interrupts calibration intents without replay')
    child_pid.unlink();configure('wait',childPid=str(child_pid));adopt(policy());job=prepare();act(job,'start');job=wait(job,{'running'});deadline=time.monotonic()+15
    while not child_pid.exists():assert time.monotonic()<deadline;time.sleep(.1)
    direct=rows()[-1]['pid'];descendant=int(child_pid.read_text());adopt(policy(enabled=False));job=wait(job)
    check(job['state']=='failed' and 'policy' in job['error'].lower() and stopped([direct,descendant]),'policy revocation stops the owned learner and descendant while retaining the actual authority failure')
    # New technical bounds and missing edit scope remain explicit after real restarts.
    configure();adopt(policy());before=len(rows());ns['env']['Factory__Calibration__MaximumPacketBytes']='1';restart();prepare(400);check(len(rows())==before,'packet byte bounds reject incomplete capture before any learner invocation')
    del ns['env']['Factory__Calibration__MaximumPacketBytes'];ns['env']['Factory__Calibration__MaximumEvidenceItems']='1';restart();prepare(400);check(len(rows())==before,'evidence item bounds reject partial packets without fabricating coverage')
    del ns['env']['Factory__Calibration__MaximumEvidenceItems'];saved_root=ns['env'].pop('Factory__Calibration__EditableRoots__0');restart();check(not call(library_path)['readiness']['ready'],'missing administrator edit roots are exposed as unavailable rather than assumed writable paths');prepare(409)
    ns['env']['Factory__Calibration__EditableRoots__0']=saved_root;restart()
    configure();p=policy(autoApply=True);adopt(p);job=prepare();job=wait(act(job,'start'));assert job['state'] in ['proposed','applied'],job
    if job['state']=='proposed':job=wait(job,{'applied'})
    check(job['state']=='applied','explicit automatic-adoption policy applies only host-validated actual mirror changes')
    # Enable automatic execution for future completions; old deliveries must stay untouched.
    before=len(rows());p=policy(autoRun=True);adopt(p);time.sleep(.7);check(len(rows())==before,'saving automatic policy does not execute historical delivered work')
    source=ns['source']();ns['adopt']();new=ns['merge'](ns['approve'](ns['publish'](ns['capture'](source))))
    path='/api/factory/deliveries/'+new['delivery']['id']+'/calibrations';deadline=time.monotonic()+30
    while time.monotonic()<deadline:
        value=call(path)
        if value['jobs'] and value['jobs'][0]['state'] not in ['prepared','queued','preparing','running','cancelling','applying'] and value['triggers'] and value['triggers'][0]['state']!='observing':break
        time.sleep(.1)
    assert value['jobs'] and value['jobs'][0]['state']=='proposed',value
    check(len(value['jobs'])==1 and len(value['triggers'])==1 and value['triggers'][0]['state']=='prepared','new verified delivery triggers one native observation and one bounded learner under explicit automatic policy')
    act(value['jobs'][0],'cancel')
    before=len(rows());adopt(policy(autoRun=True));source=ns['source']();ns['adopt']();new=ns['approve'](ns['publish'](ns['capture'](source)))
    ns['configure'](holdCalibrationPr=new['delivery']['remote']['number']);new=ns['merge'](new);marker=Path(str(ns['fixture_path'])+'.calibration-read');deadline=time.monotonic()+15
    while not marker.exists():assert time.monotonic()<deadline,'Automatic eligibility did not reach the explicit provider barrier';time.sleep(.01)
    adopt(policy(autoRun=False));ns['configure'](holdCalibrationPr=None);path='/api/factory/deliveries/'+new['delivery']['id']+'/calibrations';deadline=time.monotonic()+15
    while time.monotonic()<deadline:
        observation=call(path)
        if observation['triggers'] and observation['triggers'][0]['state']!='observing':break
        time.sleep(.05)
    check(not observation['jobs'] and observation['triggers'][0]['state']=='unavailable' and len(rows())==before,'automatic capture pins its policy and activation boundary across fresh-provider I/O; changing to manual mode cannot launch a learner')
    adopt(policy(enabled=False));call(f'/api/projects/{pid}?expectedRevision={revision()}','DELETE')
    check(not call('/api/workspace')['projects'],'test-only calibration, delivery, agent and policy records clean up through public APIs')
    print('Calibration API/process/canonical-file regression completed.',flush=True)
