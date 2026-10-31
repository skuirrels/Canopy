using System.Text.Json;
using System.Text.RegularExpressions;
using Planner.Api.Application;
using Planner.Api.Domain;

namespace Planner.Api.Infrastructure;

public sealed class GitHubDeliveryProvider(GitHubApiClient api, DeliveryOptions options) : IDeliveryProvider
{
    public string Binding => api.Binding;
    private static string Escape(string value) => Uri.EscapeDataString(value);
    private static string Repo(DeliveryPolicy policy) => $"repos/{policy.Repository}";
    public async Task<DeliveryObservation> Observe(DeliveryPolicy policy, DeliveryCapture? capture, DeliveryRemote? remote, string? marker, CancellationToken ct)
    {
        var root = Repo(policy); var repository = (await Send(root, HttpMethod.Get, null, ct))!.Value;
        if (!Text(repository, "full_name").Equals(policy.Repository, StringComparison.OrdinalIgnoreCase)) throw new RuleException("Provider repository identity does not match project policy.", 503);
        var target = (await Send(root + "/git/ref/heads/" + Escape(policy.TargetBranch), HttpMethod.Get, null, ct))!.Value;
        var baseSha = Sha(target.GetProperty("object"), "sha");
        var protection = await Send(root + "/branches/" + Escape(policy.TargetBranch) + "/protection", HttpMethod.Get, null, ct, [403, 404]);
        var strict = protection is { } p && p.TryGetProperty("required_status_checks", out var status) && status.ValueKind == JsonValueKind.Object && Flag(status, "strict");
        var protectedChecks = new List<DeliveryCheck>();
        if (protection is { } pr && pr.TryGetProperty("required_status_checks", out var required) && required.ValueKind == JsonValueKind.Object && required.TryGetProperty("checks", out var checks))
            foreach (var check in checks.EnumerateArray()) if (check.TryGetProperty("app_id", out var app) && app.TryGetInt64(out var appId) && appId > 0) protectedChecks.Add(new(Text(check, "context"), appId));
        var admin = protection is { } ap && ap.TryGetProperty("enforce_admins", out var admins) && Flag(admins, "enabled");
        var bypass = false;
        if (protection is { } bp && bp.TryGetProperty("required_pull_request_reviews", out var reviews) && reviews.ValueKind == JsonValueKind.Object && reviews.TryGetProperty("bypass_pull_request_allowances", out var allowances))
            bypass = allowances.EnumerateObject().Any(x => x.Value.ValueKind == JsonValueKind.Array && x.Value.GetArrayLength() > 0);
        var linear = protection is { } lp && lp.TryGetProperty("required_linear_history", out var linearRule) && Flag(linearRule, "enabled");
        var queue = protection is { } qp && qp.TryGetProperty("required_merge_queue", out var queueRule) && Flag(queueRule, "enabled");
        string? branchSha = null; DeliveryRemote? pull = null; var runs = new List<DeliveryCheckResult>(); var baseAncestor = false;
        if (capture is not null)
        {
            var branch = await Send(root + "/git/ref/heads/" + Escape(capture.BranchName), HttpMethod.Get, null, ct, [404]);
            if (branch.HasValue) branchSha = Sha(branch.Value.GetProperty("object"), "sha");
            if (remote is not null) pull = ParsePull((await Send(root + "/pulls/" + remote.Number, HttpMethod.Get, null, ct))!.Value, policy, capture.BranchName);
            else
            {
                var pulls = await Pages(root + "/pulls?state=all&head=" + Escape(policy.Repository.Split('/')[0] + ":" + capture.BranchName) + "&base=" + Escape(policy.TargetBranch), null, ct);
                var matching = pulls.Where(x => Text(x, "body").Contains(marker ?? "\0", StringComparison.Ordinal)).ToList();
                if (matching.Count > 1 || pulls.Count > matching.Count) throw new RuleException("The publication branch already has a different or ambiguous pull request. Choose a new branch.", 409);
                if (matching.Count == 1) pull = ParsePull(matching[0], policy, capture.BranchName);
            }
            if (pull is { State: "merged" }) pull = await MergedPull(policy, pull, ct);
            if (branchSha == capture.HeadSha || pull is { State: "merged" } && pull.HeadSha == capture.HeadSha)
            {
                var checkRuns = await Pages(root + "/commits/" + capture.HeadSha + "/check-runs?filter=latest", "check_runs", ct);
                foreach (var check in checkRuns) runs.Add(new(check.GetProperty("id").GetInt64(), Text(check, "name"), check.GetProperty("app").GetProperty("id").GetInt64(), Sha(check, "head_sha"), Text(check, "status"), Optional(check, "conclusion")));
                var compare = (await Send(root + "/compare/" + baseSha + "..." + capture.HeadSha, HttpMethod.Get, null, ct))!.Value;
                baseAncestor = Text(compare, "status") is "ahead" or "identical" && Sha(compare.GetProperty("merge_base_commit"), "sha") == baseSha;
            }
        }
        return new(policy.Repository, Text(repository, "clone_url"), Text(repository, "ssh_url"), baseSha, branchSha, pull, runs, strict, admin, bypass, protectedChecks,
            Flag(repository, "allow_merge_commit"), linear, queue, baseAncestor, DateTime.UtcNow);
    }
    public async Task<DeliveryRemote> CreateDraft(FactoryDelivery delivery, string title, string body, CancellationToken ct)
    {
        var result = (await Send(Repo(delivery.Policy) + "/pulls", HttpMethod.Post,
            new { title, body = body + "\n\n" + delivery.Marker, head = delivery.Capture.BranchName, @base = delivery.Policy.TargetBranch, draft = true }, ct))!.Value;
        return ParsePull(result, delivery.Policy, delivery.Capture.BranchName);
    }
    public async Task<DeliveryMergeResult> Merge(FactoryDelivery delivery, CancellationToken ct)
    {
        var result = (await Send(Repo(delivery.Policy) + "/pulls/" + delivery.Remote!.Number + "/merge-async", HttpMethod.Put,
            new { sha = delivery.Capture.HeadSha, merge_method = "merge", merge_action = "direct_merge", bypass_rules = false }, ct))!.Value;
        return ParseMerge(result, delivery.Capture.HeadSha);
    }
    public async Task<DeliveryMergeResult> MergeResult(FactoryDelivery delivery, CancellationToken ct)
    {
        if (delivery.Remote?.MergeRequestId is not { } requestId) return new("unknown", null, delivery.Remote?.MergeSha);
        var result = await Send(Repo(delivery.Policy) + "/pulls/" + delivery.Remote.Number + "/merge-async/" + Escape(requestId), HttpMethod.Get, null, ct, [404]);
        return result.HasValue ? ParseMerge(result.Value, delivery.Capture.HeadSha) : new("unknown", requestId, null);
    }
    public async Task<DeliveryCommit> Commit(DeliveryPolicy policy, string sha, CancellationToken ct)
    { var value = (await Send(Repo(policy) + "/git/commits/" + sha, HttpMethod.Get, null, ct))!.Value; return new(Sha(value, "sha"), Sha(value.GetProperty("tree"), "sha"), value.GetProperty("parents").EnumerateArray().Select(x => Sha(x, "sha")).ToList()); }
    public async Task<bool> TargetContains(DeliveryPolicy policy, string mergeSha, CancellationToken ct)
    {
        var value = (await Send(Repo(policy) + "/compare/" + mergeSha + "..." + Escape(policy.TargetBranch), HttpMethod.Get, null, ct))!.Value;
        return Text(value, "status") is "ahead" or "identical" && Sha(value.GetProperty("merge_base_commit"), "sha") == mergeSha;
    }
    private async Task<DeliveryRemote> MergedPull(DeliveryPolicy policy, DeliveryRemote pull, CancellationToken ct)
    {
        // REST 2026-03-10 omits merge_commit_sha. GraphQL supplies the actual
        // merged commit, including reconciliation after a lost merge response.
        const string query = "query($owner:String!,$name:String!,$number:Int!){repository(owner:$owner,name:$name){nameWithOwner pullRequest(number:$number){number merged headRefOid mergeCommit{oid}}}}";
        var parts = policy.Repository.Split('/');
        var result = (await Send("graphql", HttpMethod.Post, new { query, variables = new { owner = parts[0], name = parts[1], number = pull.Number } }, ct))!.Value;
        if (result.ValueKind != JsonValueKind.Object || result.TryGetProperty("errors", out var errors) && (errors.ValueKind != JsonValueKind.Array || errors.GetArrayLength() != 0))
            throw new RuleException("GitHub GraphQL merge identity contains errors; no complete outcome was established.", 503);
        if (!Object(result, "data", out var data) || !Object(data, "repository", out var repository) || !Object(repository, "pullRequest", out var observed)
            || !Text(repository, "nameWithOwner").Equals(policy.Repository, StringComparison.OrdinalIgnoreCase)
            || !observed.TryGetProperty("number", out var number) || number.ValueKind != JsonValueKind.Number || !number.TryGetInt32(out var id) || id != pull.Number
            || !Flag(observed, "merged") || Text(observed, "headRefOid") != pull.HeadSha || !Object(observed, "mergeCommit", out var commit))
            throw new RuleException("GitHub GraphQL merged PR identity is unavailable or does not match the observed repository, PR and head.", 503);
        return pull with { MergeSha = Sha(commit, "oid") };
    }
    private static bool Object(JsonElement value, string name, out JsonElement child)
    { child = default; return value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out child) && child.ValueKind == JsonValueKind.Object; }
    private static DeliveryMergeResult ParseMerge(JsonElement value, string headSha)
    {
        var status = Text(value, "status"); var details = value.GetProperty("details");
        var requestId = Optional(details, "uuid"); var sha = Optional(details, "sha");
        if (status is not ("pending" or "merged" or "failed")) throw new RuleException("Unsupported or unknown asynchronous merge outcome. Reconcile the actual PR; no request will be repeated.", 503);
        if (status == "merged" && (sha is null || !Regex.IsMatch(sha, "^[a-f0-9]{40}$"))) throw new RuleException("Actual merge commit identity is unavailable.", 503);
        if (status == "pending" && (requestId is null || !Guid.TryParseExact(requestId, "D", out _) || Text(details, "expected_head_sha") != headSha || Text(details, "merge_method") != "merge" || Text(details, "merge_action") != "direct_merge" || !details.TryGetProperty("bypass_rules", out var bypass) || bypass.ValueKind != JsonValueKind.False)) throw new RuleException("Asynchronous merge authority is unavailable or does not match the requested head/action.", 503);
        return new(status, requestId, sha);
    }
    private static DeliveryRemote ParsePull(JsonElement value, DeliveryPolicy policy, string branch)
    {
        var head = value.GetProperty("head"); var target = value.GetProperty("base");
        if (!Text(head.GetProperty("repo"), "full_name").Equals(policy.Repository, StringComparison.OrdinalIgnoreCase) || !Text(target.GetProperty("repo"), "full_name").Equals(policy.Repository, StringComparison.OrdinalIgnoreCase) || Text(head, "ref") != branch || Text(target, "ref") != policy.TargetBranch)
            throw new RuleException("Pull request repository or branch binding changed.", 409);
        var url = Text(value, "html_url"); DeliveryOptions.Endpoint(url);
        var merged = Flag(value, "merged") || value.TryGetProperty("merged_at", out var at) && at.ValueKind == JsonValueKind.String;
        return new(value.GetProperty("number").GetInt32(), url, Sha(head, "sha"), Sha(target, "sha"), merged ? "merged" : Text(value, "state"), Flag(value, "draft"), null,
            Mergeable: value.TryGetProperty("mergeable", out var can) && can.ValueKind is JsonValueKind.True or JsonValueKind.False ? can.GetBoolean() : null);
    }
    private async Task<List<JsonElement>> Pages(string path, string? arrayName, CancellationToken ct)
    {
        var result = new List<JsonElement>();
        for (var page = 1; page <= options.MaximumPages; page++)
        {
            var value = (await Send(path + (path.Contains('?') ? "&" : "?") + "per_page=100&page=" + page, HttpMethod.Get, null, ct))!.Value;
            var array = arrayName is null ? value : value.GetProperty(arrayName);
            if (array.ValueKind != JsonValueKind.Array) throw new RuleException("Provider collection shape is unavailable.", 503);
            result.AddRange(array.EnumerateArray().Select(x => x.Clone()));
            if (array.GetArrayLength() < 100)
            {
                if (arrayName is not null && value.GetProperty("total_count").GetInt64() > result.Count) throw new RuleException("Provider check coverage is truncated.", 503);
                return result;
            }
        }
        throw new RuleException("Provider pagination limit reached; coverage is incomplete.", 503);
    }
    private Task<JsonElement?> Send(string path, HttpMethod method, object? body, CancellationToken ct, int[]? absent = null) => api.Send(path, method, body, ct, absent);
    private static bool Flag(JsonElement value, string name) => value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.True;
    private static string Text(JsonElement value, string name) => Optional(value, name) ?? "";
    private static string? Optional(JsonElement value, string name) => value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String ? property.GetString() : null;
    private static string Sha(JsonElement value, string name) { var sha = Text(value, name); return Regex.IsMatch(sha, "^[a-f0-9]{40}$") ? sha : throw new RuleException("Provider commit identity is unavailable.", 503); }
}
