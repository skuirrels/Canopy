// One codec for browser imports and MCP. Consumers supply their Tiptap runtime.
export const MAX_SPEC_BYTES = 2_000_000;
export function createSpecCodec(manager, schema) {
 const checkBytes = value => { if (new TextEncoder().encode(value).byteLength > MAX_SPEC_BYTES) throw new Error('The specification is too large (maximum 2 MB, including rich-text JSON).'); };
 function parseMarkdown(markdown) {
  checkBytes(markdown);
  manager.instance.walkTokens(manager.instance.lexer(markdown), token => {
   if (token.type === 'image' || token.type === 'html') throw new Error('Images and raw HTML are not supported in task specs. Use Markdown links and text instead. No changes were saved.');
   if (token.type === 'link' && !['http:', 'https:', 'mailto:', 'tel:'].includes(new URL(token.href, 'https://planner.invalid/').protocol)) throw new Error('Specification links must use HTTP(S), mailto, tel or a relative URL. No changes were saved.');
  });
  const document = manager.parse(markdown); schema.nodeFromJSON(document).check();
  checkBytes(JSON.stringify(document)); return document;
 }
 function toMarkdown(document) { schema.nodeFromJSON(document).check(); return manager.serialize(document); }
 function specJson(markdown, previous, mode) {
  if (!['append', 'replace'].includes(mode)) throw new Error('Choose append or replace.');
  const incoming = parseMarkdown(markdown);
  const document = mode === 'append' ? { type: 'doc', content: [...(JSON.parse(previous).content ?? []), ...(incoming.content ?? [])] } : incoming;
  schema.nodeFromJSON(document).check(); const json = JSON.stringify(document); checkBytes(json); return json;
 }
 return { parseMarkdown, toMarkdown, specJson };
}
