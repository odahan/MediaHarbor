using System.Globalization;
using System.IO;
using Xunit;

namespace MediaHarbor.Tests;

public class DownloadEngineTests
{
    private static DownloadOptions BaseOptions() => new("C:\\Downloads", "mp3", true, 128, "Unlimited", false, false, false, true, "", "", 1);

    [Fact]
    public void Tool_Returns_Path_Under_Tools_Folder()
    {
        var path = DownloadEngine.Tool("yt-dlp");
        Assert.EndsWith(Path.Combine("tools", "yt-dlp.exe"), path);
    }

    [Fact]
    public void StartInfo_Configures_Process_For_Silent_Piped_Execution()
    {
        var info = DownloadEngine.StartInfo("yt-dlp", ["--version"]);
        Assert.False(info.UseShellExecute);
        Assert.True(info.CreateNoWindow);
        Assert.True(info.RedirectStandardOutput);
        Assert.True(info.RedirectStandardError);
        Assert.Single(info.ArgumentList);
        Assert.Equal("--version", info.ArgumentList[0]);
    }

    [Fact]
    public void Arguments_Always_Ends_With_Separator_And_Url()
    {
        var args = DownloadEngine.Arguments("https://youtube.com/watch?v=a?x=1&y=2", BaseOptions());
        Assert.Equal("--", args[^2]);
        Assert.Equal("https://youtube.com/watch?v=a?x=1&y=2", args[^1]);
    }

    [Fact]
    public void Arguments_Audio_Adds_Format_And_Quality()
    {
        var args = DownloadEngine.Arguments("https://youtube.com/watch?v=x", BaseOptions());
        var formatIndex = args.IndexOf("--audio-format");
        Assert.True(formatIndex >= 0);
        Assert.Equal("mp3", args[formatIndex + 1]);
        var qualityIndex = args.IndexOf("--audio-quality");
        Assert.True(qualityIndex >= 0);
        Assert.Equal("128K", args[qualityIndex + 1]);
        Assert.Contains("--extract-audio", args);
        Assert.Contains("--embed-metadata", args);
    }

    [Fact]
    public void Arguments_Audio_Embeds_Thumbnail_Only_For_Mp3_And_M4a()
    {
        var mp3 = DownloadEngine.Arguments("u", BaseOptions());
        Assert.Contains("--embed-thumbnail", mp3);
        var m4a = DownloadEngine.Arguments("u", BaseOptions() with { Format = "m4a" });
        Assert.Contains("--embed-thumbnail", m4a);
        var opus = DownloadEngine.Arguments("u", BaseOptions() with { Format = "opus", Artwork = true });
        Assert.DoesNotContain("--embed-thumbnail", opus);
        var noArtwork = DownloadEngine.Arguments("u", BaseOptions() with { Artwork = false });
        Assert.DoesNotContain("--embed-thumbnail", noArtwork);
    }

    [Fact]
    public void Arguments_Video_Uses_Height_Capped_Format()
    {
        var args = DownloadEngine.Arguments("u", BaseOptions() with { Audio = false, Format = "mp4", Quality = 1080 });
        var formatIndex = args.IndexOf("-f");
        Assert.True(formatIndex >= 0);
        Assert.Equal("bv*[height<=1080]+ba/b[height<=1080]/b", args[formatIndex + 1]);
        Assert.Contains("--merge-output-format", args);
        Assert.Contains("--remux-video", args);
        Assert.Contains("res:1080,vcodec:h264,acodec:aac", args);
    }

    [Fact]
    public void Arguments_Playlist_Switch()
    {
        Assert.Contains("--yes-playlist", DownloadEngine.Arguments("u", BaseOptions() with { Playlist = true }));
        Assert.Contains("--no-playlist", DownloadEngine.Arguments("u", BaseOptions() with { Playlist = false }));
    }

    [Fact]
    public void Arguments_Subtitles_Added_When_Enabled()
    {
        var args = DownloadEngine.Arguments("u", BaseOptions() with { Subtitles = true });
        Assert.Contains("--write-subs", args);
        Assert.Contains("--write-auto-subs", args);
        Assert.Contains("--convert-subs", args);
    }

    [Theory]
    [InlineData("250 KB/s", "250K")]
    [InlineData("500 KB/s", "500K")]
    [InlineData("1 MB/s", "1M")]
    [InlineData("2 MB/s", "2M")]
    [InlineData("4 MB/s", "4M")]
    public void Arguments_Maps_Speed_Limits(string label, string expected)
    {
        var args = DownloadEngine.Arguments("u", BaseOptions() with { Speed = label });
        var index = args.IndexOf("--limit-rate");
        Assert.True(index >= 0);
        Assert.Equal(expected, args[index + 1]);
    }

    [Fact]
    public void Arguments_Unlimited_Speed_Adds_No_Limit()
    {
        var args = DownloadEngine.Arguments("u", BaseOptions() with { Speed = "Unlimited" });
        Assert.DoesNotContain("--limit-rate", args);
    }

    [Fact]
    public void Arguments_Proxy_And_Cookies_Are_Passed_When_Set()
    {
        var args = DownloadEngine.Arguments("u", BaseOptions() with { Proxy = "http://127.0.0.1:8888", Cookies = "cookies.txt" });
        Assert.Contains("--proxy", args);
        Assert.Contains("http://127.0.0.1:8888", args);
        Assert.Contains("--cookies", args);
        Assert.Contains("cookies.txt", args);
    }

    [Fact]
    public void Arguments_Threads_Adds_Ffmpeg_Postprocessor_Args()
    {
        var args = DownloadEngine.Arguments("u", BaseOptions() with { Threads = 4 });
        var index = args.IndexOf("--postprocessor-args");
        Assert.True(index >= 0);
        Assert.Equal("ffmpeg:-threads 4", args[index + 1]);
    }

    [Fact]
    public void Arguments_Escapes_Percent_In_Output_Prefix()
    {
        var args = DownloadEngine.Arguments("u", BaseOptions() with { Prefix = "100% - " });
        var outputIndex = args.IndexOf("-o");
        Assert.True(outputIndex >= 0);
        Assert.Contains("100%% - ", args[outputIndex + 1]);
    }

    [Theory]
    [InlineData("PROGRESS| 42.5%|1.2MiB/s|00:02", 42.5)]
    [InlineData("PROGRESS|100.0%|2MiB/s|00:00", 100.0)]
    [InlineData("PROGRESS|  0.0%|NA|NA", 0.0)]
    [InlineData("\x1B[0;32mPROGRESS| 7.25%|1MiB/s|00:10\x1B[0m", 7.25)]
    public void ParsePercent_Extracts_Value(string line, double expected)
    {
        Assert.Equal(expected, DownloadEngine.ParsePercent(line));
    }

    [Theory]
    [InlineData("PROGRESS| NA%|NA|NA")]
    [InlineData("[download] 42.5% of ~10MiB")]
    [InlineData("")]
    [InlineData("TITLE|Some video")]
    public void ParsePercent_Returns_Null_For_Non_Matching_Lines(string line)
    {
        Assert.Null(DownloadEngine.ParsePercent(line));
    }
}
