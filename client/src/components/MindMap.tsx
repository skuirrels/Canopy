import { useState, useEffect, useRef, useMemo, useCallback } from 'react';
import { ReactFlow, ReactFlowProvider, Background, Controls, MiniMap, Handle, Position, applyNodeChanges, useReactFlow, BackgroundVariant, MarkerType, useUpdateNodeInternals, useNodesInitialized, ControlButton } from '@xyflow/react';
import type { Node, Edge, NodeProps, NodeChange, Connection } from '@xyflow/react';
import ELK from 'elkjs/lib/elk-api.js';
import elkWorker from 'elkjs/lib/elk-worker.min.js?url';
import { Plus, GitBranch, ChevronRight, ChevronDown, Expand, WandSparkles, Link2, Focus, FileText, ArrowRight, ArrowDown, ChevronUp } from 'lucide-react';
import { toast } from 'sonner';
import { useWorkspace } from '../lib/workspace';
import { descendants, fields, dateLabel, assignedPeople } from '../lib/types';
import type { Item, Project, Status, Person } from '../lib/types';
import { displayMapPoint, storedMapPoint, MAP_NODE_WIDTH as NODE_WIDTH, MAP_NODE_HEIGHT as NODE_HEIGHT } from '../lib/map-layout';
import type { MapDirection } from '../lib/map-layout';
import { initialMapDirection, mapDirectionKey, mapFitPadding, MAP_MIN_ZOOM } from '../lib/map-view';
import { AssigneeAvatars, PriorityIcon } from './TaskCard';
import { Button } from './ui/button';
import '@xyflow/react/dist/style.css';
import { TaskEstimate } from './TaskEstimate';
import { ChartExport } from './ChartExport';
import { captureMindMap } from '../lib/chart-export';

