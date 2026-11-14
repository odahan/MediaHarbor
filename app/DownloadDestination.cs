namespace MediaHarbor;

internal readonly record struct DownloadDestination(string Folder, string MediaId)
{
    internal static IEqualityComparer<DownloadDestination> Comparer { get; } = new DestinationComparer();

    private sealed class DestinationComparer : IEqualityComparer<DownloadDestination>
    {
        public bool Equals(DownloadDestination x, DownloadDestination y) =>
            StringComparer.OrdinalIgnoreCase.Equals(x.Folder, y.Folder) &&
            StringComparer.OrdinalIgnoreCase.Equals(x.MediaId, y.MediaId);

        public int GetHashCode(DownloadDestination value) => HashCode.Combine(
            StringComparer.OrdinalIgnoreCase.GetHashCode(value.Folder),
            StringComparer.OrdinalIgnoreCase.GetHashCode(value.MediaId));
    }
}
