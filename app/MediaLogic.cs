using System.IO;
using System.Text.RegularExpressions;

namespace MediaHarbor;

public static class MediaLogic
{
    public static bool IsValidDownloadUrl(string text) => YouTubeLinks.IsSupported(text) || text.StartsWith("ytsearch1:", StringComparison.Ordinal);

    public static string[] SplitLinks(string text) => text.Split(['\r', '\n', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries).Where(YouTubeLinks.IsSupported).Distinct().ToArray();

    public static string PlaylistFolderName(IReadOnlyList<SearchResult> results)
    {
        var name = results.Select(r => r.PlaylistTitle).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t)) ?? "";
        var invalid = Path.GetInvalidFileNameChars();
        name = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim().TrimEnd('.', ' ');
        if (name.Length > 120) name = name[..120].TrimEnd('.', ' ');
        return name.Length > 0 ? name : "Playlist " + DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss");
    }

    public static bool ValidTime(string text) => double.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, out var seconds) && seconds >= 0 || TimeSpan.TryParse(text, out var time) && time >= TimeSpan.Zero;

    public static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        name = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim().TrimEnd('.', ' ');
        return name.Length > 0 ? name : "Untitled";
    }
}
