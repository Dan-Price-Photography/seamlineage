using Seamlineage.Docs;

namespace Seamlineage.Tests;

/// <summary>One page per stage: how it decides (pipeline text) and what it decides on each example (tables), not JSON.</summary>
public class StagePageTests
{
    private const string Manifest = """
        {
          "graph": "demo",
          "input": "Seed",
          "stages": [
            { "name": "grow", "description": "Turns a seed into a tree.", "input": "Seed", "output": "Tree", "schemaVersion": 1, "code": "src/Demo/Grow.cs" },
            {
              "name": "pick-trees", "description": "Keeps groups of 3 or more trees | of one height.", "input": "Tree", "output": "Forest", "schemaVersion": 1, "code": "src/Demo/PickTrees.cs",
              "items": "trees",
              "steps": [
                { "operator": "group-by", "parameters": { "key": "height" } },
                { "operator": "partition-by-size", "parameters": { "minimum": "3" } }
              ],
              "judgments": { "height": "How tall the tree is | to the metre." },
              "exampleView": {
                "rows": "trees",
                "columns": [ { "label": "tree", "path": "name" } ],
                "outcome": { "from": "groups", "members": "trees", "match": "name", "columns": [ { "label": "group", "path": "height" } ] }
              }
            }
          ],
          "operators": {
            "group-by": { "description": "Splits items into groups by key.", "phrase": "group by {key}" },
            "partition-by-size": { "description": "Keeps groups of at least minimum items.", "phrase": "keep groups of {minimum} or more" }
          },
          "types": {
            "Forest": { "groups": "Grove[]" },
            "Grove": { "height": "int", "trees": "Tree[]" },
            "Seed": { "name": "string" },
            "Tree": { "name": "string", "height": "int" }
          }
        }
        """;

    private static readonly ExampleCase[] Cases =
    [
        new("one-tall-grove", """{ "trees": [ { "name": "oak" }, { "name": "ash" } ] }""", """{ "groups": [ { "height": 3, "trees": [ { "name": "oak" } ] } ] }"""),
        new("no-trees", """{ "trees": [] }""", """{ "groups": [] }"""),
    ];

    private static string Page(string stage, IReadOnlyList<ExampleCase>? cases = null, ICollection<string>? problems = null) =>
        StagePage.FromManifest(Manifest, stage, cases ?? Cases, problems: problems);

    [Fact]
    public void Titles_the_page_with_the_stage_and_links_back_to_the_graph()
    {
        Assert.StartsWith(
            "# pick-trees\n\nGenerated from `graph.manifest.json` by `seamlineage graph`. Do not edit. Part of the [demo graph](../GRAPH.md).\n\n"
            + "Keeps groups of 3 or more trees | of one height.\n\n",
            Page("pick-trees"));
    }

    [Fact]
    public void Tabulates_the_contract_with_links_to_code_and_examples()
    {
        var page = Page("pick-trees");

        Assert.Contains("| Input | `Tree` |\n", page);
        Assert.Contains("| Output | `Forest` |\n", page);
        Assert.Contains("| Code | [PickTrees.cs](../src/Demo/PickTrees.cs) |\n", page);
        Assert.Contains("| Examples | [2 cases](../fixtures/pick-trees/) |\n", page);
    }

    [Fact]
    public void Draws_the_stage_as_in_the_graph_with_only_its_own_input_and_output()
    {
        var page = Page("pick-trees");

        Assert.Contains("```mermaid\nflowchart LR\n    Tree[\"`**Tree**\nname: string\nheight: int`\"]\n    Forest[\"`**Forest**\ngroups: Grove[]`\"]\n    subgraph pick_trees", page);
        Assert.Contains("        pick_trees_1[\"`**group-by**\nkey: height`\"]\n", page);
        Assert.DoesNotContain("Seed[", page);
    }

    [Fact]
    public void Writes_the_pipeline_text_then_the_judgments_it_names()
    {
        var page = Page("pick-trees");

        Assert.Contains("## Pipeline\n\n", page);
        Assert.Contains("```text\ntrees\n| group by height\n| keep groups of 3 or more\n```\n", page);
        Assert.Contains("## Judgments\n\n", page);
        Assert.Contains("| `height` | How tall the tree is \\| to the metre. |\n", page);
        Assert.True(page.IndexOf("## Pipeline", StringComparison.Ordinal) < page.IndexOf("## Judgments", StringComparison.Ordinal));
    }

    [Fact]
    public void Shows_each_case_as_a_table_under_a_heading_that_links_to_its_folder()
    {
        var page = Page("pick-trees");

        Assert.Contains("### [one-tall-grove](../fixtures/pick-trees/one-tall-grove/)\n\n| tree | → group |\n|---|---|\n| oak | 3 |\n| ash | – |\n", page);
        Assert.Contains("### [no-trees](../fixtures/pick-trees/no-trees/)\n\n", page);
        Assert.True(page.IndexOf("### [one-tall-grove]", StringComparison.Ordinal) < page.IndexOf("### [no-trees]", StringComparison.Ordinal));
    }

    [Fact]
    public void Says_which_view_the_tables_use()
    {
        Assert.Contains("Each row is one item of `trees` in the case's input.json; columns marked → are its outcome", Page("pick-trees"));
        Assert.Contains("This stage declares no example view", Page("grow", [new("tall", """{ "name": "oak" }""", """{ "name": "oak", "height": 3 }""")]));
    }

    [Fact]
    public void A_plain_stage_has_no_pipeline_or_judgments_and_is_drawn_as_an_arrow()
    {
        var page = Page("grow", []);

        Assert.DoesNotContain("## Pipeline", page);
        Assert.DoesNotContain("## Judgments", page);
        Assert.Contains("    Seed -->|\"`**grow**\nTurns a seed into a tree.`\"| Tree\n", page);
        Assert.Contains("| Examples | none |\n", page);
        Assert.Contains("## Examples\n\nNone yet.", page);
    }

    [Fact]
    public void Collects_problems_with_the_example_view_naming_the_case()
    {
        var problems = new List<string>();

        Page("pick-trees", [new("odd", """{ "forest": [] }""", """{ "groups": [] }""")], problems);

        Assert.Equal(["pick-trees/odd: the example view's rows path 'trees' is not a list in input.json"], problems);
    }

    [Fact]
    public void Rejects_a_stage_the_manifest_does_not_have()
    {
        Assert.Throws<InvalidDataException>(() => Page("prune"));
    }
}
