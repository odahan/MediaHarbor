using System.IO;
using Xunit;

namespace MediaHarbor.Tests;

public class DownloadReliabilityTests
{
    [WpfFact]
    public async Task Playlist_Starts_Are_Spaced_But_Direct_Titles_Use_A_Free_Slot_Immediately()
    {
        var queue = new DownloadQueue(3, _ => TimeSpan.FromMilliseconds(100));
        Assert.Null(await queue.WaitAsync(playlistEntry: true));
        var second = queue.WaitAsync(playlistEntry: true);
        var direct = queue.WaitAsync();

        Assert.True(direct.IsCompletedSuccessfully);
        Assert.False(second.IsCompleted);
        Assert.Null(await second.WaitAsync(TimeSpan.FromSeconds(3)));
        queue.Release();
        queue.Release();
        queue.Release();
    }

    [WpfFact]
    public async Task Full_Queue_Paces_Refill_And_Retry_After_Applies_To_Direct_Titles()
    {
        var queue = new DownloadQueue(8, active => active >= 8 ? TimeSpan.FromMilliseconds(100) : TimeSpan.Zero);
        for (var index = 0; index < 8; index++) Assert.Null(await queue.WaitAsync());

        var playlist = queue.WaitAsync(playlistEntry: true);
        queue.Release();
        Assert.False(playlist.IsCompleted);
        var until = DateTimeOffset.UtcNow.AddMilliseconds(250);
        Assert.True(queue.DeferUntil(until));
        var direct = queue.WaitAsync();
        Assert.False(direct.IsCompleted);

        Assert.Null(await direct.WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.True(DateTimeOffset.UtcNow >= until);
        Assert.False(playlist.IsCompleted);
        queue.Release();
        Assert.Null(await playlist.WaitAsync(TimeSpan.FromSeconds(3)));
        for (var index = 0; index < 8; index++) queue.Release();
    }

    [Theory]
    [InlineData("reply: 'HTTP/1.1 429 Too Many Requests\\r\\n'", "header: Retry-After: 13", 13)]
    [InlineData("< HTTP/2 503", "< retry-after: Thu, 19 Nov 2026 12:00:21 GMT", 21)]
    [InlineData("reply: 'HTTP/1.1 200 OK\\r\\n'", "header: Retry-After: 13", null)]
    [InlineData("< HTTP/2 429", "> Retry-After: 13", null)]
    [InlineData("< HTTP/2 429", "< Retry-After: invalid", null)]
    public void Retry_After_Is_Parsed_From_Rate_Limited_Responses(string status, string header, int? seconds)
    {
        var now = new DateTimeOffset(2026, 11, 19, 12, 0, 0, TimeSpan.Zero);
        var response = new DownloadResponse();
        Assert.Null(response.ReadRetryAfter(status, now));
        Assert.Equal(seconds is { } delay ? now.AddSeconds(delay) : (DateTimeOffset?)null, response.ReadRetryAfter(header, now));
        Assert.Equal(seconds.HasValue, response.HasRetryAfter);
    }

    [Fact]
    public async Task Lowering_Limit_Waits_For_Active_Downloads_And_Preserves_Queue_Order()
    {
        var queue = new DownloadQueue(2);
        await queue.WaitAsync();
        await queue.WaitAsync();
        using var cancellation = new CancellationTokenSource();
        var cancelled = queue.WaitAsync(cancellation.Token);
        var first = queue.WaitAsync();
        queue.SetLimit(1);
        var second = queue.WaitAsync();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);

        queue.Release();
        Assert.False(first.IsCompleted);
        queue.Release();
        await first.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(second.IsCompleted);
        queue.Release();
        await second.WaitAsync(TimeSpan.FromSeconds(2));
        queue.Release();
    }

    [Theory]
    [InlineData(0, true, "original")]
    [InlineData(1, false, "original")]
    [InlineData(0, false, "new output")]
    public async Task Conversion_Publishes_Only_Successful_Uncancelled_Output(int exitCode, bool cancel, string expected)
    {
        var folder = Directory.CreateTempSubdirectory();
        try
        {
            var destination = Path.Combine(folder.FullName, "output.mp3");
            File.WriteAllText(destination, "original");
            using var cancellation = new CancellationTokenSource();
            var conversion = LocalConversion.RunAsync(destination, ["-n"], true, (info, _, _, _, token) =>
            {
                Assert.EndsWith(".mp3", info.ArgumentList[^1]);
                Assert.NotEqual(destination, info.ArgumentList[^1]);
                File.WriteAllText(info.ArgumentList[^1], "new output");
                if (cancel) cancellation.Cancel();
                return Task.FromResult(exitCode);
            }, cancellation.Token);

            if (cancel) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => conversion);
            else if (exitCode != 0) await Assert.ThrowsAsync<IOException>(() => conversion);
            else await conversion;

            Assert.Equal(expected, File.ReadAllText(destination));
            Assert.Equal(destination, Assert.Single(Directory.GetFiles(folder.FullName)));
        }
        finally { folder.Delete(recursive: true); }
    }

    [Fact]
    public void History_Migrates_Once_And_Prefers_New_Location()
    {
        var folder = Directory.CreateTempSubdirectory();
        try
        {
            var legacy = folder.CreateSubdirectory("legacy");
            var current = Path.Combine(folder.FullName, "current");
            File.WriteAllText(Path.Combine(legacy.FullName, "history.json"), """[{"Title":"Old","Url":"url","File":"file","Date":"2025-01-01"}]""");
            var storage = new LocalState(current, legacy.FullName);

            Assert.Equal("Old", Assert.Single(storage.LoadHistory()!).Title);
            Assert.True(File.Exists(Path.Combine(current, "history.json")));
            storage.SaveHistory([]);
            Assert.Empty(storage.LoadHistory()!);
        }
        finally { folder.Delete(recursive: true); }
    }
}
