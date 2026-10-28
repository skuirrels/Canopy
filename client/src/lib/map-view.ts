import type { MapDirection } from './map-layout';

// This preference version gives existing projects the new vertical default;
// only subsequent explicit orientation choices are remembered.
export const mapDirectionKey = (projectId: string) => `sprout:map-direction-v2:${projectId}`;
export function initialMapDirection(saved: string | null): MapDirection {
 return saved === 'horizontal' ? 'horizontal' : 'vertical';
}

export function mapFitPadding(width: number, toolbarBottom: number, bottomInset: number) {
 const side = width <= 550 ? 16 : 28;
 return { top: `${Math.max(16, toolbarBottom + 16)}px`, bottom: `${Math.max(28, bottomInset + 12)}px`, left: `${side}px`, right: `${side}px` } as const;
}
export const MAP_MIN_ZOOM = 0.01;
export const MAP_MAX_ZOOM = 1.8;
export const MAP_START_ZOOM = 0.65;
export type MapViewport = { x: number; y: number; zoom: number };
export const mapViewportKey = (projectId: string, focusId: string | null, direction: MapDirection) =>
 `sprout:map-viewport-v1:${projectId}:${focusId ?? 'root'}:${direction}`;
export function savedMapViewport(value: string | null): MapViewport | null {
 try {
  const viewport = JSON.parse(value ?? 'null');
  if (!viewport || !Number.isFinite(viewport.x) || !Number.isFinite(viewport.y) || !Number.isFinite(viewport.zoom) || viewport.zoom < MAP_MIN_ZOOM || viewport.zoom > MAP_MAX_ZOOM) return null;
  return { x: viewport.x, y: viewport.y, zoom: viewport.zoom };
 } catch { return null; }
}
export function initialMapViewport(bounds: { x: number; y: number; width: number; height: number }, width: number, height: number, toolbarBottom: number, bottomInset: number): MapViewport {
 const padding = mapFitPadding(width, toolbarBottom, bottomInset);
 const top = parseFloat(padding.top), bottom = parseFloat(padding.bottom);
 const availableHeight = Math.max(1, height - top - bottom);
 // Fit height at a readable opening scale; width deliberately spills to the right.
 const zoom = Math.max(MAP_MIN_ZOOM, Math.min(MAP_START_ZOOM, availableHeight / Math.max(1, bounds.height)));
 const left = width <= 550 ? 16 : Math.min(96, Math.max(28, width * .055));
 return { x: left - bounds.x * zoom, y: top + Math.max(0, (availableHeight - bounds.height * zoom) / 2) - bounds.y * zoom, zoom };
}
