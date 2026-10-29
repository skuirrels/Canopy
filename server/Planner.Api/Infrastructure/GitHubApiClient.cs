using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Planner.Api.Domain;
namespace Planner.Api.Infrastructure;
// Shared authenticated transport: no redirects, bounded reads and deadlines. Tokens never leave this layer.
public sealed class GitHubApiClient(IHttpClientFactory clients, DeliveryOptions options)
{
    public string Binding => FactoryArtifact.Hash(options.ApiBaseUrl.TrimEnd('/') + "\n" + options.GitHubHost + "\n" + options.ApiVersion);
    public string Identity => options.GitHubHost.ToLowerInvariant();
    public async Task<JsonElement?> Send(string path, HttpMethod method, object? body, CancellationToken ct, int[]? absent = null, Action<HttpResponseMessage>? inspect = null)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromSeconds(options.RequestTimeoutSeconds));
        try
        {
            using var request = new HttpRequestMessage(method, options.ApiBaseUrl.TrimEnd('/') + "/" + path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await Token(deadline.Token));
            request.Headers.UserAgent.ParseAdd("Canopy-Factory-Delivery"); request.Headers.Accept.ParseAdd("application/vnd.github+json"); request.Headers.Add("X-GitHub-Api-Version", options.ApiVersion);
            if (body is not null) request.Content = new StringContent(JsonSerializer.Serialize(body, new JsonSerializerOptions(JsonSerializerDefaults.Web)), Encoding.UTF8, "application/json");
            using var response = await clients.CreateClient("delivery").SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (absent?.Contains((int)response.StatusCode) == true) return null;
            if (!response.IsSuccessStatusCode) throw new RuleException($"GitHub returned HTTP {(int)response.StatusCode}. No successful provider outcome was established.", 503);
            inspect?.Invoke(response);
            using var stream = await response.Content.ReadAsStreamAsync(deadline.Token); using var bytes = new MemoryStream(); var buffer = new byte[8192]; int count;
            while ((count = await stream.ReadAsync(buffer, deadline.Token)) > 0) { if (bytes.Length + count > options.MaxResponseBytes) throw new RuleException("Provider response exceeds the configured limit; coverage is incomplete.", 503); bytes.Write(buffer, 0, count); }
            using var document = JsonDocument.Parse(bytes.ToArray()); return document.RootElement.Clone();
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new RuleException("GitHub request timed out. No successful provider outcome was established.", 503); }
        catch (Exception error) when (error is HttpRequestException or JsonException or KeyNotFoundException or InvalidOperationException)
        { throw new RuleException("GitHub response is unavailable or does not satisfy the provider contract.", 503); }
    }
    private async Task<string> Token(CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(options.TokenEnvironmentVariable)) return Environment.GetEnvironmentVariable(options.TokenEnvironmentVariable) is { Length: > 0 } token ? token : throw new RuleException("The configured GitHub credential environment variable is unavailable.", 503);
        var executable = LocalAgentEnvironment.Resolve(options.GitHubExecutable) ?? throw new RuleException("Configure a GitHub credential variable or local gh executable.", 503);
        var info = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in new[] { "auth", "token", "--hostname", options.GitHubHost }) info.ArgumentList.Add(argument);
        using var process = Process.Start(info) ?? throw new RuleException("GitHub credentials are unavailable.", 503);
        try { var output = process.StandardOutput.ReadToEndAsync(ct); var error = process.StandardError.ReadToEndAsync(ct); await process.WaitForExitAsync(ct); var value = (await output).Trim(); await error; return process.ExitCode == 0 && value.Length > 0 ? value : throw new RuleException("Sign in to gh for the configured host or configure an explicit credential variable.", 503); }
        finally { if (!process.HasExited) { process.Kill(true); await process.WaitForExitAsync(CancellationToken.None); } }
    }
}
