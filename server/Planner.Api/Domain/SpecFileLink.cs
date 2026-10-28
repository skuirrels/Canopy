namespace Planner.Api.Domain;

public sealed class SpecFileLink
{
    private SpecFileLink() { }
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Path { get; private set; } = "";
    public string Hash { get; private set; } = "";
    public static SpecFileLink Create(Guid itemId, Guid projectId, string path, string hash) =>
        new() { Id = itemId, ProjectId = projectId, Path = path, Hash = hash };
    public void Relink(string path, string hash) { Path = path; Hash = hash; }
}
