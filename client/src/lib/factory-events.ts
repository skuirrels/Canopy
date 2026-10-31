// Factory panels and task dialogs are siblings in the planner. Notify only the
// factory experience; keep planner navigation and persistence independent.
export const factoryHistoryEvent = 'canopy:factory-history-changed';
export const factoryDesignerEvent = 'canopy:factory-designer-requested';
export function factoryHistoryChanged() { window.dispatchEvent(new Event(factoryHistoryEvent)); }
let pendingDesigner:{projectId:string;pipelineId?:string}|null=null;
export function pendingFactoryDesigner(projectId:string) { return pendingDesigner?.projectId===projectId ? pendingDesigner : null; }
export function clearFactoryDesigner() { pendingDesigner=null; }
export function factoryDesignerRequested(projectId:string,pipelineId?:string) { pendingDesigner={projectId,pipelineId};window.dispatchEvent(new CustomEvent(factoryDesignerEvent,{detail:pendingDesigner})); }
