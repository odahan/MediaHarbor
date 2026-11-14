using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace MediaHarbor;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            await DistributionFiles.ExtractAsync();
            if (DistributionFiles.IsPublished && ToolSetup.IsNeeded)
            {
                if (MessageBox.Show(
                    "MediaHarbor needs FFmpeg and yt-dlp. Download the missing tools directly from their publishers on GitHub?",
                    "MediaHarbor setup", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                {
                    Shutdown();
                    return;
                }

                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                var setup = new Window
                {
                    Title = "MediaHarbor setup",
                    SizeToContent = SizeToContent.WidthAndHeight,
                    ResizeMode = ResizeMode.NoResize,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    Content = new System.Windows.Controls.TextBlock
                    {
                        Text = "Downloading and verifying the media tools. Please wait.",
                        Margin = new Thickness(24)
                    }
                };
                setup.Closing += PreventSetupClose;
                setup.Show();
                try
                {
                    await ToolSetup.InstallAsync();
                }
                finally
                {
                    setup.Closing -= PreventSetupClose;
                    MainWindow = null;
                    setup.Close();
                    ShutdownMode = ShutdownMode.OnLastWindowClose;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or HttpRequestException or TaskCanceledException)
        {
            MessageBox.Show($"MediaHarbor could not prepare its files in {DistributionFiles.ApplicationDirectory}.\n\n{ex.Message}",
                "MediaHarbor", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        if (e.Args.Contains("--self-test"))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            _ = RunSelfTestAsync();
            return;
        }
        var shotIndex = Array.IndexOf(e.Args, "--screenshot");
        var screenshot = shotIndex >= 0 && shotIndex + 1 < e.Args.Length ? e.Args[shotIndex + 1] : null;
        var vm = new MainViewModel(screenshot != null);
        var window = new MainWindow { DataContext = vm, ShowActivated = screenshot == null };
        if (screenshot != null)
        {
            window.SourceInitialized += (_, _) =>
            {
                var handle = new WindowInteropHelper(window).Handle;
                SetWindowPos(handle, new IntPtr(1), 0, 0, 0, 0, 0x0013);
            };
            window.Loaded += (_, _) =>
            {
                var frames = 0;
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
                timer.Tick += (_, _) =>
                {
                    if (++frames < 120) return;
                    timer.Stop();
                    window.UpdateLayout();
                    var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(window);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    var path = Path.GetFullPath(screenshot);
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    using (var stream = File.Create(path)) encoder.Save(stream);
                    window.Close();
                };
                timer.Start();
            };
        }
        window.Closed += (_, _) => vm.Dispose();
        window.Show();
    }

    private static void PreventSetupClose(object? sender, System.ComponentModel.CancelEventArgs e) => e.Cancel = true;

    private async Task RunSelfTestAsync()
    {
        try { await EngineSelfTest.RunAsync(); Shutdown(0); }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(DistributionFiles.ApplicationDirectory, "self-test-failure.txt"), ex.ToString());
            Shutdown(1);
        }
    }

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
}
