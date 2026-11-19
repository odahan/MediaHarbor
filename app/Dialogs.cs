using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace MediaHarbor;

public static class Dialogs
{
    private static Window Window(string title, double width = 480, double height = 240) => new()
    {
        Title = title, Width = width, Height = height, Owner = Application.Current.MainWindow,
        WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = System.Windows.Media.Brushes.AliceBlue,
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI"), FontSize = 13, MinWidth = 340, MinHeight = 180
    };

    /// <summary>Shows application information and an entry point to the local guide.</summary>
    internal static void About(System.Windows.Input.ICommand helpCommand)
    {
        var window = Window("About MediaHarbor", 420, 250);
        window.ResizeMode = ResizeMode.NoResize;
        var version = typeof(Dialogs).Assembly.GetName().Version?.ToString(3);
        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock { Text = "MediaHarbor", FontSize = 22, FontWeight = FontWeights.Bold });
        panel.Children.Add(new TextBlock { Text = $"Version {version}", Margin = new Thickness(0, 4, 0, 12) });
        panel.Children.Add(new TextBlock { Text = "Powered by yt-dlp, FFmpeg and CommunityToolkit.Mvvm.", TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new TextBlock
        {
            Text = "Copyright © 2026 Olivier Dahan. GPLv3 or later. You may redistribute MediaHarbor under its license. It comes without warranty.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0)
        });

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
        buttons.Children.Add(new Button { Content = "Help", Command = helpCommand, MinWidth = 75, Margin = new Thickness(0, 0, 8, 0) });
        if (DistributionFiles.IsPublished)
        {
            var licenses = new Button { Content = "Licenses", MinWidth = 75, Margin = new Thickness(0, 0, 8, 0) };
            licenses.Click += (_, _) =>
            {
                var dialog = Window("MediaHarbor license", 650, 500);
                dialog.Content = new TextBox
                {
                    Text = File.ReadAllText(Path.Combine(DistributionFiles.ApplicationDirectory, "licenses", "LICENSE")),
                    IsReadOnly = true,
                    TextWrapping = TextWrapping.Wrap,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                };
                dialog.ShowDialog();
            };
            buttons.Children.Add(licenses);
        }
        var close = new Button { Content = "Close", IsCancel = true, IsDefault = true, MinWidth = 75 };
        buttons.Children.Add(close);
        panel.Children.Add(buttons);
        window.Content = panel;
        window.ShowDialog();
    }

