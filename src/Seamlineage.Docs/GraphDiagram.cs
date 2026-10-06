using System.Text;

namespace Seamlineage.Docs;

/// <summary>How a generated page names its source and links to code and examples.</summary>
public sealed record DiagramOptions
{
    /// <summary>The manifest's file name, as the page's header names it.</summary>
    public string ManifestFileName { get; init; } = "graph.manifest.json";

    /// <summary>The command that regenerates the page, as the page's header names it.</summary>
    public string GeneratedBy { get; init; } = "seamlineage graph";

    /// <summary>The examples folder (fixtures/), relative to the page.</summary>
    public string FixturesLink { get; init; } = "fixtures";

    /// <summary>The directory stage <c>code</c> paths are relative to (the manifest's), relative to the page. Empty: the same directory.</summary>
    public string CodeRoot { get; init; } = "";

    /// <summary>The folder of the stage pages (graph/), relative to GRAPH.md.</summary>
    public string PagesLink { get; init; } = "graph";

    /// <summary>GRAPH.md, relative to a stage page.</summary>
    public string GraphLink { get; init; } = "../GRAPH.md";

    /// <summary>The options of a stage page in graph/ beside GRAPH.md, the manifest and fixtures/.</summary>
    public static DiagramOptions ForStagePage { get; } = new() { CodeRoot = "..", FixturesLink = "../fixtures" };
}

/// <summary>Everything a section of the page may draw on.</summary>
public sealed record DiagramContext(ManifestView Manifest, IReadOnlyDictionary<string, int> FixtureCases, DiagramOptions Options)
{
    public string CodeLink(string code) => CodeRoot(Options.CodeRoot) + code;

    public string FixturesLink(string stage) => Prefix(Options.FixturesLink) + stage + "/";

    public string CaseLink(string stage, string @case) => FixturesLink(stage) + @case + "/";

    public string PageLink(string stage) => Prefix(Options.PagesLink) + stage + ".md";

    private static string CodeRoot(string root) => Prefix(root);

    private static string Prefix(string dir) => dir is "" or "." ? "" : dir.TrimEnd('/') + "/";
}

/// <summary>
/// One part of the generated page. The page is its sections in order, so a new view (a pipeline text per composed
/// stage, example tables per case, ...) is a new section, and existing sections stay byte-for-byte the same.
/// </summary>
public interface IDiagramSection
{
    void Write(DiagramContext context, StringBuilder markdown);
}

/// <summary>
/// Renders graph.manifest.json as Markdown with a Mermaid flowchart (GitHub draws it in files and PR diffs).
/// Built from the manifest rather than the code, so the picture can never disagree with what reviewers diff, and any
/// implementation language can use it.
/// </summary>
public static class GraphDiagram
{
    /// <summary>The sections of GRAPH.md, in order.</summary>
    public static IReadOnlyList<IDiagramSection> DefaultSections { get; } =
    [
        new HeaderSection(),
        new FlowchartSection(),
        new StagesSection(),
        new JudgmentsSection(),
        new OperatorsSection(),
        new OtherTypesSection(),
    ];

    /// <param name="fixtureCases">Number of example cases per stage name; the caller counts them on disk.</param>
    /// <param name="sections">The sections to write; defaults to <see cref="DefaultSections"/>.</param>
    public static string FromManifest(
        string manifestJson,
        IReadOnlyDictionary<string, int>? fixtureCases = null,
        DiagramOptions? options = null,
        IReadOnlyList<IDiagramSection>? sections = null)
    {
        var context = new DiagramContext(
            ManifestView.Parse(manifestJson),
            fixtureCases ?? new Dictionary<string, int>(),
            options ?? new DiagramOptions());
        var markdown = new StringBuilder();
        foreach (var section in sections ?? DefaultSections)
            section.Write(context, markdown);
        return markdown.ToString();
    }

    // Mermaid ids: stage names are kebab-case, and '-' in an id can be read as part of an arrow.
    internal static string NodeId(string name) => string.Concat(name.Select(c => char.IsAsciiLetterOrDigit(c) ? c : '_'));

    // Inside a markdown string only the delimiters can break the label: a double quote ends the string and a backtick
    // ends the markdown. Both become a single quote; everything else (; _ * etc.) is safe as written.
    internal static string Plain(string text) => text.Replace('"', '\'').Replace('`', '\'');

    // A pipe would end a Markdown table cell.
    internal static string Cell(string text) => text.Replace("|", "\\|");
}
