using System.Text.Json.Nodes;
using Seamlineage.Contracts;
using Seamlineage.Testing;

namespace Seamlineage.Tests;

public class JsonDiffTests
{
    private static IReadOnlyList<string> Diff(string expected, string actual) =>
        JsonDiff.Describe(JsonNode.Parse(expected), JsonNode.Parse(actual));

    [Fact]
    public void Equal_documents_have_no_differences_whatever_the_key_order()
    {
        Assert.Empty(Diff("""{"a":1,"b":[1,2]}""", """{ "b": [1, 2], "a": 1 }"""));
    }

    [Fact]
    public void Names_the_path_of_a_changed_value()
    {
        Assert.Equal(["$.rows[1].size: expected \"small\", actual \"large\""],
            Diff("""{"rows":[{"size":"large"},{"size":"small"}]}""", """{"rows":[{"size":"large"},{"size":"large"}]}"""));
    }

    [Fact]
    public void Names_missing_and_unexpected_fields()
    {
        Assert.Equal(["$.a: missing, expected 1", "$.c: unexpected 3"], Diff("""{"a":1,"b":2}""", """{"b":2,"c":3}"""));
    }

    [Fact]
    public void Reports_a_different_number_of_items_then_compares_the_items_both_have()
    {
        Assert.Equal(["$: expected 2 items, actual 1 item","$[0]: expected \"x\", actual \"y\""], Diff("""["x","z"]""", """["y"]"""));
    }

    [Fact]
    public void Reports_null_against_a_value()
    {
        Assert.Equal(["$.at: expected null, actual \"2026-01-01T00:00:00\""], Diff("""{"at":null}""", """{"at":"2026-01-01T00:00:00"}"""));
    }

    [Fact]
    public void Stops_after_the_maximum_and_says_how_many_more()
    {
        var lines = JsonDiff.Describe(JsonNode.Parse("[1,2,3,4]"), JsonNode.Parse("[5,6,7,8]"), max: 2);

        Assert.Equal(["$[0]: expected 1, actual 5", "$[1]: expected 2, actual 6", "... and 2 more"], lines);
    }
}

public class FixturesTests
{
    private static TempDir FixturesFor(string stage, string @case, string input, string expected)
    {
        var dir = new TempDir();
        dir.Write($"{stage}/{@case}/input.json", input);
        dir.Write($"{stage}/{@case}/expected.json", expected);
        return dir;
    }

    private const string Input = """{ "seeds": [ { "name": "pea", "count": 12 }, { "name": "bean", "count": 3 } ] }""";

    [Fact]
    public void Discovers_every_case_in_stage_then_case_order()
    {
        using var dir = new TempDir();
        dir.Write("sow/b-case/input.json", "{}");
        dir.Write("sow/a-case/input.json", "{}");
        dir.Write("count/only/input.json", "{}");

        Assert.Equal(["count/only", "sow/a-case", "sow/b-case"], Fixtures.Discover(dir.Path).Select(c => c.ToString()));
    }

    [Fact]
    public void A_case_passes_when_the_output_is_json_equal_to_expected()
    {
        using var dir = FixturesFor("sow", "sizes", Input, """{"rows":[{"size":"large","name":"pea"},{"name":"bean","size":"small"}]}""");

        Fixtures.Verify(Garden.Define(), dir.Path, "sow", "sizes");
    }

    [Fact]
    public void A_failing_case_says_where_the_output_differs_and_shows_both_documents()
    {
        using var dir = FixturesFor("sow", "sizes", Input, """{"rows":[{"name":"pea","size":"large"},{"name":"bean","size":"large"}]}""");

        var ex = Assert.Throws<CheckFailedException>(() => Fixtures.Verify(Garden.Define(), dir.Path, "sow", "sizes"));

        Assert.StartsWith("sow/sizes output differs from expected.json:\n  $.rows[1].size: expected \"large\", actual \"small\"\n--- expected\n", ex.Message);
        Assert.Contains("--- actual\n", ex.Message);
    }

    [Fact]
    public void Runs_with_a_fixed_correlation_id_and_clock()
    {
        Assert.Equal(new StageContext("fixture", DateTimeOffset.UnixEpoch), Fixtures.Context);
    }

    [Fact]
    public void A_folder_that_names_no_stage_fails_clearly()
    {
        using var dir = FixturesFor("sprout", "x", "{}", "{}");

        var ex = Assert.Throws<CheckFailedException>(() => Fixtures.Verify(Garden.Define(), dir.Path, "sprout", "x"));

        Assert.Contains("fixtures/sprout does not match any stage in the 'garden' graph", ex.Message);
    }

