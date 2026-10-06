using System.Text.Json;
using System.Text.Json.Nodes;
using Seamlineage.Contracts;
using Seamlineage.Hosting.InProc;

namespace Seamlineage.Tests;

/// <summary>
/// The wire format escapes only what JSON requires, so generated files diff as they read: an apostrophe, an ampersand,
/// an angle bracket or an arrow is written as itself, never as a \u escape.
/// </summary>
public class WireJsonTests
{
    private sealed record Note(string Text);

    private const string Readable = "the picker's <first> & last → done, café";

    [Fact]
    public void Writes_apostrophes_html_characters_and_non_ascii_text_as_themselves()
    {
        var json = JsonSerializer.Serialize(new Note(Readable), WireJson.Options);

        Assert.Contains(Readable, json);
        Assert.DoesNotContain("\\u", json);
    }

    [Fact]
    public void Still_escapes_what_json_requires()
    {
        var json = JsonSerializer.Serialize(new Note("a \"quote\", a \\ and a\nnew line"), WireJson.Options);

        Assert.Contains("""a \"quote\", a \\ and a\nnew line""", json);
    }

    [Fact]
    public void Reads_any_valid_escaping()
    {
        var note = JsonSerializer.Deserialize<Note>("""{ "text": "the picker's <first> & last → done, café" }""", WireJson.Options);

        Assert.Equal(Readable, note!.Text);
    }

    [Fact]
    public void The_manifest_writes_descriptions_without_escapes()
    {
        var graph = Graph.Start<Garden.Packet>("garden")
            .Then("sow", "Decides each row's size: 10 or more seeds is large → \"large\" & <big>.", new Garden.Sow())
            .Build();

        var manifest = GraphManifest.Generate(graph);

        Assert.Contains("""Decides each row's size: 10 or more seeds is large → \"large\" & <big>.""", manifest);
        Assert.DoesNotContain("\\u", manifest);
    }

    [Fact]
    public void Recordings_and_promoted_examples_write_text_without_escapes()
    {
        using var recordings = new TempDir();
        using var fixtures = new TempDir();
        var packet = new Garden.Packet([new Garden.Seed("o'brien's <pea> & bean", 12)]);

        new InProcRunner(new EdgeRecorder(recordings.Path)).Run(Garden.Define(), packet, "run-1");
        FixturePromoter.Promote(recordings.Path, "sow", "named", fixtures.Path);

        var recorded = File.ReadAllText(Path.Combine(recordings.Path, "01-sow.jsonl"));
        var promoted = File.ReadAllText(Path.Combine(fixtures.Path, "sow", "named", "input.json"));
        Assert.Contains("o'brien's <pea> & bean", recorded);
        Assert.Contains("o'brien's <pea> & bean", promoted);
        Assert.Equal("o'brien's <pea> & bean", (string?)JsonNode.Parse(promoted)!["seeds"]![0]!["name"]);
    }
}
