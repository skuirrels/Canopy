using DuckDB.EFCoreProvider.Extensions;
using Microsoft.EntityFrameworkCore;
using Planner.Api.Application;
using Planner.Api.Domain;
using Planner.Api.Infrastructure;
using Planner.Api;

if (args.FirstOrDefault() == LocalProcessHost.Argument)
{ Environment.ExitCode = await LocalProcessHost.Run(args); return; }

var builder = WebApplication.CreateBuilder(args);
// Optional machine settings stay out of source control. Preserve environment/CLI precedence.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false).AddEnvironmentVariables().AddCommandLine(args);
builder.Services.AddOpenApi();
var configuredPath = builder.Configuration["Database:Path"] ?? throw new InvalidOperationException("Database:Path is required.");
var databasePath = Path.GetFullPath(configuredPath, builder.Environment.ContentRootPath);
Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
builder.Services.AddDbContextFactory<PlannerDb>(options => options.UseDuckDB($"Data Source={databasePath}"));
builder.Services.AddSingleton<IWorkspaceStore, DuckDbWorkspaceStore>();
builder.Services.AddSingleton<IReadOnlyList<WorkflowDefault>>(builder.Configuration.GetSection("WorkflowDefaults").Get<List<WorkflowDefault>>() ?? []);
builder.Services.AddScoped<PlannerService>();
builder.Services.AddScoped<PlanImportService>();
builder.Services.AddSingleton<IMarkdownFiles, LocalMarkdownFiles>();
var skillDiscovery = builder.Configuration.GetSection("Factory:SkillDiscovery").Get<FactorySkillDiscoveryOptions>() ?? throw new InvalidOperationException("Configure Factory:SkillDiscovery.");
skillDiscovery.Validate(); builder.Services.AddSingleton(skillDiscovery);
builder.Services.AddSingleton<IFactorySkillCatalogue, LocalFactorySkillCatalogue>();
builder.Services.AddScoped<FactorySkillService>();
builder.Services.AddScoped<SpecFileService>();
var factoryTemplatePath = builder.Configuration["Factory:TemplatesPath"] ?? throw new InvalidOperationException("Configure Factory:TemplatesPath.");
var factoryTemplates = FactoryTemplateLoader.Load(Path.GetFullPath(factoryTemplatePath, builder.Environment.ContentRootPath));
builder.Services.AddSingleton<IReadOnlyList<FactoryTemplate>>(factoryTemplates);
builder.Services.AddScoped<FactoryService>();
var setupOptions = builder.Configuration.GetSection("Factory:Setup").Get<FactorySetupOptions>() ?? throw new InvalidOperationException("Configure Factory:Setup.");
if (setupOptions.SuggestedSkills is null || setupOptions.SuggestedSkills.Any(x => !FactoryRules.AgentRoles.Contains(x.Key) || x.Value is null || x.Value.Any(s => s is null)) || !factoryTemplates.Any(x => x.Id == setupOptions.DefaultTemplateId)) throw new InvalidOperationException("Configure an existing Factory:Setup:DefaultTemplateId.");
builder.Services.AddSingleton(setupOptions);
builder.Services.AddScoped<FactorySetupService>();
var calibrationOptions = builder.Configuration.GetSection("Factory:Calibration").Get<FactoryCalibrationOptions>() ?? throw new InvalidOperationException("Configure Factory:Calibration.");
calibrationOptions.Validate(); builder.Services.AddSingleton(calibrationOptions);
builder.Services.AddSingleton<ICalibrationWorkspace, LocalCalibrationWorkspace>();
builder.Services.AddSingleton<FactoryCalibrationService>();
builder.Services.AddHostedService<LocalCalibrationRunner>();
var parallelOptions = builder.Configuration.GetSection("Factory:Parallel").Get<FactoryParallelOptions>() ?? throw new InvalidOperationException("Configure Factory:Parallel.");
parallelOptions.Validate(); builder.Services.AddSingleton(parallelOptions);
builder.Services.AddSingleton<IFactoryEnvironment, LocalFactoryEnvironment>();
builder.Services.AddSingleton<IFactoryParallelWorkspace, LocalFactoryParallelWorkspace>();
builder.Services.AddSingleton<LocalFactoryStepExecutor>();
builder.Services.AddScoped<FactoryRunService>();
builder.Services.AddSingleton<FactoryExecutionPolicyService>();
builder.Services.AddHostedService<LocalFactoryPolicyMonitor>();
builder.Services.AddSingleton<LocalVerificationEnvironment>();
builder.Services.AddSingleton<IVerificationEnvironment>(services => services.GetRequiredService<LocalVerificationEnvironment>());
builder.Services.AddSingleton<IReviewChanges>(services => services.GetRequiredService<LocalVerificationEnvironment>());
builder.Services.AddSingleton<FactoryQualityService>();
builder.Services.AddSingleton<FactoryReviewService>();
builder.Services.AddSingleton<IRepairWorkspace, LocalRepairWorkspace>();
builder.Services.AddSingleton<FactoryRepairService>();
var deliveryOptions = builder.Configuration.GetSection("Delivery").Get<DeliveryOptions>() ?? throw new InvalidOperationException("Configure Delivery.");
deliveryOptions.Validate(); builder.Services.AddSingleton(deliveryOptions);
builder.Services.AddHttpClient("delivery", client => client.Timeout = Timeout.InfiniteTimeSpan).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddSingleton<GitHubApiClient>();
builder.Services.AddSingleton<IDeliveryProvider, GitHubDeliveryProvider>();
var intakeLimits = builder.Configuration.GetSection("Intake").Get<IntakeLimits>() ?? throw new InvalidOperationException("Configure Intake.");
intakeLimits.Validate(); builder.Services.AddSingleton(intakeLimits);
builder.Services.AddSingleton<IIntakeIssues, GitHubIntakeIssues>();
builder.Services.AddSingleton<IIntakeErrorFeeds, LocalIntakeErrorFeeds>();
builder.Services.AddSingleton<IIntakeAnalyticsFeeds, LocalIntakeAnalyticsFeeds>();
builder.Services.AddSingleton<FactoryIntakeService>();
builder.Services.AddSingleton<IDeliveryWorkspace, LocalDeliveryWorkspace>();
builder.Services.AddSingleton<IDeliveryHooks, LocalDeliveryHooks>();
builder.Services.AddSingleton<FactoryDeliveryService>();
builder.Services.AddSingleton<FactoryDeploymentService>();
builder.Services.AddScoped<FactoryDispatchService>();
builder.Services.AddSingleton<StartupRunRecovery>();
var agentOptions = builder.Configuration.GetSection("Agents").Get<AgentOptions>() ?? throw new InvalidOperationException("Configure Agents.");
agentOptions.WorkspaceRoot = Path.GetFullPath(agentOptions.WorkspaceRoot, builder.Environment.ContentRootPath);
agentOptions.Validate();
builder.Services.AddSingleton(agentOptions);
builder.Services.AddSingleton<LocalAgentEnvironment>();
builder.Services.AddSingleton<IAgentEnvironment>(services => services.GetRequiredService<LocalAgentEnvironment>());
builder.Services.AddSingleton<IFolderPicker, LocalFolderPicker>();
builder.Services.AddScoped<AgentService>();
builder.Services.AddHostedService<LocalAgentRunner>();
builder.Services.AddHostedService<LocalFactoryRunner>();
builder.Services.AddHostedService<LocalParallelFactoryRunner>();
builder.Services.AddHostedService<LocalRepairCoordinator>();
builder.Services.AddHostedService<LocalDeliveryCoordinator>();
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
    await PlannerDatabase.Initialize(db, CancellationToken.None);
}
await app.Services.GetRequiredService<StartupRunRecovery>().Recover(CancellationToken.None);
app.MapOpenApi();
app.MapAgentEndpoints();
app.MapSpecFileEndpoints();
app.MapFactoryEndpoints();
app.UseDefaultFiles(); app.UseStaticFiles();
var instanceId = Guid.NewGuid();
var informationalVersion = System.Reflection.CustomAttributeExtensions.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(typeof(Program).Assembly)?.InformationalVersion;
var revision = informationalVersion?.Split('+').LastOrDefault();
if (revision is null || !System.Text.RegularExpressions.Regex.IsMatch(revision, "^[a-f0-9]{40}$")) revision = null;
app.MapGet("/api/health", () => Results.Ok(new { status = "ready", database = "DuckDB", authentication = "phase-2", instanceId, revision }));
app.MapGet("/api/workspace", (PlannerService service, CancellationToken ct) => service.Read(ct));
app.MapPost("/api/projects", (CreateProject input, PlannerService service, CancellationToken ct) => service.AddProject(input, ct));
app.MapPatch("/api/projects/{id:guid}", (Guid id, EditProject input, PlannerService service, CancellationToken ct) => service.UpdateProject(id, input, ct));
app.MapDelete("/api/projects/{id:guid}", (Guid id, long expectedRevision, PlannerService service, CancellationToken ct) => service.DeleteProject(id, expectedRevision, ct));
app.MapPost("/api/projects/{id:guid}/items", (Guid id, CreateItem input, PlannerService service, CancellationToken ct) => service.AddItem(id, input, ct));
app.MapPost("/api/projects/{id:guid}/plan-imports", (Guid id, ImportPlan input, PlanImportService service, CancellationToken ct) => service.Import(id, input, ct))
    .WithSummary("Import a reviewed plan draft")
    .WithDescription("Atomically creates components, tasks, acceptance criteria and dependencies. Source and item keys identify previous imports, which are preserved. Explicit existingItemId adopts a task in this project without editing it. Unknown references, hierarchy/dependency cycles and stale revisions reject the entire import.")
    .Produces<WorkspaceData>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
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
