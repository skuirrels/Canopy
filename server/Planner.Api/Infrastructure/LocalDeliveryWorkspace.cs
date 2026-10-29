using Planner.Api.Application;
using Planner.Api.Domain;

namespace Planner.Api.Infrastructure;

public sealed class LocalDeliveryWorkspace(LocalVerificationEnvironment verification, AgentOptions agents, DeliveryOptions options) : IDeliveryWorkspace
{
    public async Task<DeliveryGitState> Inspect(FactoryRun run, DeliveryPolicy policy, DeliveryObservation provider, CancellationToken ct)
    {
        var root = verification.Workspace(run);
        if ((await verification.ReadGit(root, ["status", "--porcelain=v1", "--untracked-files=all"], ct)).Length > 0) throw new RuleException("Commit the retained candidate before fresh verification and review. Delivery requires a clean code/index worktree.", 409);
        var remote = (await verification.ReadGit(root, ["remote", "get-url", "--push", "--", policy.RemoteName], ct)).Trim();
        if (!SameRemote(remote, provider.CloneUrl) && !SameRemote(remote, provider.SshUrl)) throw new RuleException("The configured push remote does not identify the policy's provider repository.", 409);
        var head = (await verification.ReadGit(root, ["rev-parse", "HEAD"], ct)).Trim();
        var tree = (await verification.ReadGit(root, ["rev-parse", "HEAD^{tree}"], ct)).Trim();
        string mergeBase;
        try { mergeBase = (await verification.ReadGit(root, ["merge-base", provider.BaseSha, head], ct)).Trim(); }
        catch (RuleException) { throw new RuleException("Fetch and integrate the current target base, then commit and repeat verification/review before delivery.", 409); }
        if (mergeBase != provider.BaseSha) throw new RuleException("The candidate does not contain the current target base. Integrate it and obtain fresh verification/review.", 409);
        var changed = (await verification.ReadGit(root, ["diff", "--name-only", "--no-renames", "-z", provider.BaseSha, head, "--"], ct)).Split('\0', StringSplitOptions.RemoveEmptyEntries);
        if (changed.Length == 0) throw new RuleException("There are no candidate file changes relative to the target base.", 409);
        return new(head, tree, changed);
    }
    public async Task Publish(FactoryRun run, FactoryDelivery delivery, DeliveryObservation provider, CancellationToken ct)
    {
        var state = await Inspect(run, delivery.Policy, provider, ct);
        if (state.HeadSha != delivery.Capture.HeadSha || state.TreeSha != delivery.Capture.TreeSha || provider.BaseSha != delivery.Capture.BaseSha) throw new RuleException("Captured candidate or target base changed before publication.", 409);
        if (provider.BranchSha is not null)
        { if (provider.BranchSha != state.HeadSha) throw new RuleException("The publication branch belongs to a different commit; it will not be overwritten.", 409); return; }
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromSeconds(agents.GitTimeoutSeconds));
        var code = await AgentProcess.Run(LocalAgentEnvironment.Resolve(agents.GitExecutable) ?? throw new RuleException("Git is unavailable."), verification.Workspace(run),
            ["push", "--porcelain", "--no-verify", "--force-with-lease=refs/heads/" + delivery.Capture.BranchName + ":", "--", delivery.Policy.RemoteName, state.HeadSha + ":refs/heads/" + delivery.Capture.BranchName], null,
            _ => Task.CompletedTask, _ => Task.CompletedTask, deadline.Token, new Dictionary<string, string> { ["GIT_TERMINAL_PROMPT"] = "0" });
        if (code != 0) throw new RuleException("Git could not establish publication. Reconcile the actual remote branch before another action.", 503);
    }
    private bool SameRemote(string actual, string expected)
    {
        static string Normalize(string value) => value.EndsWith(".git", StringComparison.Ordinal) ? value[..^4] : value.TrimEnd('/');
        if (!Normalize(actual).Equals(Normalize(expected), StringComparison.OrdinalIgnoreCase)) return false;
        if (Uri.TryCreate(actual, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http") return uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0 && uri.Host.Equals(options.GitHubHost, StringComparison.OrdinalIgnoreCase) && (uri.Scheme == "https" || uri.IsLoopback);
        return actual.StartsWith("git@" + options.GitHubHost + ":", StringComparison.OrdinalIgnoreCase);
    }
}
