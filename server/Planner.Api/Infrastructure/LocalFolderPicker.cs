using System.Text;
using Planner.Api.Application;
using Planner.Api.Domain;

namespace Planner.Api.Infrastructure;

/// <summary>Opens the operating system's folder dialog on the machine running the backend, which is the same machine as the browser in phase 1.</summary>
public sealed class LocalFolderPicker(AgentOptions options) : IFolderPicker
{
    // AppleScript reads the optional starting folder from argv and prints the POSIX path of the chosen folder.
    private static readonly string[] MacScript =
    [
        "-e", "on run argv",
        "-e", "activate",
        "-e", "if (count of argv) is 0 then return POSIX path of (choose folder with prompt \"Choose the repository folder\")",
        "-e", "return POSIX path of (choose folder with prompt \"Choose the repository folder\" default location (POSIX file (item 1 of argv)))",
        "-e", "end run",
    ];
    private readonly SemaphoreSlim gate = new(1, 1);

    public bool Available => Executable() is not null;

    public async Task<FolderChoice> Choose(string? start, CancellationToken ct)
    {
        var executable = Executable() ?? throw new RuleException("Folder browsing is unavailable on this machine. Type the repository path instead.", 503);
        if (!await gate.WaitAsync(0, ct)) throw new RuleException("A folder picker is already open.", 409);
        try
        {
            var arguments = (string.IsNullOrWhiteSpace(options.FolderPickerExecutable) ? MacScript : [.. options.FolderPickerArguments]).ToList();
            if (!string.IsNullOrWhiteSpace(start) && Path.IsPathRooted(start) && Directory.Exists(start)) arguments.Add(Path.GetFullPath(start));
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(options.FolderPickerTimeoutSeconds));
            var output = new StringBuilder(); var errors = new StringBuilder();
            int code;
            try
            {
                code = await AgentProcess.Run(executable, Path.GetTempPath(), arguments, null,
                    line => { if (output.Length < 10000) output.AppendLine(line); return Task.CompletedTask; },
                    line => { if (errors.Length < 10000) errors.AppendLine(line); return Task.CompletedTask; }, timeout.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new RuleException("No folder was chosen in time.", 504); }
            var chosen = output.ToString().Trim();
            // Pickers exit non-zero without output when the person cancels; AppleScript reports that as error -128.
            if (code != 0 && chosen.Length == 0 && (errors.ToString().Trim().Length == 0 || errors.ToString().Contains("(-128)"))) return new(null);
            if (code != 0 || chosen.Length == 0) throw new RuleException("The folder picker failed: " + AgentRun.Tail(errors.ToString(), 500).Trim(), 502);
            var path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(chosen));
            return Directory.Exists(path) ? new(path) : throw new RuleException("The chosen folder does not exist.", 502);
        }
        finally { gate.Release(); }
    }

    private string? Executable() => string.IsNullOrWhiteSpace(options.FolderPickerExecutable)
        ? OperatingSystem.IsMacOS() ? LocalAgentEnvironment.Resolve("osascript") : null
        : LocalAgentEnvironment.Resolve(options.FolderPickerExecutable);
}
