namespace MediaHarbor;

public static class YouTubeLinks
{
    internal static string? QueryValue(Uri url, string name) => url.Query.TrimStart('?')
        .Split('&', StringSplitOptions.RemoveEmptyEntries)
        .Select(part => part.Split('=', 2))
        .Where(part => part.Length == 2 && part[0].Equals(name, StringComparison.OrdinalIgnoreCase))
        .Select(part => Uri.UnescapeDataString(part[1]))
        .FirstOrDefault(value => value.Length > 0);

    internal static string? VideoId(string link)
    {
        if (!IsSupported(link)) return null;
        var url = new Uri(link);
        var segments = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (url.Host.Equals("youtu.be", StringComparison.OrdinalIgnoreCase)) return segments.FirstOrDefault();
        if (segments.Length >= 2 && segments[0] is "shorts" or "live" or "embed") return segments[1];
        return QueryValue(url, "v");
    }

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
