using Xunit;

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
        var (_, job) = CreateJob();
        await job.StartAsync();
        // Second call returns immediately without changing state.
        await job.StartAsync();
        Assert.False(job.Running);
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
    public void Toggle_When_Not_Running_Starts_Job()
    {
        var (_, job) = CreateJob();
        job.ToggleCommand.Execute(null);
        Assert.True(job.Running);
        job.Stop();
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
