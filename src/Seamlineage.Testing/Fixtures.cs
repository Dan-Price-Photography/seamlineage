using System.Text.Json;
using System.Text.Json.Nodes;
using Seamlineage.Contracts;

namespace Seamlineage.Testing;

/// <summary>One example: <c>fixtures/&lt;stage&gt;/&lt;case&gt;/</c> holding input.json and expected.json.</summary>
public sealed record FixtureCase(string Stage, string Case, string Directory)
{
    public override string ToString() => $"{Stage}/{Case}";
}

/// <summary>
/// Runs examples (spec/examples.md). Every folder under <c>fixtures/&lt;stage&gt;/&lt;case&gt;/</c> is a test: feed
/// input.json to the stage, compare its output to expected.json. Adding a case is adding a folder, no code.
/// </summary>
/// <example>
/// With xUnit:
/// <code>
/// public static TheoryData&lt;string, string&gt; Cases()
/// {
///     var data = new TheoryData&lt;string, string&gt;();
///     foreach (var c in Fixtures.Discover(dir)) data.Add(c.Stage, c.Case);
///     return data;
/// }
///
/// [Theory, MemberData(nameof(Cases))]
/// public void Stage_output_matches_expected(string stage, string @case) =&gt; Fixtures.Verify(graph, dir, stage, @case);
/// </code>
/// </example>
public static class Fixtures
{
    /// <summary>The fixed context every example runs with: stages are pure, so a constant id and clock.</summary>
    public static StageContext Context { get; } = new("fixture", DateTimeOffset.UnixEpoch);

    /// <summary>Every case under the examples folder, ordered by stage then case (ordinal).</summary>
    public static IReadOnlyList<FixtureCase> Discover(string fixturesDir) =>
    [
        .. from stageDir in Directory.GetDirectories(fixturesDir).Order(StringComparer.Ordinal)
           from caseDir in Directory.GetDirectories(stageDir).Order(StringComparer.Ordinal)
           select new FixtureCase(Path.GetFileName(stageDir), Path.GetFileName(caseDir), caseDir),
    ];

    /// <summary>Runs the stage on the case's input.json and returns its output in the wire format.</summary>
    public static string Run(Graph graph, FixtureCase fixture)
    {
        var node = graph.Stages.SingleOrDefault(s => s.Name == fixture.Stage)
            ?? throw new CheckFailedException($"fixtures/{fixture.Stage} does not match any stage in the '{graph.Name}' graph.");
        var input = JsonSerializer.Deserialize(File.ReadAllText(Path.Combine(fixture.Directory, "input.json")), node.InputType, WireJson.Options)!;
        var result = node.Invoke(input, Context);
        return JsonSerializer.Serialize(result.Output, node.OutputType, WireJson.Options);
    }

    /// <summary>Throws <see cref="CheckFailedException"/>, listing each difference by path, unless the stage's output is JSON-equal to expected.json.</summary>
    public static void Verify(Graph graph, FixtureCase fixture)
    {
        var actual = Run(graph, fixture);
        var expected = File.ReadAllText(Path.Combine(fixture.Directory, "expected.json"));
        var (e, a) = (JsonNode.Parse(expected), JsonNode.Parse(actual));
        if (JsonNode.DeepEquals(e, a)) return;

        var differences = string.Join("\n", JsonDiff.Describe(e, a).Select(d => "  " + d));
        throw new CheckFailedException(
            $"{fixture} output differs from expected.json:\n{differences}\n--- expected\n{expected.TrimEnd()}\n--- actual\n{actual}");
    }

    public static void Verify(Graph graph, string fixturesDir, string stage, string @case) =>
        Verify(graph, new FixtureCase(stage, @case, Path.Combine(fixturesDir, stage, @case)));

    /// <summary>Throws unless every stage of the graph has at least one case, and every stage folder names a stage.</summary>
    public static void EveryStageHasExamples(Graph graph, string fixturesDir)
    {
        var stages = graph.Stages.Select(s => s.Name).ToList();
        var problems = Docs.GraphDocs.StagesWithoutExamples(stages, fixturesDir).Select(s => $"Stage '{s}' has no examples under {fixturesDir}.")
            .Concat(Docs.GraphDocs.FoldersWithoutStage(stages, fixturesDir).Select(f => $"{fixturesDir}/{f} does not match any stage in the graph."))
            .ToList();
        if (problems.Count > 0) throw new CheckFailedException(string.Join("\n", problems));
    }
}
