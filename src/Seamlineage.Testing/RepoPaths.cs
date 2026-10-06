namespace Seamlineage.Testing;

/// <summary>Finds files relative to the repository from a test's output folder.</summary>
public static class RepoPaths
{
    /// <summary>The nearest directory above the test's output folder that contains <paramref name="marker"/> (e.g. a .slnx or graph.manifest.json).</summary>
    public static string FindAbove(string marker, string? start = null)
    {
        for (var dir = new DirectoryInfo(start ?? AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, marker)) || Directory.Exists(Path.Combine(dir.FullName, marker)))
                return dir.FullName;
        }
        throw new InvalidOperationException($"Could not find {marker} above {start ?? AppContext.BaseDirectory}");
    }
}
