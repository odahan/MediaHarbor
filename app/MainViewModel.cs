using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;

namespace MediaHarbor;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly bool screenshotMode;
    private readonly string stateFile = Path.Combine(AppContext.BaseDirectory, "data", "settings.json");
    private readonly string historyFile = Path.Combine(AppContext.BaseDirectory, "data", "history.json");
    private readonly DispatcherTimer clipboardTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private string previousClipboard = "";
    private string previousBrowser = "";
    private bool browserProbeRunning;
    private bool loading = true;
    public event Action? SearchRequested;
    public ObservableCollection<DownloadJob> Jobs { get; } = [];
    public List<HistoryEntry> HistoryItems { get; private set; } = [];
    public SemaphoreSlim Slots { get; private set; } = new(Math.Min(8, Environment.ProcessorCount * 2));
    public string[] VideoFormats { get; } = ["mp4"];
    public string[] AudioFormats { get; } = ["mp3"];
    public string[] SearchModes { get; } = ["Most relevant", "View results"];
    public string[] Qualities => IsAudio ? ["320", "256", "192", "128", "96", "48", "36", "24"] : ["8K", "4K", "1440", "1080", "720", "480", "360", "240", "144"];
    [ObservableProperty] private string folder = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
    [ObservableProperty] private string videoFormat = "mp4";
    [ObservableProperty] private string audioFormat = "mp3";
    [ObservableProperty] private bool isAudio = true;
    public bool IsVideo { get => !IsAudio; set => IsAudio = !value; }
    [ObservableProperty] private string qualityMode = "Normal";
    public bool Best { get => QualityMode == "Best"; set { if (value) QualityMode = "Best"; } }
    public bool Normal { get => QualityMode == "Normal"; set { if (value) QualityMode = "Normal"; } }
    public bool Custom { get => QualityMode == "Custom"; set { if (value) QualityMode = "Custom"; } }
    [ObservableProperty] private string customQuality = "128";
    [ObservableProperty] private string speedLimit = "Unlimited";
    [ObservableProperty] private string query = "";
    [ObservableProperty] private string searchMode = "Most relevant";
    [ObservableProperty] private bool autoDetect;
    [ObservableProperty] private string notice = "Ready";
    public bool Numbering { get; set; }
    public bool Artwork { get; set; } = true;
    public bool IncludeSubtitles { get; set; }
    public bool BrowserDetect { get; set; }
    public int Parallel { get; set; } = Math.Min(8, Environment.ProcessorCount * 2);
    public int FfmpegThreads { get; set; } = Math.Max(1, Environment.ProcessorCount / 2);
    public string Proxy { get; set; } = "";
    public string Cookies { get; set; } = "";

    public MainViewModel(bool screenshotMode)
    {
        this.screenshotMode = screenshotMode;
        if (!screenshotMode) Load();
        loading = false;
        clipboardTimer.Tick += (_, _) => CheckClipboard();
        if (!screenshotMode) clipboardTimer.Start();
    }

    partial void OnIsAudioChanged(bool value)
    {
        var mode = QualityMode;
        OnPropertyChanged(nameof(IsVideo));
        OnPropertyChanged(nameof(Qualities));
        CustomQuality = value ? "128" : "480";
        QualityMode = mode;
    }
    partial void OnQualityModeChanged(string value)
    {
        OnPropertyChanged(nameof(Best)); OnPropertyChanged(nameof(Normal)); OnPropertyChanged(nameof(Custom));
    }
    partial void OnCustomQualityChanged(string value) { if (!loading) QualityMode = "Custom"; }

    public DownloadOptions Options(bool playlist = false) => new(Folder, IsAudio ? AudioFormat : VideoFormat, IsAudio,
        Best ? (IsAudio ? 320 : 4320) : Normal ? (IsAudio ? 128 : 480) : CustomQuality switch { "8K" => 4320, "4K" => 2160, _ => int.TryParse(CustomQuality, out var q) ? q : IsAudio ? 128 : 480 },
        SpeedLimit, playlist, IncludeSubtitles, Numbering, Artwork, Proxy, Cookies, Parallel, Threads: FfmpegThreads);

    [RelayCommand]
    private void ChooseFolder()
    {
        var dialog = new OpenFolderDialog { Title = "Choose download folder", InitialDirectory = Directory.Exists(Folder) ? Folder : "" };
        if (dialog.ShowDialog() == true) { Folder = dialog.FolderName; Save(); }
    }

    [RelayCommand]
    private async Task PasteAsync()
    {
        string text;
        try { text = Clipboard.ContainsText() ? Clipboard.GetText() : ""; }
        catch { text = ""; }
        if (string.IsNullOrWhiteSpace(text)) text = Dialogs.Prompt("YouTube downloads", "Paste one or more YouTube links:", "") ?? "";
        await AddTextAsync(text);
    }

    public async Task AddTextAsync(string text)
    {
        var links = text.Split(['\r', '\n', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries).Where(IsUrl).Distinct().ToArray();
        if (links.Length == 0) { Notice = "Paste a YouTube link (youtube.com or youtu.be). Other websites are not supported."; return; }
        foreach (var url in links)
        {
            if (url.Contains("list=", StringComparison.OrdinalIgnoreCase) || url.Contains("/playlist", StringComparison.OrdinalIgnoreCase))
            {
                var answer = MessageBox.Show("This link may contain a playlist. Select entries before downloading?", "Playlist", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (answer == MessageBoxResult.Cancel) continue;
                if (answer == MessageBoxResult.Yes) { await SelectMediaAsync(url, true); continue; }
            }
            Enqueue(url, Options());
        }
        Save();
    }

    public void Enqueue(string url, DownloadOptions options)
    {
        if (!IsUrl(url) && !url.StartsWith("ytsearch1:", StringComparison.Ordinal))
        {
            Notice = "Only YouTube links are supported.";
            return;
        }
        var job = new DownloadJob(this, url, options);
        Jobs.Add(job);
        Notice = $"{Jobs.Count} item(s) in the list";
        _ = job.StartAsync();
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(Query)) { SearchRequested?.Invoke(); Notice = "Enter a YouTube link or video name."; return; }
        if (Query.Split(['\r', '\n', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries).Any(text => Uri.TryCreate(text, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")) await AddTextAsync(Query);
        else if (SearchMode == "View results") await SelectMediaAsync(Query, false);
        else Enqueue("ytsearch1:" + Query, Options());
    }

    private async Task SelectMediaAsync(string query, bool playlist)
    {
        using var cancellation = new CancellationTokenSource();
        var busy = Dialogs.Busy(playlist ? "Reading playlist…" : "Searching videos…", cancellation);
        busy.Show();
        try
        {
            var results = await DownloadEngine.ListAsync(query, playlist, cancellation.Token);
            busy.Close();
            var selected = Dialogs.SelectResults(results, playlist ? "Choose playlist entries" : "Search results");
            var playlistFolder = playlist ? Path.Combine(Folder, PlaylistFolderName(results)) : Folder;
            foreach (var entry in selected)
                Enqueue(entry.Url, Options() with { Folder = playlistFolder, Prefix = playlist && Numbering ? $"{results.ToList().IndexOf(entry) + 1:000} - " : "" });
            Notice = $"Added {selected.Count} item(s)";
        }
        catch (OperationCanceledException) { Notice = "Search cancelled"; }
        catch (Exception ex) { Notice = ex.Message; MessageBox.Show(ex.Message, "Unable to read media"); }
        finally { busy.Close(); }
    }

    private static string PlaylistFolderName(IReadOnlyList<SearchResult> results)
    {
        var name = results.Select(r => r.PlaylistTitle).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t)) ?? "";
        var invalid = Path.GetInvalidFileNameChars();
        name = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim().TrimEnd('.', ' ');
        if (name.Length > 120) name = name[..120].TrimEnd('.', ' ');
        return name.Length > 0 ? name : "Playlist " + DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss");
    }

    private static bool IsUrl(string text) => YouTubeLinks.IsSupported(text);

    private void CheckClipboard()
    {
        if (!AutoDetect) return;
        if (BrowserDetect && !browserProbeRunning) _ = CheckBrowserAsync();
        try
        {
            var text = Clipboard.ContainsText() ? Clipboard.GetText().Trim() : "";
            if (text == previousClipboard) return;
            previousClipboard = text;
            if (IsUrl(text) && MessageBox.Show("Download this copied link?\n\n" + text, "Media detected", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes) _ = AddTextAsync(text);
        }
        catch (System.Runtime.InteropServices.COMException) { }
    }

    private async Task CheckBrowserAsync()
    {
        browserProbeRunning = true;
        try
        {
            var url = await Task.Run(BrowserDetection.CurrentUrl);
            if (url == null || url == previousBrowser) return;
            previousBrowser = url;
            if (MessageBox.Show("Download the media open in your browser?\n\n" + url, "Media detected", MessageBoxButton.YesNo) == MessageBoxResult.Yes) await AddTextAsync(url);
        }
        finally { browserProbeRunning = false; }
    }

    public void ApplyParallel() { Slots = new SemaphoreSlim(Math.Clamp(Parallel, 1, 8)); }

    [RelayCommand] private void FocusSearch() => SearchRequested?.Invoke();
    [RelayCommand] private void ResumeAll() { foreach (var job in Jobs.ToArray()) job.Resume(); }
    [RelayCommand] private void Clear() { foreach (var job in Jobs.Where(j => j.Complete).ToArray()) Jobs.Remove(job); }
    [RelayCommand] private void Settings() { Dialogs.Settings(this); Save(); }
    [RelayCommand] private void Speed()
    {
        var value = Dialogs.Choose("Download speed", ["250 KB/s", "500 KB/s", "1 MB/s", "2 MB/s", "4 MB/s", "Unlimited"], SpeedLimit);
        if (value != null) { SpeedLimit = value; Save(); Notice = "Speed applies to new downloads."; }
    }
    [RelayCommand] private void History() => Dialogs.History(this);
    [RelayCommand] private async Task ConvertAsync() => await ConvertFileAsync(false);
    [RelayCommand] private async Task TrimAsync() => await ConvertFileAsync(true);
    private async Task ConvertFileAsync(bool trim)
    {
        var file = new OpenFileDialog { Title = trim ? "Choose audio to trim" : "Choose media to convert", Filter = "Media files|*.mp3;*.mp4;*.wav;*.m4a;*.aac;*.webm;*.mkv;*.avi;*.flv;*.wmv;*.mov;*.ogg|All files|*.*" };
        if (file.ShowDialog() != true) return;
        var format = trim ? "mp3" : IsAudio ? AudioFormat : VideoFormat;
        var save = new SaveFileDialog { Title = "Save output", Filter = $"{format.ToUpperInvariant()} file|*.{format}", FileName = Path.GetFileNameWithoutExtension(file.FileName) + (trim ? " - trimmed" : " - converted"), InitialDirectory = Folder };
        if (save.ShowDialog() != true) return;
        if (string.Equals(save.FileName, file.FileName, StringComparison.OrdinalIgnoreCase)) { MessageBox.Show("Choose a different output file."); return; }
        var args = new List<string> { "-nostdin", "-y" };
        if (trim)
        {
            var start = Dialogs.Prompt("Trim audio", "Start time (hh:mm:ss or seconds):", "0");
            if (start == null) return;
            var duration = Dialogs.Prompt("Trim audio", "Duration (hh:mm:ss or seconds):", "30");
            if (duration == null) return;
            if (!ValidTime(start) || !ValidTime(duration)) { MessageBox.Show("Enter a positive time in seconds or hh:mm:ss."); return; }
            args.AddRange(["-ss", start, "-t", duration]);
        }
        args.AddRange(["-i", file.FileName]);
        if (trim || IsAudio) args.AddRange(["-vn", "-b:a", Options().Quality + "k"]);
        args.Add(save.FileName);
        await RunUtilityAsync("ffmpeg", args, "Converting media…");
    }
    private static bool ValidTime(string text) => double.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, out var seconds) && seconds >= 0 || TimeSpan.TryParse(text, out var time) && time >= TimeSpan.Zero;

    public async void DownloadSubtitles(string url, string folder)
    {
        var language = Dialogs.Prompt("Subtitles", "Language code, such as en, fr, or all:", "en");
        if (language == null) return;
        await RunUtilityAsync("yt-dlp", ["--ignore-config", "--skip-download", "--write-subs", "--write-auto-subs", "--sub-langs", language, "--convert-subs", "srt", "--ffmpeg-location", DownloadEngine.Tool("ffmpeg"), "-o", Path.Combine(folder, "%(title).170B.%(ext)s"), "--", url], "Downloading subtitles…");
    }

    private async Task RunUtilityAsync(string tool, IEnumerable<string> args, string title)
    {
        using var cancellation = new CancellationTokenSource();
        var busy = Dialogs.Busy(title, cancellation);
        busy.Show();
        var errors = new List<string>();
        try
        {
            var code = await DownloadEngine.RunAsync(DownloadEngine.StartInfo(tool, args), _ => { }, _ => { }, errors.Add, cancellation.Token);
            Notice = code == 0 ? "Finished · output saved" : string.Join("\n", errors.TakeLast(3));
            MessageBox.Show(Notice, code == 0 ? "Completed" : "Unable to finish");
        }
        catch (OperationCanceledException) { Notice = "Cancelled"; }
        catch (Exception ex) { Notice = ex.Message; MessageBox.Show(ex.Message); }
        finally { busy.Close(); }
    }

    [RelayCommand]
    private void Help() => MessageBox.Show("Choose a folder, select audio or video and a quality, then paste a YouTube link.\n\nSearch YouTube by name or choose View results. Playlist links offer an entry selector. Each download has pause/resume, retry, subtitles and folder actions.\n\nThe menu offers history, file conversion and audio trimming. Auto detect offers downloads when you copy a link. Settings accept a proxy and a Netscape cookies file for YouTube sign-in.\n\nMP4 video and MP3 audio are available. Best, Normal and Custom control video resolution or MP3 compression bitrate. Only YouTube links are supported, including Shorts, music and playlists. Media availability depends on YouTube and your access.\n\nPowered by yt-dlp, FFmpeg and CommunityToolkit.Mvvm.", "Media Harbor help");

    public void Record(DownloadJob job)
    {
        HistoryItems.Insert(0, new HistoryEntry(job.Title, job.Url, job.FilePath, DateTime.Now));
        if (screenshotMode) return;
        try { Directory.CreateDirectory(Path.GetDirectoryName(historyFile)!); File.WriteAllText(historyFile, JsonSerializer.Serialize(HistoryItems)); }
        catch (IOException ex) { Notice = "Downloaded; history could not be saved: " + ex.Message; }
    }
    public void SaveHistory() { Directory.CreateDirectory(Path.GetDirectoryName(historyFile)!); File.WriteAllText(historyFile, JsonSerializer.Serialize(HistoryItems)); }

    public void Save()
    {
        if (screenshotMode || loading) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(stateFile)!);
            File.WriteAllText(stateFile, JsonSerializer.Serialize(new SavedSettings(Folder, VideoFormat, AudioFormat, IsAudio, QualityMode, CustomQuality, SpeedLimit, AutoDetect, Parallel, Numbering, Artwork, IncludeSubtitles, Proxy, Cookies, BrowserDetect, FfmpegThreads)));
        }
        catch (IOException ex) { Notice = "Settings could not be saved: " + ex.Message; }
    }

    private void Load()
    {
        try
        {
            if (File.Exists(stateFile) && JsonSerializer.Deserialize<SavedSettings>(File.ReadAllText(stateFile)) is { } s)
            {
                Folder = s.Folder; VideoFormat = "mp4"; AudioFormat = "mp3"; IsAudio = s.IsAudio; CustomQuality = s.CustomQuality; QualityMode = s.QualityMode;
                SpeedLimit = s.SpeedLimit; AutoDetect = s.AutoDetect; Parallel = Math.Clamp(s.Parallel, 1, 8); Numbering = s.Numbering; Artwork = s.Artwork; IncludeSubtitles = s.Subtitles; Proxy = s.Proxy; Cookies = s.Cookies;
                BrowserDetect = s.BrowserDetect;
                if (s.FfmpegThreads is > 0) FfmpegThreads = Math.Clamp(s.FfmpegThreads.Value, 1, Environment.ProcessorCount);
                Slots = new SemaphoreSlim(Parallel);
            }
            if (File.Exists(historyFile)) HistoryItems = JsonSerializer.Deserialize<List<HistoryEntry>>(File.ReadAllText(historyFile)) ?? [];
        }
        catch (Exception ex) when (ex is IOException or JsonException) { Notice = "Saved preferences could not be read."; }
    }
    public void Dispose() { Save(); clipboardTimer.Stop(); foreach (var job in Jobs) job.Stop(); }
}

public sealed record SavedSettings(string Folder, string VideoFormat, string AudioFormat, bool IsAudio, string QualityMode, string CustomQuality, string SpeedLimit, bool AutoDetect, int Parallel, bool Numbering, bool Artwork, bool Subtitles, string Proxy, string Cookies, bool BrowserDetect = false, int? FfmpegThreads = null);
public sealed record HistoryEntry(string Title, string Url, string File, DateTime Date);

