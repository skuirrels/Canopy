// Structured Markdown extraction shared by the app and local callers. This does
// not infer work from prose or execute instructions contained in an uploaded plan.
export function parsePlan(markdown, source) {
 if (!markdown.trim()) throw new Error('Choose a Markdown plan or paste its contents.');
 const clean = value => value.replace(/\*\*|__/g, '').replace(/`([^`]+)`/g, '$1').trim();
 const slug = value => clean(value).toLowerCase().replace(/[^\p{L}\p{N}]+/gu, '-').replace(/^-|-$/g, '');
 const sectionTitle = value => clean(value).replace(/^\d+(?:\.\d+)*\.?\s+/, '');
 const shortTitle = value => {
  const title = clean(value);
  if (title.length <= 200) return title;
  warnings.push('A long title was shortened to fit; its full text is preserved in the specification.');
  return title.slice(0, 197).replace(/\s+\S*$/, '') + '…';
 };
 const cells = line => {
  const values = []; let value = '', code = false, escaped = false;
  for (const char of line.trim().replace(/^\|/, '').replace(/\|$/, '')) {
   if (escaped) { value += char === '|' ? '|' : '\\' + char; escaped = false; continue; }
   if (char === '\\') { escaped = true; continue; }
   if (char === '`') code = !code;
   if (char === '|' && !code) { values.push(value.trim()); value = ''; } else value += char;
  }
  if (escaped) value += '\\'; values.push(value.trim()); return values;
 };
 const items = [], warnings = [], components = new Map(), seen = new Set();
 let heading = 'Plan', section = 'Plan', fence = null, lastTask = null, checklistStack = [];
 const lines = markdown.replace(/\r\n?/g, '\n').split('\n');
 function component() {
  const key = 'section:' + slug(section).slice(0, 190);
  if (!components.has(key)) {
   const item = { key, title: shortTitle(sectionTitle(section)), parentKey: null, markdown: `Source: ${source}\n\nSection: ${heading}`, priority: 'none', sourcePriority: '', phase: '', acceptance: '', dependencyKeys: [], sourceLine: 0 };
   components.set(key, item); items.push(item); seen.add(key);
  }
  return key;
 }
 function task(key, title, body, acceptance, sourcePriority, dependencyKeys, line, parentKey) {
  if (seen.has(key)) throw new Error(`Duplicate plan ID '${key}' at line ${line}. Give every task a unique ID.`);
  seen.add(key);
  const item = { key, title: shortTitle(title), parentKey: parentKey ?? component(), markdown: `Source: ${source}\n\nSource section: ${heading}\n\nSource line: ${line}\n\n${body}`, priority: ['none','low','medium','high','urgent'].includes(sourcePriority.toLowerCase()) ? sourcePriority.toLowerCase() : 'none', sourcePriority, phase: '', acceptance: acceptance.trim(), verificationMethod: '', dependencyKeys, sourceLine: line };
  items.push(item); lastTask = item; return item;
 }
 for (let index = 0; index < lines.length; index++) {
  const line = lines[index];
  const boundary = line.match(/^\s*(`{3,}|~{3,})/);
  if (boundary) { if (!fence) fence = boundary[1][0]; else if (fence === boundary[1][0]) fence = null; continue; }
  if (fence) continue;
  const h = line.match(/^(#{1,6})\s+(.+?)\s*#*$/);
  if (h) { heading = h[2]; if (h[1].length >= 2) section = heading; lastTask = null; checklistStack = []; continue; }
  if (line.includes('|') && index + 1 < lines.length && /^\s*\|?\s*:?-{3,}/.test(lines[index+1])) {
   const headers = cells(line).map(v => clean(v).toLowerCase());
   const col = names => headers.findIndex(h => names.includes(h));
   const titleCol = col(['requirement','task','title','task title']);
   const idCol = col(['id','key','requirement id','task id']);
   const acceptanceCol = col(['acceptance','acceptance criteria','acceptance test','expected result']);
   const priorityCol = col(['pri','priority']);
   const dependenciesCol = col(['dependencies','depends on','prerequisites']);
   const phaseCol = col(['phase','delivery phase']);
   index++;
   while (index + 1 < lines.length && lines[index+1].trim().includes('|') && lines[index+1].trim()) {
    index++; if (titleCol < 0) continue;
    const row = cells(lines[index]); const title = row[titleCol] ?? '';
    if (!title.trim()) continue;
    const key = clean(idCol < 0 ? `task:${slug(section).slice(0, 70)}:${slug(title).slice(0, 120)}` : row[idCol] ?? '');
    if (!key) throw new Error(`Missing task ID on line ${index+1}.`);
    const body = headers.map((h,c) => `**${h}:** ${row[c] ?? ''}`).join('\n\n');
    const dependencies = dependenciesCol < 0 ? [] : clean(row[dependenciesCol] ?? '').split(/[,;\s]+/).filter(v => v && !['none','—','-'].includes(v.toLowerCase()));
    const item = task(key, `${idCol < 0 ? '' : key + ': '}${title}`, body, acceptanceCol < 0 ? '' : row[acceptanceCol] ?? '', priorityCol < 0 ? '' : clean(row[priorityCol] ?? ''), dependencies, index+1);
    item.phase = phaseCol < 0 ? '' : clean(row[phaseCol] ?? '');
   }
   lastTask = null; continue;
  }
  const checkbox = line.match(/^(\s*)[-*+]\s+\[[ xX]\]\s+(.+)$/);
  const numbered = line.match(/^\s*(\d+)[.)]\s+(.+)$/);
  if (checkbox || numbered) {
   const text = checkbox ? checkbox[2] : numbered[2];
   const explicit = clean(text).match(/^([A-Za-z][A-Za-z0-9_-]*-\d+)\s*[:—-]\s*(.+)$/);
   let parentKey;
   if (checkbox) {
    const indent = checkbox[1].replace(/\t/g, '    ').length;
    while (checklistStack.length && checklistStack.at(-1).indent >= indent) checklistStack.pop();
    parentKey = checklistStack.at(-1)?.key;
    const key = explicit?.[1] ?? `task:${slug(section).slice(0, 70)}:${slug(text).slice(0, 120)}`;
    task(key, text, text, '', '', [], index+1, parentKey);
    checklistStack.push({indent, key});
   } else {
    task(`step:${slug(section)}:${numbered[1]}`, text, text, '', '', [], index+1);
    checklistStack = [];
   }
   continue;
  }
  // Preserve supporting prose and explicit criteria/dependencies beneath a step.
  if (lastTask && line.trim()) {
   lastTask.markdown += '\n' + line;
   const acceptance = line.match(/^\s*(?:[-*]\s*)?Acceptance(?: criteria)?\s*:\s*(.+)$/i);
   if (acceptance) lastTask.acceptance = acceptance[1].trim();
   const dependency = line.match(/^\s*(?:[-*]\s*)?(?:Depends on|Dependencies)\s*:\s*(.+)$/i);
   if (dependency) lastTask.dependencyKeys = clean(dependency[1]).split(/[,;\s]+/).filter(Boolean);
  }
 }
 const tasks = items.filter(i => i.parentKey !== null);
 if (!tasks.length) throw new Error('No structured tasks found. Use a table with Requirement or Task columns, numbered steps, or Markdown checklists. Prose-only plans need a task breakdown first.');
 for (const item of tasks) {
  for (const dependency of item.dependencyKeys) if (!seen.has(dependency)) warnings.push(`${item.key}: dependency '${dependency}' is unresolved. Correct it before importing.`);
 }
 const missing = tasks.filter(i => !i.acceptance).length;
 if (missing) warnings.push(`${missing} tasks have no explicit acceptance criteria. These will remain open for clarification.`);
 warnings.push('Only requirement/task tables, numbered steps and checklists are extracted. Narrative, findings, delivery-phase tables and risk tables remain in the source plan. Dependencies are extracted only when explicitly supplied; review or add them below.');
 return { items, warnings: [...new Set(warnings)] };
}
