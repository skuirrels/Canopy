using System.Text.Json;
using System.Text.RegularExpressions;
using Planner.Api.Application;
using Planner.Api.Domain;
namespace Planner.Api.Infrastructure;

// Suggests build and test commands from files that are actually in the repository. Nothing is run.
public sealed partial class LocalRepositoryChecks : IRepositoryChecks
{
    private static readonly HashSet<string> Skipped = new(StringComparer.OrdinalIgnoreCase) { "node_modules", "bin", "obj", "target", "dist", "build", "vendor", "out" };
    public IReadOnlyList<SuggestedCheckGroup> Suggest(string repositoryPath, int timeoutSeconds)
    {
        if (!Directory.Exists(repositoryPath)) return [];
        var groups = new List<SuggestedCheckGroup>();
        foreach (var directory in Directories(repositoryPath))
        {
            var relative = Path.GetRelativePath(repositoryPath, directory).Replace('\\', '/');
            foreach (var group in Detect(directory, relative == "." ? "" : relative + "/"))
                groups.Add(group with { Checks = group.Checks.Select(x => x with { WorkingDirectory = relative, TimeoutSeconds = timeoutSeconds }).ToArray() });
        }
        return groups;
    }
    private static IEnumerable<string> Directories(string root)
    {
        yield return root;
        IEnumerable<string> children;
        try { children = Directory.EnumerateDirectories(root).Where(x => !Path.GetFileName(x).StartsWith('.') && !Skipped.Contains(Path.GetFileName(x)) && new DirectoryInfo(x).LinkTarget is null).OrderBy(x => x, StringComparer.Ordinal).Take(200).ToList(); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { yield break; }
        foreach (var child in children) yield return child;
    }
    private static IEnumerable<SuggestedCheckGroup> Detect(string directory, string prefix)
    {
        static FactoryCommand Command(string executable, params string[] arguments) => new(executable, arguments, ".", 0);
        var solutions = Files(directory, "*.sln").Concat(Files(directory, "*.slnx")).ToList();
        var projects = Files(directory, "*.csproj").Concat(Files(directory, "*.fsproj")).ToList();
        var dotnet = solutions.Count == 1 ? solutions[0] : solutions.Count == 0 && projects.Count == 1 ? projects[0] : null;
        if (dotnet is not null) yield return new(".NET", prefix + dotnet, [Command("dotnet", "build", dotnet), Command("dotnet", "test", dotnet)]);
        var package = Path.Combine(directory, "package.json");
        if (File.Exists(package) && Scripts(package) is { } scripts)
        {
            var manager = File.Exists(Path.Combine(directory, "pnpm-lock.yaml")) ? "pnpm" : File.Exists(Path.Combine(directory, "yarn.lock")) ? "yarn"
                : File.Exists(Path.Combine(directory, "bun.lock")) || File.Exists(Path.Combine(directory, "bun.lockb")) ? "bun" : "npm";
            var checks = new List<FactoryCommand>();
            if (scripts.ContainsKey("build")) checks.Add(Command(manager, "run", "build"));
            if (scripts.TryGetValue("test", out var test) && !test.Contains("no test specified", StringComparison.OrdinalIgnoreCase)) checks.Add(Command(manager, "test"));
            if (checks.Count > 0) yield return new("Node.js", prefix + "package.json", [.. checks]);
        }
        if (File.Exists(Path.Combine(directory, "Cargo.toml"))) yield return new("Rust", prefix + "Cargo.toml", [Command("cargo", "build"), Command("cargo", "test")]);
        if (File.Exists(Path.Combine(directory, "go.mod"))) yield return new("Go", prefix + "go.mod", [Command("go", "build", "./..."), Command("go", "test", "./...")]);
        var pyproject = Path.Combine(directory, "pyproject.toml");
        if (File.Exists(pyproject) && Read(pyproject).Contains("pytest", StringComparison.OrdinalIgnoreCase)) yield return new("Python", prefix + "pyproject.toml", [Command("python3", "-m", "pytest")]);
        var makefile = Path.Combine(directory, "Makefile");
        if (File.Exists(makefile))
        {
            var targets = MakeTarget().Matches(Read(makefile)).Select(x => x.Groups[1].Value).ToHashSet();
            var checks = new[] { "build", "test", "check" }.Where(targets.Contains).Select(x => Command("make", x)).ToArray();
            if (checks.Length > 0) yield return new("Make", prefix + "Makefile", checks);
        }
    }
    private static List<string> Files(string directory, string pattern)
    {
        try { return Directory.EnumerateFiles(directory, pattern).Select(Path.GetFileName).OfType<string>().OrderBy(x => x, StringComparer.Ordinal).ToList(); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { return []; }
    }
    private static string Read(string path)
    {
        try { var info = new FileInfo(path); return info.Length > 1_000_000 ? "" : File.ReadAllText(path); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { return ""; }
    }
    private static Dictionary<string, string>? Scripts(string path)
    {
        try
        {
            using var document = JsonDocument.Parse(Read(path));
            if (!document.RootElement.TryGetProperty("scripts", out var scripts) || scripts.ValueKind != JsonValueKind.Object) return null;
            return scripts.EnumerateObject().Where(x => x.Value.ValueKind == JsonValueKind.String).ToDictionary(x => x.Name, x => x.Value.GetString()!);
        }
        catch (JsonException) { return null; }
    }
    [GeneratedRegex(@"^([A-Za-z][\w-]*)\s*:(?!=)", RegexOptions.Multiline)]
    private static partial Regex MakeTarget();
}
