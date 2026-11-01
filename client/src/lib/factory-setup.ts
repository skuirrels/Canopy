import type { AgentConnection, AgentProviders } from './agents';
import type { FactoryCommand, FactoryDefinition, FactorySkill, FactoryTemplate, PipelineVersion } from './factory';
export type ProjectSetup = {
 projectRevision: number; setup: {id: string; templateId: string; pipelineVersionId: string} | null;
 version: PipelineVersion | null; available: boolean; connection: AgentConnection | null;
 providers: AgentProviders['providers']; templates: FactoryTemplate[];
 defaults: {defaultTemplateId: string; suggestedSkills: Record<string, FactorySkill[]>; defaultCheckTimeoutSeconds: number; starterChecks: {ecosystem: string; commands: string[]}[]};
 suggestedChecks: SuggestedCheckGroup[];
};
export type SuggestedCheckGroup = { ecosystem: string; evidence: string; checks: FactoryCommand[] };
export type CheckDraft = { line: string; directory: string; timeout: string; environmentReferences?: FactoryCommand['environmentReferences']; evidence?: string };
// Direct argv execution: quote grouping is supported; shell expansion is not.
export function parseCheck(line: string): string[] {
 const words: string[] = []; let word = '', quote = '', started = false;
 for (let i = 0; i < line.length; i++) {
  const c = line[i];
  if (c === '\\' && quote !== "'" && i + 1 < line.length && /[\s\\'"]/.test(line[i+1])) { word += line[++i]; started = true; continue; }
  if (quote) { if (c === quote) quote = ''; else word += c; started = true; continue; }
  if (c === '"' || c === "'") { quote = c; started = true; continue; }
  if (/\s/.test(c)) { if (started) { words.push(word); word = ''; started = false; } continue; }
  if (/[|&;<>`$]/.test(c)) throw new Error('Use one command per check, without pipes, && or $variables.');
  word += c; started = true;
 }
 if (quote) throw new Error('Close the quote in the command.');
 if (started) words.push(word);
 if (!words[0]) throw new Error('Enter the command to run.');
 return words;
}
export function checkCommand(draft: CheckDraft): FactoryCommand {
 const [executable, ...args] = parseCheck(draft.line);
 const timeout = Number(draft.timeout);
 if (!Number.isInteger(timeout) || timeout <= 0) throw new Error('Set how many seconds the check may run.');
 if (!draft.directory.trim()) throw new Error('Choose the folder to run it from (. is the repository root).');
 return {executable, arguments: args, workingDirectory:draft.directory.trim(), timeoutSeconds:timeout, environmentReferences:draft.environmentReferences};
}
export function commandDraft(command: FactoryCommand): CheckDraft {
 return {line:[command.executable,...command.arguments].map(x => /^[\w./:=@%+,-]+$/.test(x) ? x : "'"+x.replaceAll("'", "'\\''")+"'").join(' '), directory:command.workingDirectory, timeout:String(command.timeoutSeconds), environmentReferences:command.environmentReferences};
}
// "15 min" or "90 s", for the one-line summary under a check.
export function timeoutLabel(timeout: string): string {
 const seconds = Number(timeout);
 return Number.isInteger(seconds) && seconds > 0 ? seconds % 60 === 0 ? `${seconds / 60} min` : `${seconds} s` : 'no time limit set';
}
// Why a check can't be saved yet, or undefined when it can.
export function checkProblem(draft: CheckDraft): string | undefined {
 try { checkCommand(draft); return undefined; } catch (cause) { return (cause as Error).message; }
}
export function workflowRoles(definition: FactoryDefinition) {
 return [...new Set(definition.stages.flatMap(stage => stage.steps.filter(step => step.kind === 'agent').map(step => step.role!)))];
}
