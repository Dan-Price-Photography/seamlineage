using System.Text;
using System.Text.Json.Nodes;
using static Seamlineage.Docs.GraphDiagram;

namespace Seamlineage.Docs;

/// <summary>One example case of a stage: its folder name and the text of its files (null when a file is missing).</summary>
public sealed record ExampleCase(string Name, string? InputJson, string? ExpectedJson);

/// <summary>
/// The detail page of one stage, <c>graph/&lt;stage&gt;.md</c> (spec/diagram.md): what it decides, its box drawn as in
/// GRAPH.md, how it decides (pipeline text and judgments) and what it decides on each example (a table per case).
/// Rendered from the manifest and the examples only, so any implementation language can use it.
/// </summary>
public static class StagePage
{
    /// <param name="cases">The stage's example cases, in the order to show them.</param>
    /// <param name="options">Links relative to the page; defaults to <see cref="DiagramOptions.ForStagePage"/>.</param>
    /// <param name="problems">Collects each case the stage's example view does not fit, as <c>stage/case: problem</c>.</param>
    public static string FromManifest(
        string manifestJson,
        string stage,
        IReadOnlyList<ExampleCase> cases,
        DiagramOptions? options = null,
        ICollection<string>? problems = null)
    {
        var manifest = ManifestView.Parse(manifestJson);
        var node = manifest.Stage(stage);
        var context = new DiagramContext(manifest, new Dictionary<string, int> { [stage] = cases.Count }, options ?? DiagramOptions.ForStagePage);
        var md = new StringBuilder();

        Header(context, node, md);
        Drawing(manifest, node, md);
        Pipeline(manifest, node, md);
        Examples(context, node, cases, md, problems);
        return md.ToString();
    }

    private static void Header(DiagramContext context, JsonNode stage, StringBuilder md)
    {
        var name = (string)stage["name"]!;
        var code = (string)stage["code"]!;
        md.Append($"# {name}\n\n");
        md.Append($"Generated from `{context.Options.ManifestFileName}` by `{context.Options.GeneratedBy}`. Do not edit. ");
        md.Append($"Part of the [{context.Manifest.Graph} graph]({context.Options.GraphLink}).\n\n");
        md.Append($"{(string)stage["description"]!}\n\n");

        var count = context.FixtureCases[name];
        var examples = count > 0 ? $"[{count} case{(count == 1 ? "" : "s")}]({context.FixturesLink(name)})" : "none";
        md.Append("| | |\n|---|---|\n");
        md.Append($"| Input | `{(string)stage["input"]!}` |\n");
        md.Append($"| Output | `{(string)stage["output"]!}` |\n");
        md.Append($"| Code | [{Path.GetFileName(code)}]({context.CodeLink(code)}) |\n");
        md.Append($"| Examples | {examples} |\n");
    }

    private static void Drawing(ManifestView manifest, JsonNode stage, StringBuilder md)
    {
        md.Append("\n## Diagram\n\n");
        md.Append(stage["steps"] is JsonArray
            ? "The stage's input and output, and its steps in order (drawn as in GRAPH.md).\n\n"
            : "The stage's input and output (drawn as in GRAPH.md). A plain stage: one block of code.\n\n");
        string[] types = [.. new[] { (string)stage["input"]!, (string)stage["output"]! }.Distinct()];
        FlowchartSection.Draw(manifest, types, [stage], md);
    }

    private static void Pipeline(ManifestView manifest, JsonNode stage, StringBuilder md)
    {
        if (PipelineText.Render(manifest, stage) is not { } text) return;

        md.Append("\n## Pipeline\n\n");
        md.Append("How the stage decides, one step per line, from its operators' phrases. The names in it are judgments, listed below.\n\n");
        md.Append("```text\n").Append(text).Append("\n```\n");

        if (stage["judgments"] is JsonObject { Count: > 0 } judgments)
        {
            md.Append("\n## Judgments\n\n");
            md.Append("The decisions the stage makes itself; everything else in the pipeline is a generic operator.\n\n");
            md.Append("| Judgment | Meaning |\n|---|---|\n");
            foreach (var (name, meaning) in judgments)
                md.Append($"| `{name}` | {Cell((string)meaning!)} |\n");
        }
    }

    private static void Examples(DiagramContext context, JsonNode stage, IReadOnlyList<ExampleCase> cases, StringBuilder md, ICollection<string>? problems)
    {
        var name = (string)stage["name"]!;
        md.Append("\n## Examples\n\n");
        if (cases.Count == 0)
        {
            md.Append("None yet.\n");
            return;
        }

        var view = stage["exampleView"];
        md.Append(view is not null
            ? $"What the stage decides on each example. Each row is one item of `{(string?)view["rows"] ?? ""}` in the case's input.json; "
              + "columns marked → are its outcome, read from expected.json (– when no output holds it).\n"
            : "This stage declares no example view, so each case shows the top-level lists of its input.json and "
              + "expected.json as tables, with nested values summarised.\n");

        foreach (var example in cases)
        {
            md.Append($"\n### [{example.Name}]({context.CaseLink(name, example.Name)})\n\n");
            var found = new List<string>();
            md.Append(ExampleTable.Render(view, example.InputJson, example.ExpectedJson, found));
            foreach (var problem in found) problems?.Add($"{name}/{example.Name}: {problem}");
        }
    }
}
