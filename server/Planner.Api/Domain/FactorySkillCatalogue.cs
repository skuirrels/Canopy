namespace Planner.Api.Domain;

public sealed record FactorySkillEntry(string Id, string Source, IReadOnlyList<string> Paths, string? Hash, string? Title, string State, string? Error, IReadOnlyList<string> Origins);
public sealed record FactorySkillScope(string Source, string Path, string Kind, bool Complete, string? Error);
public sealed record FactorySkillCatalogue(DateTime ObservedAt, string Fingerprint, bool Complete, IReadOnlyList<FactorySkillEntry> Entries, IReadOnlyList<FactorySkillScope> Scopes)
{
    public FactorySkillEntry Resolve(FactorySkill reference)
    {
        if (Scopes.Any(x => x.Source == reference.Source && !x.Complete)) throw new RuleException($"Skill source '{reference.Source}' has incomplete coverage. Resolve its catalogue diagnostics before preparation.");
        var matches = Entries.Where(x => x.Id == reference.Id && x.Source == reference.Source).ToList();
        if (matches.Count != 1) throw new RuleException($"No unique configured skill '{reference.Id}' exists for source '{reference.Source}'. Refresh the catalogue or configure its location.");
        var entry = matches[0];
        if (entry.State != "available" || entry.Hash is null || entry.Paths.Count != 1) throw new RuleException(entry.Error ?? $"Skill '{reference.Id}' is unavailable or ambiguous.");
        return entry;
    }
}
