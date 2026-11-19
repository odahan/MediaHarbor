using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace MediaHarbor;

public partial class DownloadJob : ObservableObject
{
    private readonly MainViewModel owner;
    private readonly bool playlistEntry;
    private CancellationTokenSource? cancellation;
    private bool paused;
    private bool complete;
    private bool failed;
    private bool postProcessing;
    private string lastError = "";
    public DownloadOptions Options { get; }
    public string Url { get; }
    public string FilePath { get; private set; } = "";
    public bool Running { get; private set; }
    public bool Complete { get => complete; private set => SetProperty(ref complete, value); }
    public bool Failed { get => failed; private set => SetProperty(ref failed, value); }
    public bool Removed { get; private set; }
    public string Symbol => Options.Audio ? "♫" : "▶";
    [ObservableProperty] private string title;
    [ObservableProperty] private string detail;
    [ObservableProperty] private string status = "Queued";
    [ObservableProperty] private string actionLabel = "Pause";
    [ObservableProperty] private double progress;
    private string? resolvedUrl;

    public DownloadJob(MainViewModel owner, string url, DownloadOptions options, bool playlistEntry = false)
    {
        this.owner = owner;
        this.playlistEntry = playlistEntry;
        Url = url;
        Options = options;
        title = Path.GetFileName(Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.AbsolutePath : url);
        if (string.IsNullOrEmpty(title)) title = url;
        detail = $"{options.Format.ToUpperInvariant()} · {options.Quality}{(options.Audio ? " kbit/s" : "p")} · {options.Folder}";
    }

