using System.Security.Cryptography;
using System.Text;
using Planner.Api.Application;
using Planner.Api.Domain;

namespace Planner.Api.Infrastructure;

public sealed class LocalMarkdownFiles : IMarkdownFiles
{
    private const int MaxBytes = 2_000_000;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    public SpecFileContent Read(string path) => Access(() => ReadFile(ValidatePath(path)));
    public SpecFileContent Write(string path, string expectedHash, string markdown) => Access(() =>
    {
        var target = ValidatePath(path);
        if (markdown is null || Utf8.GetByteCount(markdown) > MaxBytes) throw new RuleException("Use a UTF-8 Markdown file smaller than 2 MB.");
        var bytes = Utf8.GetBytes(markdown);
        if (ReadFile(target).Hash != expectedHash) throw new RuleException("The file changed outside Canopy. Refresh from file and review it before saving.", 409);
        // A sibling temporary file keeps replacement on the same filesystem. Preserve permissions.
        var temporary = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(target)!, $".canopy-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllBytes(temporary, bytes);
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(temporary, File.GetUnixFileMode(target));
            // Recheck after preparing the replacement, including symlink/size validation.
            if (ReadFile(ValidatePath(target)).Hash != expectedHash) throw new RuleException("The file changed outside Canopy. Refresh from file before saving.", 409);
            File.Move(temporary, target, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        return new SpecFileContent(target, markdown, Hash(bytes));
    });
    private static string ValidatePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !System.IO.Path.IsPathFullyQualified(path)) throw new RuleException("Enter an absolute path to an existing Markdown file on this machine.");
        var full = System.IO.Path.GetFullPath(path);
        if (!new[] { ".md", ".markdown" }.Contains(System.IO.Path.GetExtension(full), StringComparer.OrdinalIgnoreCase)) throw new RuleException("Choose a .md or .markdown file.");
        // Reject symlinks in every component, so replacement cannot follow a redirected parent.
        for (var current = new FileInfo(full) as FileSystemInfo; current is not null; current = current is FileInfo f ? f.Directory : ((DirectoryInfo)current).Parent)
            if (current.LinkTarget is not null) throw new RuleException("Choose the real file path, without symbolic links.");
        if (!File.Exists(full)) throw new RuleException("The linked Markdown file was not found. Check its path.", 404);
        if ((File.GetAttributes(full) & FileAttributes.Directory) != 0) throw new RuleException("Choose a Markdown file, not a directory.");
        return full;
    }
    private static SpecFileContent ReadFile(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (stream.Length > MaxBytes) throw new RuleException("Use a UTF-8 Markdown file smaller than 2 MB.");
        var buffer = new byte[MaxBytes + 1];
        var count = stream.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false);
        if (count > MaxBytes) throw new RuleException("Use a UTF-8 Markdown file smaller than 2 MB.");
        var bytes = buffer.AsSpan(0, count);
        var markdown = Utf8.GetString(bytes);
        // UTF-8 BOM participates in the byte hash but is not document content.
        return new(path, markdown.TrimStart('\uFEFF'), Hash(bytes));
    }
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static T Access<T>(Func<T> operation)
    {
        try { return operation(); }
        catch (DecoderFallbackException) { throw new RuleException("Choose a valid UTF-8 Markdown file."); }
        catch (EncoderFallbackException) { throw new RuleException("The Markdown contains invalid Unicode."); }
        catch (FileNotFoundException) { throw new RuleException("The linked Markdown file was not found.", 404); }
        catch (DirectoryNotFoundException) { throw new RuleException("The Markdown folder was not found.", 404); }
        catch (UnauthorizedAccessException) { throw new RuleException("Canopy does not have permission to access this Markdown file."); }
        catch (IOException) { throw new RuleException("Cannot access the Markdown file. Check its permissions and whether another program has locked it."); }
        catch (ArgumentException) { throw new RuleException("Enter a valid absolute Markdown file path."); }
    }
}
