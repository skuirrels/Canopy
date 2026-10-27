import type { BuildPresentation } from './build-experience';

// Decorative geometry reacts to recorded run activity; it never estimates progress.
export function createBuildScene(canvas: HTMLCanvasElement, initial: BuildPresentation, provider: string) {
 const context=canvas.getContext('2d');
 if(!context)return null;
 const ctx: CanvasRenderingContext2D=context;
 let presentation=initial;
 const motion=matchMedia('(prefers-reduced-motion: reduce)');
 const state={yaw:.48,pitch:.43,drag:false,exploded:!initial.succeeded};
 const look={energy:1};
 let width=0,height=0,scale=1,clock=0,last=0,explosion=state.exploded?1:.18;
 let activityPulse=0,successPulse=0,frame=0,disposed=false,visible=true,transitionUntil=0;
 let pointerId: number|null=null,pointerX=0,pointerY=0,dragYaw=0,dragPitch=0;
 const cyan='#65deff',amber='#ffba7c';
 const satellites=['Work order',provider,'Worktree','Result'].map((name,i)=>({name,color:i%2?amber:cyan,angle:[.3,2,3.6,5.1][i],y:[65,-5,30,100][i],hit:{x:0,y:0}}));
 let seed=41;function random(){seed=(seed*16807)%2147483647;return(seed-1)/2147483646;}
 const dust=Array.from({length:110},()=>({x:random(),y:random(),r:random()*1.1+.25,a:random()*.38+.12}));
 const motes=Array.from({length:360},(_,i)=>({a:random()*Math.PI*2,r:270+random()*210,y:(random()-.5)*380,layer:i%4,x:(random()-.5)*290,z:(random()-.5)*180,s:random()*1.6+.4}));
function project(x: number,y: number,z: number){const c=Math.cos(state.yaw),s=Math.sin(state.yaw),a=x*c-z*s,b=x*s+z*c;const yy=y*Math.cos(state.pitch)-b*Math.sin(state.pitch),zz=y*Math.sin(state.pitch)+b*Math.cos(state.pitch);const f=1000/(1000+zz);return{x:width*.5+a*scale*f,y:height*.5-yy*scale*f,f,depth:zz};}
function path(points: number[][],color: string,lineWidth=1,fill: string|null=null,glow=0,close=false){if(!points.length)return;ctx.beginPath();points.forEach((p,i)=>{const q=project(p[0],p[1],p[2]);if(i===0)ctx.moveTo(q.x,q.y);else ctx.lineTo(q.x,q.y);});if(close)ctx.closePath();ctx.strokeStyle=color;ctx.lineWidth=lineWidth;ctx.shadowBlur=glow*look.energy;ctx.shadowColor=color;if(fill){ctx.fillStyle=fill;ctx.fill();}ctx.stroke();ctx.shadowBlur=0;}
function plateRect(x: number,z: number,w: number,d: number,y: number,color: string,fill: string|null=null,line=1){path([[x,y,z],[x+w,y,z],[x+w,y,z+d],[x,y,z+d]],color,line,fill,0,true);}
function ring(radius: number,y: number,mode: number,rotation: number,color: string,line=1,from=0,to=Math.PI*2){const points=[];for(let i=0;i<=100;i++){const a=from+(to-from)*i/100;let x=Math.cos(a)*radius,z=Math.sin(a)*radius,yy=y;if(mode===1){yy=Math.sin(a)*radius*.85+y;z=Math.sin(a)*radius*.4;}if(mode===2){yy=Math.sin(a)*radius*.73+y;x=Math.cos(a)*radius*.55;}const xx=x*Math.cos(rotation)-z*Math.sin(rotation),zz=x*Math.sin(rotation)+z*Math.cos(rotation);points.push([xx,yy,zz]);}path(points,color,line,null,line>1?8:0);}
function draw(){
 if(!width)return;ctx.clearRect(0,0,width,height);
 const time=clock,energy=look.energy+activityPulse*.25;
 dust.forEach((p,i)=>{ctx.fillStyle='rgba(145,199,229,'+(p.a*(.65+.35*Math.sin(time*.4+i)))+')';ctx.fillRect(p.x*width,p.y*height,p.r,p.r);});
 const center=project(0,0,0),halo=ctx.createRadialGradient(center.x,center.y,0,center.x,center.y,360*scale);halo.addColorStop(0,'rgba(41,172,224,'+(.13*energy)+')');halo.addColorStop(.45,'rgba(15,98,143,.06)');halo.addColorStop(1,'rgba(0,0,0,0)');ctx.fillStyle=halo;ctx.fillRect(0,0,width,height);
 for(let i=-6;i<=6;i++){path([[i*85,-195,-650],[i*85,-195,650]],'#16324455',.7);path([[-650,-195,i*85],[650,-195,i*85]],'#16324455',.7);}
 ring(260,-178,0,0,'#3e8baf66',1);ring(268,-178,0,0,'#3e8baf33',.6);ring(235,-178,0,0,'#6fdbff88',1.4,time*.08,time*.08+1.5);
 for(let i=0;i<64;i++){const a=i*Math.PI/32,r=i%4?275:284;path([[Math.cos(a)*270,-178,Math.sin(a)*270],[Math.cos(a)*r,-178,Math.sin(a)*r]],i%4?'#38617a66':'#7ccce3aa',1);}
 // Thin gyroscopes create a spatial cage around the suspended assembly.
 ring(278,10,1,.3,'#4590b849',.8);ring(278,10,1,.3,cyan,1.7,time*.2,time*.2+.6);
 ring(299,10,2,-.65,'#5d99ba44',.8);ring(299,10,2,-.65,amber,1.5,-time*.17+2,-time*.17+2.55);
 ring(325,-10,0,time*.015,'#3c718333',.8);ring(325,-10,0,time*.015,cyan,1.3,-time*.2,-time*.2+.55);
 const py=[-100,-30,40,110].map(v=>v*explosion),evidence=presentation.evidence;
 // Depth sorting keeps the construction plates readable as the camera moves.
 [0,1,2,3].sort((a,b)=>project(0,py[b],0).depth-project(0,py[a],0).depth).forEach(i=>{
   const y=py[i],lit=evidence[i],col=presentation.attention?'#f39093':i===1?amber:cyan,alpha=lit?'aa':'40';
   plateRect(-160,-100,320,200,y,col+alpha,'#071522b8',1);
   plateRect(-151,-91,302,182,y,col+'33',null,.6);
   [[-160,-100],[160,-100],[-160,100],[160,100]].forEach(([x,z])=>{const dx=x<0?15:-15,dz=z<0?15:-15;path([[x+dx,y,z],[x,y,z],[x,y,z+dz]],col,1.8,null,7);});
   if(i===0){for(let x=-125;x<=125;x+=50)for(let z=-65;z<=65;z+=43){plateRect(x-10,z-7,20,14,y+.5,col+'77',col+'10',.7);}for(let z=-65;z<=65;z+=43)path([[-125,y+1,z],[125,y+1,z]],col+'33',.6);}
   if(i===1){for(let n=0;n<4;n++){const x=-110+n*72;plateRect(x-20,-26,40,52,y+1,col+'99',col+'09',.8);if(n<3)path([[x+20,y+2,0],[x+52,y+2,0]],col+'99',1);}path([[-110,y+2,-26],[-110,y+2,-62],[106,y+2,-62],[106,y+2,-26]],col+'66',.8);}
   if(i===2){for(let n=0;n<6;n++){const x=-110+(n%3)*105,z=-48+Math.floor(n/3)*87;plateRect(x-29,z-24,58,48,y+1,col+'88',col+'12',.8);path([[x-16,y+2,z],[x-5,y+2,z+9],[x+17,y+2,z-10]],col+'99',1);}}
   if(i===3){plateRect(-136,-77,272,154,y+1,col+'99','#0b2639c0',.9);plateRect(-136,-77,43,154,y+2,col+'55',col+'13',.5);for(let n=0;n<4;n++)path([[-125,y+3,-50+n*27],[-105,y+3,-50+n*27]],col+'88',1.5);path([[-75,y+3,-51],[100,y+3,-51]],col+'cc',2);for(let n=0;n<3;n++){plateRect(-74+n*60,-25,49,40,y+2,col+'77',col+'12',.7);}for(let n=0;n<3;n++)path([[-74,y+3,34+n*13],[105-n*20,y+3,34+n*13]],col+'77',1);}
   if(width>=520){const tag=project(175,y,80);ctx.font='10px ui-monospace,monospace';ctx.fillStyle=col;ctx.fillText(['WORK ORDER','AGENT','WORKTREE','RESULT'][i],tag.x+6,tag.y+3);}
 });
 for(const [x,z] of [[-146,-86],[146,-86],[-146,86],[146,86]]){path([[x,py[0],z],[x,py[3],z]],'#65deff24',.7);if(presentation.running){const f=(time*.55+(x+z)/500)%1,y=py[0]+(py[3]-py[0])*((f+1)%1);path([[x,y-10,z],[x,y+10,z]],cyan,1.8,null,11);}}
 // Particles bend from orbital trajectories toward their destination layer.
 const convergence=presentation.succeeded ? 1 : presentation.running ? .38 : .18;
 motes.forEach((p,i)=>{const a=p.a+time*(.025+(i%5)*.006),r=p.r*(1-convergence);const x=Math.cos(a)*r+p.x*convergence,y=p.y*(1-convergence)+py[p.layer]*convergence,z=Math.sin(a)*r+p.z*convergence;const q=project(x,y,z);const alpha=(.2+(i%7)*.08)*energy;ctx.fillStyle=(i%9===0?'rgba(255,186,124,':'rgba(112,222,255,')+alpha+')';ctx.fillRect(q.x,q.y,p.s*q.f,p.s*q.f);if(presentation.running&&i%27===0){ctx.shadowBlur=12;ctx.shadowColor=cyan;ctx.fillRect(q.x,q.y,2,2);ctx.shadowBlur=0;}});
 // Orbital labels describe the recorded work order, provider, workspace and result.
 satellites.forEach((sat,i)=>{const a=sat.angle+time*.055,r=340,x=Math.cos(a)*r,z=Math.sin(a)*r,y=sat.y+Math.sin(time*.3+i)*9;const q=project(x,y,z);sat.hit=q;const destination=project(0,py[i],0);ctx.strokeStyle=sat.color+'25';ctx.lineWidth=.8;ctx.beginPath();ctx.moveTo(q.x,q.y);ctx.lineTo(destination.x,destination.y);ctx.stroke();const rad=7*q.f;ctx.shadowBlur=15*energy;ctx.shadowColor=sat.color;ctx.strokeStyle=sat.color+'bb';ctx.fillStyle='#071523';ctx.lineWidth=1.2;ctx.beginPath();ctx.moveTo(q.x,q.y-rad);ctx.lineTo(q.x+rad,q.y);ctx.lineTo(q.x,q.y+rad);ctx.lineTo(q.x-rad,q.y);ctx.closePath();ctx.fill();ctx.stroke();ctx.shadowBlur=0;ctx.fillStyle=sat.color;ctx.fillRect(q.x-1,q.y-1,2,2);ctx.font='10px ui-monospace,monospace';ctx.textAlign=q.x>width-110?'right':q.x<110?'left':q.x>center.x?'left':'right';ctx.fillStyle='#afcadd';ctx.fillText(sat.name.toUpperCase(),q.x+(ctx.textAlign==='left'?14:-14),q.y+3);ctx.textAlign='left';});
 if(presentation.running&&activityPulse>.05){const scanY=-140+(1-activityPulse)*310;plateRect(-185,-120,370,240,scanY,'#a0f1ff88','#65deff08',1);}
 if(presentation.succeeded&&successPulse>.01)ring(270+(1-successPulse)*180,-40,0,0,'rgba(160,236,255,'+successPulse*.7+')',2);
 const vignette=ctx.createRadialGradient(width*.56,height*.52,height*.2,width*.56,height*.52,width*.65);vignette.addColorStop(0,'#04080d00');vignette.addColorStop(1,'#04080d9a');ctx.fillStyle=vignette;ctx.fillRect(0,0,width,height);
}

 function wake(){if(!disposed&&!frame)frame=requestAnimationFrame(tick);}
 function tick(now: number){
  frame=0;if(disposed||document.hidden||!visible){last=0;return;}
  const dt=Math.min(.05,(now-last)/1000||0);last=now;
  if(!motion.matches&&presentation.running&&!state.drag)clock+=dt;
  activityPulse=Math.max(0,activityPulse-dt*.75);successPulse=Math.max(0,successPulse-dt*.7);
  const target=state.exploded?1:.18;explosion+=(target-explosion)*(motion.matches?1:Math.min(1,dt*4));
  draw();
  if(!motion.matches&&(presentation.running||now<transitionUntil))wake();else last=0;
 }
 function resize(){const bounds=canvas.getBoundingClientRect();width=bounds.width;height=bounds.height;scale=Math.min(width/850,height/560);const dpr=Math.min(devicePixelRatio,1.7);canvas.width=Math.round(width*dpr);canvas.height=Math.round(height*dpr);ctx.setTransform(dpr,0,0,dpr,0,0);draw();wake();}
 function down(event: PointerEvent){
  if(!event.isPrimary||event.button!==0||pointerId!==null)return;
  pointerId=event.pointerId;state.drag=true;pointerX=event.clientX;pointerY=event.clientY;dragYaw=state.yaw;dragPitch=state.pitch;canvas.setPointerCapture(event.pointerId);
 }
 function move(event: PointerEvent){if(event.pointerId!==pointerId)return;state.yaw=dragYaw+(event.clientX-pointerX)*Math.PI/Math.max(width,600);state.pitch=Math.max(.14,Math.min(1.08,dragPitch-(event.clientY-pointerY)*Math.PI*.55/Math.max(height,600)));draw();}
 function release(event: PointerEvent){if(event.pointerId!==pointerId)return;pointerId=null;state.drag=false;wake();}
 function visibility(){if(!document.hidden)wake();}
 const resizeObserver=new ResizeObserver(resize);resizeObserver.observe(canvas);
 const intersection=new IntersectionObserver(entries=>{visible=entries[0].isIntersecting;if(visible)wake();});intersection.observe(canvas);
 canvas.addEventListener('pointerdown',down);canvas.addEventListener('pointermove',move);canvas.addEventListener('pointerup',release);canvas.addEventListener('pointercancel',release);canvas.addEventListener('lostpointercapture',release);
 document.addEventListener('visibilitychange',visibility);motion.addEventListener('change',wake);
 return {
  update(next: BuildPresentation){
   if(next.activityKey!==presentation.activityKey&&next.running)activityPulse=motion.matches?0:1;
   if(next.succeeded&&!presentation.succeeded)successPulse=motion.matches?0:1;
   if(next.succeeded!==presentation.succeeded){state.exploded=!next.succeeded;transitionUntil=performance.now()+1600;}
   if(activityPulse||successPulse)transitionUntil=performance.now()+1600;
   presentation=next;draw();wake();
  },
  reset(){state.yaw=.48;state.pitch=.43;draw();},
  orbit(direction: number){state.yaw+=direction*.3;draw();},
  explode(){state.exploded=!state.exploded;transitionUntil=performance.now()+1600;wake();return state.exploded;},
  dispose(){disposed=true;cancelAnimationFrame(frame);resizeObserver.disconnect();intersection.disconnect();document.removeEventListener('visibilitychange',visibility);motion.removeEventListener('change',wake);canvas.removeEventListener('pointerdown',down);canvas.removeEventListener('pointermove',move);canvas.removeEventListener('pointerup',release);canvas.removeEventListener('pointercancel',release);canvas.removeEventListener('lostpointercapture',release);if(pointerId!==null&&canvas.hasPointerCapture(pointerId))canvas.releasePointerCapture(pointerId);}
 };
}
export type BuildScene = NonNullable<ReturnType<typeof createBuildScene>>;
