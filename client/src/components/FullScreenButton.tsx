import { useEffect, useState } from 'react';
import { Maximize, Minimize } from 'lucide-react';
import { toast } from 'sonner';

export function FullScreenButton() {
 const [fullScreen, setFullScreen] = useState(() => !!document.fullscreenElement);
 const [pending, setPending] = useState(false);
 const available = !!document.fullscreenEnabled;
 const label = fullScreen ? 'Exit Full Screen' : 'Enter Full Screen';
 useEffect(() => {
  const update = () => setFullScreen(!!document.fullscreenElement);
  document.addEventListener('fullscreenchange', update);
  return () => document.removeEventListener('fullscreenchange', update);
 }, []);
 async function toggle() {
  setPending(true);
  try {
   if (document.fullscreenElement) await document.exitFullscreen();
   else await document.documentElement.requestFullscreen();
  } catch {
   toast.error('Full screen could not be changed. Your browser may have blocked it.');
  } finally { setPending(false); }
 }
 return <button className="icon-button fullscreen-toggle" aria-label={label} aria-pressed={fullScreen} disabled={pending || (!available && !fullScreen)} data-tooltip={!available && !fullScreen ? 'Full screen is unavailable in this browser' : fullScreen ? 'Exit Full Screen (Esc)' : label} onClick={() => { void toggle(); }}>
  {fullScreen ? <Minimize size={17}/> : <Maximize size={17}/>}<span>{label}</span>
 </button>;
}
