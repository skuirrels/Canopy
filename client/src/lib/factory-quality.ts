import type { FactoryReadinessCheck } from './factory-runs';
export type AcceptanceCriterion = { id: string; description: string; expectedResult: string; verificationMethod: string; needsClarification: boolean };
export type AcceptanceCriteriaDetail = { projectRevision: number; criteria: AcceptanceCriterion[]; checks: FactoryReadinessCheck[] };
export type VerificationContext = { headCommit: string; codeHash: string; configurationHash: string; fingerprint: string };
export type VerificationReceipt = { id: string; stepRunId: string; evidenceHash: string; before: VerificationContext | null; after: VerificationContext | null; stable: boolean; error: string | null };
export type CapturedCriterionEvidence = { stepRunId: string; evidenceHash: string; testPath: string; line: number; fileHash: string; assertion: string; failureCase: string };
export type CriterionVerdict = { id: string; requestId: string; verdict: string; reason: string; recordedRevision: number; createdAt: string; context: VerificationContext | null; evidence: CapturedCriterionEvidence[] };
export type CriterionQuality = { criterion: AcceptanceCriterion; status: string; staleReason: string | null; history: CriterionVerdict[] };
export type FactoryQualityDetail = { runId: string; runRevision: number; context: VerificationContext | null; contextError: string | null; sourceChanged: boolean; criteria: CriterionQuality[]; receipts: VerificationReceipt[] };
export function qualityLabel(value: string) { return value === 'notApplicable' ? 'Not applicable' : value.charAt(0).toUpperCase() + value.slice(1); }
export function qualityCounts(criteria: CriterionQuality[]) {
 return criteria.reduce<Record<string, number>>((counts, criterion) => { counts[criterion.status] = (counts[criterion.status] ?? 0) + 1; return counts; }, {});
}
