using System.Text.Json.Nodes;

namespace Seamlineage.Docs;

/// <summary>
/// A parsed graph.manifest.json (spec/manifest.md), read as JSON only: no implementation's assemblies are needed, so
/// a manifest from any language can be drawn.
/// </summary>
public sealed class ManifestView
{
    private ManifestView(JsonNode root, IReadOnlyList<JsonNode> stages, JsonObject types)
    {
        Root = root;
        Stages = stages;
        Types = types;

        var edgeTypes = new List<string> { Input };
        edgeTypes.AddRange(stages.Select(s => (string)s["output"]!).Where(t => !edgeTypes.Contains(t)));
        EdgeTypes = edgeTypes;
    }

    public JsonNode Root { get; }

    public string Graph => (string)Root["graph"]!;

    public string Input => (string)Root["input"]!;

    /// <summary>Stages in execution order.</summary>
    public IReadOnlyList<JsonNode> Stages { get; }

    /// <summary>Every type reachable from an edge: name → record shape (object) or enum values (array).</summary>
    public JsonObject Types { get; }

    /// <summary>Generic operators used by composed stages: name → description; null when the graph uses none.</summary>
    public JsonObject? Operators => Root["operators"] as JsonObject;

    /// <summary>The types on edges, in graph order: the graph's input, then each new stage output.</summary>
    public IReadOnlyList<string> EdgeTypes { get; }

    public static ManifestView Parse(string manifestJson)
    {
        var root = JsonNode.Parse(manifestJson) ?? throw new InvalidDataException("The manifest is empty.");
        foreach (var field in new[] { "graph", "input", "stages", "types" })
            if (root[field] is null)
                throw new InvalidDataException($"The manifest has no '{field}' field (see spec/manifest.md).");

        var stages = root["stages"]!.AsArray().Select(s => s!).ToList();
        foreach (var stage in stages)
        foreach (var field in new[] { "name", "description", "input", "output", "code" })
            if (stage[field] is null)
                throw new InvalidDataException($"A stage in the manifest has no '{field}' field (see spec/manifest.md).");

        return new ManifestView(root, stages, root["types"]!.AsObject());
    }
}
