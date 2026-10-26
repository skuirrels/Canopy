import { bindScrollPan } from './scroll-pan.ts';

export function bindHorizontalPan(surface: HTMLElement): () => void {
 return bindScrollPan(surface, 'horizontal');
}
