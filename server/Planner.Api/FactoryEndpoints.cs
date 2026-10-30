using System.Net;
using Planner.Api.Application;
using Planner.Api.Domain;

namespace Planner.Api;

public static class FactoryEndpoints
{
    public static void MapFactoryEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/factory").WithTags("Software Factory configuration").AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext; var origin = http.Request.Headers.Origin.ToString(); var host = http.Request.Host.Host;
            if (http.Connection.RemoteIpAddress is not { } address || !IPAddress.IsLoopback(address) ||
                !(host == "localhost" || (IPAddress.TryParse(host.Trim('[', ']'), out var hostAddress) && IPAddress.IsLoopback(hostAddress))) ||
                http.Request.Headers["Sec-Fetch-Site"] == "cross-site" || (origin.Length > 0 &&
                (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.Authority != http.Request.Host.Value || uri.Scheme != http.Request.Scheme)))
                return Results.Problem(title: "Open factory controls from the local Canopy application.", statusCode: 403);
            http.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });
        group.MapGet("/projects/{id:guid}/skills", (Guid id, FactorySkillService service, CancellationToken ct) => service.Read(id, ct))
            .WithSummary("Observe configured installed skills").WithDescription("Read-only bounded catalogue for this project's explicit skill files and discovery roots. Entries retain exact IDs/sources, paths and byte hashes. Source coverage and unavailable/ambiguous references remain visible. Reading or selecting never installs, executes or verifies provider compatibility.")
            .ProducesProblem(400).ProducesProblem(403).ProducesProblem(404);
        group.MapGet("/projects/{id:guid}/intake", (Guid id, FactoryIntakeService service, CancellationToken ct) => service.List(id, ct))
            .WithSummary("Read intake sources, collection coverage and candidate previews").WithDescription("Optional configured intake. Collection success is distinct from unavailable coverage. Exact request retries never duplicate tasks. Filing requires current source evidence, an explicit verification rationale and backlog column; it grants no execution or acceptance authority.")
            .ProducesProblem(400).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409).ProducesProblem(503);
        group.MapPost("/projects/{id:guid}/intake/sources", (Guid id, AddIntakeSource input, FactoryIntakeService service, CancellationToken ct) => service.AddSource(id, input, ct))
            .WithSummary("Configure an optional intake source").WithDescription("Optional configured intake. Collection success is distinct from unavailable coverage. Exact request retries never duplicate tasks. Filing requires current source evidence, an explicit verification rationale and backlog column; it grants no execution or acceptance authority.")
            .ProducesProblem(400).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409).ProducesProblem(503);
        group.MapPut("/intake/sources/{id:guid}", (Guid id, EditIntakeSource input, FactoryIntakeService service, CancellationToken ct) => service.EditSource(id, input, ct))
            .WithSummary("Edit intake source filters and authority").WithDescription("Optional configured intake. Collection success is distinct from unavailable coverage. Exact request retries never duplicate tasks. Filing requires current source evidence, an explicit verification rationale and backlog column; it grants no execution or acceptance authority.")
            .ProducesProblem(400).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409).ProducesProblem(503);
        group.MapPost("/intake/sources/{id:guid}/collections", (Guid id, CollectIntake input, FactoryIntakeService service, CancellationToken ct) => service.Collect(id, input, ct))
            .WithSummary("Collect complete source evidence without filing work").WithDescription("Optional configured intake. Collection success is distinct from unavailable coverage. Exact request retries never duplicate tasks. Filing requires current source evidence, an explicit verification rationale and backlog column; it grants no execution or acceptance authority.")
            .ProducesProblem(400).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409).ProducesProblem(503);
        group.MapGet("/intake/candidates/{id:guid}", (Guid id, FactoryIntakeService service, CancellationToken ct) => service.Read(id, ct))
            .WithSummary("Read candidate evidence and triage history").WithDescription("Optional configured intake. Collection success is distinct from unavailable coverage. Exact request retries never duplicate tasks. Filing requires current source evidence, an explicit verification rationale and backlog column; it grants no execution or acceptance authority.")
            .ProducesProblem(400).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409).ProducesProblem(503);
        group.MapPost("/intake/candidates/{id:guid}/actions", (Guid id, ActOnIntake input, FactoryIntakeService service, CancellationToken ct) => service.Act(id, input, ct))
            .WithSummary("Verify, dismiss, reopen, file or link a candidate explicitly").WithDescription("Optional configured intake. Collection success is distinct from unavailable coverage. Exact request retries never duplicate tasks. Filing requires current source evidence, an explicit verification rationale and backlog column; it grants no execution or acceptance authority.")
            .ProducesProblem(400).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409).ProducesProblem(503);
        group.MapGet("/projects/{id:guid}/delivery-policy", (Guid id, FactoryDeliveryService service, CancellationToken ct) => service.Policy(id, ct))
            .WithSummary("Read project delivery policy").WithDescription("Commit-bound delivery authority and persistent operation receipts. Capturing executes nothing. Mutations are asynchronous; exact retries never repeat side effects. Unknown outcomes require reconciliation.")
            .ProducesProblem(400).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409).ProducesProblem(503);
        group.MapPut("/projects/{id:guid}/delivery-policy", (Guid id, EditDeliveryPolicy input, FactoryDeliveryService service, CancellationToken ct) => service.EditPolicy(id, input, ct))
            .WithSummary("Adopt explicit delivery authority").WithDescription("Commit-bound delivery authority and persistent operation receipts. Capturing executes nothing. Mutations are asynchronous; exact retries never repeat side effects. Unknown outcomes require reconciliation.")
            .ProducesProblem(400).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409).ProducesProblem(503);
        group.MapGet("/runs/{id:guid}/delivery", (Guid id, FactoryDeliveryService service, CancellationToken ct) => service.List(id, ct))
            .WithSummary("Read delivery readiness and history").WithDescription("Commit-bound delivery authority and persistent operation receipts. Capturing executes nothing. Mutations are asynchronous; exact retries never repeat side effects. Unknown outcomes require reconciliation.")
            .ProducesProblem(400).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409).ProducesProblem(503);
        group.MapPost("/runs/{id:guid}/deliveries", (Guid id, PrepareDelivery input, FactoryDeliveryService service, CancellationToken ct) => service.Prepare(id, input, ct))
            .WithSummary("Capture a reviewed committed candidate").WithDescription("Commit-bound delivery authority and persistent operation receipts. Capturing executes nothing. Mutations are asynchronous; exact retries never repeat side effects. Unknown outcomes require reconciliation.")
            .ProducesProblem(400).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409).ProducesProblem(503);
        group.MapGet("/deliveries/{id:guid}", (Guid id, FactoryDeliveryService service, CancellationToken ct) => service.Read(id, ct))
            .WithSummary("Read delivery evidence and operations").WithDescription("Commit-bound delivery authority and persistent operation receipts. Capturing executes nothing. Mutations are asynchronous; exact retries never repeat side effects. Unknown outcomes require reconciliation.")
            .ProducesProblem(400).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409).ProducesProblem(503);
        group.MapPost("/deliveries/{id:guid}/actions", (Guid id, ActOnDelivery input, FactoryDeliveryService service, CancellationToken ct) => service.Act(id, input, ct))
            .WithSummary("Request an explicit delivery action").WithDescription("Commit-bound delivery authority and persistent operation receipts. Capturing executes nothing. Mutations are asynchronous; exact retries never repeat side effects. Unknown outcomes require reconciliation.")
            .ProducesProblem(400).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409).ProducesProblem(503);
        group.MapGet("/projects/{id:guid}/repair-policy", (Guid id, FactoryRepairService service, CancellationToken ct) => service.Policy(id, ct))
            .WithDescription("Bounded repair policy, immutable scope and attempt lineage. Execution requires explicit advance; fresh QA and independent review remain required. Money usage is unavailable.")
            .ProducesProblem(400).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
        group.MapPut("/projects/{id:guid}/repair-policy", (Guid id, EditRepairPolicy input, FactoryRepairService service, CancellationToken ct) => service.EditPolicy(id, input, ct))
            .WithDescription("Bounded repair policy, immutable scope and attempt lineage. Execution requires explicit advance; fresh QA and independent review remain required. Money usage is unavailable.")
            .ProducesProblem(400).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
        group.MapGet("/runs/{id:guid}/repair-loops", (Guid id, FactoryRepairService service, CancellationToken ct) => service.List(id, ct))
            .WithDescription("Bounded repair policy, immutable scope and attempt lineage. Execution requires explicit advance; fresh QA and independent review remain required. Money usage is unavailable.")
            .ProducesProblem(400).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
        group.MapPost("/runs/{id:guid}/repair-loops", (Guid id, PrepareRepairLoop input, FactoryRepairService service, CancellationToken ct) => service.Prepare(id, input, ct))
            .WithDescription("Bounded repair policy, immutable scope and attempt lineage. Execution requires explicit advance; fresh QA and independent review remain required. Money usage is unavailable.")
            .ProducesProblem(400).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
        group.MapGet("/repair-loops/{id:guid}", (Guid id, FactoryRepairService service, CancellationToken ct) => service.Read(id, ct))
            .WithDescription("Bounded repair policy, immutable scope and attempt lineage. Execution requires explicit advance; fresh QA and independent review remain required. Money usage is unavailable.")
            .ProducesProblem(400).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
        group.MapPost("/repair-loops/{id:guid}/advance", (Guid id, AdvanceRepairLoop input, FactoryRepairService service, CancellationToken ct) => service.Advance(id, input, ct))
            .WithDescription("Bounded repair policy, immutable scope and attempt lineage. Execution requires explicit advance; fresh QA and independent review remain required. Money usage is unavailable.")
            .ProducesProblem(400).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
        group.MapPost("/repair-loops/{id:guid}/stop", (Guid id, StopRepairLoop input, FactoryRepairService service, CancellationToken ct) => service.Stop(id, input, ct))
            .WithDescription("Bounded repair policy, immutable scope and attempt lineage. Execution requires explicit advance; fresh QA and independent review remain required. Money usage is unavailable.")
            .ProducesProblem(400).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
        group.MapGet("/runs/{id:guid}/reviews", (Guid id, FactoryReviewService service, CancellationToken ct) => service.List(id, ct))
            .WithDescription("Task-wide review rounds and persistent findings, current context and eligible independent assessments. Process completion is never approval.").ProducesProblem(403).ProducesProblem(404);
        group.MapPost("/runs/{id:guid}/reviews", (Guid id, BeginFactoryReview input, FactoryReviewService service, CancellationToken ct) => service.Begin(id, input, ct))
            .WithDescription("Capture a raw independent review packet before builder explanations. expectedRevision is the run revision; requestId replays exactly.")
            .ProducesProblem(400).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
        group.MapGet("/reviews/{id:guid}", (Guid id, FactoryReviewService service, CancellationToken ct) => service.Read(id, ct))
            .WithDescription("Review packet, immutable findings and freshness. Builder reports are withheld until independent assessment is recorded.").ProducesProblem(403).ProducesProblem(404);
        group.MapPost("/reviews/{id:guid}/assessment", (Guid id, AssessFactoryReview input, FactoryReviewService service, CancellationToken ct) => service.Assess(id, input, ct))
            .ProducesProblem(400).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
        group.MapPost("/reviews/{id:guid}/submit", (Guid id, SubmitFactoryReview input, FactoryReviewService service, CancellationToken ct) => service.Submit(id, input, ct))
            .WithDescription("Explicit review verdict bound to code and QA. Recheck every prior finding; fixed needs current code and successful verification. Does not accept the task or merge code.")
            .ProducesProblem(400).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
        group.MapPost("/reviews/{id:guid}/abandon", (Guid id, AbandonFactoryReview input, FactoryReviewService service, CancellationToken ct) => service.Abandon(id, input, ct))
            .ProducesProblem(400).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
        group.MapGet("/tasks/{id:guid}/criteria", (Guid id, FactoryQualityService service, CancellationToken ct) => service.Criteria(id, ct));
        group.MapPut("/tasks/{id:guid}/criteria", (Guid id, EditAcceptanceCriteria input, FactoryQualityService service, CancellationToken ct) => service.Edit(id, input, ct))
            .WithDescription("Explicitly adopt acceptance criteria with stable IDs. Uses the project revision; captured work orders remain immutable.")
            .ProducesProblem(400).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
        group.MapGet("/runs/{id:guid}/qa", (Guid id, FactoryQualityService service, CancellationToken ct) => service.Read(id, ct))
            .WithDescription("Criterion coverage, immutable verdict history, command receipts and current verification context. Missing inputs never imply pass.");
        group.MapPost("/runs/{id:guid}/qa", (Guid id, RecordCriterionVerdict input, FactoryQualityService service, CancellationToken ct) => service.Record(id, input, ct))
            .WithDescription("Record explicit operator QA after execution stops. Pass/fail requires current command evidence and an asserting test reference. Idempotent request ID; expectedRevision is the run revision.")
            .ProducesProblem(400).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
        group.MapPost("/runs/{id:guid}/start", (Guid id, StartFactoryRun input, FactoryDispatchService service, CancellationToken ct) => service.Start(id, input, ct))
            .WithDescription("Explicitly dispatch a prepared work order in its own worktree. expectedRevision is the run revision. Reusing the same requestId never redispatches work.")
            .ProducesProblem(400).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
        group.MapPost("/runs/{id:guid}/cancel", (Guid id, CancelFactoryRun input, FactoryDispatchService service, CancellationToken ct) => service.Cancel(id, input, ct))
            .ProducesProblem(400).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
        group.MapPost("/runs/{id:guid}/steps/{stepId:guid}/decision", (Guid id, Guid stepId, DecideFactoryStep input, FactoryDispatchService service, CancellationToken ct) => service.Decide(id, stepId, input, ct))
            .WithDescription("Explicit human decision for a waiting manual step. expectedRevision is the run revision; a decision requestId is idempotent for the same decision.")
            .ProducesProblem(400).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
        group.MapGet("/tasks/{id:guid}/readiness", (Guid id, FactoryRunService service, CancellationToken ct) => service.Readiness(id, ct))
            .WithDescription("Read-only local observations for preparing an immutable work order. Does not verify credentials/model access or execute work.").ProducesProblem(403).ProducesProblem(404);
        group.MapGet("/tasks/{id:guid}/runs", (Guid id, FactoryRunService service, CancellationToken ct) => service.List(id, ct)).ProducesProblem(403).ProducesProblem(404);
        group.MapGet("/projects/{id:guid}/runs", (Guid id, FactoryRunService service, CancellationToken ct) => service.ProjectRuns(id, ct))
            .WithDescription("Read-only project run history, newest first, with captured task titles and pipeline versions.").ProducesProblem(403).ProducesProblem(404);
        group.MapPost("/tasks/{id:guid}/runs", async (Guid id, PrepareFactoryRun input, FactoryRunService service, CancellationToken ct) =>
        {
            var detail = await service.Prepare(id, input, ct);
            return Results.Created($"/api/factory/runs/{detail.Run.Id}", detail);
        }).WithDescription("Prepare an immutable run snapshot and pending stage/step records. Reusing a requestId for this task returns the original snapshot. Does not dispatch execution.")
            .Produces<FactoryRunDetail>(201).ProducesProblem(400).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
        group.MapGet("/runs/{id:guid}", (Guid id, FactoryRunService service, CancellationToken ct) => service.Read(id, ct)).ProducesProblem(403).ProducesProblem(404);
        group.MapGet("/templates", (FactoryService service) => service.Templates()).ProducesProblem(403);
        group.MapPost("/validate", (FactoryDefinition input, FactoryService service) => service.Validate(input))
            .WithDescription("Read-only structural validation. Valid does not certify skill availability, provider access, command execution or execution readiness.")
            .ProducesProblem(400).ProducesProblem(403);
        group.MapGet("/projects/{id:guid}/pipelines", (Guid id, FactoryService service, CancellationToken ct) => service.List(id, ct)).ProducesProblem(403).ProducesProblem(404);
        group.MapPost("/projects/{id:guid}/pipelines", async (Guid id, CreateFactoryPipeline input, FactoryService service, CancellationToken ct) =>
        {
            var detail = await service.Create(id, input, ct);
            return Results.Created($"/api/factory/pipelines/{detail.Pipeline.Id}", detail);
        }).Produces<FactoryPipelineDetail>(201).ProducesProblem(400).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
        group.MapGet("/pipelines/{id:guid}", (Guid id, FactoryService service, CancellationToken ct) => service.Read(id, ct)).ProducesProblem(403).ProducesProblem(404);
        group.MapPut("/pipelines/{id:guid}/draft", (Guid id, EditFactoryDraft input, FactoryService service, CancellationToken ct) => service.Edit(id, input, ct)).ProducesProblem(400).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
        group.MapPost("/pipelines/{id:guid}/publish", (Guid id, ChangeFactoryPipeline input, FactoryService service, CancellationToken ct) => service.Publish(id, input, ct)).ProducesProblem(400).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
        group.MapPost("/pipelines/{id:guid}/archive", (Guid id, ChangeFactoryPipeline input, FactoryService service, CancellationToken ct) => service.Archive(id, input, ct)).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
        group.MapGet("/versions/{id:guid}", (Guid id, FactoryService service, CancellationToken ct) => service.Version(id, ct)).ProducesProblem(403).ProducesProblem(404);
        group.MapGet("/tasks/{id:guid}/configuration", (Guid id, FactoryService service, CancellationToken ct) => service.TaskConfiguration(id, ct)).ProducesProblem(403).ProducesProblem(404);
        group.MapPut("/tasks/{id:guid}/configuration", (Guid id, AttachFactoryPipeline input, FactoryService service, CancellationToken ct) => service.Attach(id, input, ct)).ProducesProblem(400).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
        group.MapDelete("/tasks/{id:guid}/configuration", (Guid id, long expectedRevision, FactoryService service, CancellationToken ct) => service.Detach(id, expectedRevision, ct)).ProducesProblem(400).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
    }
}
