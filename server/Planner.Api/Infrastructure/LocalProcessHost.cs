using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Planner.Api.Infrastructure;

// A supervisor keeps its session alive while descendants hold inherited pipes.
// Killing only a direct child after it exits cannot close those pipes or bound
// the worker's wait. This is process ownership, not an OS security sandbox.
internal static class LocalProcessHost
{
    public const string Argument = "--canopy-owned-process";
    public static bool Supported => OperatingSystem.IsLinux() || OperatingSystem.IsMacOS();
    [DllImport("libc", SetLastError = true)] private static extern int setsid();
    [DllImport("libc", SetLastError = true)] private static extern int kill(int pid, int signal);
    public static ProcessStartInfo Start(string executable, string directory, IReadOnlyList<string> arguments)
    {
        var start = new ProcessStartInfo(executable) { WorkingDirectory = directory };
        if (Supported)
        {
            start.FileName = Environment.ProcessPath ?? throw new InvalidOperationException("The owned process host is unavailable.");
            if (Path.GetFileNameWithoutExtension(start.FileName).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
                start.ArgumentList.Add(typeof(LocalProcessHost).Assembly.Location);
            start.ArgumentList.Add(Argument); start.ArgumentList.Add(executable);
        }
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        return start;
    }
    public static void Stop(Process process)
    {
        try
        {
            if (process.HasExited) return;
            // The supervisor remains alive until its child and output forwarders
            // settle, retaining the group identity for the active operation.
            if (Supported) kill(-process.Id, 9);
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException) { }
    }
    public static async Task<int> Run(string[] arguments)
    {
        try
        {
            if (!Supported || arguments.Length < 2) throw new InvalidOperationException("An owned process requires a supported platform and executable.");
            if (setsid() < 0) throw new Win32Exception(Marshal.GetLastPInvokeError(), "Cannot establish the owned process session.");
            var start = new ProcessStartInfo(arguments[1]) { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in arguments.Skip(2)) start.ArgumentList.Add(argument);
            using var child = Process.Start(start) ?? throw new InvalidOperationException("The owned process could not start.");
            async Task Input()
            {
                try { await Console.OpenStandardInput().CopyToAsync(child.StandardInput.BaseStream); }
                catch (IOException) { /* A command may close its input before consuming all bytes. */ }
                finally { child.StandardInput.Close(); }
            }
            await Task.WhenAll(Input(), child.StandardOutput.BaseStream.CopyToAsync(Console.OpenStandardOutput()),
                child.StandardError.BaseStream.CopyToAsync(Console.OpenStandardError()), child.WaitForExitAsync());
            return child.ExitCode;
        }
        catch (Exception error) { await Console.Error.WriteLineAsync("Owned process could not complete: " + error.Message); return 1; }
    }
}
