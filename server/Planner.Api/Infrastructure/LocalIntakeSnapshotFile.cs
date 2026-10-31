using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Planner.Api.Domain;

namespace Planner.Api.Infrastructure;

internal static class LocalIntakeSnapshotFile
{
    [DllImport("libc", EntryPoint = "open", SetLastError = true)]
    private static extern int Open([MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags);
    public static bool Supported => OperatingSystem.IsLinux() || OperatingSystem.IsMacOS();
    public static FileStream Read(string path)
    {
        if (!Supported) throw new RuleException("This platform has no nonblocking regular-snapshot adapter. Snapshot collection is unavailable.", 503);
        // Native O_NONBLOCK prevents a configured FIFO from hanging at open,
        // before a managed cancellation token can apply. The stream must seek.
        // O_NOFOLLOW and O_CLOEXEC retain link and descriptor ownership rules.
        var flags = OperatingSystem.IsMacOS() ? 0x0004 | 0x0100 | 0x1000000 : 0x0800 | 0x20000 | 0x80000;
        var fd = Open(path, flags); if (fd < 0) throw new Win32Exception(Marshal.GetLastPInvokeError());
        var handle = new SafeFileHandle((IntPtr)fd, ownsHandle: true);
        try { return new FileStream(handle, FileAccess.Read, bufferSize: 4096, isAsync: false); }
        catch { handle.Dispose(); throw; }
    }
}
