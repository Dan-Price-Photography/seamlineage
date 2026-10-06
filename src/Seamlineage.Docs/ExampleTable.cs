using System.Text;
using System.Text.Json.Nodes;

namespace Seamlineage.Docs;

/// <summary>
/// One example case as compact Markdown tables instead of JSON (spec/examples.md). With the stage's declared example
/// view: one row per input item, its columns, then its outcome read from expected.json. Without one: the top-level
/// lists of input.json and expected.json as tables, scalar fields as columns and nested values summarised.
/// </summary>
public static class ExampleTable
{
    /// <param name="view">The stage's <c>exampleView</c> from the manifest; null for the generic view.</param>
    /// <param name="inputJson">The case's input.json; null when the file is missing.</param>
    /// <param name="expectedJson">The case's expected.json; null when the file is missing.</param>
    /// <param name="problems">Collects each way the view does not fit this case.</param>
    public static string Render(JsonNode? view, string? inputJson, string? expectedJson, ICollection<string>? problems = null)
    {
        problems ??= [];
        var input = inputJson is null ? null : JsonNode.Parse(inputJson);
        var expected = expectedJson is null ? null : JsonNode.Parse(expectedJson);
        return view is null
            ? Generic(inputJson is not null, input, expectedJson is not null, expected)
            : Viewed(view, inputJson is not null, input, expected, problems);
    }

    private static string Viewed(JsonNode view, bool hasInput, JsonNode? input, JsonNode? expected, ICollection<string> problems)
    {
        if (!hasInput) return "Input: no input.json.\n";

        var rowsPath = (string?)view["rows"] ?? "";
        if (!DotPath.TryResolve(input, rowsPath, out var rowsNode) || rowsNode is not JsonArray rows)
        {
            problems.Add($"the example view's rows path '{rowsPath}' is not a list in input.json");
            return $"*The example view's rows path `{rowsPath}` is not a list in this case's input.json.*\n";
        }

        var columns = Columns(view["columns"]);
        var outcome = view["outcome"];
        var outcomeColumns = Columns(outcome?["columns"]);
        var groups = new List<(JsonNode Group, List<JsonNode?> Members)>();
        string? note = null;
        if (outcome is not null)
        {
            var from = (string?)outcome["from"] ?? "";
            if (DotPath.TryResolve(expected, from, out var found) && found is JsonArray list)
            {
                var members = (string?)outcome["members"];
                foreach (var group in list.OfType<JsonNode>())
                {
                    var held = members is null ? [group]
                        : DotPath.TryResolve(group, members, out var m) && m is JsonArray array ? array.ToList()
                        : [];
                    groups.Add((group, held));
                }
            }
            else
            {
                problems.Add($"the example view's outcome path '{from}' is not a list in expected.json");
                note = $"*The example view's outcome path `{from}` is not a list in this case's expected.json.*\n";
            }
        }

        var match = (string?)outcome?["match"] ?? "";
        var md = new StringBuilder();
        Header(md, [.. columns.Select(c => c.Label), .. outcomeColumns.Select(c => "→ " + c.Label)]);
        foreach (var row in rows)
        {
            var cells = columns.Select(c => Cell(row, c.Path)).ToList();
            if (outcome is not null)
            {
                var key = DotPath.TryResolve(row, match, out var k) ? k : null;
                var home = groups.FirstOrDefault(g => g.Members.Any(member =>
                    DotPath.TryResolve(member, match, out var other) && JsonNode.DeepEquals(key, other))).Group;
                cells.AddRange(outcomeColumns.Select(c => home is null ? "–" : Cell(home, c.Path)));
            }
            Row(md, cells);
        }
        if (note is not null) md.Append('\n').Append(note);
        return md.ToString();
    }

    private static string Generic(bool hasInput, JsonNode? input, bool hasExpected, JsonNode? expected)
    {
        var blocks = new List<string>();
        blocks.AddRange(hasInput ? Describe("Input", input) : ["Input: no input.json.\n"]);
        blocks.AddRange(hasExpected ? Describe("Expected", expected) : ["Expected: no expected.json.\n"]);
        return string.Join("\n", blocks);
    }

    // A document's top level: each field of an object as its own block, or the whole document as one.
    private static IEnumerable<string> Describe(string title, JsonNode? document) =>
        document is JsonObject fields
            ? fields.Count == 0 ? [$"{title}: empty.\n"] : fields.Select(f => Block($"{title} `{f.Key}`", f.Value))
            : [Block(title, document)];

    private static string Block(string title, JsonNode? value)
    {
        if (value is not JsonArray list) return $"{title}: {Show(value)}\n";
        if (list.Count == 0) return $"{title}: none\n";
        if (!list.All(item => item is JsonObject)) return $"{title}: {string.Join(", ", list.Select(Show))}\n";

        var records = list.Select(item => item!.AsObject()).ToList();
        var keys = records.SelectMany(r => r.Select(f => f.Key)).Distinct(StringComparer.Ordinal).ToList();
        var md = new StringBuilder($"{title}:\n\n");
        Header(md, keys);
        foreach (var record in records)
            Row(md, keys.Select(k => record.TryGetPropertyValue(k, out var v) ? Show(v) : ""));
        return md.ToString();
    }

    private static List<(string Label, string Path)> Columns(JsonNode? columns) =>
        columns is JsonArray list
            ? [.. list.Select(c => ((string?)c?["label"] ?? "", (string?)c?["path"] ?? ""))]
            : [];

    private static void Header(StringBuilder md, IReadOnlyList<string> labels)
    {
        Row(md, labels.Select(Escape));
        md.Append('|').Append(string.Concat(labels.Select(_ => "---|"))).Append('\n');
    }

    private static void Row(StringBuilder md, IEnumerable<string> cells) => md.Append("| ").Append(string.Join(" | ", cells)).Append(" |\n");

    private static string Cell(JsonNode? node, string path) => DotPath.TryResolve(node, path, out var value) ? Show(value) : "";

    /// <summary>A value as one table cell: text as written, numbers and booleans as JSON, nested values summarised.</summary>
    internal static string Show(JsonNode? value) => Escape(value switch
    {
        null => "null",
        JsonArray list => list.Count == 1 ? "1 item" : $"{list.Count} items",
        JsonObject => "{…}",
        JsonValue v when v.TryGetValue<string>(out var text) => text,
        _ => value.ToJsonString(),
    });

    private static string Escape(string text) => text.Replace("|", "\\|").ReplaceLineEndings(" ");
}

/// <summary>
/// The path syntax of example views (spec/examples.md): field names separated by dots, where a segment of digits picks
/// an item of a list by position (from 0); the empty path is the whole document. No wildcards, no expressions.
/// </summary>
public static class DotPath
{
    /// <returns>false when a segment names no field or position; a field that is present but null is found, as null.</returns>
    public static bool TryResolve(JsonNode? node, string path, out JsonNode? value)
    {
        value = node;
        if (path.Length == 0) return true;
        foreach (var segment in path.Split('.'))
        {
            switch (value)
            {
                case JsonObject obj when obj.TryGetPropertyValue(segment, out var child):
                    value = child;
                    break;
                case JsonArray list when segment.Length > 0 && segment.All(char.IsAsciiDigit)
                                         && int.TryParse(segment, out var index) && index < list.Count:
                    value = list[index];
                    break;
                default:
                    value = null;
                    return false;
            }
        }
        return true;
    }
}
