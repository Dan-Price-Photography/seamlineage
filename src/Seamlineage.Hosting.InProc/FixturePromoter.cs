using System.Text.Json;
using System.Text.Json.Nodes;
using Seamlineage.Contracts;

namespace Seamlineage.Hosting.InProc;

/// <summary>
/// Turns one recorded run into an example: the stage's incoming edge becomes input.json, its outgoing edge
/// expected.json. A promoted expected.json records what the code did, which is only a regression guard until a person
/// agrees it is what the code should do.
/// </summary>
public static class FixturePromoter
{
    private static readonly JsonSerializerOptions Output = new(WireJson.Options) { NewLine = "\n" };

    public static void Promote(string recordingDir, string stage, string caseName, string fixturesRoot)
    {
        var edges = Directory.GetFiles(recordingDir, "*.jsonl").Order(StringComparer.Ordinal).ToList();
        var outIndex = edges.FindIndex(path => Path.GetFileNameWithoutExtension(path)[3..] == stage);
        if (outIndex < 1)
            throw new ArgumentException($"No recorded output for stage '{stage}' in {recordingDir}", nameof(stage));

        var caseDir = Path.Combine(fixturesRoot, stage, caseName);
        Directory.CreateDirectory(caseDir);
        File.WriteAllText(Path.Combine(caseDir, "input.json"), LastPayload(edges[outIndex - 1]));
        File.WriteAllText(Path.Combine(caseDir, "expected.json"), LastPayload(edges[outIndex]));
    }

    private static string LastPayload(string edgeFile)
    {
        var envelope = JsonNode.Parse(File.ReadLines(edgeFile).Last(line => line.Length > 0))!;
        return envelope["payload"]!.ToJsonString(Output) + "\n";
    }
}
