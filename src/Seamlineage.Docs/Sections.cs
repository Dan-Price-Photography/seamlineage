using System.Text;
using System.Text.Json.Nodes;
using static Seamlineage.Docs.GraphDiagram;

namespace Seamlineage.Docs;

/// <summary>Title, where the page came from, and how to read the raw diagram text.</summary>
public sealed class HeaderSection : IDiagramSection
{
    public void Write(DiagramContext context, StringBuilder md)
    {
        md.Append($"# {context.Manifest.Graph} graph\n\n");
        md.Append($"Generated from `{context.Options.ManifestFileName}` by `{context.Options.GeneratedBy}`. Do not edit.\n\n");
        md.Append("Boxes are the data passed between stages; arrows are stages.\n\n");
        md.Append("How to read the raw diagram text: a few lines only control the drawing and say nothing about the product. ");
        md.Append("`flowchart LR` draws left to right; `subgraph … end` draws a box around a stage's steps; ");
        md.Append("`direction TB` stacks the steps inside that box top to bottom; `a_1 --> a_2` is an arrow between two ");
        md.Append("steps (the `_1`, `_2` names are internal labels).\n\n");
    }
}

/// <summary>
/// The Mermaid flowchart: each edge type is a box, each plain stage a labelled arrow, and each composed stage a box of
/// its steps in order.
/// </summary>
public sealed class FlowchartSection : IDiagramSection
{
    public void Write(DiagramContext context, StringBuilder md)
    {
        var manifest = context.Manifest;
        md.Append("```mermaid\nflowchart LR\n");
        // Mermaid "markdown strings" ("`...`"): **bold**, real line breaks, automatic wrapping, and no HTML or entity
        // escapes, so the raw file reads cleanly in a diff with one field per line.
        foreach (var type in manifest.EdgeTypes)
        {
            var fields = manifest.Types[type] is JsonObject shape
                ? shape.Select(f => $"\n{f.Key}: {(string)f.Value!}")
                : [];
            md.Append($"    {type}[\"`**{type}**{string.Concat(fields)}`\"]\n");
        }
        foreach (var stage in manifest.Stages)
        {
            var label = $"**{(string)stage["name"]!}**\n{Plain((string)stage["description"]!)}";
            if (stage["steps"] is JsonArray steps)
            {
                // A composed stage: the arrow leads into a box of its steps, then on to its output.
                var id = NodeId((string)stage["name"]!);
                md.Append($"    subgraph {id}[\"`**{(string)stage["name"]!}**`\"]\n");
                md.Append("        direction TB\n");
                for (var i = 0; i < steps.Count; i++)
                {
                    var parameters = steps[i]!["parameters"]!.AsObject()
                        .Select(p => $"\n{p.Key}: {Plain((string)p.Value!)}");
                    md.Append($"        {id}_{i + 1}[\"`**{(string)steps[i]!["operator"]!}**{string.Concat(parameters)}`\"]\n");
                }
                for (var i = 1; i < steps.Count; i++)
                    md.Append($"        {id}_{i} --> {id}_{i + 1}\n");
                md.Append("    end\n");
                md.Append($"    {(string)stage["input"]!} -->|\"`{label}`\"| {id}\n");
                md.Append($"    {id} --> {(string)stage["output"]!}\n");
            }
            else
            {
                md.Append($"    {(string)stage["input"]!} -->|\"`{label}`\"| {(string)stage["output"]!}\n");
            }
        }
        md.Append("```\n");
    }
}

/// <summary>Each stage with what it decides, a link to its code, and a link to its examples with their count.</summary>
public sealed class StagesSection : IDiagramSection
{
    public void Write(DiagramContext context, StringBuilder md)
    {
        md.Append("\n## Stages\n\n");
        md.Append("Each arrow above, with the code that implements it and the examples that specify it.\n\n");
        md.Append("| Stage | What it decides | Code | Examples |\n|---|---|---|---|\n");
        foreach (var stage in context.Manifest.Stages)
        {
            var name = (string)stage["name"]!;
            var code = (string)stage["code"]!;
            var examples = context.FixtureCases.TryGetValue(name, out var count) && count > 0
                ? $"[{count} case{(count == 1 ? "" : "s")}]({context.FixturesLink(name)})"
                : "none";
            md.Append($"| **{name}** | {Cell((string)stage["description"]!)} | [{Path.GetFileName(code)}]({context.CodeLink(code)}) | {examples} |\n");
        }
    }
}

/// <summary>The named product-specific decisions each composed stage hands to its operators.</summary>
public sealed class JudgmentsSection : IDiagramSection
{
    public void Write(DiagramContext context, StringBuilder md)
    {
        var composed = context.Manifest.Stages.Where(s => s["judgments"] is JsonObject).ToList();
        if (composed.Count == 0) return;

        md.Append("\n## Judgments\n\n");
        md.Append("The decisions a composed stage makes itself. Everything else in its box above is a generic operator, listed below.\n\n");
        md.Append("| Stage | Judgment | Meaning |\n|---|---|---|\n");
        foreach (var stage in composed)
        foreach (var (name, description) in stage["judgments"]!.AsObject())
            md.Append($"| **{(string)stage["name"]!}** | `{name}` | {Cell((string)description!)} |\n");
    }
}

/// <summary>The generic operators the graph uses, with what each does.</summary>
public sealed class OperatorsSection : IDiagramSection
{
    public void Write(DiagramContext context, StringBuilder md)
    {
        if (context.Manifest.Operators is not { } operators) return;

        md.Append("\n## Operators\n\n");
        md.Append("Generic building blocks, product-agnostic and the same in every stage that uses them.\n\n");
        md.Append("| Operator | What it does |\n|---|---|\n");
        foreach (var (name, description) in operators)
            md.Append($"| **{name}** | {Cell((string)description!)} |\n");
    }
}

/// <summary>Types not drawn as boxes (nested records and enums), one line each.</summary>
public sealed class OtherTypesSection : IDiagramSection
{
    public void Write(DiagramContext context, StringBuilder md)
    {
        var nested = context.Manifest.Types.Where(t => !context.Manifest.EdgeTypes.Contains(t.Key)).ToList();
        if (nested.Count == 0) return;

        md.Append("\n## Other types\n\n");
        foreach (var (name, shape) in nested)
        {
            var body = shape is JsonArray values
                ? string.Join(" | ", values.Select(v => (string)v!))
                : string.Join(", ", shape!.AsObject().Select(f => $"{f.Key}: {(string)f.Value!}"));
            md.Append($"- **{name}**: {body}\n");
        }
    }
}
