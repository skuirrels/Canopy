namespace Planner.Api.Domain;

// Completion counters retain the provider's scope and semantics. They do not
// establish account billing, or a complete total across resumed conversations.
public sealed record AgentUsageObservation(string Provider, string EventType, DateTime ObservedAt,
    string Status, long? InputTokens, long? OutputTokens, string Payload, string Hash);
public sealed record AgentUsage(IReadOnlyList<AgentUsageObservation> Observations, bool RetentionLimitReached)
{
    public string Status => RetentionLimitReached ? "incomplete" : Observations.Count == 0 || Observations.All(x => x.Status == "missing") ? "unknown" : Observations.Any(x => x.Status != "reported") ? "incomplete" : "reported";
    public static AgentUsage Empty => new([], false);
    public AgentUsage Append(AgentUsageObservation? observation, int maxEvents, int maxCharacters)
    {
        if (observation is null) return this;
        if (Observations.Count >= maxEvents || (long)observation.Payload.Length + Observations.Sum(x => (long)x.Payload.Length) > maxCharacters)
            return this with { RetentionLimitReached = true };
        return this with { Observations = [.. Observations, observation] };
    }
}
