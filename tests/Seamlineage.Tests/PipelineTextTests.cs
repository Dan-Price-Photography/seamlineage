using Seamlineage.Docs;

namespace Seamlineage.Tests;

/// <summary>A composed stage's steps as plain words, one line per step, rendered from the manifest alone.</summary>
public class PipelineTextTests
{
    private static string Phrase(string? template, params (string Key, string Value)[] parameters) =>
        PipelineText.Phrase("an-operator", template, [.. parameters.Select(p => KeyValuePair.Create(p.Key, p.Value))]);

    [Fact]
    public void Replaces_each_placeholder_with_its_parameter_as_written()
    {
        Assert.Equal("group by row", Phrase("group by {key}", ("key", "row")));
        Assert.Equal("order by picked-at, then id", Phrase("order by {by}", ("by", "picked-at, then id")));
    }

    [Fact]
    public void Keeps_an_optional_part_only_when_every_parameter_in_it_is_given()
    {
        const string template = "split when more than {maxGap} apart[, or {breakWhen}]";

        Assert.Equal("split when more than 5 min apart, or variety-changes", Phrase(template, ("maxGap", "5 min"), ("breakWhen", "variety-changes")));
        Assert.Equal("split when more than 5 min apart", Phrase(template, ("maxGap", "5 min")));
    }

    [Fact]
    public void Shows_a_parameter_the_template_does_not_mention_rather_than_hiding_it()
    {
        Assert.Equal("group by row (limit: 3, note: a)", Phrase("group by {key}", ("key", "row"), ("limit", "3"), ("note", "a")));
    }

    [Fact]
    public void A_parameter_in_a_dropped_optional_part_is_still_shown()
    {
        Assert.Equal("keep (a: 1)", Phrase("keep[ {a} and {b}]", ("a", "1")));
    }

    [Fact]
    public void Falls_back_to_the_operator_name_and_its_parameters_without_a_template()
    {
        Assert.Equal("an-operator (minimum: 3)", Phrase(null, ("minimum", "3")));
        Assert.Equal("an-operator", Phrase(null));
    }

    [Fact]
    public void Leaves_a_placeholder_with_no_parameter_visible_outside_an_optional_part()
    {
        Assert.Equal("group by {key}", Phrase("group by {key}"));
    }

    [Fact]
    public void Doubled_braces_and_brackets_are_literal()
    {
        Assert.Equal("keep {all} [x] of row", Phrase("keep {{all}} [[x]] of {key}", ("key", "row")));
    }

    private const string Manifest = """
        {
          "graph": "demo", "input": "Seed",
          "stages": [
            {
              "name": "pick-trees", "description": "Keeps tall trees.", "input": "Seed", "output": "Forest", "schemaVersion": 1, "code": "src/Demo/PickTrees.cs",
              "items": "trees",
              "steps": [
                { "operator": "group-by", "parameters": { "key": "height" } },
                { "operator": "partition-by-size", "parameters": { "minimum": "3" } }
              ],
              "judgments": { "height": "How tall the tree is." }
            },
            { "name": "count", "description": "Counts.", "input": "Forest", "output": "Seed", "schemaVersion": 1, "code": "src/Demo/Count.cs" }
          ],
          "operators": {
            "group-by": { "description": "Splits items into groups by key.", "phrase": "group by {key}" },
            "partition-by-size": "Keeps groups of at least minimum items."
          },
          "types": { "Seed": { "name": "string" }, "Forest": { "trees": "string[]" } }
        }
        """;

    [Fact]
    public void Starts_with_what_the_steps_run_over_then_one_line_per_step()
    {
        var manifest = ManifestView.Parse(Manifest);

        Assert.Equal(
            "trees\n| group by height\n| partition-by-size (minimum: 3)",
            PipelineText.Render(manifest, manifest.Stages[0]));
    }

    [Fact]
    public void A_plain_stage_has_no_pipeline_text()
    {
        var manifest = ManifestView.Parse(Manifest);

        Assert.Null(PipelineText.Render(manifest, manifest.Stages[1]));
    }

    [Fact]
    public void A_composed_stage_that_names_no_items_starts_with_items()
    {
        var manifest = ManifestView.Parse(Manifest.Replace("\"items\": \"trees\",", ""));

        Assert.StartsWith("items\n| group by", PipelineText.Render(manifest, manifest.Stages[0]));
    }
}
