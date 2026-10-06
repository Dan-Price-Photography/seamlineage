using System.Text.Json.Nodes;
using Seamlineage.Contracts;
using Seamlineage.Docs;

namespace Seamlineage.Testing;

/// <summary>
/// Generated files are never hand-edited: these checks fail when a committed graph.manifest.json or GRAPH.md is not
/// what the code and examples generate now, or when a link in it resolves to nothing.
/// </summary>
public static class GeneratedFiles
{
    /// <param name="regenerate">The command that regenerates the manifest, shown in the failure message.</param>
    public static void ManifestIsCurrent(Graph graph, string manifestPath, string regenerate, Func<Type, string>? codePath = null)
    {
        var committed = File.Exists(manifestPath) ? File.ReadAllText(manifestPath) : "(missing)";
        if (committed.ReplaceLineEndings() != GraphManifest.Generate(graph, codePath).ReplaceLineEndings())
            throw new CheckFailedException($"{manifestPath} is stale. Regenerate it with: {regenerate}");
    }

    /// <summary>Throws unless every stage's <c>code</c> path in the manifest is a file, relative to the manifest.</summary>
    public static void CodePathsExist(string manifestPath)
    {
        var root = Path.GetDirectoryName(Path.GetFullPath(manifestPath))!;
        var missing = JsonNode.Parse(File.ReadAllText(manifestPath))!["stages"]!.AsArray()
            .Select(s => (string)s!["code"]!)
            .Where(code => !File.Exists(Path.Combine(root, code)))
            .ToList();
        if (missing.Count > 0)
            throw new CheckFailedException($"{manifestPath} points at code that does not exist: {string.Join(", ", missing)}");
    }

    /// <summary>
    /// Throws unless GRAPH.md is what <c>seamlineage graph</c> generates from the committed manifest and examples, every
    /// link in it resolves, and every stage has examples. The same rules as <c>seamlineage check</c>.
    /// </summary>
    /// <param name="regenerate">The command that regenerates the page; it is also written in the page's header.</param>
    public static void DiagramIsCurrent(string manifestPath, string fixturesDir, string graphMdPath, string regenerate = "seamlineage graph")
    {
        var problems = GraphDocs.Check(manifestPath, fixturesDir, graphMdPath, regenerate);
        if (problems.Count > 0) throw new CheckFailedException(string.Join("\n", problems));
    }
}
