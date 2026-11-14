using System.IO;
using Xunit;

namespace MediaHarbor.Tests;

public class DownloadReliabilityTests
{
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
