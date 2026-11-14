using System.IO;

namespace MediaHarbor;

internal static class LocalConversion
{
    /// <summary>Publishes a conversion only after FFmpeg exits successfully.</summary>
    internal static Task RunAsync(string destination, IReadOnlyList<string> arguments, bool overwrite, ProcessRunner run, CancellationToken token) =>
        AtomicFile.WriteAsync(destination, async temporary =>
        {
            var errors = new Queue<string>();
            var code = await run(DownloadEngine.StartInfo("ffmpeg", [.. arguments, temporary]), _ => { }, _ => { }, line =>
            {
                if (errors.Count == 3) errors.Dequeue();
                errors.Enqueue(line);
            }, token);
            if (code != 0)
                throw new IOException(string.Join(Environment.NewLine, errors.DefaultIfEmpty($"FFmpeg exited with code {code}.")));
            if (!File.Exists(temporary) || new FileInfo(temporary).Length == 0)
                throw new IOException("FFmpeg did not produce a media file.");
        }, token, overwrite);
}
