using Xunit;

namespace MediaHarbor.Tests;

public class MainViewModelTests
{
    private static MainViewModel CreateViewModel()
    {
        WpfTestApplication.Ensure();
        return new MainViewModel(screenshotMode: true);
    }

    [WpfFact]
    public void New_ViewModel_Has_Expected_Defaults()
    {
        using var vm = CreateViewModel();
        Assert.True(vm.IsAudio);
        Assert.False(vm.IsVideo);
        Assert.Equal("mp3", vm.AudioFormat);
        Assert.Equal("mp4", vm.VideoFormat);
        Assert.Equal("Normal", vm.QualityMode);
        Assert.Equal("128", vm.CustomQuality);
        Assert.Equal("Unlimited", vm.SpeedLimit);
        Assert.Equal("Ready", vm.Notice);
        Assert.Empty(vm.Jobs);
        Assert.Empty(vm.HistoryItems);
    }

    [WpfFact]
    public void IsVideo_Toggles_IsAudio()
    {
        using var vm = CreateViewModel();
        vm.IsVideo = true;
        Assert.False(vm.IsAudio);
        vm.IsVideo = false;
        Assert.True(vm.IsAudio);
    }

    [WpfFact]
    public void Qualities_Switch_Between_Audio_And_Video_Sets()
    {
        using var vm = CreateViewModel();
        Assert.Contains("320", vm.Qualities);
        Assert.DoesNotContain("1080", vm.Qualities);
        vm.IsVideo = true;
        Assert.Contains("1080", vm.Qualities);
        Assert.Contains("8K", vm.Qualities);
        Assert.DoesNotContain("320", vm.Qualities);
    }

    [WpfFact]
    public void Options_Reflect_Current_Audio_Video_Mode()
    {
        using var vm = CreateViewModel();
        var audio = vm.Options();
        Assert.True(audio.Audio);
        Assert.Equal("mp3", audio.Format);
        Assert.Equal(128, audio.Quality);

        vm.IsVideo = true;
        var video = vm.Options();
        Assert.False(video.Audio);
        Assert.Equal("mp4", video.Format);
        Assert.Equal(480, video.Quality);
    }

    [WpfFact]
    public void Options_Best_Maps_To_Max_Quality()
    {
        using var vm = CreateViewModel();
        vm.Best = true;
        Assert.Equal(320, vm.Options().Quality);
        vm.IsVideo = true;
        Assert.Equal(4320, vm.Options().Quality);
    }

    [WpfFact]
    public void Options_Custom_Quality_Is_Used_When_Custom_Mode()
    {
        using var vm = CreateViewModel();
        vm.CustomQuality = "96";
        Assert.True(vm.Custom);
        Assert.Equal(96, vm.Options().Quality);
    }

    [Fact]
    public void Options_Custom_Quality_Parses_8K_And_4K()
    {
        // Pure expression path, no WPF dispatcher needed for construction when screenshotMode.
        WpfTestApplication.Ensure();
        using var vm = new MainViewModel(true);
        vm.IsVideo = true;
        vm.CustomQuality = "8K";
        Assert.Equal(4320, vm.Options().Quality);
        vm.CustomQuality = "4K";
        Assert.Equal(2160, vm.Options().Quality);
    }

    [WpfFact]
    public void Changing_CustomQuality_Switches_To_Custom_Mode()
    {
        using var vm = CreateViewModel();
        vm.QualityMode = "Best";
        vm.CustomQuality = "192";
        Assert.Equal("Custom", vm.QualityMode);
    }

    [WpfFact]
    public async Task AddTextAsync_With_No_YouTube_Links_Shows_Guidance_And_Queues_Nothing()
    {
        using var vm = CreateViewModel();
        await vm.AddTextAsync("https://vimeo.com/123");
        Assert.Empty(vm.Jobs);
        Assert.Contains("YouTube", vm.Notice);
    }

    [WpfFact]
    public async Task AddTextAsync_With_Empty_Text_Shows_Guidance()
    {
        using var vm = CreateViewModel();
        await vm.AddTextAsync("   ");
        Assert.Empty(vm.Jobs);
        Assert.Contains("Paste a YouTube link", vm.Notice);
    }

    [WpfFact]
    public void Enqueue_Rejects_Unsupported_Url()
    {
        using var vm = CreateViewModel();
        vm.Enqueue("https://vimeo.com/1", vm.Options());
        Assert.Empty(vm.Jobs);
        Assert.Equal("Only YouTube links are supported.", vm.Notice);
    }

    [WpfFact]
    public void Record_Adds_Entry_At_Top_Of_History()
    {
        using var vm = CreateViewModel();
        var job = new DownloadJob(vm, "https://youtu.be/a", vm.Options());
        vm.Record(job);
        Assert.Single(vm.HistoryItems);
        Assert.Equal("https://youtu.be/a", vm.HistoryItems[0].Url);
    }

    [WpfFact]
    public void ApplyParallel_Clamps_Between_1_And_8()
    {
        using var vm = CreateViewModel();
        vm.Parallel = 99;
        vm.ApplyParallel();
        Assert.Equal(8, vm.Slots.CurrentCount);
        vm.Parallel = 0;
        vm.ApplyParallel();
        Assert.Equal(1, vm.Slots.CurrentCount);
    }
}
