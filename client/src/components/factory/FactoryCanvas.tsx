import { useEffect, useMemo, useRef, useState } from 'react';
import { ReactFlow, ReactFlowProvider, Background, BackgroundVariant, Controls, MiniMap, Handle, Position, MarkerType, applyNodeChanges, useReactFlow } from '@xyflow/react';
import type { Node, NodeProps, Connection, NodeChange, Viewport } from '@xyflow/react';
import ELK from 'elkjs/lib/elk-api.js';
import elkWorker from 'elkjs/lib/elk-worker.min.js?url';
import { ArrowRight, Bot, CheckSquare, FileCheck2, GripVertical, Plus, Terminal, WandSparkles, Expand } from 'lucide-react';
import type { FactoryDefinition, FactoryDiagnostic, FactoryStage } from '../../lib/factory';
import { stageWouldCycle } from '../../lib/factory';
import '@xyflow/react/dist/style.css';

export type FactorySelection = { stageId: string; stepId?: string } | null;
type StageData = { stage: FactoryStage; selectedStep?: string; invalid: boolean; readOnly: boolean; onSelect: (selection: FactorySelection) => void };
type StageNode = Node<StageData, 'factoryStage'>;
type GroupNode = Node<{ name: string; count: number; onAdd?: () => void }, 'factoryGroup'>;
const icons = { agent: Bot, command: Terminal, manual: CheckSquare, artifact: FileCheck2 };
const elk = new ELK({ workerUrl: elkWorker, algorithms: ['layered'] });
const stageHeight = (stage: FactoryStage) => 112 + stage.steps.length * 90;

function StageCard({ data, selected }: NodeProps<StageNode>) {
 const { stage } = data;
 return <article className={`factory-stage-card ${selected ? 'selected' : ''} ${data.invalid ? 'invalid' : ''}`}>
  <Handle type="target" position={Position.Left} role="button" tabIndex={data.readOnly ? -1 : 0} aria-disabled={data.readOnly} aria-label={`Prerequisite handle for ${stage.name || 'untitled stage'}`} onKeyDown={event => { if (!data.readOnly && (event.key === 'Enter' || event.key === ' ')) { event.preventDefault(); event.currentTarget.click(); } }}/>
  <button type="button" className="factory-stage-heading" onClick={() => data.onSelect({ stageId: stage.id })}><span className="factory-stage-grip"><GripVertical size={15}/></span><span><strong>{stage.name || 'Untitled stage'}</strong><small>{stage.steps.length} step{stage.steps.length === 1 ? '' : 's'}</small></span>{data.invalid && <span className="factory-invalid-dot" aria-label="Validation errors"/>}</button>
  <div className="factory-stage-steps">{stage.steps.map(step => {
   const Icon = icons[step.kind as keyof typeof icons] ?? FileCheck2;
   return <button type="button" key={step.id} className={`factory-step-summary nodrag ${data.selectedStep === step.id ? 'active' : ''}`} onClick={event => { event.stopPropagation(); data.onSelect({ stageId: stage.id, stepId: step.id }); }}><span className={`factory-kind-icon kind-${step.kind}`}><Icon size={17}/></span><span><strong>{step.name || 'Untitled step'}</strong><small>{step.kind === 'agent' ? `${step.provider || 'Project agent'} · ${step.role || 'Choose role'}` : step.kind === 'command' ? step.command?.executable || 'Command required' : step.kind === 'manual' ? 'Human decision' : step.kind === 'artifact' ? 'Artifact check' : 'Choose step type'}</small>{!!step.skills?.length && <span className="factory-skill-chips">{step.skills.map((skill, index) => <span key={index} title={`${skill.source} · availability unverified`}>{skill.id || 'Unnamed skill'}</span>)}</span>}</span><ArrowRight size={13}/></button>;
  })}{!stage.steps.length && <p className="factory-help">Select this stage to add its first step.</p>}</div>
  <footer>{stage.dependsOn.length ? `${stage.dependsOn.length} prerequisite${stage.dependsOn.length === 1 ? '' : 's'}` : 'Root stage'}<span>Configuration</span></footer>
  <Handle type="source" position={Position.Right} role="button" tabIndex={data.readOnly ? -1 : 0} aria-disabled={data.readOnly} aria-label={`Dependency handle from ${stage.name || 'untitled stage'}`} onKeyDown={event => { if (!data.readOnly && (event.key === 'Enter' || event.key === ' ')) { event.preventDefault(); event.currentTarget.click(); } }}/>
 </article>;
}
function GroupCard({ data }: NodeProps<GroupNode>) { return <div className="factory-group-heading"><span><strong>{data.name || 'Ungrouped'}</strong><small>{data.count} stage{data.count === 1 ? '' : 's'}</small></span>{data.onAdd && <button type="button" className="icon-button nodrag" aria-label={`Add stage to ${data.name || 'ungrouped'}`} onClick={data.onAdd}><Plus size={16}/></button>}</div>; }
const nodeTypes = { factoryStage: StageCard, factoryGroup: GroupCard };
type Point = { x: number; y: number };
function preference<T>(key: string): T | null { try { return JSON.parse(localStorage.getItem(key) || 'null'); } catch { return null; } }
function remember(key: string, value: unknown) { try { localStorage.setItem(key, JSON.stringify(value)); } catch { /* Camera and arrangement are optional browser preferences. */ } }