type TaskData = { direction: MapDirection; item?: Item; title: string; color: string; status?: Status; people: Person[]; childCount: number; collapsed: boolean; onToggle: () => void; onOpen: () => void; onFocus: () => void; onAdd: () => void; root: boolean };
type MapNode = Node<TaskData>;
function MapCard({ id, data }: NodeProps<MapNode>) {
 const vertical = data.direction === 'vertical'; const updateNodeInternals = useUpdateNodeInternals();
 useEffect(() => { updateNodeInternals(id); }, [id, data.direction, updateNodeInternals]);
 return <div className={`map-card ${data.root ? 'map-root' : ''} ${vertical ? 'map-card-vertical' : ''}`} style={{ '--branch-color': data.color } as React.CSSProperties}>
  <Handle type="target" position={vertical ? Position.Top : Position.Left} id="tree-in" isConnectable={false}/>
  {!data.root && <Handle type="target" position={vertical ? Position.Left : Position.Top} id="dependency-in" className="dependency-handle"/>}
  {data.root ? <><span className="map-root-eyebrow"><GitBranch size={12}/>{data.item ? 'FOCUSED BRANCH' : 'YOUR PROJECT'}</span>{data.item ? <button className="nodrag map-title" onClick={data.onOpen}>{data.title}</button> : <div className="map-title">{data.title}</div>}<div className="map-root-footer">{data.childCount} direct item{data.childCount !== 1 ? 's' : ''}<button className="nodrag" onClick={data.onAdd} aria-label="Add branch"><Plus size={16}/></button></div></> : <><div className="map-node-top"><span className="map-status"><span className="status-dot" style={{ background: data.status?.color }}/>{data.status?.name}</span>{data.item && <PriorityIcon priority={data.item.priority}/>}</div><button className="nodrag map-title" onClick={data.onOpen}>{data.title}</button><div className="map-node-footer"><span className="map-task-details"><TaskEstimate hours={data.item?.estimatedHours}/>{data.item?.dueDate ? dateLabel(data.item.dueDate) : <FileText size={12}/>}</span><div className="map-node-buttons">{data.childCount > 0 && <button className="nodrag" onClick={data.onFocus} title="Focus on this branch" aria-label={`Focus ${data.title}`}><Focus size={13}/></button>}<button className="nodrag" onClick={data.onAdd} title="Add subitem" aria-label={`Add subitem to ${data.title}`}><Plus size={14}/></button><AssigneeAvatars people={data.people}/></div></div></>}
  <Handle type="source" position={vertical ? Position.Bottom : Position.Right} id="tree-out" isConnectable={false}/>
  {!data.root && <Handle type="source" position={vertical ? Position.Right : Position.Bottom} id="dependency-out" className="dependency-handle"/>}
  {data.childCount > 0 && !data.root && <button className="nodrag branch-toggle" onClick={data.onToggle} aria-label={`${data.collapsed ? 'Expand' : 'Collapse'} ${data.title}, ${data.childCount} subitems`} title={data.collapsed ? 'Expand branch' : 'Collapse branch'}>{data.collapsed ? (vertical ? <ChevronDown size={12}/> : <ChevronRight size={12}/>) : (vertical ? <ChevronUp size={12}/> : <ChevronDown size={12}/>)}<span>{data.childCount}</span></button>}
 </div>;
}
const nodeTypes = { task: MapCard };
const elk = new ELK({ workerUrl: elkWorker, algorithms: ['layered'] });
function MapCanvas({ project, items, focusId, onOpen, onFocus, onAdd }: { project: Project; items: Item[]; focusId: string | null; onOpen: (id: string) => void; onFocus: (id: string) => void; onAdd: (parent?: string | null) => void }) {
 const { data, pending, mutate } = useWorkspace(); const { fitView, getIntersectingNodes, getNodesBounds, viewportInitialized } = useReactFlow();
 const nodesInitialized = useNodesInitialized();
 const [fitRevision, setFitRevision] = useState(0);
 const dropTarget = useRef<string | null>(null); const preserveViewport = useRef(false);
 const [direction, setDirection] = useState<MapDirection>(() => initialMapDirection(localStorage.getItem(mapDirectionKey(project.id))));
 const [nodes, setNodes] = useState<MapNode[]>([]); const [edges, setEdges] = useState<Edge[]>([]); const [collapsed, setCollapsed] = useState<Set<string>>(new Set()); const [layoutVersion, setLayoutVersion] = useState(0); const [links, setLinks] = useState(true); const [arranging, setArranging] = useState(true);
 const container = useRef<HTMLDivElement>(null); const toolbar = useRef<HTMLDivElement>(null);
 const previousStructure = useRef(''); const forceLayout = useRef(false); const rootId = focusId || `project:${project.id}`;
 // Fit only after ELK and React Flow agree on the measured, visible nodes.
 // Both Fit buttons use the same overlay-aware camera framing as initial entry.
 const fitMapToScreen = useCallback((duration = 300) => {
  const root = container.current; if (!root || !root.clientWidth || !root.clientHeight) return Promise.resolve(false);
  const box = root.getBoundingClientRect();
  const top = toolbar.current ? toolbar.current.getBoundingClientRect().bottom - box.top : 0;
  const overlays = root.querySelectorAll<HTMLElement>('.react-flow__controls,.react-flow__minimap,.map-hint');
  const bottom = Math.max(0, ...[...overlays].filter(element => element.getClientRects().length).map(element => box.bottom - element.getBoundingClientRect().top));
  return fitView({ padding: mapFitPadding(root.clientWidth, top, bottom), duration, minZoom: MAP_MIN_ZOOM, maxZoom: 1 });
 }, [fitView]);
 const showOverview = useCallback(() => {
  if (!preserveViewport.current) void fitMapToScreen(0);
 }, [fitMapToScreen]);
 useEffect(() => {
  if (!fitRevision || arranging || !nodesInitialized || !viewportInitialized) return;
  let second = 0;
  const first = requestAnimationFrame(() => { second = requestAnimationFrame(showOverview); });
  return () => { cancelAnimationFrame(first); cancelAnimationFrame(second); };
 }, [fitRevision, arranging, nodesInitialized, viewportInitialized, showOverview]);
 useEffect(() => {
  let timer: ReturnType<typeof setTimeout>;
  const observer = new ResizeObserver(() => { clearTimeout(timer); timer = setTimeout(showOverview, 100); });
  if (container.current) observer.observe(container.current);
  if (toolbar.current) observer.observe(toolbar.current);
  return () => { clearTimeout(timer); observer.disconnect(); };
 }, [showOverview]);
 const allItems = useMemo(() => data.items.filter(i => i.projectId === project.id), [data.items, project.id]);
 const scope = useMemo(() => focusId ? descendants(allItems, focusId) : new Set(allItems.map(i => i.id)), [allItems, focusId]);
 const visible = useMemo(() => {
  const wanted = new Set(items.filter(i => scope.has(i.id)).map(i => i.id));
  // Keep ancestor paths when filtering, so matching tasks remain structurally readable.
  for (const id of [...wanted]) { let current = allItems.find(i => i.id === id); while (current?.parentId && current.parentId !== focusId) { wanted.add(current.parentId); current = allItems.find(i => i.id === current!.parentId); } }
  const hidden = new Set<string>(); for (const id of collapsed) for (const child of descendants(allItems, id)) hidden.add(child);
  return allItems.filter(i => wanted.has(i.id) && !hidden.has(i.id));
 }, [items, scope, allItems, collapsed, focusId]);
 const visibleKey = visible.map(i => `${i.id}:${i.parentId}`).join('|') + rootId + ':' + direction;
 const callbacks = useRef({ onOpen, onFocus, onAdd }); callbacks.current = { onOpen, onFocus, onAdd };
 useEffect(() => {
  let cancelled = false;
  async function arrange() {
   const focus = allItems.find(i => i.id === focusId);
   const makeData = (item?: Item, root = false): TaskData => ({ direction, item, root, title: item?.title || project.name, color: project.color, status: data.statuses.find(s => s.id === item?.statusId), people: item ? assignedPeople(item, data.people) : [], childCount: allItems.filter(i => i.parentId === (item?.id || null)).length, collapsed: !!item && collapsed.has(item.id), onToggle: () => item && setCollapsed(old => { const next = new Set(old); if (next.has(item.id)) next.delete(item.id); else next.add(item.id); return next; }), onOpen: () => item && callbacks.current.onOpen(item.id), onFocus: () => item && callbacks.current.onFocus(item.id), onAdd: () => callbacks.current.onAdd(item?.id ?? null) });
   const baseNodes: MapNode[] = [{ id: rootId, type: 'task', position: { x: 0, y: 0 }, data: makeData(focus, true), draggable: false, width: NODE_WIDTH, height: NODE_HEIGHT }, ...visible.map(item => ({ id: item.id, type: 'task', position: { x: 0, y: 0 }, data: makeData(item), width: NODE_WIDTH, height: NODE_HEIGHT }))];
   const treeEdges: Edge[] = visible.map(item => ({ id: `tree-${item.id}`, source: item.parentId && visible.some(i => i.id === item.parentId) ? item.parentId : rootId, target: item.id, sourceHandle: 'tree-out', targetHandle: 'tree-in', type: 'smoothstep', style: { stroke: 'var(--map-tree)', strokeWidth: 1.7, opacity: 0.7 } }));
   const structureChanged = previousStructure.current !== visibleKey; const forced = forceLayout.current;
   const layout = await elk.layout({ id: 'root', layoutOptions: { 'elk.algorithm': 'layered', 'elk.direction': 'RIGHT', 'elk.spacing.nodeNode': '40', 'elk.layered.spacing.nodeNodeBetweenLayers': '90', 'elk.layered.considerModelOrder.strategy': 'NODES_AND_EDGES' }, children: baseNodes.map(n => ({ id: n.id, width: NODE_WIDTH, height: NODE_HEIGHT })), edges: treeEdges.map(e => ({ id: e.id, sources: [e.source], targets: [e.target] })) });
   if (cancelled) return;
   const layoutRoot = layout.children?.find(n => n.id === rootId);
   const offset = focus && focus.x !== null && focus.y !== null ? { x: focus.x - (layoutRoot?.x || 0), y: focus.y - (layoutRoot?.y || 0) } : { x: 0, y: 0 };
   const arranged = baseNodes.map(node => { const position = layout.children?.find(n => n.id === node.id); const item = node.data.item; return { ...node, position: displayMapPoint(!forced && item?.x != null && item?.y != null && !node.data.root ? { x: item.x, y: item.y } : { x: (position?.x || 0) + offset.x, y: (position?.y || 0) + offset.y }, direction) }; });
   const ids = new Set(arranged.map(n => n.id));
   const depEdges: Edge[] = links ? data.dependencies.filter(d => ids.has(d.itemId) && ids.has(d.dependsOnId)).map(d => ({ id: d.id, source: d.dependsOnId, target: d.itemId, sourceHandle: 'dependency-out', targetHandle: 'dependency-in', type: 'default', style: { stroke: 'var(--map-dependency)', strokeDasharray: '5 5', strokeWidth: 1.5 }, markerEnd: { type: MarkerType.ArrowClosed, color: 'var(--map-dependency)' }, label: 'blocks', labelStyle: { fill: 'var(--map-dependency-text)', fontSize: 14 }, labelBgStyle: { fill: 'var(--map-dependency-bg)' } })) : [];
   setNodes(arranged); setEdges([...treeEdges, ...depEdges]); previousStructure.current = visibleKey; forceLayout.current = false; setArranging(false);
   if (structureChanged || forced) { preserveViewport.current = false; setFitRevision(version => version + 1); }
   if (forced) { try { await mutate(`/api/projects/${project.id}/layout`, 'PUT', { positions: arranged.filter(n => !n.data.root).map(n => ({ id: n.id, ...storedMapPoint(n.position, direction) })) }, project.id); toast.success('Map arranged'); } catch { /* reported */ } }
  }
  void arrange().catch(() => { if (!cancelled) { setArranging(false); toast.error('The map could not be arranged. Try Auto arrange again.'); } }); return () => { cancelled = true; };
 }, [visibleKey, visible, allItems, focusId, rootId, project.name, project.color, project.id, data.statuses, data.people, data.dependencies, collapsed, layoutVersion, links, showOverview, mutate, direction]);
 function chooseDirection(next: MapDirection) { localStorage.setItem(mapDirectionKey(project.id), next); if (next !== direction) { preserveViewport.current = false; setArranging(true); setDirection(next); } }
 const onNodesChange = useCallback((changes: NodeChange<MapNode>[]) => setNodes(current => applyNodeChanges(changes, current)), []);
 async function connect(connection: Connection) {
  const target = allItems.find(i => i.id === connection.target); const source = allItems.find(i => i.id === connection.source); if (!source || !target) return;
  const current = fields(target, data.dependencies); if (current.dependencyIds.includes(source.id)) return;
  try { await mutate(`/api/items/${target.id}`, 'PATCH', { ...current, dependencyIds: [...current.dependencyIds, source.id] }, project.id); toast.success(`“${target.title}” now depends on “${source.title}”`); } catch { /* reported */ }
 }
 return <div className="mindmap-container" ref={container} style={{ '--map-node-width': `${NODE_WIDTH}px`, '--map-node-height': `${NODE_HEIGHT}px` } as React.CSSProperties}><ReactFlow nodes={nodes} edges={edges} nodeTypes={nodeTypes} onMoveStart={event => { if (event) preserveViewport.current = true; }} onNodesChange={onNodesChange} onConnect={c => { void connect(c); }} onNodeDrag={(event, node) => {
   const invalid = descendants(allItems, node.id); invalid.add(node.id);
   const candidate = event.shiftKey ? getIntersectingNodes(node).find(n => !invalid.has(n.id))?.id ?? null : null;
   if (dropTarget.current !== candidate) { dropTarget.current = candidate; setNodes(current => current.map(n => ({ ...n, className: n.id === candidate ? 'map-drop-target' : '' }))); }
  }} onNodeDragStop={(event, node) => {
   const target = dropTarget.current; dropTarget.current = null; setNodes(current => current.map(n => ({ ...n, className: '' })));
   if (event.shiftKey && target && node.data.item) {
    const original = node.data.item;
    void mutate(`/api/items/${node.id}/move`, 'POST', { parentId: target === `project:${project.id}` ? null : target, statusId: original.statusId, beforeId: null }, project.id).then(() => toast.success('Branch moved', { action: { label: 'Undo', onClick: () => { void mutate(`/api/items/${node.id}/move`, 'POST', { parentId: original.parentId, statusId: original.statusId, beforeId: null }, project.id).catch(() => {}); } } })).catch(() => {});
   } else { void mutate(`/api/projects/${project.id}/layout`, 'PUT', { positions: [{ id: node.id, ...storedMapPoint(node.position, direction) }] }, project.id).catch(() => {}); }
  }} onNodeDoubleClick={(_, node) => { if (node.data.item) onFocus(node.id); }} minZoom={MAP_MIN_ZOOM} maxZoom={1.8} deleteKeyCode={null} selectionKeyCode={null} multiSelectionKeyCode={null} proOptions={{ hideAttribution: false }}><Background color="var(--map-grid)" gap={24} size={1} variant={BackgroundVariant.Dots}/><Controls showInteractive={false} showFitView={false} onZoomIn={() => { preserveViewport.current = true; }} onZoomOut={() => { preserveViewport.current = true; }}><ControlButton aria-label="Fit view" title="Fit view" onClick={() => { preserveViewport.current = false; void fitMapToScreen(); }}><Expand size={16}/></ControlButton></Controls><MiniMap nodeColor={n => n.id === rootId ? 'var(--accent-fill)' : 'var(--map-mini-node)'} maskColor="var(--map-mini-mask)" pannable zoomable/></ReactFlow>
 <div className="map-topbar" ref={toolbar}><div className="map-toolbar"><div className="map-direction" role="group" aria-label="Mind map orientation"><button aria-pressed={direction === 'vertical'} onClick={() => chooseDirection('vertical')} disabled={arranging} title="Arrange branches from top to bottom"><ArrowDown size={15}/>Vertical</button><button aria-pressed={direction === 'horizontal'} onClick={() => chooseDirection('horizontal')} disabled={arranging} title="Arrange branches from left to right"><ArrowRight size={15}/>Horizontal</button></div><Button variant="secondary" size="sm" onClick={() => { preserveViewport.current = false; forceLayout.current = true; setArranging(true); setLayoutVersion(v => v + 1); }} disabled={arranging}><WandSparkles size={14}/>{arranging ? 'Arranging…' : 'Auto arrange'}</Button><button className={`map-tool ${links ? 'active' : ''}`} onClick={() => setLinks(v => !v)} title="Show dependency connections" aria-pressed={links}><Link2 size={14}/>Dependencies</button><button className="icon-button" onClick={() => { preserveViewport.current = false; void fitMapToScreen(); }} aria-label="Fit map to screen" disabled={arranging}><Expand size={15}/></button></div><div className="map-export"><ChartExport projectName={project.name} view="Mind map" detail={`${direction === 'vertical' ? 'Vertical' : 'Horizontal'} · ${visible.length} shown tasks · ${focusId ? allItems.find(item => item.id === focusId)?.title : 'Project root'} · Dependency arrows ${links ? 'on' : 'off'}`} disabled={arranging || pending > 0 || nodes.some(node => node.dragging)} capture={() => { const root = container.current?.querySelector<HTMLElement>('.react-flow'); if (!root || !nodes.length) throw new Error('The mind map is still loading.'); return captureMindMap(root, getNodesBounds(nodes)); }}/></div></div>
 <div className="map-hint"><span className="hint-dot"/>Drag to arrange · Shift + drop to change parent · Double-click to focus</div>
 {visible.length === 0 && <div className="map-empty"><p>Every great plan starts with a little idea.</p><Button variant="secondary" size="sm" onClick={() => onAdd(focusId)}><Plus size={14}/>Add your first branch</Button></div>}
 </div>;
}
export default function MindMap(props: Parameters<typeof MapCanvas>[0]) { return <ReactFlowProvider><MapCanvas {...props}/></ReactFlowProvider>; }
