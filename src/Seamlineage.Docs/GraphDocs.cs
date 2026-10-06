using System.Text.RegularExpressions;

namespace Seamlineage.Docs;

/// <summary>
/// GRAPH.md as files on disk: render it from a manifest and an examples folder, and check a committed copy. Shared by
/// the CLI and the test helpers, so both apply exactly the same rules.
/// </summary>
public static partial class GraphDocs
{
    /// <summary>The number of case folders under <c>fixtures/&lt;stage&gt;/</c> for each stage; 0 when there are none.</summary>
    public static IReadOnlyDictionary<string, int> CountCases(IEnumerable<string> stages, string? fixturesDir) =>
        stages.Distinct().ToDictionary(
            stage => stage,
            stage => fixturesDir is not null && Directory.Exists(Path.Combine(fixturesDir, stage))
                ? Directory.GetDirectories(Path.Combine(fixturesDir, stage)).Length
                : 0);

    /// <summary>Renders the page that belongs at <paramref name="outPath"/>, with links relative to it.</summary>
    /// <param name="generatedBy">The regenerate command the page's header names.</param>
    public static string Render(string manifestPath, string? fixturesDir, string outPath, string generatedBy = "seamlineage graph")
    {
        var json = File.ReadAllText(manifestPath);
        var manifest = ManifestView.Parse(json);
        var pageDir = Path.GetDirectoryName(Path.GetFullPath(outPath))!;
        var options = new DiagramOptions
        {
            ManifestFileName = Path.GetFileName(manifestPath),
            GeneratedBy = generatedBy,
            CodeRoot = Relative(pageDir, Path.GetDirectoryName(Path.GetFullPath(manifestPath))!),
            FixturesLink = fixturesDir is null ? "fixtures" : Relative(pageDir, Path.GetFullPath(fixturesDir)),
        };
        var cases = CountCases(manifest.Stages.Select(s => (string)s["name"]!), fixturesDir);
        return GraphDiagram.FromManifest(json, cases, options);
    }

    /// <summary>Everything wrong with a committed GRAPH.md and examples folder; empty when all is well.</summary>
    public static IReadOnlyList<string> Check(string manifestPath, string? fixturesDir, string outPath, string generatedBy = "seamlineage graph")
    {
        var problems = new List<string>();
        if (!File.Exists(outPath))
        {
            problems.Add($"{outPath} does not exist. Generate it with: {generatedBy}");
            return problems;
        }

        var expected = Render(manifestPath, fixturesDir, outPath, generatedBy);
        if (File.ReadAllText(outPath).ReplaceLineEndings() != expected.ReplaceLineEndings())
            problems.Add($"{outPath} is stale. Regenerate it with: {generatedBy}");

        problems.AddRange(BrokenLinks(outPath).Select(link => $"{outPath} links to {link}, which does not exist."));

        if (fixturesDir is not null)
        {
            var stages = ManifestView.Parse(File.ReadAllText(manifestPath)).Stages.Select(s => (string)s["name"]!).ToList();
            problems.AddRange(StagesWithoutExamples(stages, fixturesDir).Select(s => $"Stage '{s}' has no examples under {fixturesDir}."));
            problems.AddRange(FoldersWithoutStage(stages, fixturesDir).Select(f => $"{fixturesDir}/{f} does not match any stage in the manifest."));
        }
        return problems;
    }

    /// <summary>Relative links in a Markdown file that point at no file or folder. Web links and anchors are not checked.</summary>
    public static IReadOnlyList<string> BrokenLinks(string markdownPath)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(markdownPath))!;
        return
        [
            .. Link().Matches(File.ReadAllText(markdownPath))
                .Select(m => m.Groups[1].Value)
                .Where(link => !link.Contains("://", StringComparison.Ordinal) && !link.StartsWith('#'))
                .Where(link => !File.Exists(Path.Combine(dir, link)) && !Directory.Exists(Path.Combine(dir, link))),
        ];
    }

    /// <summary>Stages with no case folder under <c>fixtures/&lt;stage&gt;/</c>. Every stage needs at least one.</summary>
    public static IReadOnlyList<string> StagesWithoutExamples(IEnumerable<string> stages, string fixturesDir) =>
        [.. CountCases(stages, fixturesDir).Where(c => c.Value == 0).Select(c => c.Key)];

    /// <summary>Folders under the examples folder whose name is not a stage, e.g. after a stage was renamed.</summary>
    public static IReadOnlyList<string> FoldersWithoutStage(IEnumerable<string> stages, string fixturesDir)
    {
        if (!Directory.Exists(fixturesDir)) return [];
        var names = stages.ToHashSet(StringComparer.Ordinal);
        return
        [
            .. Directory.GetDirectories(fixturesDir)
                .Select(Path.GetFileName)
                .Where(name => !names.Contains(name!))
                .Order(StringComparer.Ordinal)!,
        ];
    }

    private static string Relative(string from, string to)
    {
        var relative = Path.GetRelativePath(from, to).Replace('\\', '/');
        return relative == "." ? "" : relative;
    }

    [GeneratedRegex(@"\]\(([^)]+)\)")]
    private static partial Regex Link();
}
