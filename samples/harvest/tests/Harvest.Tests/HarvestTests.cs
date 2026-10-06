using System.Text.Json;
using System.Text.Json.Nodes;
using Seamlineage.Contracts;
using Seamlineage.Testing;

namespace Harvest.Tests;

/// <summary>The sample's whole loop, as any product using Seamlineage would test it.</summary>
public class HarvestTests
{
    private static readonly string Root = RepoPaths.FindAbove("src/Harvest/Harvest.csproj");
    private static readonly string FixturesDir = Path.Combine(Root, "fixtures");
    private static readonly Graph Graph = HarvestGraph.Define();

    private const string RegenerateManifest = "dotnet run --project src/Harvest.Host -- manifest";

    public static TheoryData<string, string> Cases()
    {
        var data = new TheoryData<string, string>();
        foreach (var c in Fixtures.Discover(FixturesDir)) data.Add(c.Stage, c.Case);
        return data;
    }

    /// <summary>Every folder under fixtures/&lt;stage&gt;/&lt;case&gt;/ is a test. Adding a case is adding a folder.</summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void Stage_output_matches_expected(string stage, string @case) => Fixtures.Verify(Graph, FixturesDir, stage, @case);

    [Fact]
    public void Every_stage_has_at_least_one_example() => Fixtures.EveryStageHasExamples(Graph, FixturesDir);

    [Fact]
    public void Committed_manifest_matches_the_graph() =>
        GeneratedFiles.ManifestIsCurrent(Graph, Path.Combine(Root, "graph.manifest.json"), RegenerateManifest);

    [Fact]
    public void Every_stage_points_at_code_that_exists() => GeneratedFiles.CodePathsExist(Path.Combine(Root, "graph.manifest.json"));

    [Fact]
    public void Committed_diagram_is_current_and_its_links_resolve() =>
        GeneratedFiles.DiagramIsCurrent(Path.Combine(Root, "graph.manifest.json"), FixturesDir, Path.Combine(Root, "GRAPH.md"));

    /// <summary>The product may not know how it is deployed: base class library and Seamlineage's product-facing libraries only.</summary>
    [Fact]
    public void The_product_references_only_the_base_class_library_and_seamlineage_contracts_and_operators() =>
        Architecture.ReferencesOnlyBaseClassLibraryAnd(typeof(HarvestGraph).Assembly, "Seamlineage.Contracts", "Seamlineage.Operators");
}

/// <summary>
/// group-baskets only groups: every accepted pick comes out exactly once, unchanged, in one group; a basket has 3 or
/// more picks and a loose group exactly one. Checked for every group-baskets example, on both the stage's actual
/// output and expected.json.
/// </summary>
public class GroupBasketsInvariantTests
{
    private const string Stage = "group-baskets";
    private static readonly string FixturesDir = Path.Combine(RepoPaths.FindAbove("src/Harvest/Harvest.csproj"), "fixtures");

    public static TheoryData<string> Cases()
    {
        var data = new TheoryData<string>();
        foreach (var c in Fixtures.Discover(FixturesDir).Where(c => c.Stage == Stage)) data.Add(c.Case);
        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Every_accepted_pick_appears_exactly_once_in_one_group(string @case)
    {
        var (input, expected, actual) = Load(@case);

        var picks = Canonical(input.Accepted);
        Assert.Equal(picks, Canonical(actual.Groups.SelectMany(g => g.Picks)));
        Assert.Equal(picks, Canonical(expected.Groups.SelectMany(g => g.Picks)));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void A_basket_has_at_least_3_picks_and_a_loose_group_exactly_1(string @case)
    {
        var (_, expected, actual) = Load(@case);

        foreach (var output in new[] { expected, actual })
        foreach (var group in output.Groups)
        {
            Assert.True(
                group.Kind switch { BasketKind.Basket => group.Picks.Count >= 3, BasketKind.Loose => group.Picks.Count == 1, _ => false },
                $"{@case}: {group.Kind} group {group.Key} has {group.Picks.Count} picks");
            Assert.Equal(group.Picks[0].Id, group.Key);
        }
    }

    private static (CheckedPicks Input, Baskets Expected, Baskets Actual) Load(string @case)
    {
        var dir = Path.Combine(FixturesDir, Stage, @case);
        var input = JsonSerializer.Deserialize<CheckedPicks>(File.ReadAllText(Path.Combine(dir, "input.json")), WireJson.Options)!;
        var expected = JsonSerializer.Deserialize<Baskets>(File.ReadAllText(Path.Combine(dir, "expected.json")), WireJson.Options)!;
        var actual = new GroupBaskets().Run(input, Fixtures.Context).Output;
        return (input, expected, actual);
    }

    // Whole pick as JSON, sorted: equal lists mean no duplicates, none lost, none altered.
    private static List<string> Canonical(IEnumerable<Pick> picks) =>
        picks.Select(p => JsonNode.Parse(JsonSerializer.Serialize(p, WireJson.Options))!.ToJsonString())
            .Order(StringComparer.Ordinal)
            .ToList();
}
