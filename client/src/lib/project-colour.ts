export const REACT_FLOW_PINK = '#ff0073';
type RGB = [number, number, number];
function rgb(hex: string): RGB {
 // Production CSS may shorten theme colours (for example #ffffff to #fff).
 hex = hex.replace(/^#([\da-f])([\da-f])([\da-f])$/i, '#$1$1$2$2$3$3');
 if (!/^#[\da-f]{6}$/i.test(hex)) throw new Error('Expected a six-digit hex colour.');
 return [1, 3, 5].map(index => parseInt(hex.slice(index, index + 2), 16)) as RGB;
}
function hex(channels: RGB): string { return '#' + channels.map(channel => Math.round(channel).toString(16).padStart(2, '0')).join(''); }
function mix(colour: string, target: string, amount: number): string {
 const from = rgb(colour), to = rgb(target);
 return hex(from.map((value, index) => value + (to[index] - value) * amount) as RGB);
}
function luminance(colour: string): number {
 const [r, g, b] = rgb(colour).map(value => { const channel = value / 255; return channel <= .04045 ? channel / 12.92 : ((channel + .055) / 1.055) ** 2.4; });
 return .2126 * r + .7152 * g + .0722 * b;
}
export function contrastRatio(first: string, second: string): number {
 const a = luminance(first), b = luminance(second);
 return (Math.max(a, b) + .05) / (Math.min(a, b) + .05);
}
export function contrastTextColour(background: string): string {
 return contrastRatio('#000000', background) > contrastRatio('#ffffff', background) ? '#000000' : '#ffffff';
}
function readable(colour: string, background: string): string {
 if (contrastRatio(colour, background) >= 4.5) return colour;
 const target = contrastTextColour(background);
 let low = 0, high = 1;
 for (let step = 0; step < 16; step++) {
  const middle = (low + high) / 2;
  if (contrastRatio(mix(colour, target, middle), background) >= 4.5) high = middle;
  else low = middle;
 }
 return mix(colour, target, high);
}
export function projectAccentTokens(colour: string, surface: string): Record<string, string> {
 const soft = mix(surface, colour, .12);
 return {
  '--accent-source': colour,
  '--accent': readable(colour, surface),
  '--accent-fill': readable(colour, '#ffffff'),
  '--accent-hover': mix(readable(colour, '#ffffff'), '#000000', .12),
  '--accent-border': mix(surface, colour, .5),
  '--accent-soft': soft,
  '--accent-soft-strong': mix(surface, colour, .22),
  '--accent-soft-text': readable(colour, soft),
  '--on-accent-muted': '#ffffff',
 };
}
