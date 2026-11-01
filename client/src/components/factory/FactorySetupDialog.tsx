import { useEffect, useState } from 'react';
import { ArrowLeft, ArrowRight, Check, Loader2, Plus, Settings2, Sparkles, Trash2 } from 'lucide-react';
import { factoryRequest } from '../../lib/factory';
import type { FactoryDefinition, FactorySkill } from '../../lib/factory';
import type { SkillCatalogue } from '../../lib/factory-skills';
import { skillSelectionStatus } from '../../lib/factory-skills';
import { checkCommand, checkProblem, commandDraft, timeoutLabel, workflowRoles } from '../../lib/factory-setup';
import { blockedBy } from '../../lib/blocked';
import type { CheckDraft, ProjectSetup, SuggestedCheckGroup } from '../../lib/factory-setup';
import { useWorkspace } from '../../lib/workspace';
import { Button } from '../ui/button';
import { Dialog } from '../ui/dialog';
import { AgentConnectionDialog } from '../AgentConnectionDialog';
import { SkillFields } from './FactorySkillPicker';
import { FactoryWorkflowPreview } from './FactoryWorkflowPreview';
import './factory-setup.css';

// Checks found in the repository, with the file that suggested them.
const suggestedDrafts=(group?:SuggestedCheckGroup):CheckDraft[]=>group?group.checks.map(command=>({...commandDraft(command),evidence:group.evidence})):[];
export function FactorySetupDialog({projectId,itemId,onClose,onSaved}: {projectId:string; itemId?:string; onClose:()=>void; onSaved:()=>void}) {
 const {refresh} = useWorkspace();
 const [data,setData]=useState<ProjectSetup|null>(null), [catalogue,setCatalogue]=useState<SkillCatalogue|null>(null);
 const [templateId,setTemplateId]=useState(''),[name,setName]=useState(''),[checks,setChecks]=useState<CheckDraft[]>([]),[skills,setSkills]=useState<Record<string,FactorySkill[]>>({});
 const [page,setPage]=useState<'configure'|'review'|'reuse'>('configure'),[definition,setDefinition]=useState<FactoryDefinition|null>(null);
 const [busy,setBusy]=useState(false),[error,setError]=useState(''),[connectionOpen,setConnectionOpen]=useState(false),[loadEpoch,setLoadEpoch]=useState(0);
 useEffect(()=>{
  const abort=new AbortController();
  void Promise.all([factoryRequest<ProjectSetup>(`/projects/${projectId}/setup`,'GET',undefined,abort.signal),factoryRequest<SkillCatalogue>(`/projects/${projectId}/skills`,'GET',undefined,abort.signal)]).then(([value,found])=>{
   if(abort.signal.aborted)return;
   setData(value);setCatalogue(found);setError('');
   const id=value.setup?.templateId??value.defaults.defaultTemplateId;setTemplateId(id);
   const template=value.templates.find(t=>t.id===id);setName(value.version?.name??template?.name??'');
   const stored=value.version?.definition;
   setChecks(stored?stored.stages.flatMap(s=>s.steps.filter(step=>step.kind==='command'&&step.command).map(step=>commandDraft(step.command!))):suggestedDrafts(value.suggestedChecks[0]));
   const chosen:Record<string,FactorySkill[]>={};
   for(const role of workflowRoles(stored??template?.definition??{stages:[]})) chosen[role]=stored ? stored.stages.flatMap(s=>s.steps.filter(step=>step.role===role).flatMap(step=>step.skills??[])).filter((ref,i,all)=>all.findIndex(x=>x.id===ref.id&&x.source===ref.source)===i) : (value.defaults.suggestedSkills[role]??[]).filter(ref=>found.complete&&skillSelectionStatus(ref,found).state==='available');
   setSkills(chosen);setPage(value.available?'reuse':'configure');
  }).catch(cause=>{if(!abort.signal.aborted)setError((cause as Error).message);});
  return ()=>abort.abort();
 },[projectId,itemId,loadEpoch]);
 const template=data?.templates.find(t=>t.id===templateId);
 const needsChecks=!!template?.definition.stages.some(s=>s.steps.some(step=>step.kind==='command'));
 const roles=template?workflowRoles(template.definition):[];
 const provider=data?.providers.find(p=>p.id===data.connection?.provider);
 const checkIssue=needsChecks?checks.map((check,index)=>({index,problem:checkProblem(check)})).find(x=>x.problem):undefined;
 const reviewBlocked=data&&blockedBy([!data.connection,'Connect a repository and agent first.'],[!provider?.available,`${provider?.name??'The selected agent'} isn't available on this machine. Change the connection.`],[!template,'Choose what kind of work this is.'],[needsChecks&&!checks.length,'Add at least one check, such as your test command, so Canopy can tell whether the work passes.'],[checkIssue,`Check ${(checkIssue?.index??0)+1}: ${checkIssue?.problem}`],[!name.trim(),'Give the workflow a name under Customise skills and workflow name.']);
 // With no build files to detect, offer common commands: every stack until one is picked, then only the rest of that stack.
 const added=(line:string)=>checks.some(check=>check.line.trim()===line);
 const starters=(data?.defaults.starterChecks??[]).filter(group=>!checks.length||group.commands.some(added)).map(group=>({...group,commands:group.commands.filter(line=>!added(line))})).filter(group=>group.commands.length);
 const unusedSuggestions=(data?.suggestedChecks??[]).map(group=>({...group,checks:group.checks.filter(command=>!checks.some(check=>check.line===commandDraft(command).line&&check.directory===command.workingDirectory))})).filter(group=>group.checks.length);
 function choose(id:string) {
  if(!data)return;const template=data.templates.find(t=>t.id===id);setTemplateId(id);setName(template?.name??'');
  setSkills(Object.fromEntries(workflowRoles(template?.definition??{stages:[]}).map(role=>[role,(data.defaults.suggestedSkills[role]??[]).filter(ref=>catalogue?.complete&&skillSelectionStatus(ref,catalogue).state==='available')])));
 }
 function body(){ return {expectedRevision:data!.projectRevision,templateId,name,commands:needsChecks?checks.map(checkCommand):[],skills:Object.fromEntries(roles.map(role=>[role,skills[role]??[]])),itemId}; }
 async function refreshConnection(){setBusy(true);setError('');try{const [updated,found]=await Promise.all([factoryRequest<ProjectSetup>(`/projects/${projectId}/setup`),factoryRequest<SkillCatalogue>(`/projects/${projectId}/skills`)]);setData(updated);setCatalogue(found);if(!data?.connection&&!data?.setup&&template)setSkills(current=>Object.fromEntries(workflowRoles(template.definition).map(role=>[role,current[role]?.length?current[role]:(updated.defaults.suggestedSkills[role]??[]).filter(ref=>found.complete&&skillSelectionStatus(ref,found).state==='available')])));}catch(cause){setError((cause as Error).message);}finally{setBusy(false);}}
 async function review(){setBusy(true);setError('');try{const value=await factoryRequest<FactoryDefinition>(`/projects/${projectId}/setup/preview`,'POST',body());setDefinition(value);setPage('review');}catch(cause){setError((cause as Error).message);}finally{setBusy(false);}}
 async function save(){if(!data)return;setBusy(true);setError('');try{
  if(page==='reuse'&&!itemId){onClose();return;}
  if(page==='reuse')await factoryRequest(`/tasks/${itemId}/use-project-setup`,'POST',{expectedRevision:data.projectRevision,pipelineVersionId:data.version!.id});
  else await factoryRequest(`/projects/${projectId}/setup`,'PUT',body());
  await refresh();onSaved();
 }catch(cause){setError((cause as Error).message);}finally{setBusy(false);}}
 return <><Dialog open onOpenChange={open=>{if(!open&&!busy)onClose();}} title={page==='reuse'?'Use your project workflow':page==='review'?'Review your workflow':'Set up your factory'} description={page==='reuse'?'Your project is configured. Review the workflow and use it for this task.':page==='review'?'We assembled these steps from the selected template and your project settings.':'Configure this once. Tasks can reuse it without building their own pipeline.'} wide className="factory-dialog factory-setup-dialog">
 {!data ? <div className="factory-state">{error?<><p role="alert">{error}</p><Button onClick={()=>setLoadEpoch(x=>x+1)}>Retry</Button></>:<><Loader2 className="spin"/>Loading project setup…</>}</div> : <>
 <div className="factory-setup-progress" aria-label="Setup progress"><span className={page==='configure'?'active':''}>1 · Project settings</span><ArrowRight size={14}/><span className={page!=='configure'?'active':''}>2 · Review and use</span></div>
 {page==='configure' ? <fieldset disabled={busy} className="factory-setup-fields">
 <section className="factory-setup-connection"><div><h3>Repository &amp; agent</h3>{data.connection?<><p><strong>Project agent: {provider?.name??data.connection.provider}</strong> · {data.connection.model??'CLI default model'}</p><code>{data.connection.repositoryPath}</code><small>Base: {data.connection.baseRef}. Model routes and budgets continue to apply.</small></>:<p>Connect the repository and agent this project will use.</p>}{data.connection&&!provider?.available&&<p role="alert" className="factory-error">The selected agent is unavailable on this backend.</p>}</div><Button variant="secondary" onClick={()=>setConnectionOpen(true)}><Settings2 size={14}/>{data.connection?'Change connection':'Connect repository'}</Button></section>
 <section><h3>What kind of work?</h3><div className="factory-template-choices">{data.templates.map(t=><button type="button" key={t.id} aria-label={t.name} aria-pressed={templateId===t.id} className={templateId===t.id?'active':''} onClick={()=>choose(t.id)}>{templateId===t.id&&<Check size={15}/>}<strong>{t.name}</strong></button>)}</div></section>
 {needsChecks&&<section><h3>Checks</h3><p className="factory-help">When the agent finishes, Canopy runs these commands in its copy of the repository. If any of them fails, the work goes back to the agent instead of on to review. Usually these are your build and test commands, such as <code>dotnet test</code> or <code>npm test</code>.</p>
 {checks.map((check,index)=>{const problem=checkProblem(check),settingsProblem=!!problem&&!!check.line.trim()&&!problem.startsWith('Use one')&&!problem.startsWith('Close')&&!problem.startsWith('Enter');return <div className="factory-setup-check" key={index}><div className="factory-setup-check-line"><label>{checks.length>1?`Check ${index+1}`:'Command'}{check.evidence&&<small className="factory-check-evidence">Found in {check.evidence}</small>}<input aria-label={`Check ${index+1} command`} value={check.line} placeholder="For example: dotnet test" onChange={e=>setChecks(checks.map((c,i)=>i===index?{...c,line:e.target.value}:c))}/></label><Button variant="ghost" size="sm" aria-label={`Remove check ${index+1}`} onClick={()=>setChecks(checks.filter((_,i)=>i!==index))}><Trash2 size={13}/></Button></div>{problem&&check.line.trim()&&<small className="blocked-reason" role="note">{problem}</small>}<details className="factory-check-options" open={settingsProblem||undefined}><summary>Runs from {check.directory.trim()==='.'?'the repository root':check.directory.trim()||'no folder set'} · stops after {timeoutLabel(check.timeout)}</summary><div className="factory-form-row"><label>Folder, relative to the repository<input aria-label={`Check ${index+1} directory`} value={check.directory} placeholder=". for the repository root" onChange={e=>setChecks(checks.map((c,i)=>i===index?{...c,directory:e.target.value}:c))}/></label><label>Stop after (seconds)<input aria-label={`Check ${index+1} timeout`} type="number" min={1} step={1} value={check.timeout} onChange={e=>setChecks(checks.map((c,i)=>i===index?{...c,timeout:e.target.value}:c))}/></label></div></details></div>;})}
 {unusedSuggestions.map(group=><div className="factory-check-suggestions" key={group.evidence}><small>Also found in {group.evidence}</small>{group.checks.map(command=><Button key={commandDraft(command).line} variant="secondary" size="sm" onClick={()=>setChecks([...checks,{...commandDraft(command),evidence:group.evidence}])}><Plus size={13}/><code>{commandDraft(command).line}</code></Button>)}</div>)}
 {!data.suggestedChecks.length&&starters.length>0&&<div className="factory-setup-gap factory-check-starters">{!checks.length&&<p><strong>There's no code to build or test in this repository yet.</strong> If your first tasks will create it, pick the commands it will use. They will fail until that code exists.</p>}{starters.map(group=><div className="factory-check-suggestions" key={group.ecosystem}><small>{group.ecosystem}</small>{group.commands.map(line=><Button key={line} variant="secondary" size="sm" onClick={()=>setChecks([...checks,{line,directory:'.',timeout:String(data.defaults.defaultCheckTimeoutSeconds)}])}><Plus size={13}/><code>{line}</code></Button>)}</div>)}</div>}
 <Button variant="secondary" size="sm" onClick={()=>setChecks([...checks,{line:'',directory:'.',timeout:String(data.defaults.defaultCheckTimeoutSeconds)}])}><Plus size={14}/>{checks.length?'Add another command':'Type a command'}</Button></section>}
 <section><h3>Skills</h3>{catalogue&&!catalogue.complete&&<p className="factory-setup-gap">Installed skill coverage is incomplete. Suggestions were not selected automatically; inspect the source errors under Customize skills.</p>}<p className="factory-help">{Object.values(skills).flat().length?<>Agents also follow these instructions: {Object.values(skills).flat().map(s=>s.id).join(', ')}.</>:'No extra instructions for the agents.'}</p>{roles.some(role=>(data.defaults.suggestedSkills[role]??[]).some(ref=>catalogue&&skillSelectionStatus(ref,catalogue).state!=='available'))&&<p className="factory-setup-gap">Some configured suggestions are unavailable. They have not been selected. You can choose other installed skills below.</p>}<details className="factory-inherited"><summary>Customise skills and workflow name</summary><label>Workflow name<input value={name} maxLength={200} onChange={e=>setName(e.target.value)}/></label>{roles.map(role=><SkillFields key={role} projectId={projectId} legend={`${role} skills`} skills={skills[role]??[]} onChange={value=>setSkills({...skills,[role]:value})}/>)}</details></section>
 </fieldset> : <><div className="factory-setup-summary"><strong>{page==='reuse'?data.version?.name:name}</strong><p>Project agent: {provider?.name??data.connection?.provider??'Not connected'} · {data.connection?.model??'CLI default model'}</p><p className="factory-help">{itemId?'This task will use this workflow.':'Every task without its own workflow will use this one.'} Nothing runs until you start a build.</p></div>{(page==='reuse'?data.version?.definition:definition)&&<FactoryWorkflowPreview expandedChecks definition={(page==='reuse'?data.version?.definition:definition)!}/>}</>}
 {error&&<div role="alert" className="factory-error">{error}<button type="button" className="text-button" disabled={busy} onClick={()=>setLoadEpoch(x=>x+1)}>Reload saved setup</button></div>}
 <div className="dialog-footer"><Button variant="ghost" disabled={busy} onClick={onClose}>Cancel</Button>{page!=='configure'&&<Button variant="secondary" disabled={busy} onClick={()=>{setPage('configure');setError('');}}><ArrowLeft size={14}/>Edit project setup</Button>}{page==='configure'?<Button disabled={busy} blockedReason={reviewBlocked} onClick={()=>{void review();}}>{busy?<Loader2 size={14} className="spin"/>:<ArrowRight size={14}/>}Review workflow</Button>:<Button disabled={busy} onClick={()=>{void save();}}>{busy?<Loader2 size={14} className="spin"/>:<Sparkles size={14}/>}{page==='reuse'?(itemId?'Use workflow for this task':'Done'):itemId?'Create workflow & use for task':'Save project workflow'}</Button>}</div>
 </>}
 </Dialog>{connectionOpen&&<AgentConnectionDialog projectId={projectId} onClose={()=>{setConnectionOpen(false);void refreshConnection();}}/>}</>;
}
