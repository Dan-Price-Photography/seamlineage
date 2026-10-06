using Seamlineage.Docs;

namespace Seamlineage.Tests;

public class GraphDiagramTests
{
    private const string Manifest = """
        {
          "graph": "demo",
          "input": "Seed",
          "stages": [
            { "name": "grow", "description": "Turns a seed into a \"tree\".", "input": "Seed", "output": "Tree", "schemaVersion": 1, "code": "src/Demo/Grow.cs" }
          ],
          "types": {
            "Leaf": { "colour": "Shade" },
            "Seed": { "name": "string" },
            "Shade": [ "light", "dark" ],
            "Tree": { "leaves": "Leaf[]", "height": "int" }
          }
        }
        """;

    [Fact]
    public void Titles_the_page_and_says_where_it_came_from()
    {
        var markdown = GraphDiagram.FromManifest(Manifest);

        Assert.StartsWith(
            "# demo graph\n\nGenerated from `graph.manifest.json` by `seamlineage graph`. Do not edit.\n\n"
            + "Boxes are the data passed between stages; arrows are stages.\n\n",
            markdown);
    }

    [Fact]
    public void Names_the_manifest_file_and_command_it_was_generated_with()
    {
        var markdown = GraphDiagram.FromManifest(Manifest, options: new DiagramOptions { ManifestFileName = "demo.manifest.json", GeneratedBy = "make docs" });

        Assert.Contains("Generated from `demo.manifest.json` by `make docs`. Do not edit.", markdown);
    }

    [Fact]
    public void Draws_each_edge_type_as_a_node_and_each_stage_as_a_labelled_arrow_without_html()
    {
        var markdown = GraphDiagram.FromManifest(Manifest);

        Assert.Contains("```mermaid\nflowchart LR\n", markdown);
        Assert.Contains("    Seed[\"`**Seed**\nname: string`\"]\n", markdown);
        Assert.Contains("    Tree[\"`**Tree**\nleaves: Leaf[]\nheight: int`\"]\n", markdown);
        Assert.Contains("    Seed -->|\"`**grow**\nTurns a seed into a 'tree'.`\"| Tree\n", markdown);
        Assert.DoesNotContain("<br/>", markdown);
        Assert.DoesNotContain("<b>", markdown);
        Assert.DoesNotContain("#quot;", markdown);
    }

    [Fact]
    public void Explains_the_drawing_only_lines_before_the_diagram()
    {
        var markdown = GraphDiagram.FromManifest(Manifest);
        var note = markdown.IndexOf("How to read the raw diagram", StringComparison.Ordinal);

        Assert.InRange(note, 0, markdown.IndexOf("```mermaid", StringComparison.Ordinal));
        Assert.Contains("`flowchart LR`", markdown);
        Assert.Contains("`direction TB`", markdown);
        Assert.Contains("`subgraph … end`", markdown);
    }

    [Fact]
    public void Keeps_semicolons_and_underscores_as_written_and_neutralises_backticks()
    {
        var markdown = GraphDiagram.FromManifest(Manifest.Replace("into a", "into a; `ROW_1` a"));

        Assert.Contains("Turns a seed into a; 'ROW_1' a 'tree'.", markdown);
    }

    [Fact]
    public void Lists_nested_types_and_enum_values_below_the_diagram()
    {
        var markdown = GraphDiagram.FromManifest(Manifest);

        Assert.Contains("- **Leaf**: colour: Shade", markdown);
        Assert.Contains("- **Shade**: light | dark", markdown);
        Assert.DoesNotContain("- **Tree**", markdown); // already drawn as a node
    }

    [Fact]
    public void Tabulates_each_stage_with_links_to_its_code_and_its_examples()
    {
        var markdown = GraphDiagram.FromManifest(Manifest.Replace("into a", "into a | b"), new Dictionary<string, int> { ["grow"] = 2 });

        Assert.Contains("| Stage | What it decides | Code | Examples |", markdown);
        Assert.Contains(
            """| **grow** | Turns a seed into a \| b "tree". | [Grow.cs](src/Demo/Grow.cs) | [2 cases](fixtures/grow/) |""",
            markdown);
    }

    [Fact]
    public void Says_one_case_in_the_singular()
    {
        var markdown = GraphDiagram.FromManifest(Manifest, new Dictionary<string, int> { ["grow"] = 1 });

        Assert.Contains("| [1 case](fixtures/grow/) |", markdown);
    }

    [Fact]
    public void Says_none_when_a_stage_has_no_examples()
    {
        var markdown = GraphDiagram.FromManifest(Manifest);

        Assert.Contains("| [Grow.cs](src/Demo/Grow.cs) | none |", markdown);
    }

    [Fact]
    public void Links_relative_to_where_the_page_is_written()
    {
        var markdown = GraphDiagram.FromManifest(
            Manifest,
            new Dictionary<string, int> { ["grow"] = 3 },
            new DiagramOptions { CodeRoot = "..", FixturesLink = "../examples" });

        Assert.Contains("| [Grow.cs](../src/Demo/Grow.cs) | [3 cases](../examples/grow/) |", markdown);
    }

