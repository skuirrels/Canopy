using Planner.Api.Domain;

namespace Planner.Api.Application;

public sealed record PreviewSpecFile(string Path);
public sealed record SpecFileContent(string Path, string Markdown, string Hash);
public sealed record LinkSpecFile(long ExpectedRevision, string Path, string ExpectedHash, string SpecJson);
public sealed record WriteSpecFile(long ExpectedRevision, string ExpectedHash, string Markdown);
public interface IMarkdownFiles
{
    SpecFileContent Read(string path);
    SpecFileContent Write(string path, string expectedHash, string markdown);
}

public sealed class SpecFileService(IWorkspaceStore store, IMarkdownFiles files)
{
    public SpecFileContent Preview(PreviewSpecFile input) => files.Read(input.Path);
    public async Task<SpecFileContent> Read(Guid id, CancellationToken ct)
    {
        var data = await store.ReadAsync(ct);
        return files.Read(RequireLink(data, id).Path);
    }
    public Task<WorkspaceData> Link(Guid id, LinkSpecFile input, CancellationToken ct) => store.WriteAsync(data =>
    {
        var graph = data.ForItem(id, input.ExpectedRevision);
        var content = files.Read(input.Path);
        if (content.Hash != input.ExpectedHash) throw new RuleException("The file changed after preview. Preview it again before linking.", 409);
        graph.Item(id).Specify(input.SpecJson);
        var link = data.SpecFiles.SingleOrDefault(x => x.Id == id);
        if (link is null) data.SpecFiles.Add(SpecFileLink.Create(id, graph.Project.Id, content.Path, content.Hash));
        else link.Relink(content.Path, content.Hash);
        graph.Project.Touch();
    }, ct);
    public Task<WorkspaceData> Write(Guid id, WriteSpecFile input, CancellationToken ct) => store.WriteAsync(data =>
    {
        var graph = data.ForItem(id, input.ExpectedRevision);
        var link = RequireLink(data, id);
        if (link.Hash != input.ExpectedHash) throw new RuleException("This file link changed. Reload the task before saving.", 409);
        var content = files.Write(link.Path, link.Hash, input.Markdown);
        link.Relink(content.Path, content.Hash);
        graph.Project.Touch();
    }, ct);
    public Task<WorkspaceData> Unlink(Guid id, long revision, CancellationToken ct) => store.WriteAsync(data =>
    {
        var graph = data.ForItem(id, revision);
        data.SpecFiles.Remove(RequireLink(data, id));
        graph.Project.Touch();
    }, ct);
    private static SpecFileLink RequireLink(WorkspaceData data, Guid id) =>
        data.SpecFiles.SingleOrDefault(x => x.Id == id) ?? throw new RuleException("No Markdown file is linked to this task.", 404);
}
