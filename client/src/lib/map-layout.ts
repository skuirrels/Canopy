export type MapDirection = 'horizontal' | 'vertical';
export type MapPoint = { x: number; y: number };
export const MAP_NODE_WIDTH = 320;
export const MAP_NODE_HEIGHT = 176;

// The public layout contract stores one canonical horizontal layout. Projecting
// both axes with the card's aspect ratio retains node spacing in either view;
// the inverse keeps dragging and Auto arrange on the same persistence path.
export function displayMapPoint(point: MapPoint, direction: MapDirection): MapPoint {
 return direction === 'vertical' ? { x: point.y * MAP_NODE_WIDTH / MAP_NODE_HEIGHT, y: point.x * MAP_NODE_HEIGHT / MAP_NODE_WIDTH } : { ...point };
}
export function storedMapPoint(point: MapPoint, direction: MapDirection): MapPoint {
 // This projection is its own inverse.
 return displayMapPoint(point, direction);
}
