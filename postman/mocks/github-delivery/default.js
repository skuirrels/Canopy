// Local-only stateful GitHub contract simulation. Real fixture Git objects provide
// every SHA/tree/parent. Filesystem and child_process are intentionally local;
// this handler must not be deployed as a public/shared mock.
const http = require('http'), fs = require('fs'), cp = require('child_process'), crypto = require('crypto');
const file = process.env.CANOPY_DELIVERY_FIXTURE;
if (!file) throw new Error('Supply the explicit isolated CANOPY_DELIVERY_FIXTURE file.');
function fixture() { return JSON.parse(fs.readFileSync(file, 'utf8')); }
function git(...args) { return cp.execFileSync('git', ['--git-dir=' + fixture().barePath, ...args], {encoding:'utf8', stdio:['ignore','pipe','ignore']}).trim(); }
function ref(branch) { try { return git('rev-parse','--verify','refs/heads/'+branch); } catch { return null; } }
function pull(p, f) { return {number:p.number,html_url:f.gitUrl+'/pull/'+p.number,body:p.body,state:p.merged?'closed':'open',draft:!f.ready,merged:p.merged,merged_at:p.merged?'2026-01-01T00:00:00Z':null,merge_commit_sha:p.mergeSha??null,mergeable:f.mode==='conflict'?false:true,head:{sha:ref(p.head),ref:p.head,repo:{full_name:f.repository}},base:{sha:ref(p.base),ref:p.base,repo:{full_name:f.repository}}}; }
async function load() { return await pm.state.get('github-delivery.records') || {pulls:[],requests:{},writes:[]}; }
async function save(state) { await pm.state.set('github-delivery.records',state); }
function merge(p, f) {
 const base=ref(p.base),head=ref(p.head);
 cp.execFileSync('git',['--git-dir='+f.barePath,'merge-base','--is-ancestor',base,head],{stdio:'ignore'});
 const sha=cp.execFileSync('git',['--git-dir='+f.barePath,'commit-tree',git('rev-parse',head+'^{tree}'),'-p',base,'-p',head],{input:'Contract fixture merge\n',encoding:'utf8',env:{...process.env,GIT_AUTHOR_NAME:'Delivery fixture',GIT_AUTHOR_EMAIL:'fixture@example.invalid',GIT_COMMITTER_NAME:'Delivery fixture',GIT_COMMITTER_EMAIL:'fixture@example.invalid'}}).trim();
 git('update-ref','refs/heads/'+p.base,sha,base);p.merged=true;p.mergeSha=sha;return sha;
}
http.createServer(async (req,res)=>{
 const answer=(code,value)=>{res.writeHead(code,{'Content-Type':'application/json'});res.end(JSON.stringify(value));};
 try {
  const f=fixture(), u=new URL(req.url,'http://'+req.headers.host), path=decodeURIComponent(u.pathname), state=await load();
  if (path==='/__state' && req.method==='GET') return answer(200,state);
  if (path==='/__reset' && req.method==='POST') {await save({pulls:[],requests:{},writes:[]});return answer(200,{reset:true});}
  if (f.mode==='unavailable') return answer(503,{message:'Explicit provider outage fixture'});
  if (f.mode==='html') {res.writeHead(500,{'Content-Type':'text/html'});return res.end('<html>Explicit proxy failure fixture</html>');}
  if (f.mode==='hanging') return;
  const root='/repos/'+f.repository;
  if (path===root && req.method==='GET') return answer(200,{full_name:f.repository,clone_url:f.gitUrl,ssh_url:f.gitUrl,allow_merge_commit:true});
  if (path.startsWith(root+'/git/ref/heads/') && req.method==='GET') {const sha=ref(path.slice((root+'/git/ref/heads/').length));return answer(sha?200:404,sha?{object:{sha}}:{message:'Not found'});}
  if (path===root+'/branches/'+f.target+'/protection' && req.method==='GET') return answer(f.mode==='protectionMissing'?404:200,{required_status_checks:{strict:f.mode!=='loose',checks:[{context:f.checkName,app_id:f.appId}]},enforce_admins:{enabled:f.mode!=='adminBypass'},required_pull_request_reviews:{bypass_pull_request_allowances:{users:[],teams:[],apps:[]}},required_linear_history:{enabled:false}});
  if (path.startsWith(root+'/commits/') && path.endsWith('/check-runs') && req.method==='GET') {
   const sha=path.slice((root+'/commits/').length,-'/check-runs'.length), rows=f.mode==='missingCheck'?[]:[{id:1,name:f.checkName,app:{id:f.mode==='wrongApp'?f.appId+1:f.appId},head_sha:sha,status:'completed',conclusion:f.mode==='failedCheck'?'failure':'success'}];
   if(f.mode==='ambiguous'&&rows.length)rows.push({...rows[0],id:2});
   return answer(200,{total_count:f.mode==='truncated'?rows.length+1:rows.length,check_runs:rows});
  }
  if (path===root+'/pulls' && req.method==='GET') {
   const page=Number(u.searchParams.get('page')||'1');return answer(200,page>1?[]:state.pulls.filter(p=>(!u.searchParams.get('head')||u.searchParams.get('head')===f.repository.split('/')[0]+':'+p.head)&&(!u.searchParams.get('base')||u.searchParams.get('base')===p.base)).map(p=>pull(p,f)));
  }
  if (path===root+'/pulls' && req.method==='POST') {
   const b=req.body;
   if(!b||typeof b.title!=='string'||typeof b.body!=='string'||b.draft!==true||!ref(b.head)||b.base!==f.target)return answer(422,{message:'Invalid contract input'});
   if(state.pulls.some(p=>p.head===b.head&&!p.merged))return answer(422,{message:'A pull request already exists'});
   const p={number:state.pulls.length+1,body:b.body,head:b.head,base:b.base,merged:false};state.pulls.push(p);state.writes.push({kind:'create',number:p.number});await save(state);
   if(f.mode==='lostCreate')return res.destroy();return answer(201,pull(p,{...f,ready:false}));
  }
  const match=path.match(new RegExp('^'+root+'/pulls/(\\d+)(.*)$'));
  if(match){
   const p=state.pulls.find(p=>p.number===Number(match[1]));if(!p)return answer(404,{message:'Not found'});
   if(!match[2]&&req.method==='GET')return answer(200,pull(p,f));
   if(match[2]==='/merge-async'&&req.method==='PUT'){
    const b=req.body;if(!b||b.sha!==ref(p.head))return answer(409,{message:'Head changed'});
    if(b.merge_method!=='merge'||b.merge_action!=='direct_merge'||b.bypass_rules!==false)return answer(422,{message:'Invalid merge contract'});
    if(!f.ready||['failedCheck','missingCheck','wrongApp','loose','adminBypass','protectionMissing'].includes(f.mode))return answer(403,{message:'Protected branch'});
    if(f.mode==='baseRace'){
     const base=ref(p.base), next=cp.execFileSync('git',['--git-dir='+f.barePath,'commit-tree',git('rev-parse',base+'^{tree}'),'-p',base],{input:'Concurrent target writer\n',encoding:'utf8',env:{...process.env,GIT_AUTHOR_NAME:'Concurrent fixture',GIT_AUTHOR_EMAIL:'fixture@example.invalid',GIT_COMMITTER_NAME:'Concurrent fixture',GIT_COMMITTER_EMAIL:'fixture@example.invalid'}}).trim();git('update-ref','refs/heads/'+p.base,next,base);
    }
    try{cp.execFileSync('git',['--git-dir='+f.barePath,'merge-base','--is-ancestor',ref(p.base),ref(p.head)],{stdio:'ignore'});}catch{return answer(403,{message:'Strict protection rejected outdated branch'});}
    if(f.mode==='protectionRace')return answer(403,{message:'Concurrent server protection rejected merge'});
    const uuid=crypto.randomUUID();state.writes.push({kind:'merge',number:p.number,sha:b.sha});
    if(f.mode==='enqueued'){state.requests[uuid]={number:p.number,enqueued:true};await save(state);return answer(200,{status:'enqueued',details:{uuid}});}
    if(['pending','pendingWrongHead','pendingBypass'].includes(f.mode)){
     state.requests[uuid]={number:p.number,head:b.sha};await save(state);
     return answer(202,{status:'pending',details:{uuid,expected_head_sha:f.mode==='pendingWrongHead'?ref(p.base):b.sha,merge_method:'merge',merge_action:'direct_merge',bypass_rules:f.mode==='pendingBypass'}});
    }
    const sha=merge(p,f);state.requests[uuid]={number:p.number,sha};await save(state);
    if(f.mode==='lostMerge')return res.destroy();return answer(200,{status:'merged',details:{sha}});
   }
   if(match[2].startsWith('/merge-async/')&&req.method==='GET'){
    const uuid=match[2].slice('/merge-async/'.length), r=state.requests[uuid];if(!r||f.mode==='expired')return answer(404,{message:'Not found'});
    if(f.mode==='failedResult'||r.failed){r.failed=true;await save(state);return answer(200,{status:'failed',details:{message:'Explicit terminal merge rejection'}});}
    if(!r.sha&&!['pending','pendingWrongHead','pendingBypass'].includes(f.mode)){r.sha=merge(p,f);await save(state);}
    return answer(200,r.sha?{status:'merged',details:{sha:r.sha}}:{status:'pending',details:{uuid,expected_head_sha:r.head,merge_method:'merge',merge_action:'direct_merge',bypass_rules:false}});
   }
  }
  if(path.startsWith(root+'/git/commits/')&&req.method==='GET'){
   const sha=path.slice((root+'/git/commits/').length);try{return answer(200,{sha,tree:{sha:git('rev-parse',sha+'^{tree}')},parents:git('show','-s','--format=%P',sha).split(' ').map(sha=>({sha}))});}catch{return answer(404,{message:'Not found'});}
  }
  if(path.startsWith(root+'/compare/')&&req.method==='GET'){
   const [base,input]=path.slice((root+'/compare/').length).split('...'),head=ref(input)||input;
   try {const common=git('merge-base',base,head);return answer(200,{status:base===head?'identical':common===base?'ahead':'diverged',merge_base_commit:{sha:common}});}catch{return answer(404,{message:'Not found'});}
  }
  return answer(501,{message:'Endpoint outside this consumed contract subset'});
 }catch(error){return answer(500,{message:'Local contract simulation error',detail:String(error)});}
}).listen(Number(process.env.PORT));
