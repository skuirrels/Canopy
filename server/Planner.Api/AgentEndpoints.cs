using System.Net;
using Planner.Api.Application;

namespace Planner.Api;

public static class AgentEndpoints
{
    public static void MapAgentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/agents").AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            if (http.Connection.RemoteIpAddress is not { } address || !IPAddress.IsLoopback(address))
                return Results.Problem(title: "Agent controls are available only on this machine.", statusCode: 403);
            var origin = http.Request.Headers.Origin.ToString();
            if (http.Request.Headers["Sec-Fetch-Site"] == "cross-site" || (origin.Length > 0 &&
                (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.Authority != http.Request.Host.Value || uri.Scheme != http.Request.Scheme)))
                return Results.Problem(title: "Open agent controls from the local Canopy application.", statusCode: 403);
            http.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });
        group.MapGet("/providers", (AgentService service) => Results.Ok(service.Providers()));
        group.MapGet("/providers/{provider}/models", (string provider, AgentService service, CancellationToken ct) => service.Models(provider, ct));
        // POST keeps local filesystem paths out of URLs and request logs.
        group.MapPost("/repository-refs", (ReadRepositoryRefs input, AgentService service, CancellationToken ct) => service.RepositoryRefs(input, ct));
        group.MapPost("/repository-init", (InitialiseRepository input, AgentService service, CancellationToken ct) => service.InitialiseRepository(input, ct));
        group.MapPost("/folder-picker", (ChooseFolder input, AgentService service, CancellationToken ct) => service.ChooseFolder(input, ct));
        group.MapGet("/projects/{id:guid}/connection", (Guid id, AgentService service, CancellationToken ct) => service.Connection(id, ct));
        group.MapPut("/projects/{id:guid}/connection", (Guid id, SaveAgentConnection input, AgentService service, CancellationToken ct) => service.SaveConnection(id, input, ct));
        group.MapDelete("/projects/{id:guid}/connection", async (Guid id, long expectedRevision, AgentService service, CancellationToken ct) =>
        { await service.Disconnect(id, expectedRevision, ct); return Results.Ok((object?)null); });
        group.MapGet("/tasks/{id:guid}/runs", (Guid id, AgentService service, CancellationToken ct) => service.Runs(id, ct));
        group.MapPost("/tasks/{id:guid}/runs", (Guid id, StartAgentRun input, AgentService service, CancellationToken ct) => service.Start(id, input, ct));
        group.MapGet("/runs/{id:guid}", (Guid id, AgentService service, CancellationToken ct) => service.Run(id, ct));
        group.MapPost("/runs/{id:guid}/cancel", (Guid id, AgentService service, CancellationToken ct) => service.Cancel(id, ct));
    }
}