    [Fact]
    public void Every_stage_needs_an_example()
    {
        using var dir = FixturesFor("sow", "sizes", Input, "{}");

        var ex = Assert.Throws<CheckFailedException>(() => Fixtures.EveryStageHasExamples(Garden.Define(), dir.Path));

        Assert.Contains("Stage 'count' has no examples", ex.Message);
        Assert.DoesNotContain("'sow'", ex.Message);
    }
}

public class GeneratedFilesTests
{
    [Fact]
    public void A_current_manifest_passes_and_a_stale_one_names_the_regenerate_command()
    {
        using var dir = new TempDir();
        var path = dir.Write("graph.manifest.json", GraphManifest.Generate(Garden.Define()).Replace("\n", "\r\n"));

        GeneratedFiles.ManifestIsCurrent(Garden.Define(), path, "make manifest");

        File.WriteAllText(path, "{}");
        var ex = Assert.Throws<CheckFailedException>(() => GeneratedFiles.ManifestIsCurrent(Garden.Define(), path, "make manifest"));
        Assert.Contains("is stale. Regenerate it with: make manifest", ex.Message);
    }

    [Fact]
    public void Code_paths_are_checked_relative_to_the_manifest()
    {
        using var dir = new TempDir();
        var path = dir.Write("graph.manifest.json", GraphManifest.Generate(Garden.Define()));
        dir.Write("src/Seamlineage.Tests/Sow.cs", "");

        var ex = Assert.Throws<CheckFailedException>(() => GeneratedFiles.CodePathsExist(path));

        Assert.Contains("src/Seamlineage.Tests/Count.cs", ex.Message);
        Assert.DoesNotContain("Sow.cs", ex.Message);
    }

    [Fact]
    public void A_stale_diagram_fails_with_the_same_rules_as_the_cli()
    {
        using var dir = new TempDir();
        var manifest = dir.Write("graph.manifest.json", GraphManifest.Generate(Garden.Define()));
        var page = dir.Write("GRAPH.md", "# old\n");
        dir.Write("fixtures/sow/a/input.json", "{}");
        dir.Write("fixtures/count/a/input.json", "{}");

        var ex = Assert.Throws<CheckFailedException>(() => GeneratedFiles.DiagramIsCurrent(manifest, Path.Combine(dir.Path, "fixtures"), page));

        Assert.Contains("is stale", ex.Message);
    }
}

public class ArchitectureCheckTests
{
    [Fact]
    public void An_assembly_that_references_only_the_base_class_library_and_the_allowlist_passes()
    {
        Architecture.ReferencesOnlyBaseClassLibraryAnd(typeof(Seamlineage.Operators.Pipeline).Assembly, "Seamlineage.Contracts");
    }

    [Fact]
    public void Anything_else_is_named_in_the_failure()
    {
        var offenders = Architecture.ReferencesOutside(typeof(ArchitectureCheckTests).Assembly, "Seamlineage.");

        Assert.Contains("xunit.core", offenders);
        Assert.Throws<CheckFailedException>(() => Architecture.ReferencesOnlyBaseClassLibraryAnd(typeof(ArchitectureCheckTests).Assembly, "Seamlineage."));
    }

    /// <summary>Seamlineage's own libraries need nothing but .NET: no third-party package reaches a consumer.</summary>
    [Theory]
    [InlineData(typeof(Graph))]
    [InlineData(typeof(Seamlineage.Operators.Pipeline))]
    [InlineData(typeof(Seamlineage.Docs.GraphDiagram))]
    [InlineData(typeof(Fixtures))]
    [InlineData(typeof(Seamlineage.Hosting.InProc.InProcRunner))]
    [InlineData(typeof(Seamlineage.Cli.CliApp))]
    public void Seamlineage_assemblies_reference_only_the_base_class_library(Type marker)
    {
        Architecture.ReferencesOnlyBaseClassLibraryAnd(marker.Assembly, "Seamlineage.");
    }

    /// <summary>Contracts and operators are what product code references: they may not depend on hosting, docs or testing.</summary>
    [Theory]
    [InlineData(typeof(Graph), "Seamlineage.Contracts")]
    [InlineData(typeof(Seamlineage.Operators.Pipeline), "Seamlineage.Contracts")]
    [InlineData(typeof(Seamlineage.Docs.GraphDiagram), "Seamlineage.Docs")]
    public void Product_facing_assemblies_reference_no_plumbing(Type marker, string allowed)
    {
        Architecture.ReferencesOnlyBaseClassLibraryAnd(marker.Assembly, allowed);
    }
}
