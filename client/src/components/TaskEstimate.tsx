import { Clock3 } from 'lucide-react';
export function TaskEstimate({ hours }: { hours: number | null | undefined }) {
 if (hours == null) return null;
 return <span className="task-estimate" aria-label={`Estimated effort: ${hours} hours`} data-tooltip="Estimated effort, independent of calendar dates"><Clock3 size={13}/>{hours} h</span>;
}
