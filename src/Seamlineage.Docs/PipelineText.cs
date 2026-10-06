using System.Text;
using System.Text.Json.Nodes;

namespace Seamlineage.Docs;

/// <summary>
/// A composed stage's steps in plain words, one line per step, rendered from the manifest alone (spec/diagram.md):
/// <code>
/// accepted picks
/// | group by row
/// | order each group by picked-at, then id
/// </code>
/// Each line is the operator's phrase template filled from the step's parameters; judgment names stay names.
/// </summary>
public static class PipelineText
{
    /// <summary>The stage's pipeline text, lines separated by \n; null for a plain stage.</summary>
    public static string? Render(ManifestView manifest, JsonNode stage)
    {
        if (stage["steps"] is not JsonArray steps) return null;

        var lines = new List<string> { (string?)stage["items"] ?? "items" };
        foreach (var step in steps)
        {
            var name = (string)step!["operator"]!;
            var parameters = (step["parameters"] as JsonObject ?? [])
                .Select(p => KeyValuePair.Create(p.Key, (string?)p.Value ?? ""))
                .ToList();
            lines.Add("| " + Phrase(name, manifest.OperatorPhrase(name), parameters));
        }
        return string.Join("\n", lines);
    }

    /// <summary>
    /// Fills a phrase template: <c>{name}</c> is the parameter's value as written; <c>[...]</c> is kept only when every
    /// placeholder in it has a parameter; <c>{{ }} [[ ]]</c> are literal braces and brackets. A placeholder with no
    /// parameter outside <c>[...]</c> stays visible as written. Parameters the template does not mention are added in
    /// brackets, so nothing is hidden. Without a template the line is the operator's name and its parameters.
    /// </summary>
    public static string Phrase(string @operator, string? template, IReadOnlyList<KeyValuePair<string, string>> parameters)
    {
        var values = parameters.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var used = new HashSet<string>(StringComparer.Ordinal);
        var text = new StringBuilder();
        var optional = (StringBuilder?)null;
        var optionalComplete = true;
        var optionalUsed = new HashSet<string>(StringComparer.Ordinal); // counted as shown only if the part is kept

        template ??= @operator;
        for (var i = 0; i < template.Length; i++)
        {
            var c = template[i];
            var target = optional ?? text;
            if (c is '{' or '}' or '[' or ']' && i + 1 < template.Length && template[i + 1] == c)
            {
                target.Append(c);
                i++;
            }
            else if (c == '{' && template.IndexOf('}', i) is var close and > 0)
            {
                var name = template[(i + 1)..close];
                if (values.TryGetValue(name, out var value))
                {
                    (optional is null ? used : optionalUsed).Add(name);
                    target.Append(value);
                }
                else
                {
                    optionalComplete = false;
                    target.Append('{').Append(name).Append('}');
                }
                i = close;
            }
            else if (c == '[' && optional is null)
            {
                optional = new StringBuilder();
                optionalComplete = true;
                optionalUsed.Clear();
            }
            else if (c == ']' && optional is not null)
            {
                Close();
            }
            else
            {
                target.Append(c);
            }
        }
        if (optional is not null) Close(); // an unclosed part runs to the end

        var rest = parameters.Where(p => !used.Contains(p.Key)).Select(p => $"{p.Key}: {p.Value}").ToList();
        if (rest.Count > 0) text.Append(" (").Append(string.Join(", ", rest)).Append(')');
        return text.ToString();

        void Close()
        {
            if (optionalComplete)
            {
                text.Append(optional);
                used.UnionWith(optionalUsed);
            }
            optional = null;
        }
    }
}
