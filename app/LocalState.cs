using System.IO;
using System.Text.Json;

namespace MediaHarbor;

internal sealed class LocalState
{
    private readonly string directory;
    private readonly string legacyDirectory;

    internal LocalState() : this(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MediaHarbor"),
        Path.Combine(DistributionFiles.ApplicationDirectory, "data")) { }

    internal LocalState(string directory, string legacyDirectory)
    {
        this.directory = directory;
        this.legacyDirectory = legacyDirectory;
    }

    internal SavedSettings? LoadSettings() => Load<SavedSettings>("settings.json");
    internal List<HistoryEntry>? LoadHistory() => Load<List<HistoryEntry>>("history.json");
    internal void SaveSettings(SavedSettings settings) => Save("settings.json", settings);
    internal void SaveHistory(List<HistoryEntry> history) => Save("history.json", history);

    private T? Load<T>(string name)
    {
        var destination = Path.Combine(directory, name);
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(destination));
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            var legacy = Path.Combine(legacyDirectory, name);
            if (!File.Exists(legacy)) return default;
            var contents = File.ReadAllText(legacy);
            var value = JsonSerializer.Deserialize<T>(contents);
            AtomicFile.WriteAsync(destination, path =>
            {
                File.WriteAllText(path, contents);
                return Task.CompletedTask;
            }, overwrite: false).GetAwaiter().GetResult();
            return value;
        }
    }

    private void Save<T>(string name, T value) =>
        AtomicFile.WriteText(Path.Combine(directory, name), JsonSerializer.Serialize(value));
}
