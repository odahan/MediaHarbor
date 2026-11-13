namespace MediaHarbor;

public static class YouTubeLinks
{
    /// <summary>Recognizes YouTube HTTPS/HTTP links without accepting lookalike domains.</summary>
    public static bool IsSupported(string text)
    {
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(uri.UserInfo)) return false;

        var host = uri.Host;
        return host.Equals("youtube.com", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".youtube.com", StringComparison.OrdinalIgnoreCase) ||
            host.Equals("youtu.be", StringComparison.OrdinalIgnoreCase);
    }
}
