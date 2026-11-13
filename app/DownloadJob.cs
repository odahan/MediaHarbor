using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace MediaHarbor;

public partial class DownloadJob : ObservableObject
{
    private readonly MainViewModel owner;
    private CancellationTokenSource? cancellation;
    private bool paused;
    private string lastError = "";
    private int runVersion;
    public DownloadOptions Options { get; }
    public string Url { get; }
    public string FilePath { get; private set; } = "";
    public bool Running { get; private set; }
    public bool Complete { get; private set; }
    public bool Removed { get; private set; }
    public string Symbol => Options.Audio ? "♫" : "▶";
    [ObservableProperty] private string title;
    [ObservableProperty] private string detail;
    [ObservableProperty] private string status = "Queued";
    [ObservableProperty] private string actionLabel = "Pause";
    [ObservableProperty] private double progress;

    public DownloadJob(MainViewModel owner, string url, DownloadOptions options)
    {
        this.owner = owner;
        Url = url;
        Options = options;
        title = Path.GetFileName(Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.AbsolutePath : url);
        if (string.IsNullOrEmpty(title)) title = url;
        detail = $"{options.Format.ToUpperInvariant()} · {options.Quality}{(options.Audio ? " kbit/s" : "p")} · {options.Folder}";
    }

    public async Task StartAsync()
    {
        if (Running || Complete || Removed || paused) return;
        var version = ++runVersion;
        cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        Running = true;
        lastError = "";
        ActionLabel = "Pause";
        try
        {
            Status = "Waiting for a download slot…";
            var slots = owner.Slots;
            await slots.WaitAsync(token);
            try
            {
                Directory.CreateDirectory(Options.Folder);
                Status = "Reading media information…";
                var code = await DownloadEngine.RunAsync(DownloadEngine.StartInfo("yt-dlp", DownloadEngine.Arguments(Url, Options)), _ => { }, line => Ui(() => Receive(line)),
                    line => Ui(() => { if (DownloadEngine.ParsePercent(line) is null) lastError = line; else Receive(line); }), token);
                if (code != 0) throw new InvalidOperationException(string.IsNullOrEmpty(lastError) ? $"Download failed (exit {code})." : lastError);
                if (string.IsNullOrEmpty(FilePath) || !File.Exists(FilePath)) throw new IOException("The downloader did not report a saved media file.");
                Complete = true;
                Progress = 100;
                Status = "Completed";
                ActionLabel = "Play";
                owner.Record(this);
            }
            finally { slots.Release(); }
        }
        catch (OperationCanceledException) { if (!Removed) { Status = "Paused · partial download retained"; ActionLabel = "Resume"; } }
        catch (Exception ex) { Status = ex.Message; ActionLabel = "Retry"; }
        finally
        {
            if (version == runVersion) Running = false;
        }
    }

    private static void Ui(Action action) => Application.Current.Dispatcher.Invoke(action);
    private void Receive(string line)
    {
        if (line.StartsWith("TITLE|")) Title = line[6..];
        else if (line.StartsWith("FILE|")) FilePath = line[5..];
        else if (DownloadEngine.ParsePercent(line) is { } value)
        {
            Progress = value;
            Status = "Downloading · " + System.Text.RegularExpressions.Regex.Replace(line, @"\x1B\[[0-9;]*m", "").Replace("PROGRESS|", "").Trim().Replace("|", " · ");
        }
        else if (line.Contains("[ExtractAudio]") || line.Contains("[VideoConvertor]") || line.Contains("[Merger]") || line.Contains("[VideoRemuxer]"))
        {
            if (Status != "Converting media…") Progress = 0;
            Status = "Converting media…";
        }
    }

    [RelayCommand]
    private void Toggle()
    {
        if (Complete) { Open(); return; }
        if (Running)
        {
            paused = true;
            cancellation?.Cancel();
            return;
        }
        paused = false;
        _ = StartAsync();
    }

    public void Resume()
    {
        if (Running || Complete || Removed) return;
        paused = false;
        _ = StartAsync();
    }

    [RelayCommand] private void Open() { if (File.Exists(FilePath)) Launch(FilePath); }
    [RelayCommand] private void Reveal() { if (File.Exists(FilePath)) Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{FilePath}\"") { UseShellExecute = true }); else Launch(Options.Folder); }
    [RelayCommand] private void Copy() => Clipboard.SetText(Url);
    [RelayCommand] private void Subtitles() => owner.DownloadSubtitles(Url, Options.Folder);
    [RelayCommand] private void Remove() { Removed = true; cancellation?.Cancel(); owner.Jobs.Remove(this); }
    public void Stop() { paused = true; cancellation?.Cancel(); }
    public static void Launch(string path) { try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); } catch (Exception ex) { MessageBox.Show(ex.Message, "Unable to open"); } }
}
