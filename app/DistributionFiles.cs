using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace MediaHarbor;

internal static class DistributionFiles
{
    private const string GuideName = "MediaHarborGuide.html";
    private const string ResourcePrefix = "MediaHarbor.Distribution.";
    internal static bool IsPublished { get; } = typeof(DistributionFiles).Assembly
        .GetManifestResourceInfo(ResourcePrefix + "LICENSE") is not null;

    /// <summary>Gets the executable folder for the distribution or the output folder for development.</summary>
    internal static string ApplicationDirectory { get; } = IsPublished
        ? Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory
        : AppContext.BaseDirectory;

    /// <summary>Updates the local guide and extracts the published license documents.</summary>
    internal static async Task ExtractAsync()
    {
        await UpdateGuideAsync();

        if (!IsPublished)
            return;

        await ExtractAsync("LICENSE", Path.Combine(ApplicationDirectory, "licenses"));
        await ExtractAsync("THIRD-PARTY-NOTICES.md", ApplicationDirectory);
        foreach (var resourceName in typeof(DistributionFiles).Assembly.GetManifestResourceNames())
        {
            if (resourceName.StartsWith(ResourcePrefix, StringComparison.Ordinal) &&
                resourceName.EndsWith(".txt", StringComparison.Ordinal))
                await ExtractAsync(resourceName[ResourcePrefix.Length..], Path.Combine(ApplicationDirectory, "licenses"));
        }
    }

    /// <summary>Opens the local guide using the default HTML handler.</summary>
    internal static async Task OpenGuideAsync()
    {
        var path = await UpdateGuideAsync();
        try
        {
            using var process = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Win32Exception ex)
        {
            throw new IOException($"Could not open {path}. {ex.Message}", ex);
        }
    }

    private static async Task<string> UpdateGuideAsync()
    {
        var destination = Path.Combine(ApplicationDirectory, GuideName);
        using var resource = OpenResource(GuideName);
        var bundledGuide = new byte[checked((int)resource.Length)];
        await resource.ReadExactlyAsync(bundledGuide);

        byte[]? localGuide = null;
        try
        {
            localGuide = await File.ReadAllBytesAsync(destination);
        }
        catch (FileNotFoundException)
        {
            // The guide has not been extracted yet.
        }

        if (localGuide is null || !bundledGuide.AsSpan().SequenceEqual(localGuide))
            await AtomicFile.WriteAsync(destination, temporary => File.WriteAllBytesAsync(temporary, bundledGuide));

        return destination;
    }

    private static async Task ExtractAsync(string name, string directory)
    {
        var destination = Path.Combine(directory, name);
        if (File.Exists(destination))
            return;

        using var resource = OpenResource(name);

        try
        {
            await AtomicFile.WriteAsync(destination, async temporary =>
            {
                await using var output = File.Create(temporary);
                await resource.CopyToAsync(output);
            }, overwrite: false);
        }
        catch (IOException) when (File.Exists(destination))
        {
            // Another instance finished extracting the same file.
        }
    }

    private static Stream OpenResource(string name) =>
        typeof(DistributionFiles).Assembly.GetManifestResourceStream(ResourcePrefix + name)
            ?? throw new IOException($"The bundled file {name} is missing.");
}