    public async Task StartAsync()
    {
        if (Running || Complete || Removed || paused) return;
        using var attempt = new CancellationTokenSource();
        cancellation = attempt;
        var token = attempt.Token;
        Running = true;
        Failed = false;
        postProcessing = false;
        lastError = "";
        FilePath = "";
        Progress = 0;
        ActionLabel = "Pause";
        DownloadDestination? reservation = null;
        try
        {
            Status = "Waiting for a download slot…";
            var slots = owner.Slots;
            if (await slots.WaitAsync(token, playlistEntry) is { } stopped)
            {
                PausePending(stopped, token);
                return;
            }
            try
            {
                Status = "Reading media information…";
                token.ThrowIfCancellationRequested();
                if (slots.PauseReason is { } pausedQueue)
                {
                    PausePending(pausedQueue, token);
                    return;
                }
                var url = resolvedUrl ?? Url;
                var options = Options with { Cookies = owner.Cookies };
                if (url.StartsWith("ytsearch1:", StringComparison.Ordinal))
                {
                    IReadOnlyList<SearchResult> matches;
                    try { matches = await DownloadEngine.ListAsync(url, false, options, owner.RunProcess, token); }
                    catch (InvalidOperationException ex)
                    {
                        owner.HandleYouTubeError(ex.Message);
                        throw;
                    }
                    url = matches.FirstOrDefault()?.Url ?? throw new InvalidOperationException("No matching YouTube video was found.");
                    resolvedUrl = url;
                }
                var folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Options.Folder));
                var destination = new DownloadDestination(folder, YouTubeLinks.VideoId(url) ?? url);
                if (!owner.Reserve(destination))
                    throw new IOException("This media is already being downloaded to this folder. Retry after that download finishes.");
                reservation = destination;

                Directory.CreateDirectory(folder);
                var info = DownloadEngine.StartInfo("yt-dlp", DownloadEngine.Arguments(url, options with { Folder = folder }));
                var code = await RunDownloadAsync(info, token);
                token.ThrowIfCancellationRequested();
                if (code != 0) throw new InvalidOperationException(DownloadError(code));
                if (string.IsNullOrEmpty(FilePath) || !File.Exists(FilePath)) throw new IOException("The downloader did not report a saved media file.");
                Complete = true;
                Progress = 100;
                Status = "Completed";
                ActionLabel = "Play";
                owner.Record(this);
            }
            finally
            {
                if (reservation is { } destination) owner.Release(destination);
                slots.Release();
            }
        }
        catch (OperationCanceledException) { if (!Removed) { Status = "Paused · partial download retained"; ActionLabel = "Resume"; } }
        catch (Exception ex) { Status = ex.Message; ActionLabel = "Retry"; Failed = true; }
        finally
        {
            Running = false;
            cancellation = null;
        }
    }

    private void PausePending(string reason, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        paused = true;
        Status = reason;
        ActionLabel = "Resume";
    }

    /// <summary>Marshals job updates to WPF and applies them before evaluating the process result.</summary>
    private async Task<int> RunDownloadAsync(ProcessStartInfo info, CancellationToken token)
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        var outputResponse = new DownloadResponse();
        var errorResponse = new DownloadResponse();
        var outputUpdate = Task.CompletedTask;
        var errorUpdate = Task.CompletedTask;
        int code;
        try
        {
            code = await owner.RunProcess(info, _ => { }, line =>
            {
                outputUpdate = ReadOutput(line, outputResponse) ?? outputUpdate;
            }, line =>
            {
                if (line.StartsWith("ERROR:", StringComparison.Ordinal))
                {
                    lastError = line;
                    if (DownloadResponse.RequiresSignIn(line))
                        errorUpdate = Dispatch(() => owner.HandleYouTubeError(line));
                }
                else errorUpdate = ReadOutput(line, errorResponse) ?? errorUpdate;
            }, token);
        }
        finally
        {
            await Task.WhenAll(outputUpdate, errorUpdate);
        }
        if (code != 0 && !outputResponse.HasRetryAfter && !errorResponse.HasRetryAfter && DownloadResponse.IsRateLimited(lastError))
            owner.HandleYouTubeError(lastError);
        return code;

        Task? ReadOutput(string line, DownloadResponse response)
        {
            if (IsJobMessage(line)) return Dispatch(() => Receive(line));
            return response.ReadRetryAfter(line, DateTimeOffset.UtcNow) is { } retryAfter
                ? Dispatch(() => owner.DeferYouTubeDownloads(retryAfter))
                : null;
        }

        Task Dispatch(Action update) => dispatcher.InvokeAsync(update, DispatcherPriority.Normal, token).Task;
    }

    private string DownloadError(int code)
    {
        if (!Options.Audio && lastError.Contains("Requested format is not available", StringComparison.OrdinalIgnoreCase))
            return $"No video format matches the {Options.Quality}p limit. Try a higher resolution if available.";
        return string.IsNullOrEmpty(lastError) ? $"Download failed (exit {code})." : lastError;
    }

    private static bool IsJobMessage(string line) => line.StartsWith("POSTPROCESS|", StringComparison.Ordinal) ||
        line.StartsWith("TITLE|", StringComparison.Ordinal) || line.StartsWith("FILE|", StringComparison.Ordinal) ||
        line.StartsWith("PROGRESS|", StringComparison.Ordinal);

    private void Receive(string line)
    {
        if (line.StartsWith("TITLE|", StringComparison.Ordinal)) Title = line[6..];
        else if (line.StartsWith("FILE|", StringComparison.Ordinal))
        {
            FilePath = line[5..];
            Status = "Finalizing download…";
        }
        else if (line.StartsWith("PROGRESS|", StringComparison.Ordinal))
        {
            if (postProcessing || FilePath.Length > 0) return;
            var parts = line.Split('|');
            var percent = DownloadEngine.ParsePercent(line);
            if (percent is { } value) Progress = value;
            var phase = parts.Length < 6 ? "media" : parts[4] == "none" ? "audio" : parts[5] == "none" ? "video" : "media";
            Status = "Downloading " + phase + " · " + string.Join(" · ", parts.Skip(1).Take(3).Select(part => part.Trim()));
        }
        else if (line.StartsWith("POSTPROCESS|", StringComparison.Ordinal))
        {
            if (FilePath.Length > 0) return;
            postProcessing = true;
            Status = "Assembling or converting media…";
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
        owner.Slots.Resume();
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
    [RelayCommand] private Task SubtitlesAsync() => owner.DownloadSubtitlesAsync(resolvedUrl ?? Url, Options);
    [RelayCommand] private void Remove() { Removed = true; cancellation?.Cancel(); owner.Jobs.Remove(this); }
    public void Stop() { paused = true; cancellation?.Cancel(); }
    public static void Launch(string path) { try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); } catch (Exception ex) { MessageBox.Show(ex.Message, "Unable to open"); } }
}
