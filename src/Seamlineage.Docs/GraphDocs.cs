using System.Text.RegularExpressions;

namespace Seamlineage.Docs;

/// <summary>
/// GRAPH.md and its stage pages as files on disk: render them from a manifest and an examples folder, and check
/// committed copies. Shared by the CLI and the test helpers, so both apply exactly the same rules.
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

    /// <summary>The folder of the stage pages, beside GRAPH.md.</summary>
    public const string PagesFolder = "graph";

    /// <summary>Where the stage pages of the GRAPH.md at <paramref name="outPath"/> go: <c>graph/</c> beside it.</summary>
    public static string PagesDir(string outPath) => Path.Combine(Path.GetDirectoryName(Path.GetFullPath(outPath))!, PagesFolder);

    /// <summary>
    /// GRAPH.md and one page per stage (<c>graph/&lt;stage&gt;.md</c>, in stage order), each with links relative to
    /// itself: full path → content.
    /// </summary>
    /// <param name="problems">Collects each example case a stage's example view does not fit.</param>
    public static IReadOnlyDictionary<string, string> RenderAll(
        string manifestPath, string? fixturesDir, string outPath, string generatedBy = "seamlineage graph", ICollection<string>? problems = null)
    {
        var json = File.ReadAllText(manifestPath);
        var manifest = ManifestView.Parse(json);
        var pagesDir = PagesDir(outPath);
        var manifestDir = Path.GetDirectoryName(Path.GetFullPath(manifestPath))!;
        var options = new DiagramOptions
        {
            ManifestFileName = Path.GetFileName(manifestPath),
            GeneratedBy = generatedBy,
            CodeRoot = Relative(pagesDir, manifestDir),
            FixturesLink = Relative(pagesDir, fixturesDir is null ? Path.Combine(manifestDir, "fixtures") : Path.GetFullPath(fixturesDir)),
            GraphLink = Relative(pagesDir, Path.GetFullPath(outPath)),
        };

        var files = new OrderedDictionary<string, string>(StringComparer.Ordinal)
        {
            [Path.GetFullPath(outPath)] = Render(manifestPath, fixturesDir, outPath, generatedBy),
        };
        foreach (var stage in manifest.Stages.Select(s => (string)s["name"]!))
            files[Path.Combine(pagesDir, stage + ".md")] = StagePage.FromManifest(json, stage, ReadCases(fixturesDir, stage), options, problems);
        return files;
    }

    /// <summary>Writes GRAPH.md and the stage pages, and deletes pages in <c>graph/</c> of stages that no longer exist.</summary>
    /// <returns>The number of stage pages written.</returns>
    public static int Write(string manifestPath, string? fixturesDir, string outPath, string generatedBy = "seamlineage graph")
    {
        var files = RenderAll(manifestPath, fixturesDir, outPath, generatedBy);
        foreach (var stray in StrayPages(outPath, files.Keys)) File.Delete(stray);
        foreach (var (path, content) in files)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }
        return files.Count - 1;
    }

    /// <summary>The example cases of a stage, in case order (ordinal), with the text of their files.</summary>
    public static IReadOnlyList<ExampleCase> ReadCases(string? fixturesDir, string stage)
    {
        var stageDir = fixturesDir is null ? null : Path.Combine(fixturesDir, stage);
        if (stageDir is null || !Directory.Exists(stageDir)) return [];
        return
        [
            .. Directory.GetDirectories(stageDir).Order(StringComparer.Ordinal).Select(dir => new ExampleCase(
                Path.GetFileName(dir),
                ReadIfExists(Path.Combine(dir, "input.json")),
                ReadIfExists(Path.Combine(dir, "expected.json")))),
        ];
    }

    /// <summary>Everything wrong with a committed GRAPH.md, its stage pages and the examples folder; empty when all is well.</summary>
    public static IReadOnlyList<string> Check(string manifestPath, string? fixturesDir, string outPath, string generatedBy = "seamlineage graph")
    {
        var problems = new List<string>();
        if (!File.Exists(outPath))
        {
            problems.Add($"{outPath} does not exist. Generate it with: {generatedBy}");
            return problems;
        }

        var viewProblems = new List<string>();
        var files = RenderAll(manifestPath, fixturesDir, outPath, generatedBy, viewProblems);
        var outDir = Path.GetDirectoryName(outPath) ?? "";
        foreach (var (path, expected) in files)
        {
            // Name each page as the caller named GRAPH.md: relative paths stay relative.
            var shown = path == Path.GetFullPath(outPath) ? outPath : Path.Combine(outDir, PagesFolder, Path.GetFileName(path));
            if (!File.Exists(path))
            {
                problems.Add($"{shown} does not exist. Generate it with: {generatedBy}");
                continue;
            }
            if (File.ReadAllText(path).ReplaceLineEndings() != expected.ReplaceLineEndings())
                problems.Add($"{shown} is stale. Regenerate it with: {generatedBy}");
            problems.AddRange(BrokenLinks(path).Select(link => $"{shown} links to {link}, which does not exist."));
        }
        problems.AddRange(StrayPages(outPath, files.Keys).Select(stray =>
            $"{Path.Combine(outDir, PagesFolder, Path.GetFileName(stray))} is not the page of any stage. Delete it, or regenerate with: {generatedBy}"));
        problems.AddRange(viewProblems);

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

    // Markdown files in graph/ that are not a current stage page, e.g. after a stage was renamed. Other files are left alone.
    private static IEnumerable<string> StrayPages(string outPath, IEnumerable<string> pages)
    {
        var dir = PagesDir(outPath);
        if (!Directory.Exists(dir)) return [];
        var current = pages.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return Directory.GetFiles(dir, "*.md").Where(f => !current.Contains(Path.GetFullPath(f))).Order(StringComparer.Ordinal).ToList();
    }

    private static string? ReadIfExists(string path) => File.Exists(path) ? File.ReadAllText(path) : null;

    private static string Relative(string from, string to)
    {
        var relative = Path.GetRelativePath(from, to).Replace('\\', '/');
        return relative == "." ? "" : relative;
    }

    [GeneratedRegex(@"\]\(([^)]+)\)")]
    private static partial Regex Link();
}