    /// <summary>Prompts for a detected link above other applications.</summary>
    internal static bool ConfirmDetectedMedia(string message, string url)
    {
        var window = Window("Media detected", 440, 200);
        window.Topmost = true;
        window.ShowActivated = true;
        window.ShowInTaskbar = false;
        window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        window.SizeToContent = SizeToContent.Height;
        window.ResizeMode = ResizeMode.NoResize;
        window.ContentRendered += (_, _) => window.Activate();

        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new TextBlock { Text = url, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0) });

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
        var yes = new Button { Content = "Yes", MinWidth = 75, Margin = new Thickness(0, 0, 8, 0) };
        yes.Click += (_, _) => window.DialogResult = true;
        var no = new Button { Content = "No", MinWidth = 75, IsCancel = true, IsDefault = true };
        buttons.Children.Add(yes);
        buttons.Children.Add(no);
        panel.Children.Add(buttons);
        window.Content = panel;
        window.Loaded += (_, _) => no.Focus();
        return window.ShowDialog() == true;
    }

    public static string? Prompt(string title, string label, string initial)
    {
        var window = Window(title);
        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) });
        var input = new TextBox { Text = initial, AcceptsReturn = false };
        panel.Children.Add(input);
        var ok = new Button { Content = "Continue", IsDefault = true, Margin = new Thickness(0, 15, 0, 0), HorizontalAlignment = HorizontalAlignment.Right };
        ok.Click += (_, _) => window.DialogResult = true;
        panel.Children.Add(ok);
        window.Content = panel;
        window.Loaded += (_, _) => { input.Focus(); input.SelectAll(); };
        return window.ShowDialog() == true ? input.Text.Trim() : null;
    }

    public static string? Choose(string title, string[] items, string initial)
    {
        var window = Window(title, 350, 205);
        var panel = new StackPanel { Margin = new Thickness(22) };
        var combo = new ComboBox { ItemsSource = items, SelectedItem = initial };
        panel.Children.Add(combo);
        var ok = new Button { Content = "Apply", Margin = new Thickness(0, 22, 0, 0), IsDefault = true };
        ok.Click += (_, _) => window.DialogResult = true;
        panel.Children.Add(ok);
        window.Content = panel;
        return window.ShowDialog() == true ? combo.SelectedItem as string : null;
    }

    internal static bool? ChoosePlaylistMode(bool hasVideo)
    {
        var window = Window("YouTube playlist", 540, 205);
        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock
        {
            Text = hasVideo
                ? "This video belongs to a playlist. Download only this video, or choose which videos to download from the playlist."
                : "Choose which videos to download from this playlist.",
            TextWrapping = TextWrapping.Wrap
        });
        var buttons = new WrapPanel { Margin = new Thickness(0, 20, 0, 0) };
        bool? playlist = null;
        Add("Choose playlist videos", () => { playlist = true; window.DialogResult = true; }, isDefault: true);
        if (hasVideo)
            Add("Download this video only", () => { playlist = false; window.DialogResult = true; });
        Add("Cancel", () => window.DialogResult = false, isCancel: true);
        panel.Children.Add(buttons);
        window.Content = panel;
        return window.ShowDialog() == true ? playlist : null;

        void Add(string label, Action action, bool isDefault = false, bool isCancel = false)
        {
            var button = new Button { Content = label, IsDefault = isDefault, IsCancel = isCancel, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(8, 4, 8, 4) };
            button.Click += (_, _) => action();
            buttons.Children.Add(button);
        }
    }

    public static Window Busy(string title, CancellationTokenSource cancellation)
    {
        var window = Window(title, 400, 185);
        window.ResizeMode = ResizeMode.NoResize;
        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock { Text = title, Margin = new Thickness(0, 0, 0, 15) });
        panel.Children.Add(new ProgressBar { IsIndeterminate = true, Height = 8 });
        var cancel = new Button { Content = "Cancel", Margin = new Thickness(0, 15, 0, 0), HorizontalAlignment = HorizontalAlignment.Right };
        cancel.Click += (_, _) => cancellation.Cancel();
        panel.Children.Add(cancel);
        window.Content = panel;
        window.Closing += (_, _) => cancellation.Cancel();
        return window;
    }

    public static IReadOnlyList<SearchResult> SelectResults(IReadOnlyList<SearchResult> results, string title)
    {
        var window = Window(title, 700, 500);
        var dock = new DockPanel { Margin = new Thickness(15) };
        var bottom = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        DockPanel.SetDock(bottom, Dock.Bottom);
        var all = new Button { Content = "Select all", Margin = new Thickness(4, 0, 4, 0) };
        var none = new Button { Content = "Select none", Margin = new Thickness(4, 0, 4, 0) };
        var ok = new Button { Content = "Download selected", IsDefault = true };
        var cancel = new Button { Content = "Cancel", IsCancel = true, Margin = new Thickness(6, 0, 0, 0) };
        bottom.Children.Add(all); bottom.Children.Add(none); bottom.Children.Add(ok); bottom.Children.Add(cancel); dock.Children.Add(bottom);
        var grid = new DataGrid { ItemsSource = results, AutoGenerateColumns = false, CanUserAddRows = false, HeadersVisibility = DataGridHeadersVisibility.Column, SelectionMode = DataGridSelectionMode.Extended };
        grid.Columns.Add(new DataGridCheckBoxColumn { Header = "✓", Binding = new Binding("Selected") { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged }, Width = 40 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Title", Binding = new Binding("Title"), Width = new DataGridLength(1, DataGridLengthUnitType.Star), IsReadOnly = true });
        dock.Children.Add(grid);
        all.Click += (_, _) => { foreach (var entry in results) entry.Selected = true; grid.Items.Refresh(); };
        none.Click += (_, _) => { foreach (var entry in results) entry.Selected = false; grid.Items.Refresh(); };
        ok.Click += (_, _) => { grid.CommitEdit(); grid.CommitEdit(DataGridEditingUnit.Row, true); window.DialogResult = true; };
        window.Content = dock;
        return window.ShowDialog() == true ? results.Where(x => x.Selected).ToArray() : [];
    }

    public static void Settings(MainViewModel vm)
    {
        var window = Window("Settings", 540, 560);
        var panel = new StackPanel { Margin = new Thickness(22) };
        var startup = Check("Start MediaHarbor with Windows", false);
        try
        {
            startup.IsChecked = WindowsStartup.IsRegistered();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            startup.IsEnabled = false;
            startup.ToolTip = $"Could not read the Windows startup setting. {ex.Message}";
            ToolTipService.SetShowOnDisabled(startup, true);
        }
        var wasRegistered = startup.IsChecked == true;
        panel.Children.Add(startup);
        panel.Children.Add(new TextBlock { Text = "Downloads", FontSize = 19, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 8) });
        var artwork = Check("Embed artwork in MP3 / M4A files", vm.Artwork);
        var numbering = Check("Number playlist entries", vm.Numbering);
        var subtitles = Check("Save English and French subtitles when available", vm.IncludeSubtitles);
        panel.Children.Add(artwork); panel.Children.Add(numbering); panel.Children.Add(subtitles);
        panel.Children.Add(new TextBlock { Text = "Simultaneous downloads (1–8)", Margin = new Thickness(0, 14, 0, 5) });
        var parallel = new ComboBox { ItemsSource = Enumerable.Range(1, 8).ToArray(), SelectedItem = vm.Parallel };
        panel.Children.Add(parallel);
        panel.Children.Add(new TextBlock { Text = $"CPU threads used for conversion (1–{Environment.ProcessorCount})", Margin = new Thickness(0, 14, 0, 5) });
        var threads = new ComboBox { ItemsSource = Enumerable.Range(1, Environment.ProcessorCount).ToArray(), SelectedItem = Math.Clamp(vm.FfmpegThreads, 1, Environment.ProcessorCount) };
        panel.Children.Add(threads);
        panel.Children.Add(new TextBlock { Text = "Proxy URL (optional)", Margin = new Thickness(0, 14, 0, 5) });
        var proxy = new TextBox { Text = vm.Proxy };
        panel.Children.Add(proxy);
        panel.Children.Add(new TextBlock { Text = "YouTube cookies file (Netscape format)", Margin = new Thickness(0, 14, 0, 5) });
        var cookieDock = new DockPanel();
        var browse = new Button { Content = "Browse…", Margin = new Thickness(7, 0, 0, 0) };
        DockPanel.SetDock(browse, Dock.Right); cookieDock.Children.Add(browse);
        var cookies = new TextBox { Text = vm.Cookies }; cookieDock.Children.Add(cookies); panel.Children.Add(cookieDock);
        browse.Click += (_, _) => { var file = new OpenFileDialog { Filter = "Cookies text|*.txt|All files|*.*" }; if (file.ShowDialog() == true) cookies.Text = file.FileName; };
        panel.Children.Add(new TextBlock { Text = "The download limit applies to jobs already waiting. Cookie changes apply when a download starts or retries. Other download settings apply when you add a job. Auto detect asks before adding a link.", TextWrapping = TextWrapping.Wrap, Foreground = System.Windows.Media.Brushes.SlateGray, Margin = new Thickness(0, 15, 0, 10), FontSize = 11 });
        var save = new Button { Content = "Save settings", IsDefault = true, HorizontalAlignment = HorizontalAlignment.Right };
        save.Click += (_, _) =>
        {
            if (cookies.Text.Length > 0 && !File.Exists(cookies.Text)) { MessageBox.Show("Choose an existing cookies file."); return; }
            if (proxy.Text.Length > 0 && !Uri.TryCreate(proxy.Text, UriKind.Absolute, out _)) { MessageBox.Show("Enter a valid proxy URL."); return; }
            var startWithWindows = startup.IsChecked == true;
            if (startup.IsEnabled && startWithWindows != wasRegistered)
            {
                try
                {
                    WindowsStartup.SetRegistered(startWithWindows);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                {
                    MessageBox.Show(window, $"Could not update the Windows startup setting.\n\n{ex.Message}",
                        "MediaHarbor", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
            vm.Artwork = artwork.IsChecked == true; vm.Numbering = numbering.IsChecked == true; vm.IncludeSubtitles = subtitles.IsChecked == true;
            vm.Parallel = (int)parallel.SelectedItem; vm.FfmpegThreads = (int)threads.SelectedItem;
            vm.ApplyParallel(); vm.Proxy = proxy.Text.Trim(); vm.Cookies = cookies.Text.Trim(); window.DialogResult = true;
        };
        panel.Children.Add(save);
        window.Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        window.ShowDialog();
    }

    private static CheckBox Check(string title, bool value) => new() { Content = title, IsChecked = value, Margin = new Thickness(0, 5, 0, 5) };

    public static void History(MainViewModel vm)
    {
        var window = Window("Download history", 760, 480);
        var dock = new DockPanel { Margin = new Thickness(15) };
        var search = new TextBox { ToolTip = "Filter by title or URL", Margin = new Thickness(0, 0, 0, 10) };
        DockPanel.SetDock(search, Dock.Top); dock.Children.Add(search);
        var bottom = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        DockPanel.SetDock(bottom, Dock.Bottom); dock.Children.Add(bottom);
        var grid = new DataGrid { ItemsSource = vm.HistoryItems.ToArray(), IsReadOnly = true, AutoGenerateColumns = false, CanUserAddRows = false };
        grid.Columns.Add(new DataGridTextColumn { Header = "Title", Binding = new Binding("Title"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Date", Binding = new Binding("Date") { StringFormat = "g" }, Width = 135 });
        dock.Children.Add(grid);
        search.TextChanged += (_, _) => grid.ItemsSource = vm.HistoryItems.Where(x => (x.Title + x.Url).Contains(search.Text, StringComparison.OrdinalIgnoreCase)).ToArray();
        void Add(string title, Action<HistoryEntry> action)
        {
            var button = new Button { Content = title, Margin = new Thickness(0, 0, 7, 0) };
            button.Click += (_, _) => { if (grid.SelectedItem is HistoryEntry item) action(item); };
            bottom.Children.Add(button);
        }
        Add("Play", item => { if (File.Exists(item.File)) DownloadJob.Launch(item.File); else MessageBox.Show("The saved file is no longer available."); });
        Add("Download again", item => vm.Enqueue(item.Url, vm.Options()));
        Add("Copy URL", item => Clipboard.SetText(item.Url));
        Add("Delete entry", item => { vm.DeleteHistoryEntry(item); grid.ItemsSource = vm.HistoryItems.ToArray(); });
        window.Content = dock;
        window.ShowDialog();
    }
}

