import { ArrowLeft, ArrowUpRight } from 'lucide-react';
import type { Item } from '../../lib/types';
import { specificationText } from '../../lib/epics';
import { Button } from '../ui/button';
import { TaskFactory } from './TaskFactory';

export function FactoryTaskView({item,onBack,onEdit,onOpenRun,onDesigner,onOpenTask}: {item:Item;onBack:()=>void;onEdit:()=>void;onOpenRun:(id:string)=>void;onDesigner:(id?:string)=>void;onOpenTask:(id:string)=>void}) {
 const specification=specificationText(item.specJson);
 return <div className="factory-task-view"><header><Button variant="secondary" size="sm" onClick={onBack}><ArrowLeft size={14}/>Choose another task</Button><Button variant="secondary" size="sm" onClick={onEdit}>Edit task in planner<ArrowUpRight size={14}/></Button></header><span className="factory-eyebrow">TASK BUILD SETUP</span><h2>{item.title}</h2>{specification?<details className="factory-task-spec"><summary>Saved specification summary</summary><p>{specification}</p></details>:<p className="factory-setup-gap">This task has no saved specification. Use Edit task in planner to define the work before preparing a build.</p>}<TaskFactory key={item.id} item={item} beforeChange={()=>Promise.resolve()} onOpenDesigner={onDesigner} onOpenRun={onOpenRun} onOpenTask={onOpenTask}/></div>;
}
