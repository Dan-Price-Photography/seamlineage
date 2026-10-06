using System.Text.Json.Nodes;
using Seamlineage.Contracts;
using Seamlineage.Hosting.InProc;

namespace Seamlineage.Tests;

public class InProcRunnerTests
{
    [Fact]
    public void Runs_every_stage_and_returns_the_last_output()
    {
        using var dir = new TempDir();

        var output = new InProcRunner(new EdgeRecorder(dir.Path)).Run(Garden.Define(), Garden.Sample, "run-1");

        Assert.Equal(new Garden.Tally(Rows: 2, Large: 1), output);
    }

    [Fact]
    public void Records_one_envelope_per_edge_in_graph_order()
    {
        using var dir = new TempDir();

        new InProcRunner(new EdgeRecorder(dir.Path)).Run(Garden.Define(), Garden.Sample, "run-1");

        Assert.Equal(
            ["00-input.jsonl", "01-sow.jsonl", "02-count.jsonl"],
            Directory.GetFiles(dir.Path).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        var envelope = JsonNode.Parse(File.ReadAllLines(Path.Combine(dir.Path, "01-sow.jsonl")).Single())!;
        Assert.Equal("run-1", (string?)envelope["correlationId"]);
        Assert.Equal("run-1:sow", (string?)envelope["idempotencyKey"]);
        Assert.Null(envelope["orderingKey"]);
        Assert.Equal(1, (int?)envelope["schemaVersion"]);
        Assert.Equal(2, envelope["payload"]!["rows"]!.AsArray().Count);
    }

    [Fact]
    public void Stamps_each_edge_with_its_stage_schema_version()
    {
        using var dir = new TempDir();

        new InProcRunner(new EdgeRecorder(dir.Path)).Run(Garden.Define(), Garden.Sample, "run-1");

        var envelope = JsonNode.Parse(File.ReadAllLines(Path.Combine(dir.Path, "02-count.jsonl")).Single())!;
        Assert.Equal(2, (int?)envelope["schemaVersion"]);
        Assert.Equal("""{"rows":2,"large":1}""", envelope["payload"]!.ToJsonString());
    }

    [Fact]
    public void Gives_stages_the_hosts_clock_and_the_correlation_id()
    {
        using var dir = new TempDir();
        var clock = new FixedClock(new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero));
        var graph = Graph.Start<Garden.Packet>("test").Then("stamp", "Notes when and in which run it ran.", new Stamp()).Build();

        var output = new InProcRunner(new EdgeRecorder(dir.Path), clock).Run(graph, Garden.Sample, "run-7");

        Assert.Equal("run-7 2026-03-04T05:06:07.0000000+00:00", output);
    }

    [Fact]
    public void Refuses_to_continue_when_a_stage_emits_an_effect_no_handler_exists_for()
    {
        using var dir = new TempDir();
        var graph = Graph.Start<Garden.Packet>("test").Then("noisy", "Emits an effect nothing handles.", new EmitsEffect()).Build();

        var ex = Assert.Throws<InvalidOperationException>(
            () => new InProcRunner(new EdgeRecorder(dir.Path)).Run(graph, Garden.Sample, "run-1"));

        Assert.Contains(nameof(UnhandledEffect), ex.Message);
    }

    private sealed record UnhandledEffect : Effect;

    private sealed class EmitsEffect : IStage<Garden.Packet, Garden.Packet>
    {
        public StageResult<Garden.Packet> Run(Garden.Packet input, StageContext context) => new(input, [new UnhandledEffect()]);
    }

