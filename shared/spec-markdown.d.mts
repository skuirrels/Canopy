export const MAX_SPEC_BYTES: number;
export function createSpecStarterKit<T>(starterKit: T): T;
type Document = { type: string; content?: Document[]; text?: string; attrs?: Record<string, unknown>; marks?: { type: string; attrs?: Record<string, unknown> }[] };
export function createSpecCodec(manager: unknown, schema: unknown): {
 parseMarkdown(markdown: string): Document;
 toMarkdown(document: unknown): string;
 specJson(markdown: string, previous: string, mode: 'append' | 'replace'): string;
};
