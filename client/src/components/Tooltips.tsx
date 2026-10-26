import { useEffect, useId, useLayoutEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import './tooltips.css';

type Tip = { owner: HTMLElement; text: string };

// Delegation keeps existing buttons, drag handles and chart gestures intact.
export function Tooltips() {
 const id = useId(); const [tip, setTip] = useState<Tip | null>(null);
 const [position, setPosition] = useState<{ left: number; top: number } | null>(null);
 const popup = useRef<HTMLDivElement>(null);
 useEffect(() => {
  let owner: HTMLElement | null = null;
  let opening: ReturnType<typeof setTimeout> | undefined, closing: ReturnType<typeof setTimeout> | undefined;
  const clearTimers = () => { clearTimeout(opening); clearTimeout(closing); opening = undefined; closing = undefined; };
  const hide = () => { clearTimers(); owner = null; setTip(null); setPosition(null); };
  const find = (target: EventTarget | null) => target instanceof Element ? target.closest<HTMLElement>('[data-tooltip]') : null;
  const show = (next: HTMLElement | null, keyboard = false) => {
   if (!next || !next.dataset.tooltip?.trim()) return;
   clearTimeout(closing); closing = undefined;
   if (owner === next && (keyboard || popup.current || opening !== undefined)) { if (keyboard) { clearTimeout(opening); opening = undefined; setTip({ owner: next, text: next.dataset.tooltip! }); } return; }
   hide(); owner = next;
   const reveal = () => { opening = undefined; if (owner === next && next.isConnected) setTip({ owner: next, text: next.dataset.tooltip! }); };
   if (keyboard) reveal(); else opening = setTimeout(reveal, 150);
  };
  const leave = () => { clearTimeout(opening); opening = undefined; if (closing === undefined) closing = setTimeout(hide, 100); };
  const over = (event: PointerEvent) => {
   if (event.pointerType === 'touch' || event.buttons) return;
   if (popup.current?.contains(event.target as Node)) { clearTimeout(closing); closing = undefined; return; }
   const next = find(event.target);
   if (next) show(next); else if (owner) leave();
  };
  const out = (event: PointerEvent) => {
   const next = event.relatedTarget as Node | null;
   if (next && (owner?.contains(next) || popup.current?.contains(next))) return;
   if (owner) leave();
  };
  const focus = (event: FocusEvent) => { if (event.target instanceof Element && event.target.matches(':focus-visible')) show(find(event.target), true); };
  const blur = (event: FocusEvent) => { if (!owner?.contains(event.relatedTarget as Node | null)) hide(); };
  const key = (event: KeyboardEvent) => { if (event.key === 'Escape') hide(); };
  const scroll = (event: Event) => {
   if (popup.current?.contains(event.target as Node)) return;
   // Focusing an off-screen chart control scrolls it into view after focusin.
   // Keep its keyboard hint and measure again at the final visible position.
   if (owner?.contains(document.activeElement)) {
    const box = owner.getBoundingClientRect();
    if (box.bottom > 0 && box.top < innerHeight && box.right > 0 && box.left < innerWidth) { setTip({ owner, text: owner.dataset.tooltip! }); return; }
   }
   hide();
  };
  document.addEventListener('pointerover', over);
  document.addEventListener('pointerout', out);
  document.addEventListener('pointerdown', hide, true);
  document.addEventListener('focusin', focus);
  document.addEventListener('focusout', blur);
  document.addEventListener('keydown', key);
  document.addEventListener('scroll', scroll, true);
  window.addEventListener('resize', hide);
  window.addEventListener('blur', hide);
  return () => {
   clearTimers();
   document.removeEventListener('pointerover', over);
   document.removeEventListener('pointerout', out);
   document.removeEventListener('pointerdown', hide, true);
   document.removeEventListener('focusin', focus);
   document.removeEventListener('focusout', blur);
   document.removeEventListener('keydown', key);
   document.removeEventListener('scroll', scroll, true);
   window.removeEventListener('resize', hide);
   window.removeEventListener('blur', hide);
  };
 }, []);
 useLayoutEffect(() => {
  if (!tip || !popup.current) return;
  const anchor = tip.owner.getBoundingClientRect(), box = popup.current.getBoundingClientRect();
  const edge = 12, gap = 10;
  const left = Math.max(edge, Math.min(anchor.left + (anchor.width - box.width) / 2, window.innerWidth - box.width - edge));
  const above = anchor.top - box.height - gap;
  const top = Math.max(edge, Math.min(above >= edge ? above : anchor.bottom + gap, window.innerHeight - box.height - edge));
  setPosition({ left, top });
  const target = tip.owner.contains(document.activeElement) ? document.activeElement! : tip.owner;
  const descriptions = (target.getAttribute('aria-describedby') ?? '').split(/\s+/).filter(Boolean);
  target.setAttribute('aria-describedby', [...new Set([...descriptions, id])].join(' '));
  // A view change or removed trigger must not leave a floating tooltip behind.
  const observer = new MutationObserver(() => { if (!tip.owner.isConnected) { setTip(null); setPosition(null); } });
  observer.observe(document.body, { childList: true, subtree: true });
  return () => {
   observer.disconnect();
   const remaining = (target.getAttribute('aria-describedby') ?? '').split(/\s+/).filter(value => value && value !== id);
   if (remaining.length) target.setAttribute('aria-describedby', remaining.join(' ')); else target.removeAttribute('aria-describedby');
  };
 }, [tip, id]);
 if (!tip) return null;
 const [heading, ...details] = tip.text.split('\n');
 return createPortal(<div ref={popup} id={id} role="tooltip" className="canopy-tooltip" style={{ ...position, visibility: position ? 'visible' : 'hidden' }}><strong>{heading}</strong>{details.map((line, index) => <span key={index}>{line}</span>)}</div>, document.body);
}
