using DuckDB.EFCoreProvider.Extensions;
using Microsoft.EntityFrameworkCore;
using Planner.Api.Application;
using Planner.Api.Domain;
using Planner.Api.Infrastructure;
using Planner.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();
var configuredPath = builder.Configuration["Database:Path"] ?? throw new InvalidOperationException("Database:Path is required.");
var databasePath = Path.GetFullPath(configuredPath, builder.Environment.ContentRootPath);
Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
builder.Services.AddDbContextFactory<PlannerDb>(options => options.UseDuckDB($"Data Source={databasePath}"));
builder.Services.AddSingleton<IWorkspaceStore, DuckDbWorkspaceStore>();
builder.Services.AddSingleton<IReadOnlyList<WorkflowDefault>>(builder.Configuration.GetSection("WorkflowDefaults").Get<List<WorkflowDefault>>() ?? []);
builder.Services.AddScoped<PlannerService>();
builder.Services.AddSingleton<IMarkdownFiles, LocalMarkdownFiles>();
builder.Services.AddScoped<SpecFileService>();
var factoryTemplatePath = builder.Configuration["Factory:TemplatesPath"] ?? throw new InvalidOperationException("Configure Factory:TemplatesPath.");
var factoryTemplates = FactoryTemplateLoader.Load(Path.GetFullPath(factoryTemplatePath, builder.Environment.ContentRootPath));
builder.Services.AddSingleton<IReadOnlyList<FactoryTemplate>>(factoryTemplates);
builder.Services.AddScoped<FactoryService>();
builder.Services.AddSingleton<IFactoryEnvironment, LocalFactoryEnvironment>();
builder.Services.AddScoped<FactoryRunService>();
builder.Services.AddScoped<FactoryDispatchService>();
builder.Services.AddSingleton<StartupRunRecovery>();
var agentOptions = builder.Configuration.GetSection("Agents").Get<AgentOptions>() ?? throw new InvalidOperationException("Configure Agents.");
agentOptions.WorkspaceRoot = Path.GetFullPath(agentOptions.WorkspaceRoot, builder.Environment.ContentRootPath);
agentOptions.Validate();
builder.Services.AddSingleton(agentOptions);
builder.Services.AddSingleton<LocalAgentEnvironment>();
builder.Services.AddSingleton<IAgentEnvironment>(services => services.GetRequiredService<LocalAgentEnvironment>());
builder.Services.AddScoped<AgentService>();
builder.Services.AddHostedService<LocalAgentRunner>();
builder.Services.AddHostedService<LocalFactoryRunner>();
var app = builder.Build();
app.Use(async (context, next) =>
{
    try { await next(context); }
    catch (RuleException error) { await Results.Problem(title: error.Message, statusCode: error.Status).ExecuteAsync(context); }
    catch (BadHttpRequestException error) { await Results.Problem(title: "The request is invalid.", detail: error.Message, statusCode: 400).ExecuteAsync(context); }
});
await using (var scope = app.Services.CreateAsyncScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PlannerDb>>();
    await using var db = await factory.CreateDbContextAsync();
    await db.Database.MigrateAsync();
}
await app.Services.GetRequiredService<StartupRunRecovery>().Recover(CancellationToken.None);
app.MapOpenApi();
app.MapAgentEndpoints();
app.MapSpecFileEndpoints();
app.MapFactoryEndpoints();
app.UseDefaultFiles(); app.UseStaticFiles();
app.MapGet("/api/health", () => Results.Ok(new { status = "ready", database = "DuckDB", authentication = "phase-2" }));
app.MapGet("/api/workspace", (PlannerService service, CancellationToken ct) => service.Read(ct));
app.MapPost("/api/projects", (CreateProject input, PlannerService service, CancellationToken ct) => service.AddProject(input, ct));
app.MapPatch("/api/projects/{id:guid}", (Guid id, EditProject input, PlannerService service, CancellationToken ct) => service.UpdateProject(id, input, ct));
app.MapDelete("/api/projects/{id:guid}", (Guid id, long expectedRevision, PlannerService service, CancellationToken ct) => service.DeleteProject(id, expectedRevision, ct));
app.MapPost("/api/projects/{id:guid}/items", (Guid id, CreateItem input, PlannerService service, CancellationToken ct) => service.AddItem(id, input, ct));
app.MapPatch("/api/items/{id:guid}", (Guid id, EditItem input, PlannerService service, CancellationToken ct) => service.UpdateItem(id, input, ct));
app.MapPost("/api/items/{id:guid}/move", (Guid id, MoveItem input, PlannerService service, CancellationToken ct) => service.Move(id, input, ct));
app.MapDelete("/api/items/{id:guid}", (Guid id, long expectedRevision, PlannerService service, CancellationToken ct) => service.DeleteItem(id, expectedRevision, ct));
app.MapPut("/api/projects/{id:guid}/layout", (Guid id, SaveLayout input, PlannerService service, CancellationToken ct) => service.Layout(id, input, ct));
app.MapPost("/api/projects/{id:guid}/status-order", (Guid id, OrderStatuses input, PlannerService service, CancellationToken ct) => service.ReorderStatuses(id, input, ct));
app.MapPost("/api/projects/{id:guid}/sibling-order", (Guid id, OrderSiblings input, PlannerService service, CancellationToken ct) => service.ReorderSiblings(id, input, ct));
app.MapPost("/api/projects/{id:guid}/statuses", (Guid id, CreateStatus input, PlannerService service, CancellationToken ct) => service.AddStatus(id, input, ct));
app.MapPatch("/api/statuses/{id:guid}", (Guid id, EditStatus input, PlannerService service, CancellationToken ct) => service.UpdateStatus(id, input, ct));
app.MapDelete("/api/statuses/{id:guid}", (Guid id, long expectedRevision, PlannerService service, CancellationToken ct) => service.DeleteStatus(id, expectedRevision, ct));
app.MapDelete("/api/people/{id:guid}", (Guid id, PlannerService service, CancellationToken ct) => service.RemovePerson(id, ct));
app.MapPatch("/api/people/{id:guid}", (Guid id, EditPerson input, PlannerService service, CancellationToken ct) => service.UpdatePerson(id, input, ct))
    .WithSummary("Rename an assignee")
    .WithDescription("Updates the existing assignee name, preserving its ID and all task assignments. Names are trimmed, must contain 1–200 characters and must be unique ignoring case.")
    .ProducesProblem(400).ProducesProblem(404);
app.MapPost("/api/people", (CreatePerson input, PlannerService service, CancellationToken ct) => service.AddPerson(input, ct));
app.MapFallback(async context =>
{
    if (context.Request.Path.StartsWithSegments("/api")) { context.Response.StatusCode = 404; return; }
    var index = Path.Combine(app.Environment.WebRootPath ?? "wwwroot", "index.html");
    if (File.Exists(index)) await context.Response.SendFileAsync(index); else context.Response.StatusCode = 404;
});
app.Run();
public partial class Program;
