import { useEffect, useId, useState } from 'react';
import { Maximize, Minimize } from 'lucide-react';
import { toast } from 'sonner';

export function FullScreenButton() {
 const [fullScreen, setFullScreen] = useState(() => !!document.fullscreenElement);
 const [pending, setPending] = useState(false);
 const available = !!document.fullscreenEnabled;
 const label = fullScreen ? 'Exit Full Screen' : 'Enter Full Screen';
 // The browser cannot be changed from here, so say so in the button itself rather than beside it in the top bar.
 const unsupported = !available && !fullScreen; const reasonId = useId();
 const reason = "This browser doesn't support full screen.";
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
 return <button className="icon-button fullscreen-toggle" aria-label={label} aria-pressed={fullScreen} disabled={pending || unsupported} aria-describedby={unsupported ? reasonId : undefined} data-tooltip={unsupported ? reason : fullScreen ? 'Exit Full Screen (Esc)' : label} onClick={() => { void toggle(); }}>
  {fullScreen ? <Minimize size={17}/> : <Maximize size={17}/>}<span>{unsupported ? 'Full screen unsupported' : label}</span>{unsupported && <span hidden id={reasonId}>{reason}</span>}
 </button>;
}
