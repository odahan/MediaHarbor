using System.Net.Http.Headers;
using System.Text.RegularExpressions;

namespace MediaHarbor;

/// <summary>Reads only the status and retry instructions from yt-dlp's response trace.</summary>
internal sealed partial class DownloadResponse
{
    private bool acceptsRetryAfter;

    internal bool HasRetryAfter { get; private set; }

    internal DateTimeOffset? ReadRetryAfter(string line, DateTimeOffset now)
    {
        var status = ResponseStatus().Match(line);
        if (status.Success)
        {
            acceptsRetryAfter = status.Groups[1].Value is "429" or "503";
            return null;
        }

        if (!acceptsRetryAfter) return null;
        var header = RetryAfterHeader().Match(line);
        if (!header.Success || !RetryConditionHeaderValue.TryParse(header.Groups[1].Value, out var retry)) return null;

        HasRetryAfter = true;
        return retry.Date ?? now + retry.Delta!.Value;
    }

    internal static bool IsRateLimited(string error) =>
        error.Contains("HTTP Error 429", StringComparison.OrdinalIgnoreCase) ||
        error.Contains("429: Too Many Requests", StringComparison.OrdinalIgnoreCase);

    internal static bool RequiresSignIn(string error) =>
        (error.Contains("sign in to confirm", StringComparison.OrdinalIgnoreCase) &&
         error.Contains("not a bot", StringComparison.OrdinalIgnoreCase)) ||
        error.Contains("account cookies are no longer valid", StringComparison.OrdinalIgnoreCase) ||
        error.Contains("does not look like a Netscape format cookies file", StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex("^(?:reply: ['\"]|< )HTTP/\\d+(?:\\.\\d+)? (\\d{3})(?:\\s|['\"]|$)", RegexOptions.CultureInvariant)]
    private static partial Regex ResponseStatus();

    [GeneratedRegex("^(?:header: |< )Retry-After:\\s*(.+?)\\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RetryAfterHeader();
}
