import { forwardRef, useEffect, useImperativeHandle, useMemo, useRef, useState } from 'react';
import { Activity, ArrowLeft, ArrowRight, Expand, RotateCcw, Volume2, VolumeX } from 'lucide-react';
import type { AgentRun } from '../lib/agents';
import { describeBuild } from '../lib/build-experience';
import { useBuildPreferences } from '../lib/build-preferences';
import type { BuildPresentation } from '../lib/build-experience';
import { createBuildScene } from '../lib/build-scene';
import type { BuildScene } from '../lib/build-scene';
import { createBuildSound } from '../lib/build-sound';
import type { BuildSound } from '../lib/build-sound';
import { Dialog } from './ui/dialog';
import { Button } from './ui/button';
import './build-experience.css';

export type BuildExperienceHandle = { prepare: () => void };

function BuildCanvas({ run, presentation }: { run: AgentRun; presentation: BuildPresentation }) {
 const canvas = useRef<HTMLCanvasElement>(null); const scene = useRef<BuildScene | null>(null);
 const [exploded, setExploded] = useState(!presentation.succeeded);
 const [supported, setSupported] = useState(true);
 useEffect(() => {
  if (!canvas.current) return;
  scene.current = createBuildScene(canvas.current, presentation, run.provider === 'codex' ? 'Codex' : 'Claude');
  setSupported(!!scene.current);
  return () => { scene.current?.dispose(); scene.current = null; };
 }, [run.id, run.provider]);
 useEffect(() => { scene.current?.update(presentation); }, [presentation]);
 useEffect(() => { setExploded(!presentation.succeeded); }, [presentation.succeeded]);
 return <>
  <div className="build-chamber-stage">
   <canvas ref={canvas} aria-label="Interactive reactor showing recorded agent build activity. Drag to rotate the view."/>
   {!supported && <p className="build-canvas-fallback">Graphics are unavailable in this browser. Run activity is shown below.</p>}
   <div className="build-chamber-state" aria-live="polite"><span>{run.state}</span><strong>{presentation.label}</strong></div>
   <span className="build-chamber-caption">{presentation.running ? 'Live agent activity' : 'Recorded run state'} · Drag to rotate</span>
  </div>
  <div className="build-camera-controls" aria-label="Build camera controls">
   <button type="button" onClick={() => scene.current?.orbit(-1)} aria-label="Rotate build view left"><ArrowLeft size={14}/></button>
   <button type="button" onClick={() => scene.current?.orbit(1)} aria-label="Rotate build view right"><ArrowRight size={14}/></button>
   <button type="button" onClick={() => scene.current?.reset()}><RotateCcw size={13}/>Reset view</button>
   <button type="button" aria-pressed={exploded} onClick={() => { const value = scene.current?.explode(); if (value !== undefined) setExploded(value); }}><Expand size={13}/>{exploded ? 'Collapse layers' : 'Explode layers'}</button>
  </div>
 </>;
}

