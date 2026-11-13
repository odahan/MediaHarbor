using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace MediaHarbor;

public static class BrowserDetection
{
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);

    /// <summary>Reads only the visible address field of the foreground browser.</summary>
    public static string? CurrentUrl()
    {
        try
        {
            var handle = GetForegroundWindow();
            GetWindowThreadProcessId(handle, out var id);
            using var process = Process.GetProcessById((int)id);
            if (process.ProcessName is not ("chrome" or "msedge" or "firefox" or "brave" or "opera")) return null;
            var root = AutomationElement.FromHandle(handle);
            var edits = root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
            foreach (AutomationElement edit in edits)
            {
                if (!edit.TryGetCurrentPattern(ValuePattern.Pattern, out var pattern)) continue;
                var value = ((ValuePattern)pattern).Current.Value;
                if (!value.StartsWith("http", StringComparison.OrdinalIgnoreCase)) value = "https://" + value;
                if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) continue;
                if (YouTubeLinks.IsSupported(value)) return value;
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or ElementNotAvailableException or COMException or ArgumentException) { }
        return null;
    }
}