    private const string ComposedManifest = """
        {
          "graph": "demo",
          "input": "Seed",
          "stages": [
            { "name": "grow", "description": "Turns a seed into a tree.", "input": "Seed", "output": "Tree", "schemaVersion": 1, "code": "src/Demo/Grow.cs" },
            {
              "name": "pick-trees", "description": "Keeps groups of 3 or more trees of one height.", "input": "Tree", "output": "Forest", "schemaVersion": 1, "code": "src/Demo/PickTrees.cs",
              "steps": [
                { "operator": "group-by", "parameters": { "key": "height" } },
                { "operator": "partition-by-size", "parameters": { "minimum": "3", "note": "a \"quote\"; and a semicolon" } }
              ],
              "judgments": { "height": "How tall the tree is | to the metre." }
            }
          ],
          "operators": { "group-by": "Splits items into groups by key.", "partition-by-size": "Keeps groups of at least minimum items." },
          "types": {
            "Forest": { "trees": "Tree[]" },
            "Seed": { "name": "string" },
            "Tree": { "height": "int" }
          }
        }
        """;

    [Fact]
    public void Draws_a_composed_stage_as_a_box_of_its_steps_in_order()
    {
        var markdown = GraphDiagram.FromManifest(ComposedManifest);

        Assert.Contains(
            """
                subgraph pick_trees["`**pick-trees**`"]
                    direction TB
                    pick_trees_1["`**group-by**
            key: height`"]
                    pick_trees_2["`**partition-by-size**
            minimum: 3
            note: a 'quote'; and a semicolon`"]
                    pick_trees_1 --> pick_trees_2
                end
                Tree -->|"`**pick-trees**
            Keeps groups of 3 or more trees of one height.`"| pick_trees
                pick_trees --> Forest

            """.ReplaceLineEndings("\n"),
            markdown);
    }

    [Fact]
    public void Still_draws_a_plain_stage_as_a_labelled_arrow_beside_a_composed_one()
    {
        var markdown = GraphDiagram.FromManifest(ComposedManifest);

        Assert.Contains("    Seed -->|\"`**grow**\nTurns a seed into a tree.`\"| Tree\n", markdown);
        Assert.DoesNotContain("subgraph grow", markdown);
    }

    [Fact]
    public void Lists_the_judgments_of_each_composed_stage_with_their_meaning()
    {
        var markdown = GraphDiagram.FromManifest(ComposedManifest);

        Assert.Contains("## Judgments", markdown);
        Assert.Contains("| Stage | Judgment | Meaning |", markdown);
        Assert.Contains("| **pick-trees** | `height` | How tall the tree is \\| to the metre. |", markdown);
    }

    [Fact]
    public void Lists_the_operators_used_with_what_each_does()
    {
        var markdown = GraphDiagram.FromManifest(ComposedManifest);

        Assert.Contains("## Operators", markdown);
        Assert.Contains("| **group-by** | Splits items into groups by key. |", markdown);
        Assert.Contains("| **partition-by-size** | Keeps groups of at least minimum items. |", markdown);
    }

    [Fact]
    public void A_graph_without_composed_stages_has_no_judgments_or_operators_sections()
    {
        var markdown = GraphDiagram.FromManifest(Manifest);

        Assert.DoesNotContain("\n    subgraph ", markdown); // no box drawn (the reading note may still mention the word)
        Assert.DoesNotContain("## Judgments", markdown);
        Assert.DoesNotContain("## Operators", markdown);
    }

    [Fact]
    public void Writes_the_sections_in_a_fixed_order()
    {
        var markdown = GraphDiagram.FromManifest(Manifest.Replace("\"Leaf\"", "\"Aaa\": { \"x\": \"int\" },\n\"Leaf\""));
        var composed = GraphDiagram.FromManifest(ComposedManifest);

        int At(string text, string heading) => text.IndexOf(heading, StringComparison.Ordinal);
        Assert.True(At(markdown, "```mermaid") < At(markdown, "## Stages"));
        Assert.True(At(markdown, "## Stages") < At(markdown, "## Other types"));
        Assert.True(At(composed, "## Stages") < At(composed, "## Judgments"));
        Assert.True(At(composed, "## Judgments") < At(composed, "## Operators"));
    }

    [Fact]
    public void A_caller_can_add_a_section_without_changing_the_others()
    {
        var extra = new TextSection("\n## Notes\n\nHello.\n");

        var markdown = GraphDiagram.FromManifest(Manifest, sections: [.. GraphDiagram.DefaultSections, extra]);

        Assert.Equal(GraphDiagram.FromManifest(Manifest) + "\n## Notes\n\nHello.\n", markdown);
    }

    [Fact]
    public void Rejects_a_manifest_without_stages_with_a_clear_message()
    {
        var ex = Assert.Throws<InvalidDataException>(() => GraphDiagram.FromManifest("""{ "graph": "x", "input": "A", "types": {} }"""));

        Assert.Contains("stages", ex.Message);
    }

    private sealed class TextSection(string text) : IDiagramSection
    {
        public void Write(DiagramContext context, System.Text.StringBuilder markdown) => markdown.Append(text);
    }
}