function Canvas({ definition, identity, selection, onSelect, onConnect, onAddStage, readOnly, diagnostics }: {
 definition: FactoryDefinition; identity: string; selection: FactorySelection; onSelect: (selection: FactorySelection) => void;
 onConnect: (source: string, target: string) => void; onAddStage: (group?: string) => void; readOnly: boolean; diagnostics: FactoryDiagnostic[];
}) {
 const layoutKey = `canopy:factory-layout:${identity}`, cameraKey = `canopy:factory-camera:${identity}`;
 const saved = useRef(preference<Record<string, Point>>(layoutKey) ?? {});
 const [nodes, setNodes] = useState<Node[]>([]); const [error, setError] = useState(''); const [arranging, setArranging] = useState(false);
 const { fitView } = useReactFlow(); const generation = useRef(0);
 const groups = useMemo(() => [...new Set(definition.stages.map(stage => stage.group))], [definition]);
 const selectionRef = useRef(onSelect); selectionRef.current = onSelect;
 useEffect(() => {
  const next: Node[] = [];
  groups.forEach((name, column) => {
   const stages = definition.stages.filter(stage => stage.group === name); const groupId = `factory-group:${column}`;
   let offset = 64;
   const children: Node[] = stages.map(stage => {
    const remembered = saved.current[stage.id];
    const point = remembered && Number.isFinite(remembered.x) && Number.isFinite(remembered.y) ? { x: Math.max(18, Math.min(remembered.x, 24)), y: Math.max(64, remembered.y) } : { x: 20, y: offset };
    offset = Math.max(offset, point.y + stageHeight(stage) + 28);
    return { id: stage.id, parentId: groupId, extent: 'parent', type: 'factoryStage', position: point, selected: selection?.stageId === stage.id, style: { width: 316, height: stageHeight(stage) }, data: { stage, selectedStep: selection?.stageId === stage.id ? selection.stepId : undefined, invalid: diagnostics.some(d => d.stageId === stage.id), readOnly, onSelect: (value: FactorySelection) => selectionRef.current(value) } };
   });
   next.push({ id: groupId, type: 'factoryGroup', position: { x: column * 400, y: 0 }, selectable: false, draggable: false, connectable: false, style: { width: 356, height: Math.max(250, offset + 10) }, data: { name, count: stages.length, onAdd: readOnly ? undefined : () => onAddStage(name) } }, ...children);
  });
  setNodes(next);
 }, [definition, groups, selection, diagnostics, onAddStage, readOnly]);
 useEffect(() => () => { generation.current++; }, []);
 const edges = definition.stages.flatMap(stage => stage.dependsOn.filter(id => definition.stages.some(s => s.id === id)).map(id => ({ id: `${id}->${stage.id}`, source: id, target: stage.id, type: 'smoothstep', markerEnd: { type: MarkerType.ArrowClosed, color: 'var(--accent)' }, style: { stroke: 'var(--accent)', strokeWidth: 2 }, label: 'depends on', labelStyle: { fill: 'var(--secondary-text)', fontSize: 11 }, labelBgStyle: { fill: 'var(--surface)' } })));
 const validConnection = (connection: Connection | { source: string; target: string }) => !readOnly && !!connection.source && !!connection.target && !stageWouldCycle(definition, connection.source, connection.target);
 async function arrange() {
  const current = ++generation.current; setArranging(true); setError('');
  try {
   const points: Record<string, Point> = {};
   // Visual ordering within a display group is independent of executable dependencies.
   for (const name of groups) {
    const stages = definition.stages.filter(stage => stage.group === name);
    const result = await elk.layout({ id: 'column', layoutOptions: { 'elk.algorithm': 'layered', 'elk.direction': 'DOWN', 'elk.padding': '[top=0,left=0,bottom=0,right=0]', 'elk.layered.spacing.nodeNodeBetweenLayers': '28' }, children: stages.map(stage => ({ id: stage.id, width: 316, height: stageHeight(stage) })), edges: stages.slice(1).map((stage, index) => ({ id: `arrangement:${index}`, sources: [stages[index].id], targets: [stage.id] })) });
    for (const child of result.children ?? []) points[child.id] = { x: 20, y: 64 + (child.y ?? 0) };
   }
   if (current !== generation.current) return;
   saved.current = points; remember(layoutKey, points);
   setNodes(previous => previous.map(node => node.type === 'factoryStage' && points[node.id] ? { ...node, position: points[node.id] } : node));
  } catch (error) { if (current === generation.current) setError(`Could not arrange stages: ${(error as Error).message}`); }
  finally { if (current === generation.current) setArranging(false); }
 }
 const initial = preference<Viewport>(cameraKey);
 const viewport = initial && [initial.x, initial.y, initial.zoom].every(Number.isFinite) && initial.zoom >= .15 && initial.zoom <= 1.5 ? initial : { x: 24, y: 28, zoom: .8 };
 return <div className="factory-canvas"><div className="factory-canvas-tools"><button type="button" onClick={() => { void fitView({ padding: .12, maxZoom: 1 }); }}><Expand size={14}/>Fit</button><button type="button" disabled={arranging} onClick={() => { void arrange(); }}><WandSparkles size={14}/>{arranging ? 'Arranging…' : 'Auto arrange'}</button><span>Arrangement stays in this browser.</span></div>{error && <p role="alert" className="factory-error">{error}</p>}
  <ReactFlow nodes={nodes} edges={edges} nodeTypes={nodeTypes} defaultViewport={viewport} minZoom={.15} maxZoom={1.5} nodesDraggable={!readOnly} nodesConnectable={!readOnly} deleteKeyCode={null} onNodesChange={(changes: NodeChange[]) => setNodes(current => applyNodeChanges(changes, current))} onNodeDragStop={(_, node) => { if (node.type !== 'factoryStage') return; saved.current = { ...saved.current, [node.id]: node.position }; remember(layoutKey, saved.current); }} onNodeClick={(_, node) => { if (node.type === 'factoryStage') onSelect({ stageId: node.id }); }} onConnect={connection => { if (validConnection(connection)) onConnect(connection.source, connection.target); }} isValidConnection={validConnection} onMoveEnd={(_, camera) => remember(cameraKey, camera)} onPaneClick={() => onSelect(null)} proOptions={{ hideAttribution: false }}>
   <Background variant={BackgroundVariant.Dots} color="var(--map-grid)" gap={24}/><Controls showInteractive={false} showFitView={false}/><MiniMap pannable zoomable nodeColor={node => node.type === 'factoryGroup' ? 'var(--surface-hover)' : 'var(--accent)'} maskColor="var(--map-mini-mask)"/>
  </ReactFlow>
 </div>;
}
export function FactoryCanvas(props: Parameters<typeof Canvas>[0]) { return <ReactFlowProvider><Canvas key={props.identity} {...props}/></ReactFlowProvider>; }
