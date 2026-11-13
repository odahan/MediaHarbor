using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace MediaHarbor;

public static class EngineSelfTest
{
    /// <summary>Exercises a real local HTTP download, audio conversion, progress and history.</summary>
    public static async Task RunAsync()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "self-test");
        Directory.CreateDirectory(folder);
        var source = Path.Combine(folder, "source.wav");
        var errors = new List<string>();
        var code = await DownloadEngine.RunAsync(DownloadEngine.StartInfo("ffmpeg", ["-nostdin", "-y", "-f", "lavfi", "-i", "sine=frequency=440:duration=3", source]), _ => { }, _ => { }, errors.Add, CancellationToken.None);
        Require(code == 0 && File.Exists(source), "Generate real source audio");
        var videoSource = Path.Combine(folder, "source.mp4");
        code = await DownloadEngine.RunAsync(DownloadEngine.StartInfo("ffmpeg", ["-nostdin", "-y", "-f", "lavfi", "-i", "testsrc=size=320x180:rate=10", "-t", "2", "-c:v", "libx264", "-pix_fmt", "yuv420p", videoSource]), _ => { }, _ => { }, errors.Add, CancellationToken.None);
        Require(code == 0 && File.Exists(videoSource), "Generate real source video");
        var bytes = await File.ReadAllBytesAsync(source);
        var videoBytes = await File.ReadAllBytesAsync(videoSource);
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var server = ServeAsync(listener, bytes, videoBytes, cancellation.Token);
        try
        {
            using var vm = new MainViewModel(true);
            Require(vm.VideoFormats.SequenceEqual(new[] { "mp4" }) && vm.AudioFormats.SequenceEqual(new[] { "mp3" }), "Requested output formats");
            foreach (var url in new[] { "https://www.youtube.com/watch?v=test", "https://youtu.be/test", "https://m.youtube.com/shorts/test", "https://music.youtube.com/watch?v=test", "https://www.youtube.com/playlist?list=test" })
                Require(YouTubeLinks.IsSupported(url), "YouTube link accepted: " + url);
            foreach (var url in new[] { "https://vimeo.com/123", "https://notyoutube.com/watch?v=test", "https://youtube.com.example.org/watch", "https://youtube.com@example.org/watch", "file:///test.mp4" })
                Require(!YouTubeLinks.IsSupported(url), "Unsupported link ignored: " + url);
            await vm.AddTextAsync("https://vimeo.com/123");
            Require(vm.Jobs.Count == 0 && vm.Notice.Contains("YouTube"), "Unsupported pasted link gives guidance without queuing");
            Require(vm.Options().Quality == 128 && vm.Options().Audio, "Default audio / bitrate");
            vm.IsVideo = true;
            Require(vm.Options().Quality == 480 && !vm.Options().Audio, "Default video / height");
            vm.Best = true;
            Require(vm.Options().Quality == 4320, "Best video 8K");
            vm.IsAudio = true;
            Require(vm.Options().Quality == 320, "Best audio 320k");
            vm.CustomQuality = "96";
            Require(vm.Options().Quality == 96 && vm.Custom, "Custom MP3 compression bitrate");
            var options = new DownloadOptions(folder, "mp3", true, 128, "Unlimited", false, false, false, false, "", "", 1);
            var arguments = DownloadEngine.Arguments("https://example.test/a?x=1&y=2", options);
            Require(arguments[^1] == "https://example.test/a?x=1&y=2" && arguments.Contains("128K"), "Literal URLs and bitrate passed through argument list");
            Require(DownloadEngine.ParsePercent("PROGRESS| 42.5%|1MiB/s|00:02") == 42.5, "Progress parsing");
            var job = new DownloadJob(vm, $"http://127.0.0.1:{port}/sample.wav", options);
            await job.StartAsync();
            Require(job.Complete && File.Exists(job.FilePath), "HTTP media download and conversion: " + job.Status);
            Require(new FileInfo(job.FilePath).Length > 1000 && job.FilePath.EndsWith(".mp3"), "Nonempty MP3 output");
            Require(job.Progress == 100 && vm.HistoryItems.Any(x => x.File == job.FilePath), "Completion and successful history record");
            var videoJob = new DownloadJob(vm, $"http://127.0.0.1:{port}/video.mp4", options with { Audio = false, Format = "mp4", Quality = 480 });
            await videoJob.StartAsync();
            Require(videoJob.Complete && new FileInfo(videoJob.FilePath).Length > 1000, "HTTP video download: " + videoJob.Status);
            var trim = Path.Combine(folder, "trimmed.mp3");
            code = await DownloadEngine.RunAsync(DownloadEngine.StartInfo("ffmpeg", ["-nostdin", "-y", "-ss", "1", "-t", "1", "-i", job.FilePath, "-vn", "-b:a", "128k", trim]), _ => { }, _ => { }, errors.Add, cancellation.Token);
            Require(code == 0 && new FileInfo(trim).Length > 1000, "Audio trim saved");
            await File.WriteAllTextAsync(Path.Combine(AppContext.BaseDirectory, "self-test-report.json"), JsonSerializer.Serialize(new { passed = true, checks = new[] { "source generation", "audio/video default quality", "best quality", "safe argument handling", "progress", "HTTP audio download", "HTTP video download", "MP3 conversion", "completion", "history", "audio trimming" }, output = job.FilePath }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally
        {
            cancellation.Cancel(); listener.Stop();
            try { await server; } catch (Exception ex) when (ex is OperationCanceledException or SocketException) { }
        }
    }

    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    private static async Task ServeAsync(TcpListener listener, byte[] audio, byte[] video, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            using var client = await listener.AcceptTcpClientAsync(token);
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true);
            var first = await reader.ReadLineAsync(token) ?? "";
            while (await reader.ReadLineAsync(token) is { Length: > 0 }) { }
            var isVideo = first.Contains("/video.mp4");
            var bytes = isVideo ? video : audio;
            var head = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: {(isVideo ? "video/mp4" : "audio/wav")}\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(head, token);
            if (!first.StartsWith("HEAD")) await stream.WriteAsync(bytes, token);
        }
    }
}
