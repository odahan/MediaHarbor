using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MediaHarbor;

public sealed record DownloadOptions(string Folder, string Format, bool Audio, int Quality, string Speed, bool Playlist, bool Subtitles, bool Numbering, bool Artwork, string Proxy, string Cookies, int Parallel, string Prefix = "", int Threads = 0);

internal delegate Task<int> ProcessRunner(ProcessStartInfo info, Action<Process> started, Action<string> output, Action<string> error, CancellationToken token);

public static class DownloadEngine
{
    public static string Tool(string name) => Path.Combine(DistributionFiles.ApplicationDirectory, "tools", name + ".exe");
    internal static string OutputTemplate(DownloadOptions options) =>
        options.Prefix.Replace("%", "%%") + "%(title).150B [%(id)s].%(ext)s";

    /// <summary>Uses argument lists so media titles and URLs never become shell commands.</summary>
    public static ProcessStartInfo StartInfo(string tool, IEnumerable<string> arguments)
    {
        var info = new ProcessStartInfo(Tool(tool))
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        return info;
    }

    public static List<string> Arguments(string url, DownloadOptions options)
    {
        var args = new List<string>
        {
            "--ignore-config", "--no-warnings", "--newline", "--encoding", "utf-8",
            "--continue", "--windows-filenames", "--no-mtime", "--ffmpeg-location", Tool("ffmpeg"),
            "--progress", "--progress-delta", "0.25", "--no-color",
            "--progress-template", "download:PROGRESS|%(progress._percent_str)s|%(progress._speed_str)s|%(progress._eta_str)s|%(info.vcodec)s|%(info.acodec)s",
            "--print", "before_dl:TITLE|%(title)s", "--print", "post_process:POSTPROCESS|%(id)s",
            "--print", "after_move:FILE|%(filepath)s",
            "-P", options.Folder,
            "-o", OutputTemplate(options),
            options.Playlist ? "--yes-playlist" : "--no-playlist"
        };
        if (options.Audio)
        {
            args.AddRange(["-f", "bestaudio/best", "--extract-audio", "--audio-format", options.Format, "--audio-quality", options.Quality.ToString(CultureInfo.InvariantCulture) + "K", "--embed-metadata"]);
            if (options.Artwork && options.Format is "mp3" or "m4a") args.Add("--embed-thumbnail");
        }
        else
        {
            var height = options.Quality;
            args.AddRange(["-f", $"bv*[height<={height}]+ba/b[height<={height}]", "-S", $"res:{height},vcodec:h264,acodec:aac", "--merge-output-format", options.Format, "--remux-video", options.Format]);
        }
        if (options.Threads > 0) args.AddRange(["--postprocessor-args", "ffmpeg:-threads " + options.Threads.ToString(CultureInfo.InvariantCulture)]);
        if (options.Subtitles) args.AddRange(["--write-subs", "--write-auto-subs", "--sub-langs", "en.*,fr.*", "--sub-format", "srt/best", "--convert-subs", "srt"]);
        var speed = options.Speed switch { "250 KB/s" => "250K", "500 KB/s" => "500K", "1 MB/s" => "1M", "2 MB/s" => "2M", "4 MB/s" => "4M", _ => "" };
        if (speed.Length > 0) args.AddRange(["--limit-rate", speed]);
        if (!string.IsNullOrWhiteSpace(options.Proxy)) args.AddRange(["--proxy", options.Proxy]);
        if (!string.IsNullOrWhiteSpace(options.Cookies)) args.AddRange(["--cookies", options.Cookies]);
        args.AddRange(["--", url]);
        return args;
    }

    /// <summary>Starts the process and drains both redirected streams off the calling thread.</summary>
    public static Task<int> RunAsync(ProcessStartInfo info, Action<Process> started, Action<string> output, Action<string> error, CancellationToken token) =>
        Task.Run(() => RunProcessAsync(info, started, output, error, token), token);

    private static async Task<int> RunProcessAsync(ProcessStartInfo info, Action<Process> started, Action<string> output, Action<string> error, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using var process = new Process { StartInfo = info };
        process.Start();
        started(process);
        using var registration = token.Register(() => { try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { } });
        var stdout = Pump(process.StandardOutput, output);
        var stderr = Pump(process.StandardError, error);
        await process.WaitForExitAsync(CancellationToken.None);
        await Task.WhenAll(stdout, stderr);
        token.ThrowIfCancellationRequested();
        return process.ExitCode;
    }

    private static async Task Pump(StreamReader stream, Action<string> receive)
    {
        while (await stream.ReadLineAsync() is { } line) receive(line);
    }

    internal static async Task<IReadOnlyList<SearchResult>> ListAsync(string query, bool playlist, DownloadOptions options, ProcessRunner run, CancellationToken token)
    {
        var items = new List<SearchResult>();
        var errors = new List<string>();
        var args = new List<string> { "--ignore-config", "--flat-playlist", "--dump-json", "--skip-download", "--no-warnings", playlist ? "--yes-playlist" : "--no-playlist" };
        if (!string.IsNullOrWhiteSpace(options.Proxy)) args.AddRange(["--proxy", options.Proxy]);
        if (!string.IsNullOrWhiteSpace(options.Cookies)) args.AddRange(["--cookies", options.Cookies]);
        args.AddRange(["--", playlist || query.StartsWith("ytsearch1:", StringComparison.Ordinal) ? query : "ytsearch10:" + query]);
        var code = await run(StartInfo("yt-dlp", args), _ => { }, line =>
        {
            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                var title = ReadString(root, "title") ?? "Untitled";
                var url = ReadString(root, "webpage_url") ?? ReadString(root, "url");
                if (url != null && !Uri.TryCreate(url, UriKind.Absolute, out _)) url = "https://www.youtube.com/watch?v=" + url;
                var playlistTitle = ReadString(root, "playlist_title") ?? ReadString(root, "playlist");
                if (url != null && YouTubeLinks.IsSupported(url))
                    items.Add(new SearchResult { Title = title, Url = url, PlaylistTitle = playlistTitle ?? "" });
            }
            catch (JsonException) { }
        }, errors.Add, token);
        if (code != 0 && items.Count == 0) throw new InvalidOperationException(string.Join("\n", errors.TakeLast(3).DefaultIfEmpty($"yt-dlp exited with code {code}.")));
        return items;
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    public static double? ParsePercent(string line)
    {
        var match = Regex.Match(Regex.Replace(line, @"\x1B\[[0-9;]*m", ""), @"PROGRESS\|\s*([\d.]+)%");
        return match.Success && double.TryParse(match.Groups[1].Value, CultureInfo.InvariantCulture, out var value) ? value : null;
    }
}

public sealed class SearchResult
{
    public bool Selected { get; set; } = true;
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
    public string PlaylistTitle { get; set; } = "";
}
