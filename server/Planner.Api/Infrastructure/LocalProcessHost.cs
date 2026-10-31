using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;
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
    public static ProcessStartInfo Start(string executable, string directory, IReadOnlyList<string> arguments, string? ownerLease)
    {
        var start = new ProcessStartInfo(executable) { WorkingDirectory = directory };
        if (Supported)
        {
            start.FileName = Environment.ProcessPath ?? throw new InvalidOperationException("The owned process host is unavailable.");
            if (Path.GetFileNameWithoutExtension(start.FileName).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
                start.ArgumentList.Add(typeof(LocalProcessHost).Assembly.Location);
            start.ArgumentList.Add(Argument); start.ArgumentList.Add(ownerLease ?? throw new InvalidOperationException("The owner lease is unavailable.")); start.ArgumentList.Add(executable);
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
            if (Supported && kill(-process.Id, 9) == 0) return;
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException) { }
        catch (Win32Exception) when (process.HasExited) { }
    }
    public static async Task<int> Run(string[] arguments)
    {
        var started = false;
        try
        {
            if (!Supported || arguments.Length < 3) throw new InvalidOperationException("An owned process requires a supported platform and executable.");
            if (setsid() < 0) throw new Win32Exception(Marshal.GetLastPInvokeError(), "Cannot establish the owned process session.");
            using var owner = new AnonymousPipeClientStream(PipeDirection.In, arguments[1]);
            var lostOwner = owner.ReadAsync(new byte[1]).AsTask();
            if (lostOwner.IsCompleted) return 1;
            var start = new ProcessStartInfo(arguments[2]) { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in arguments.Skip(3)) start.ArgumentList.Add(argument);
            using var child = Process.Start(start) ?? throw new InvalidOperationException("The owned process could not start.");
            started = true;
            async Task Input()
            {
                try { await Console.OpenStandardInput().CopyToAsync(child.StandardInput.BaseStream); }
                catch (IOException) { /* A command may close its input before consuming all bytes. */ }
                finally { child.StandardInput.Close(); }
            }
            var work = Task.WhenAll(Input(), child.StandardOutput.BaseStream.CopyToAsync(Console.OpenStandardOutput()),
                child.StandardError.BaseStream.CopyToAsync(Console.OpenStandardError()), child.WaitForExitAsync());
            if (await Task.WhenAny(work, lostOwner) == lostOwner && !work.IsCompleted)
            { kill(-Environment.ProcessId, 9); return 1; }
            await work; return child.ExitCode;
        }
        catch (Exception error)
        {
            // Loss of the owner/forwarding channel must not orphan a live child.
            if (started) { kill(-Environment.ProcessId, 9); return 1; }
            await Console.Error.WriteLineAsync("Owned process could not complete: " + error.Message); return 1;
        }
    }
}
