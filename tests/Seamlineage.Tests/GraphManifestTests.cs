using System.Text.Json.Nodes;
using Seamlineage.Contracts;

namespace Seamlineage.Tests;

public class GraphManifestTests
{
    private enum Shade { Light, DarkGrey }

    private sealed record Leaf(string Name, Shade Shade, DateTimeOffset At, long? Size, DateTime Local, DateTime? Seen);

    private sealed record Tree(IReadOnlyList<Leaf> Leaves, Leaf? Top, int Count);

    private sealed class Grow : IStage<Leaf, Tree>
    {
        public StageResult<Tree> Run(Leaf input, StageContext context) => throw new NotSupportedException();
    }

    private static JsonNode Describe() =>
        JsonNode.Parse(GraphManifest.Generate(
            Graph.Start<Leaf>("test").Then("grow", "Leaf → tree.", new Grow(), schemaVersion: 2).Build()))!;

    [Fact]
    public void Names_the_graph_and_its_input_type()
    {
        var manifest = Describe();

        Assert.Equal("test", (string?)manifest["graph"]);
        Assert.Equal("Leaf", (string?)manifest["input"]);
    }

    [Fact]
    public void Lists_stages_in_order_with_their_contract_and_purpose()
    {
        var stage = Describe()["stages"]!.AsArray().Single()!;

        Assert.Equal("grow", (string?)stage["name"]);
        Assert.Equal("Leaf → tree.", (string?)stage["description"]);
        Assert.Equal("Leaf", (string?)stage["input"]);
        Assert.Equal("Tree", (string?)stage["output"]);
        Assert.Equal(2, (int?)stage["schemaVersion"]);
    }

    [Fact]
    public void Points_each_stage_at_the_source_file_of_its_class_by_convention()
    {
        var stages = JsonNode.Parse(GraphManifest.Generate(Garden.Define()))!["stages"]!.AsArray();

        Assert.Equal("src/Seamlineage.Tests/Sow.cs", (string?)stages[0]!["code"]);
        Assert.Equal("src/Seamlineage.Tests/Count.cs", (string?)stages[1]!["code"]);
    }

    [Fact]
    public void A_graph_can_place_its_code_somewhere_other_than_the_convention()
    {
        var manifest = GraphManifest.Generate(Garden.Define(), type => $"lib/{type.Name.ToLowerInvariant()}.cs");

        Assert.Equal("lib/sow.cs", (string?)JsonNode.Parse(manifest)!["stages"]![0]!["code"]);
    }

    [Fact]
    public void Describes_every_reachable_type_with_wire_names()
    {
        var types = Describe()["types"]!;

        Assert.Equal("""{"name":"string","shade":"Shade","at":"datetime","size":"long?","local":"localdatetime","seen":"localdatetime?"}""", types["Leaf"]!.ToJsonString());
        Assert.Equal("""{"leaves":"Leaf[]","top":"Leaf?","count":"int"}""", types["Tree"]!.ToJsonString());
        Assert.Equal("""["light","darkGrey"]""", types["Shade"]!.ToJsonString());
    }

    [Fact]
    public void Sorts_types_by_name_ordinally()
    {
        var names = Describe()["types"]!.AsObject().Select(t => t.Key).ToList();

        Assert.Equal(names.Order(StringComparer.Ordinal), names);
    }

    private sealed class Prune : IStage<Tree, Tree>, IComposedStage
    {
        public IReadOnlyList<StepInfo> Steps { get; } =
        [
            new("group-by", "Splits items into groups by key.", [new("key", "shade")], [new("shade", "How dark the leaf is.")]),
            new("partition-by-size", "Keeps big groups.", [new("minimum", "3")], []),
            new("order-by", "Sorts each group.", [new("by", "shade, name")], [new("shade", "How dark the leaf is."), new("name", "The name of the leaf.")]),
        ];

        public StageResult<Tree> Run(Tree input, StageContext context) => throw new NotSupportedException();
    }

    private static JsonNode DescribeComposed() =>
        JsonNode.Parse(GraphManifest.Generate(
            Graph.Start<Leaf>("test").Then("grow", "Leaf → tree.", new Grow()).Then("prune", "Tree → smaller tree.", new Prune()).Build()))!;

    [Fact]
    public void Lists_the_steps_of_a_composed_stage_in_order_with_their_parameters()
    {
        var stage = DescribeComposed()["stages"]![1]!;

        Assert.Equal(
            """[{"operator":"group-by","parameters":{"key":"shade"}},{"operator":"partition-by-size","parameters":{"minimum":"3"}},{"operator":"order-by","parameters":{"by":"shade, name"}}]""",
            stage["steps"]!.ToJsonString());
    }

    [Fact]
    public void Lists_each_judgment_a_composed_stage_uses_once_with_its_meaning()
    {
        var stage = DescribeComposed()["stages"]![1]!;

        Assert.Equal("""{"shade":"How dark the leaf is.","name":"The name of the leaf."}""", stage["judgments"]!.ToJsonString());
    }

    [Fact]
    public void Describes_each_operator_the_graph_uses_once()
    {
        var operators = DescribeComposed()["operators"]!;

        Assert.Equal(
            """{"group-by":"Splits items into groups by key.","order-by":"Sorts each group.","partition-by-size":"Keeps big groups."}""",
            operators.ToJsonString());
    }

    [Fact]
    public void Plain_stages_have_no_steps_and_a_graph_without_composed_stages_lists_no_operators()
    {
        var manifest = DescribeComposed();
        var plain = manifest["stages"]![0]!.AsObject();

        Assert.False(plain.ContainsKey("steps"));
        Assert.False(plain.ContainsKey("judgments"));
        Assert.False(Describe().AsObject().ContainsKey("operators"));
    }

    [Fact]
    public void Is_deterministic_with_unix_line_endings_and_a_final_newline()
    {
        var first = GraphManifest.Generate(Garden.Define());

        Assert.Equal(first, GraphManifest.Generate(Garden.Define()));
        Assert.DoesNotContain("\r", first);
        Assert.EndsWith("}\n", first);
    }
}
