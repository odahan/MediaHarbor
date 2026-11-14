using System.IO;
using Microsoft.Win32;

namespace MediaHarbor;

internal static class WindowsStartup
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "MediaHarbor";

    /// <summary>Checks whether MediaHarbor is registered to run at sign-in for the current user.</summary>
    internal static bool IsRegistered()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is string { Length: > 0 };
    }

    /// <summary>Registers or removes MediaHarbor's sign-in command for the current user.</summary>
    internal static void SetRegistered(bool enabled)
    {
        if (!enabled)
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            key?.DeleteValue(ValueName, throwOnMissingValue: false);
            return;
        }

        var executable = Environment.ProcessPath ?? throw new IOException("The application path could not be found.");
        var command = $"\"{executable}\"";
        if (string.Equals(Path.GetFileName(executable), "dotnet.exe", StringComparison.OrdinalIgnoreCase))
            command += $" \"{typeof(WindowsStartup).Assembly.Location}\"";

        using var runKey = Registry.CurrentUser.CreateSubKey(RunKey)
            ?? throw new IOException("The Windows startup registry key could not be opened.");
        runKey.SetValue(ValueName, command, RegistryValueKind.String);
    }
}
