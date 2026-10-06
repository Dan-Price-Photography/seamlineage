using System.Text.Json.Nodes;

namespace Seamlineage.Testing;

/// <summary>
/// The differences between two JSON documents as one line per difference, each naming its path, e.g.
/// <c>$.rows[1].size: expected "small", actual "large"</c>. Key order and whitespace are not differences.
/// </summary>
public static class JsonDiff
{
    public static IReadOnlyList<string> Describe(JsonNode? expected, JsonNode? actual, int max = 20)
    {
        var lines = new List<string>();
        Compare("$", expected, actual, lines);
        return lines.Count <= max ? lines : [.. lines.Take(max), $"... and {lines.Count - max} more"];
    }

    private static void Compare(string path, JsonNode? expected, JsonNode? actual, List<string> lines)
    {
        switch (expected, actual)
        {
            case (JsonObject e, JsonObject a):
                foreach (var (key, value) in e)
                {
                    if (a.ContainsKey(key)) Compare($"{path}.{key}", value, a[key], lines);
                    else lines.Add($"{path}.{key}: missing, expected {Show(value)}");
                }
                foreach (var (key, value) in a.Where(p => !e.ContainsKey(p.Key)))
                    lines.Add($"{path}.{key}: unexpected {Show(value)}");
                break;
            case (JsonArray e, JsonArray a):
                if (e.Count != a.Count) lines.Add($"{path}: expected {Items(e.Count)}, actual {Items(a.Count)}");
                for (var i = 0; i < Math.Min(e.Count, a.Count); i++) Compare($"{path}[{i}]", e[i], a[i], lines);
                break;
            default:
                if (!JsonNode.DeepEquals(expected, actual)) lines.Add($"{path}: expected {Show(expected)}, actual {Show(actual)}");
                break;
        }
    }

    private static string Items(int count) => $"{count} item{(count == 1 ? "" : "s")}";

    private static string Show(JsonNode? node)
    {
        var text = node?.ToJsonString() ?? "null";
        return text.Length <= 80 ? text : text[..77] + "...";
    }
}