    private sealed class Stamp : IStage<Garden.Packet, string>
    {
        public StageResult<string> Run(Garden.Packet input, StageContext context) =>
            StageResult<string>.Pure($"{context.CorrelationId} {context.Now:O}");
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

public class FixturePromoterTests
{
    [Fact]
    public void Turns_a_stages_recorded_input_and_output_into_a_fixture_case()
    {
        using var recordings = new TempDir();
        using var fixtures = new TempDir();
        new InProcRunner(new EdgeRecorder(recordings.Path)).Run(Garden.Define(), Garden.Sample, "run-1");

        FixturePromoter.Promote(recordings.Path, "count", "from-recording", fixtures.Path);

        var caseDir = Path.Combine(fixtures.Path, "count", "from-recording");
        var recordedInput = JsonNode.Parse(File.ReadAllLines(Path.Combine(recordings.Path, "01-sow.jsonl")).Last())!["payload"];
        var recordedOutput = JsonNode.Parse(File.ReadAllLines(Path.Combine(recordings.Path, "02-count.jsonl")).Last())!["payload"];
        Assert.True(JsonNode.DeepEquals(recordedInput, JsonNode.Parse(File.ReadAllText(Path.Combine(caseDir, "input.json")))));
        Assert.True(JsonNode.DeepEquals(recordedOutput, JsonNode.Parse(File.ReadAllText(Path.Combine(caseDir, "expected.json")))));
    }

    [Fact]
    public void A_promoted_case_passes_as_an_example()
    {
        using var recordings = new TempDir();
        using var fixtures = new TempDir();
        new InProcRunner(new EdgeRecorder(recordings.Path)).Run(Garden.Define(), Garden.Sample, "run-1");

        FixturePromoter.Promote(recordings.Path, "sow", "from-recording", fixtures.Path);

        Seamlineage.Testing.Fixtures.Verify(Garden.Define(), fixtures.Path, "sow", "from-recording");
    }

    [Fact]
    public void Promotes_the_first_stage_from_the_graph_input()
    {
        using var recordings = new TempDir();
        using var fixtures = new TempDir();
        new InProcRunner(new EdgeRecorder(recordings.Path)).Run(Garden.Define(), Garden.Sample, "run-1");

        FixturePromoter.Promote(recordings.Path, "sow", "first", fixtures.Path);

        var input = JsonNode.Parse(File.ReadAllText(Path.Combine(fixtures.Path, "sow", "first", "input.json")))!;
        Assert.Equal("pea", (string?)input["seeds"]![0]!["name"]);
    }

    [Fact]
    public void Refuses_a_stage_the_recording_does_not_have()
    {
        using var recordings = new TempDir();
        using var fixtures = new TempDir();
        new InProcRunner(new EdgeRecorder(recordings.Path)).Run(Garden.Define(), Garden.Sample, "run-1");

        Assert.Throws<ArgumentException>(() => FixturePromoter.Promote(recordings.Path, "prune", "x", fixtures.Path));
        Assert.Throws<ArgumentException>(() => FixturePromoter.Promote(recordings.Path, "input", "x", fixtures.Path));
    }
}

public class InProcHostTests
{
    [Fact]
    public void Manifest_writes_the_graphs_manifest()
    {
        using var dir = new TempDir();
        var path = Path.Combine(dir.Path, "graph.manifest.json");

        var exit = InProcHost.Main(["manifest", path], Garden.Define(), TextWriter.Null, TextWriter.Null);

        Assert.Equal(0, exit);
        Assert.Equal(GraphManifest.Generate(Garden.Define()), File.ReadAllText(path));
    }

    [Fact]
    public void Run_records_a_run_from_an_input_file_and_promote_turns_it_into_an_example()
    {
        using var dir = new TempDir();
        var input = dir.Write("input.json", """{ "seeds": [ { "name": "pea", "count": 12 } ] }""");
        var recordings = Path.Combine(dir.Path, "recordings");
        var fixtures = Path.Combine(dir.Path, "fixtures");

        Assert.Equal(0, InProcHost.Main(["run", input, recordings], Garden.Define(), TextWriter.Null, TextWriter.Null));
        var run = Assert.Single(Directory.GetDirectories(recordings));
        Assert.Equal(0, InProcHost.Main(["promote", run, "count", "one-large-row", fixtures], Garden.Define(), TextWriter.Null, TextWriter.Null));

        Assert.Equal("""{"rows":1,"large":1}""", JsonNode.Parse(File.ReadAllText(Path.Combine(fixtures, "count", "one-large-row", "expected.json")))!.ToJsonString());
    }

    [Fact]
    public void Prints_usage_and_exits_2_for_an_unknown_command()
    {
        var stderr = new StringWriter();

        Assert.Equal(2, InProcHost.Main(["frobnicate"], Garden.Define(), TextWriter.Null, stderr));
        Assert.Contains("usage:", stderr.ToString());
    }
}
