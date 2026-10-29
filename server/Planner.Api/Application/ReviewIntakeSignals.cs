using System.Text.Json;
using Planner.Api.Domain;
namespace Planner.Api.Application;

// Review data already has source identity and immutable submitted evidence. Rechecks do not create occurrences.
public static class ReviewIntakeSignals
{
    public static IReadOnlyList<IntakeSignal> Collect(WorkspaceData data, Guid projectId, IntakeConfiguration configuration)
    {
        var rounds = data.ReviewRounds.Where(x => x.ProjectId == projectId && x.State == "submitted").ToDictionary(x => x.Id);
        var observations = data.ReviewFindings.Where(x => x.ProjectId == projectId && rounds.ContainsKey(x.RoundId) && configuration.Categories.Contains(x.Data.Category)).Select(finding =>
        {
            var recheck = rounds.Values.Where(x => x.Submission!.Rechecks.Any(r => r.FindingId == finding.Id)).OrderByDescending(x => x.FinishedAt).ThenByDescending(x => x.Id).FirstOrDefault();
            var status = recheck?.Submission!.Rechecks.Single(x => x.FindingId == finding.Id).Status ?? "unfixed";
            return new { finding, status, recheck };
        }).Where(x => x.status is "unfixed" or "unverified").ToList();
        return observations.GroupBy(x => IntakeRules.Fingerprint("reviewFindings", projectId, IntakeRules.Normalize(x.finding.Data.Title), x.finding.Data.Category,
            x.finding.Data.CodeReferences.Select(r => r.Path).Distinct().Order(StringComparer.Ordinal).ToArray())).Where(group => group.Select(x => x.finding.ItemId).Distinct().Count() >= configuration.MinimumOccurrences).Select(group =>
        {
            var rows = group.OrderBy(x => x.finding.Id).ToArray(); var first = rows[0].finding;
            var evidence = rows.Select(x => new { findingId = x.finding.Id, taskId = x.finding.ItemId, roundId = x.finding.RoundId, runId = rounds[x.finding.RoundId].RunId, x.finding.Data,
                context = rounds[x.finding.RoundId].Packet.Context, x.status, recheckRoundId = x.recheck?.Id, recheck = x.recheck?.Submission!.Rechecks.Single(r => r.FindingId == x.finding.Id) }).ToArray();
            return new IntakeSignal(group.Key, first.Data.Title, first.Data.Description, $"Recurring finding across {rows.Select(x => x.finding.ItemId).Distinct().Count()} tasks", null, true, JsonSerializer.Serialize(evidence, FactoryRules.Json));
        }).OrderBy(x => x.Fingerprint).ToList();
    }
}
