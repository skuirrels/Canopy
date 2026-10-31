import type { AgentConnection, AgentProviders } from './agents';
import type { FactoryCommand, FactoryDefinition, FactorySkill, FactoryTemplate, PipelineVersion } from './factory';
export type ProjectSetup = {
 projectRevision: number; setup: {id: string; templateId: string; pipelineVersionId: string} | null;
 version: PipelineVersion | null; available: boolean; connection: AgentConnection | null;
 providers: AgentProviders['providers']; templates: FactoryTemplate[];
 defaults: {defaultTemplateId: string; suggestedSkills: Record<string, FactorySkill[]>};
};
export type CheckDraft = { line: string; directory: string; timeout: string; environmentReferences?: FactoryCommand['environmentReferences'] };
// Direct argv execution: quote grouping is supported; shell expansion is not.
export function parseCheck(line: string): string[] {
 const words: string[] = []; let word = '', quote = '', started = false;
 for (let i = 0; i < line.length; i++) {
  const c = line[i];
  if (c === '\\' && quote !== "'" && i + 1 < line.length && /[\s\\'"]/.test(line[i+1])) { word += line[++i]; started = true; continue; }
  if (quote) { if (c === quote) quote = ''; else word += c; started = true; continue; }
  if (c === '"' || c === "'") { quote = c; started = true; continue; }
  if (/\s/.test(c)) { if (started) { words.push(word); word = ''; started = false; } continue; }
  if (/[|&;<>`$]/.test(c)) throw new Error('Use one direct command per check. Shell operators and expansion are not supported.');
  word += c; started = true;
 }
 if (quote) throw new Error('Close the quoted command argument.');
 if (started) words.push(word);
 if (!words[0]) throw new Error('Enter the actual build or test command.');
 return words;
}
export function checkCommand(draft: CheckDraft): FactoryCommand {
 const [executable, ...args] = parseCheck(draft.line);
 const timeout = Number(draft.timeout);
 if (!Number.isInteger(timeout) || timeout <= 0) throw new Error('Set a positive whole-number timeout for each check.');
 if (!draft.directory.trim()) throw new Error('Choose the repository-relative directory for each check.');
 return {executable, arguments: args, workingDirectory:draft.directory.trim(), timeoutSeconds:timeout, environmentReferences:draft.environmentReferences};
}
export function commandDraft(command: FactoryCommand): CheckDraft {
 return {line:[command.executable,...command.arguments].map(x => /^[\w./:=@%+,-]+$/.test(x) ? x : "'"+x.replaceAll("'", "'\\''")+"'").join(' '), directory:command.workingDirectory, timeout:String(command.timeoutSeconds), environmentReferences:command.environmentReferences};
}
export function workflowRoles(definition: FactoryDefinition) {
 return [...new Set(definition.stages.flatMap(stage => stage.steps.filter(step => step.kind === 'agent').map(step => step.role!)))];
}
