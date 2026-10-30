using System.Diagnostics;
using System.Text.Json;
using Planner.Api.Application;
using Planner.Api.Domain;
namespace Planner.Api.Infrastructure;

public sealed record FactorySkillRoot(string Source, string Path, int MaximumDepth);
public sealed class FactorySkillDiscoveryOptions
{
    public List<FactorySkillRoot> Roots { get; set; } = [];
    public int MaximumEntries { get; set; }
    public int MaximumDirectories { get; set; }
    public int TimeoutSeconds { get; set; }
    public void Validate() { if (Roots is null || MaximumEntries < 1 || MaximumDirectories < 1 || TimeoutSeconds < 1) throw new InvalidOperationException("Configure Factory:SkillDiscovery roots and positive scan bounds."); }
}
public sealed class LocalFactorySkillCatalogue(IMarkdownFiles markdown, IConfiguration configuration, FactorySkillDiscoveryOptions options) : IFactorySkillCatalogue
{
    private sealed record Location(string Id, string Source, string Path, string Origin);
    public Task<FactorySkillCatalogue> Read(string? repositoryRoot, CancellationToken ct) => Task.Run(() => Observe(repositoryRoot, ct), ct);
    private FactorySkillCatalogue Observe(string? repositoryRoot, CancellationToken ct)
    {
        var clock = Stopwatch.StartNew(); var locations = new List<Location>(); var scopes = new List<FactorySkillScope>(); var directories = 0;
        void Bound()
        {
            ct.ThrowIfCancellationRequested();
            if (clock.Elapsed.TotalSeconds >= options.TimeoutSeconds || locations.Count > options.MaximumEntries || directories > options.MaximumDirectories) throw new ScanLimitException();
        }
        string Full(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new RuleException("Configure an explicit skill file or root path.");
            if (!Path.IsPathFullyQualified(path) && string.IsNullOrWhiteSpace(repositoryRoot)) throw new RuleException("Connect this project to a repository to resolve relative skill locations.");
            return Path.IsPathFullyQualified(path) ? Path.GetFullPath(path) : Path.GetFullPath(path, repositoryRoot!);
        }
        static void RealDirectory(string path)
        {
            for (var current = new DirectoryInfo(path); current is not null; current = current.Parent)
                if (current.LinkTarget is not null || (File.GetAttributes(current.FullName) & FileAttributes.ReparsePoint) != 0) throw new RuleException("Skill discovery does not follow symbolic links or reparse points. Configure real directories.");
            if (!Directory.Exists(path)) throw new RuleException("The configured skill directory does not exist.");
        }
        var explicitLocations = configuration.GetSection("Factory:Skills").Get<List<FactorySkillLocation>>() ?? [];
        try
        {
            foreach (var location in explicitLocations)
            {
                Bound();
                try
                {
                    if (location is null) throw new RuleException("A configured skill location is null.");
                    Rules.Name(location.Id, "Skill ID"); Rules.Name(location.Source, "Skill source");
                    var path = Full(location.Path); locations.Add(new(location.Id, location.Source, path, "explicit"));
                    scopes.Add(new(location.Source, location.Path, "explicit", true, null));
                }
                catch (Exception error) when (Expected(error)) { scopes.Add(new(location?.Source ?? "", location?.Path ?? "", "explicit", false, error.Message)); }
            }
            foreach (var root in options.Roots)
            {
                Bound(); var scopeIndex = scopes.Count;
                scopes.Add(new(root?.Source ?? "", root?.Path ?? "", "root", false, "Not inspected."));
                try
                {
                    if (root is null) throw new RuleException("A configured skill root is null.");
                    Rules.Name(root.Source, "Skill source"); if (root.MaximumDepth < 0) throw new RuleException("Configure a nonnegative skill root depth.");
                    var path = Full(root.Path); RealDirectory(path);
                    var pending = new Queue<(string Path, int Depth)>(); pending.Enqueue((path, 0));
                    while (pending.TryDequeue(out var current))
                    {
                        directories++; Bound(); RealDirectory(current.Path);
                        var skillFile = Path.Combine(current.Path, "SKILL.md");
                        // Enumerate instead of File.Exists, so permission errors cannot become empty coverage.
                        foreach (var file in Directory.EnumerateFiles(current.Path))
                        {
                            Bound(); if (!Path.GetFileName(file).Equals("SKILL.md", StringComparison.Ordinal)) continue;
                            var relative = Path.GetRelativePath(path, current.Path); var id = relative == "." ? new DirectoryInfo(path).Name : relative.Replace(Path.DirectorySeparatorChar, '/');
                            Rules.Name(id, "Discovered skill ID"); locations.Add(new(id, root.Source, skillFile, "root:" + root.Path)); Bound();
                        }
                        if (current.Depth < root.MaximumDepth)
                            foreach (var child in Directory.EnumerateDirectories(current.Path)) { Bound(); pending.Enqueue((child, current.Depth + 1)); if (pending.Count + directories > options.MaximumDirectories) throw new ScanLimitException(); }
                    }
                    scopes[scopeIndex] = scopes[scopeIndex] with { Complete = true, Error = null };
                }
                catch (Exception error) when (Expected(error)) { scopes[scopeIndex] = scopes[scopeIndex] with { Error = error.Message }; }
            }
            Bound();
        }
        catch (ScanLimitException)
        {
            foreach (var source in explicitLocations.Where(x => x is not null).Select(x => x.Source).Concat(options.Roots.Where(x => x is not null).Select(x => x.Source)).Distinct())
                scopes.Add(new(source, "", "limit", false, "Skill scan reached a configured entry, directory or time bound. Coverage is incomplete."));
        }
        var entries = new List<FactorySkillEntry>();
        foreach (var group in locations.GroupBy(x => (x.Id, x.Source)).OrderBy(x => x.Key.Source, StringComparer.Ordinal).ThenBy(x => x.Key.Id, StringComparer.Ordinal))
        {
            ct.ThrowIfCancellationRequested();
            var paths = group.Select(x => x.Path).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(); var origins = group.Select(x => x.Origin).Distinct().Order().ToArray();
            if (paths.Length != 1) { entries.Add(new(group.Key.Id, group.Key.Source, paths, null, null, "ambiguous", "The same skill ID/source resolves to different files. Remove conflicting configuration.", origins)); continue; }
            try
            {
                if (clock.Elapsed.TotalSeconds >= options.TimeoutSeconds) throw new RuleException("Skill inspection exceeded the configured time bound.");
                var file = markdown.Read(paths[0]); var incomplete = scopes.FirstOrDefault(x => x.Source == group.Key.Source && !x.Complete);
                entries.Add(new(group.Key.Id, group.Key.Source, paths, file.Hash, Title(file.Markdown), incomplete is null ? "available" : "unavailable", incomplete?.Error, origins));
            }
            catch (Exception error) when (Expected(error)) { entries.Add(new(group.Key.Id, group.Key.Source, paths, null, null, "unavailable", error.Message, origins)); }
        }
        var fingerprint = FactoryArtifact.Hash(JsonSerializer.Serialize(new { entries, scopes }, FactoryRules.Json));
        return new(DateTime.UtcNow, fingerprint, scopes.All(x => x.Complete), entries, scopes);
    }
    private static bool Expected(Exception error) => error is RuleException or IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException;
    private static string? Title(string content)
    {
        var lines = content.Replace("\r\n", "\n").Split('\n'); var start = 0;
        if (lines.FirstOrDefault() == "---") { var end = Array.FindIndex(lines, 1, x => x == "---"); if (end < 0) return null; start = end + 1; }
        var heading = lines.Skip(start).FirstOrDefault(x => x.StartsWith("# ", StringComparison.Ordinal))?[2..].Trim();
        return heading is { Length: > 200 } ? heading[..200] : heading;
    }
    private sealed class ScanLimitException : Exception;
}
