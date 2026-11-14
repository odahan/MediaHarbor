using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;

namespace MediaHarbor;

internal static class ToolSetup
{
    private const string FfmpegUrl = "https://github.com/GyanD/codexffmpeg/releases/download/8.1.2/ffmpeg-8.1.2-essentials_build.zip";
    private const string YtDlpUrl = "https://github.com/yt-dlp/yt-dlp/releases/download/2026.10.26/yt-dlp.exe";

    internal static bool IsNeeded => !File.Exists(DownloadEngine.Tool("ffmpeg")) ||
        !File.Exists(DownloadEngine.Tool("yt-dlp"));

    /// <summary>Downloads missing tools directly from their versioned publisher releases.</summary>
    internal static async Task InstallAsync()
    {
        using var client = new HttpClient();
        await InstallAsync(client, "ffmpeg", FfmpegUrl,
            "db580001caa24ac104c8cb856cd113a87b0a443f7bdf47d8c12b1d740584a2ec",
            "ffmpeg-8.1.2-essentials_build/bin/ffmpeg.exe");
        await InstallAsync(client, "yt-dlp", YtDlpUrl,
            "f5389d3698ecde4ef06f4e6b1ce0294fe9ac1396228c8990b8d3416d6d177a7d");
    }

    private static async Task InstallAsync(HttpClient client, string tool, string url, string sha256, string? archiveEntry = null)
    {
        var target = DownloadEngine.Tool(tool);
        if (File.Exists(target))
            return;

        try
        {
            await AtomicFile.WriteAsync(target, async output =>
            {
                var download = archiveEntry is null ? output : output + ".zip";
                try
                {
                    using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();
                    await using (var file = File.Create(download))
                        await response.Content.CopyToAsync(file);

                    await using (var file = File.OpenRead(download))
                    {
                        var actual = Convert.ToHexString(await SHA256.HashDataAsync(file));
                        if (!actual.Equals(sha256, StringComparison.OrdinalIgnoreCase))
                            throw new IOException($"The SHA-256 check failed for {tool}.");
                    }

                    if (archiveEntry is not null)
                    {
                        using var archive = ZipFile.OpenRead(download);
                        var entry = archive.GetEntry(archiveEntry) ??
                            throw new IOException($"The {tool} archive did not contain the expected executable.");
                        entry.ExtractToFile(output);
                    }
                }
                finally
                {
                    if (archiveEntry is not null)
                        File.Delete(download);
                }
            }, overwrite: false);
        }
        catch (IOException) when (File.Exists(target))
        {
            // Another instance completed setup while this download was in progress.
        }
    }
}
