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