export const BuildExperience = forwardRef<BuildExperienceHandle, { run?: AgentRun; disconnected: boolean; onStop: () => void; stopping: boolean; controlError?: string }>(function BuildExperience({ run, disconnected, onStop, stopping, controlError }, ref) {
 const { enabled, setEnabled, soundWanted, setSoundWanted } = useBuildPreferences();
 const [audioReady, setAudioReady] = useState(false); const [audioError, setAudioError] = useState('');
 const [open, setOpen] = useState(false);
 const dismissed = useRef<string | null>(null); const sound = useRef<BuildSound | null>(null);
 const observed = useRef<{ id: string; state: AgentRun['state']; output: string } | null>(null);
 const lastSoundActivity = useRef(0);
 const presentation = useMemo(() => run ? describeBuild(run, disconnected) : null, [run, disconnected]);
 async function unlockSound() {
  try {
   sound.current ??= createBuildSound();
   await sound.current.setEnabled(true);
   setAudioReady(true); setAudioError('');
  } catch (error) { setAudioError((error as Error).message); setAudioReady(false); }
 }
 useImperativeHandle(ref, () => ({ prepare() { if (enabled) { dismissed.current = null; if (soundWanted) void unlockSound(); } } }), [enabled, soundWanted]);
 useEffect(() => () => { sound.current?.close(); sound.current = null; }, []);
 useEffect(() => {
  if (enabled) dismissed.current = null;
  else { setOpen(false); void sound.current?.setEnabled(false); }
 }, [enabled]);
 useEffect(() => {
  if (enabled && run?.active && dismissed.current !== run.id) setOpen(true);
 }, [enabled, run?.id, run?.active]);
 useEffect(() => {
  const previous = observed.current;
  observed.current = run ? { id: run.id, state: run.state, output: run.output } : null;
  const engine = sound.current;
  if (!engine) return;
  if (!enabled || !open || !soundWanted || !audioReady || disconnected || !run || !presentation) { void engine.setEnabled(false); return; }
  let cancelled = false;
  void engine.setEnabled(true).then(() => {
   if (cancelled) return;
   engine.hum(presentation.running);
   // Opening historical results never replays a completion sound.
   if (previous?.id === run.id && previous.state !== run.state) {
    if (run.state === 'succeeded') engine.complete();
    else if (run.state === 'failed' || run.state === 'interrupted') engine.attention();
    else if (run.state === 'running') engine.preview();
   } else if (run.state === 'running' && previous?.id !== run.id) engine.preview();
   else if (presentation.running && previous?.output !== run.output && performance.now() - lastSoundActivity.current > 1500) {
    engine.mechanical(); lastSoundActivity.current = performance.now();
   }
  }).catch(error => { if (!cancelled) { setAudioReady(false); setAudioError((error as Error).message); } });
  return () => { cancelled = true; };
 }, [run?.id, run?.state, run?.output, enabled, open, soundWanted, audioReady, disconnected]);
 useEffect(() => {
  function visibility() { sound.current?.hum(enabled && open && soundWanted && !!presentation?.running); }
  document.addEventListener('visibilitychange', visibility);
  return () => document.removeEventListener('visibilitychange', visibility);
 }, [enabled, open, soundWanted, presentation?.running]);

 function changeEnabled(value: boolean) {
  setEnabled(value);
  if (!value) { setOpen(false); void sound.current?.setEnabled(false); }
  else { dismissed.current = null; if (soundWanted) void unlockSound(); }
 }
 function close() { dismissed.current = run?.id ?? null; setOpen(false); void sound.current?.setEnabled(false); }
 function watch() { if (soundWanted) void unlockSound(); setOpen(true); }
 function toggleSound() {
  const value = !soundWanted || !audioReady;
  setSoundWanted(value);
  if (value) void unlockSound(); else void sound.current?.setEnabled(false);
 }

 return <>
  {enabled && run && presentation && <div className="build-experience-preview" data-run-state={run.state}>
   <Activity size={24}/><div><strong>{run.provider === 'codex' ? 'Codex' : 'Claude'} · {presentation.label}</strong><span>{run.title}</span></div>
   <Button size="sm" variant="secondary" onClick={watch}>View build</Button>
  </div>}
  <Dialog open={enabled && open && !!run} onOpenChange={value => { if (!value) close(); }} title={`${run?.provider === 'claude' ? 'Claude' : 'Codex'} build`} description={run?.title} className="agent-build-dialog">
   {run && presentation && <div className={`build-chamber ${presentation.attention ? 'build-chamber-attention' : ''}`} data-run-state={run.state}>
    <header className="build-chamber-header"><span><i/>{run.provider === 'codex' ? 'CODEX' : 'CLAUDE'} / {run.active ? 'BUILD ACTIVITY' : 'RUN RESULT'}</span><button type="button" className="build-sound-button" aria-pressed={soundWanted && audioReady} onClick={toggleSound}>{soundWanted && audioReady ? <Volume2 size={15}/> : <VolumeX size={15}/>} {soundWanted && audioReady ? 'Mute sound' : 'Enable sound'}</button></header>
    <BuildCanvas run={run} presentation={presentation}/>
    <div className="build-chamber-activity"><span className="build-activity-marker"/><div><strong>{presentation.label}</strong><p>{presentation.detail}</p></div></div>
    <div className="build-chamber-footer"><span>{run.state === 'succeeded' ? 'Agent finished · review required' : run.active ? 'Driven by the agent’s recorded activity' : 'Run stopped · retained changes remain available'}</span><div><button type="button" onClick={() => changeEnabled(false)}>Turn build effects off</button>{run.active && <Button size="sm" variant="secondary" disabled={stopping || run.state === 'cancelling'} onClick={onStop}>Stop run</Button>}<Button size="sm" variant="secondary" onClick={close}>Run details</Button></div></div>
    {audioError && <p className="build-audio-note" role="status">Sound unavailable: {audioError} Graphics and run controls remain available.</p>}
    {controlError && <p className="build-control-error" role="alert">{controlError}</p>}
   </div>}
  </Dialog>
 </>;
});
