export type IntakeConfiguration = { name: string; kind: 'githubIssues' | 'reviewFindings' | 'applicationErrors'; enabled: boolean; repository: string; labels: string[]; minimumOccurrences: number; categories: string[]; errorFeedId?: string | null; levels?: string[] | null };
export type IntakeSource = { id: string; projectId: string; revision: number; configuration: IntakeConfiguration };
export type IntakeCollection = { id: string; sourceId: string; sourceRevision: number; state: string; error: string | null; observed: number; createdAt: string; finishedAt: string | null };
export type IntakeSignal = { fingerprint: string; title: string; description: string; origin: string; url: string | null; available: boolean; evidenceJson: string };
export type IntakeObservation = { id: string; candidateId: string; collectionId: string; signal: IntakeSignal };
export type IntakeCandidate = { id: string; projectId: string; revision: number; state: string; reason: string; taskId: string | null; verifiedHash: string | null };
export type IntakePreview = { candidate: IntakeCandidate; observation: IntakeObservation; current: boolean; linkedTaskExists: boolean; matchingTaskIds: string[] };
export type IntakeLibrary = { projectRevision: number; sources: IntakeSource[]; collections: IntakeCollection[]; candidates: IntakePreview[]; errorFeedReceipts: ErrorFeedReceipt[]; errorFeeds: ErrorFeedCatalogue };
export type IntakeDetail = { preview: IntakePreview; observations: IntakeObservation[]; decisions: { id: string; action: string; reason: string; taskId: string | null; observationId: string; createdAt: string }[]; errorFeedReceipts: ErrorFeedReceipt[] };
export type IntakeAction = 'verify' | 'dismiss' | 'reopen' | 'file' | 'link';
export type IntakeActionRequest = { requestId: string; expectedRevision: number; expectedProjectRevision: number; action: IntakeAction; reason: string; taskId: string | null; statusId: string | null; taskTitle: string | null };

export type ErrorFeedIdentity = { sourceId: string; application: string; environment: string };
export type ErrorFeedChoice = { id: string; name: string; identity: ErrorFeedIdentity; levels: string[]; maxAgeSeconds: number; maxWindowSeconds: number };
export type ErrorFeedCatalogue = { feeds: ErrorFeedChoice[]; supported: boolean };
export type ErrorFeedCoverage = { feedId: string; identity: ErrorFeedIdentity; fileHash: string; bytes: number; generatedAt: string; windowStart: string; windowEnd: string; rawEvents: number; distinctEvents: number; selectedEvents: number; eligibleGroups: number };
export type ErrorFeedReceipt = { id: string; projectId: string; sourceId: string; coverage: ErrorFeedCoverage };
export type ErrorGroupEvidence = { kind: 'applicationErrors'; feedId: string; identity: ErrorFeedIdentity; group: { id: string; title: string; exceptionType: string; messageTemplate: string; code: string | null; frames: { path: string; symbol: string; line: number | null }[]; occurrences: { id: string; occurredAt: string; level: string; release: string | null }[] } };
