using System.Windows;

namespace MediaHarbor.Tests;

/// <summary>Ensures a shared WPF Application exists for tests that touch the dispatcher.</summary>
public static class WpfTestApplication
{
    private static readonly object Sync = new();

    public static void Ensure()
    {
        lock (Sync)
        {
            if (Application.Current == null)
            {
                var app = new Application();
                app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            }
        }
    }
}
