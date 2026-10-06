using Seamlineage.Contracts;

namespace Seamlineage.Tests;

public class GraphTests
{
    [Fact]
    public void Builds_a_linear_graph_of_named_stages_in_order()
    {
        var graph = Garden.Define();

        Assert.Equal("garden", graph.Name);
        Assert.Equal(typeof(Garden.Packet), graph.InputType);
        Assert.Equal(["sow", "count"], graph.Stages.Select(s => s.Name));
        Assert.Equal(typeof(Garden.Bed), graph.Stages[0].OutputType);
        Assert.Equal(typeof(Garden.Bed), graph.Stages[1].InputType);
        Assert.Equal(2, graph.Stages[1].SchemaVersion);
    }

    [Fact]
    public void Invokes_a_stage_through_its_type_erased_node()
    {
        var node = Garden.Define().Stages[0];

        var result = node.Invoke(Garden.Sample, new StageContext("test", DateTimeOffset.UnixEpoch));

        var bed = Assert.IsType<Garden.Bed>(result.Output);
        Assert.Equal([Garden.Size.Large, Garden.Size.Small], bed.Rows.Select(r => r.Size));
        Assert.Empty(result.Effects);
    }

    [Fact]
    public void A_plain_stage_has_no_steps()
    {
        Assert.All(Garden.Define().Stages, s => Assert.Null(s.Steps));
    }

    [Fact]
    public void Wire_json_uses_camel_case_names_and_enum_values()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new Garden.Sown("pea", Garden.Size.Large), WireJson.Options);

        Assert.Contains("\"name\": \"pea\"", json);
        Assert.Contains("\"size\": \"large\"", json);
    }
}
