using System.Net;
using Planner.Api.Application;

namespace Planner.Api;

public static class SpecFileEndpoints
{
    public static void MapSpecFileEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/spec-files").AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            var origin = http.Request.Headers.Origin.ToString();
            var host = http.Request.Host.Host;
            if (http.Connection.RemoteIpAddress is not { } address || !IPAddress.IsLoopback(address) ||
                !(host == "localhost" || (IPAddress.TryParse(host.Trim('[', ']'), out var hostAddress) && IPAddress.IsLoopback(hostAddress))) ||
                http.Request.Headers["Sec-Fetch-Site"] == "cross-site" || (origin.Length > 0 &&
                (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.Authority != http.Request.Host.Value || uri.Scheme != http.Request.Scheme)))
                return Results.Problem(title: "Open linked Markdown files from the local Canopy application.", statusCode: 403);
            http.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });
        group.MapPost("/preview", (PreviewSpecFile input, SpecFileService service) => service.Preview(input));
        group.MapGet("/{id:guid}", (Guid id, SpecFileService service, CancellationToken ct) => service.Read(id, ct));
        group.MapPut("/{id:guid}", (Guid id, LinkSpecFile input, SpecFileService service, CancellationToken ct) => service.Link(id, input, ct));
        group.MapPost("/{id:guid}/write", (Guid id, WriteSpecFile input, SpecFileService service, CancellationToken ct) => service.Write(id, input, ct));
        group.MapDelete("/{id:guid}", (Guid id, long expectedRevision, SpecFileService service, CancellationToken ct) => service.Unlink(id, expectedRevision, ct));
    }
}
