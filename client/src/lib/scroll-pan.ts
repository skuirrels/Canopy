// Only explicitly marked blank surfaces can start a pan. Text, controls and
// drag handles keep their existing pointer behavior; touch stays browser-native.
export function bindScrollPan(surface: HTMLElement, axes: 'horizontal' | 'both' = 'both'): () => void {
 const horizontalOnly = axes === 'horizontal';
 const activeClass = horizontalOnly ? 'is-horizontal-panning' : 'is-grab-panning';
 const view = surface.ownerDocument.defaultView!;
 let gesture: { id: number; x: number; y: number; left: number; top: number; active: boolean } | undefined;
 let suppressClick = false;
 function stop() {
  const previous = gesture; gesture = undefined;
  surface.classList.remove(activeClass);
  if (previous && surface.hasPointerCapture(previous.id)) surface.releasePointerCapture(previous.id);
 }
 function down(event: PointerEvent) {
  suppressClick = false;
  if (event.defaultPrevented || event.pointerType !== 'mouse' || !event.isPrimary || event.button !== 0 || event.altKey || event.ctrlKey || event.metaKey || event.shiftKey) return;
  const target = event.target as HTMLElement | null;
  if (!target?.hasAttribute('data-pan-surface')) return;
  const overflowX = surface.scrollWidth > surface.clientWidth;
  const overflowY = surface.scrollHeight > surface.clientHeight;
  if (horizontalOnly ? !overflowX : !overflowX && !overflowY) return;
  // Native scrollbar thumbs must remain draggable, including nested card bars.
  for (let node: HTMLElement | null = target; node; node = node.parentElement) {
   const box = node.getBoundingClientRect();
   if (event.clientX >= box.left + node.clientLeft + node.clientWidth || event.clientY >= box.top + node.clientTop + node.clientHeight) return;
   if (node === surface) break;
  }
  gesture = { id: event.pointerId, x: event.clientX, y: event.clientY, left: surface.scrollLeft, top: surface.scrollTop, active: false };
 }
 function move(event: PointerEvent) {
  if (!gesture || gesture.id !== event.pointerId) return;
  if (event.buttons !== 1) { stop(); return; }
  const dx = event.clientX - gesture.x, dy = event.clientY - gesture.y;
  if (!gesture.active) {
   if (horizontalOnly) {
    if (Math.abs(dy) > 6 && Math.abs(dy) >= Math.abs(dx)) { stop(); return; }
    if (Math.abs(dx) <= 6 || Math.abs(dx) <= Math.abs(dy)) return;
   } else if (Math.hypot(dx, dy) <= 6) return;
   gesture.active = true;
   surface.classList.add(activeClass);
   surface.setPointerCapture(event.pointerId);
  }
  event.preventDefault();
  surface.scrollLeft = gesture.left - dx;
  if (!horizontalOnly) surface.scrollTop = gesture.top - dy;
 }
 function up(event: PointerEvent) {
  if (gesture?.id !== event.pointerId) return;
  suppressClick = gesture.active; stop();
 }
 function cancel(event: PointerEvent) { if (gesture?.id === event.pointerId) stop(); }
 function click(event: MouseEvent) {
  if (!suppressClick || event.detail === 0) return;
  suppressClick = false; event.preventDefault(); event.stopImmediatePropagation();
 }
 function key(event: KeyboardEvent) { if (event.key === 'Escape') stop(); }
 surface.addEventListener('pointerdown', down);
 surface.addEventListener('pointermove', move);
 surface.addEventListener('lostpointercapture', cancel);
 surface.addEventListener('click', click, true);
 view.addEventListener('pointerup', up);
 view.addEventListener('pointercancel', cancel);
 view.addEventListener('blur', stop);
 view.addEventListener('keydown', key);
 return () => {
  stop();
  surface.removeEventListener('pointerdown', down);
  surface.removeEventListener('pointermove', move);
  surface.removeEventListener('lostpointercapture', cancel);
  surface.removeEventListener('click', click, true);
  view.removeEventListener('pointerup', up);
  view.removeEventListener('pointercancel', cancel);
  view.removeEventListener('blur', stop);
  view.removeEventListener('keydown', key);
 };
}
