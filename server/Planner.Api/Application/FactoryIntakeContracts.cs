using System.Text.Json.Serialization;
using Planner.Api.Domain;
namespace Planner.Api.Application;
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AddIntakeSource(long ExpectedRevision, IntakeConfiguration Configuration);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EditIntakeSource(long ExpectedRevision, long ExpectedSourceRevision, IntakeConfiguration Configuration);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CollectIntake(Guid RequestId, long ExpectedSourceRevision);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ActOnIntake(Guid RequestId, long ExpectedRevision, long ExpectedProjectRevision, string Action, string Reason, Guid? TaskId, Guid? StatusId, string? TaskTitle);
public sealed record IntakeLibrary(long ProjectRevision, IReadOnlyList<FactoryIntakeSource> Sources, IReadOnlyList<FactoryIntakeCollection> Collections, IReadOnlyList<IntakeCandidatePreview> Candidates, IReadOnlyList<FactoryErrorFeedReceipt> ErrorFeedReceipts, ErrorFeedCatalogue ErrorFeeds);
public sealed record IntakeCandidatePreview(FactoryIntakeCandidate Candidate, IntakeObservation Observation, bool Current, bool LinkedTaskExists, IReadOnlyList<Guid> MatchingTaskIds);
public sealed record IntakeCandidateDetail(IntakeCandidatePreview Preview, IReadOnlyList<IntakeObservation> Observations, IReadOnlyList<IntakeDecision> Decisions, IReadOnlyList<FactoryErrorFeedReceipt> ErrorFeedReceipts);
public interface IIntakeIssues
{
    string Binding { get; }
    string Identity { get; }
    Task<IReadOnlyList<IntakeSignal>> Collect(IntakeConfiguration configuration, CancellationToken ct);
    Task<IntakeSignal> Refresh(IntakeSignal signal, IntakeConfiguration configuration, CancellationToken ct);
}
