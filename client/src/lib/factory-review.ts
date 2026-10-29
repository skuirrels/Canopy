import type { AcceptanceCriterion, CapturedCriterionEvidence, VerificationContext } from './factory-quality';
export type ReviewCode = { path: string; line: number; fileHash: string };
export type FindingData = { title: string; description: string; category: string; severity: string; owner: string; criterionIds: string[]; codeReferences: ReviewCode[]; evidence: CapturedCriterionEvidence[] };
export type ReviewFinding = { id: string; itemId: string; roundId: string; number: number; data: FindingData };
export type FindingRecheck = { findingId: string; status: string; reason: string; codeReferences: ReviewCode[]; evidence: CapturedCriterionEvidence[] };
export type ReviewSubmission = { verdict: string; summary: string; qaHash: string; qaReceipts: { criterionId: string; status: string; verdictId: string | null }[]; rechecks: FindingRecheck[]; newFindingIds: string[] };
export type ReviewRound = { id: string; runId: string; itemId: string; number: number; revision: number; reviewer: string; state: string; open: boolean; assessment: string; assessmentStepRunId: string | null; abandonReason: string | null; submission: ReviewSubmission | null; createdAt: string; assessedAt: string | null; finishedAt: string | null };
export type PriorFinding = { id: string; number: number; data: FindingData; lastStatus: string; lastReason: string };
export type ReviewPacket = { title: string; specJson: string; baseCommit: string; context: VerificationContext; priorFindings: PriorFinding[]; criteria: AcceptanceCriterion[]; rawChange: string; verificationEvidence: { stepRunId: string; name: string; evidence: string; current: boolean; evidenceHash: string }[] };
export type FindingHistory = { roundId: string; runId: string; roundNumber: number; reviewer: string; status: string; reason: string; context: VerificationContext; codeReferences: ReviewCode[]; evidence: CapturedCriterionEvidence[] };
export type FindingView = { finding: ReviewFinding; status: string; history: FindingHistory[] };
export type ReviewRoundSummary = Pick<ReviewRound, 'id' | 'runId' | 'number' | 'revision' | 'reviewer' | 'state' | 'open' | 'createdAt'> & { verdict: string | null; staleReason: string | null };
export type ReviewLibrary = { runId: string; runRevision: number; context: VerificationContext | null; contextError: string | null; sourceChanged: boolean; rounds: ReviewRoundSummary[]; findings: FindingView[]; assessments: { stepRunId: string; name: string }[] };
export type ReviewDetail = { round: ReviewRound; packet: ReviewPacket; builderReports: { stepRunId: string; stepName: string; report: string; evidenceHash: string }[] | null; staleReason: string | null; findings: FindingView[] };
export function findingLabel(number: number) { return `F-${String(number).padStart(3, '0')}`; }
export function reviewLabel(value: string) { return ({ noLongerApplies: 'No longer applies', needsRecheck: 'Needs recheck', requestChanges: 'Changes requested', approve: 'Approved', missingBehavior: 'Missing behaviour', verificationGap: 'Verification gap', scopeDrift: 'Scope drift' } as Record<string,string>)[value] ?? value.charAt(0).toUpperCase() + value.slice(1); }
