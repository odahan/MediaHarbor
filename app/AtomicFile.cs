using System.IO;

namespace MediaHarbor;

internal static class AtomicFile
{
    /// <summary>Publishes a complete file from the same directory as its destination.</summary>
    internal static async Task WriteAsync(string destination, Func<string, Task> write, CancellationToken token = default, bool overwrite = true)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(destination))!;
        Directory.CreateDirectory(folder);
        var temporary = Path.Combine(folder, $".mediaharbor-{Guid.NewGuid():N}{Path.GetExtension(destination)}");
        try
        {
            await write(temporary);
            token.ThrowIfCancellationRequested();
            File.Move(temporary, destination, overwrite);
        }
        finally
        {
            TryDelete(temporary);
        }
    }

    internal static void WriteText(string destination, string contents) =>
        WriteAsync(destination, path => { File.WriteAllText(path, contents); return Task.CompletedTask; }).GetAwaiter().GetResult();

    private static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
