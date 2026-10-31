export type PlanDraft = { key: string; title: string; parentKey: string | null; markdown: string; priority: string; sourcePriority: string; phase: string; acceptance: string; verificationMethod?: string; dependencyKeys: string[]; sourceLine: number; existingItemId?: string | null };
export function parsePlan(markdown: string, source: string): { items: PlanDraft[]; warnings: string[] };
