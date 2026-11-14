using Xunit;
using System.IO;

namespace MediaHarbor.Tests;

public class DownloadJobTests
{
    private static (MainViewModel vm, DownloadJob job) CreateJob(string url = "https://www.youtube.com/watch?v=abc123", DownloadOptions? options = null)
    {
        WpfTestApplication.Ensure();
        var vm = new MainViewModel(screenshotMode: true);
        var job = new DownloadJob(vm, url, options ?? new DownloadOptions("C:\\Downloads", "mp3", true, 128, "Unlimited", false, false, false, false, "", "", 1));
        return (vm, job);
    }

    [WpfFact]
    public void Constructor_Initializes_State_From_Options()
    {
        var (_, job) = CreateJob();
        Assert.False(job.Running);
        Assert.False(job.Complete);
        Assert.False(job.Removed);
        Assert.Equal("Queued", job.Status);
        Assert.Equal("Pause", job.ActionLabel);
        Assert.Equal(0, job.Progress);
        Assert.Equal("♫", job.Symbol);
        Assert.Equal("MP3 · 128 kbit/s · C:\\Downloads", job.Detail);
    }

    [WpfFact]
    public void Constructor_Uses_Video_Symbol_And_Height_Detail_For_Video()
    {
        var (_, job) = CreateJob(options: new DownloadOptions("C:\\Videos", "mp4", false, 1080, "Unlimited", false, false, false, false, "", "", 1));
        Assert.Equal("▶", job.Symbol);
        Assert.Equal("MP4 · 1080p · C:\\Videos", job.Detail);
    }

    [WpfFact]
    public void Constructor_Extracts_Title_From_Url_Path()
    {
        var (_, job) = CreateJob("https://www.youtube.com/watch?v=dQw4w9WgXcQ");
        Assert.Equal("watch", job.Title);
    }

    [WpfFact]
    public void Constructor_Falls_Back_To_Url_When_Path_Empty()
    {
        var (_, job) = CreateJob("ytsearch1:mysearch");
        Assert.Equal("mysearch", job.Title);
    }

    [WpfFact]
    public async Task StartAsync_Is_NoOp_When_Already_Complete()
    {
        var folder = Directory.CreateTempSubdirectory();
        try
        {
            var runs = 0;
            using var vm = new MainViewModel(true, (_, _, output, _, _) =>
            {
                runs++;
                var file = Path.Combine(folder.FullName, "Sample [abc123].mp3");
                File.WriteAllText(file, "media");
                output("PROGRESS|84.0%|1MiB/s|00:01|none|opus");
                output("POSTPROCESS");
                output("FILE|" + file);
                return Task.FromResult(0);
            });
            var job = new DownloadJob(vm, "https://youtu.be/abc123", vm.Options() with { Folder = folder.FullName });
            await job.StartAsync();
            Assert.True(job.Complete);
            Assert.Equal(100, job.Progress);
            Assert.False(job.ProgressIndeterminate);
            Assert.Single(vm.HistoryItems);

            await job.StartAsync();

            Assert.Equal(1, runs);
            Assert.False(job.Running);
        }
        finally { folder.Delete(recursive: true); }
    }

    [WpfFact]
    public async Task Alternate_Links_To_The_Same_Video_Cannot_Run_In_The_Same_Folder()
    {
        var folder = Directory.CreateTempSubdirectory();
        using var finish = new CancellationTokenSource();
        try
        {
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var runs = 0;
            using var vm = new MainViewModel(true, async (_, _, _, _, token) =>
            {
                runs++;
                started.TrySetResult();
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, finish.Token);
                await Task.Delay(Timeout.Infinite, linked.Token);
                return 0;
            });
            vm.Slots.SetLimit(2);
            var options = vm.Options() with { Folder = folder.FullName };
            var first = new DownloadJob(vm, "https://youtu.be/abc123", options);
            var duplicate = new DownloadJob(vm, "https://www.youtube.com/watch?v=abc123", options);
            var firstRun = first.StartAsync();
            try
            {
                await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
                await duplicate.StartAsync();
                Assert.Equal(1, runs);
                Assert.Equal("Retry", duplicate.ActionLabel);
                Assert.Contains("already being downloaded", duplicate.Status);
            }
            finally
            {
                finish.Cancel();
                await firstRun;
            }
        }
        finally { folder.Delete(recursive: true); }
    }

    [WpfFact]
    public void Resume_Is_NoOp_When_Removed()
    {
        var (_, job) = CreateJob();
        job.Stop();
        job.RemoveCommand.Execute(null);
        job.Resume();
        Assert.True(job.Removed);
        Assert.False(job.Running);
    }

    [WpfFact]
    public async Task Toggle_When_Not_Running_Starts_Job()
    {
        var (vm, job) = CreateJob();
        vm.Slots.SetLimit(1);
        await vm.Slots.WaitAsync();
        try
        {
            job.ToggleCommand.Execute(null);
            Assert.True(job.Running);
            job.Stop();
        }
        finally { vm.Slots.Release(); }
    }

    [WpfFact]
    public async Task Toggle_When_Running_Pauses()
    {
        var (vm, job) = CreateJob();
        vm.Parallel = 1;
        vm.ApplyParallel();
        // Occupy the only slot so the job stays in "Waiting" state.
        await vm.Slots.WaitAsync();
        try
        {
            job.ToggleCommand.Execute(null);
            Assert.True(job.Running);
            await Task.Delay(100);
            Assert.Equal("Waiting for a download slot…", job.Status);
            job.ToggleCommand.Execute(null);
            await Task.Delay(200);
            Assert.Equal("Resume", job.ActionLabel);
            Assert.Contains("Paused", job.Status);
        }
        finally
        {
            vm.Slots.Release();
            job.Stop();
        }
    }

    [WpfFact]
    public void Remove_Marks_Job_Removed_And_Removes_From_Owner()
    {
        var (vm, job) = CreateJob();
        vm.Jobs.Add(job);
        job.RemoveCommand.Execute(null);
        Assert.True(job.Removed);
        Assert.DoesNotContain(job, vm.Jobs);
    }

    [WpfFact]
    public void Stop_Cancels_And_Marks_Paused()
    {
        var (_, job) = CreateJob();
        job.Stop();
        Assert.False(job.Running);
    }
}
