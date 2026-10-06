using Seamlineage.Docs;

namespace Seamlineage.Tests;

public class GraphDocsTests
{
    private const string Manifest = """
        {
          "graph": "demo",
          "input": "Seed",
          "stages": [
            { "name": "grow", "description": "Turns a seed into a tree.", "input": "Seed", "output": "Tree", "schemaVersion": 1, "code": "src/Demo/Grow.cs" }
          ],
          "types": { "Seed": { "name": "string" }, "Tree": { "height": "int" } }
        }
        """;

    // A repo with the manifest at its root, the stage's code, and two examples of it.
    private static TempDir Repo()
    {
        var repo = new TempDir();
        repo.Write("graph.manifest.json", Manifest);
        repo.Write("src/Demo/Grow.cs", "// grow");
        repo.Write("fixtures/grow/tall/input.json", "{}");
        repo.Write("fixtures/grow/short/input.json", "{}");
        return repo;
    }

    private static string At(TempDir dir, string relative) => Path.Combine(dir.Path, relative);

    [Fact]
    public void Counts_the_case_folders_of_each_stage()
    {
        using var repo = Repo();

        var cases = GraphDocs.CountCases(["grow", "prune"], At(repo, "fixtures"));

        Assert.Equal(2, cases["grow"]);
        Assert.Equal(0, cases["prune"]);
    }

    [Fact]
    public void Renders_beside_the_manifest_with_the_same_links_as_the_proving_ground()
    {
        using var repo = Repo();

        var page = GraphDocs.Render(At(repo, "graph.manifest.json"), At(repo, "fixtures"), At(repo, "GRAPH.md"));

        Assert.Contains("| [Grow.cs](src/Demo/Grow.cs) | [2 cases](fixtures/grow/) |", page);
    }

    [Fact]
    public void Renders_links_relative_to_a_page_in_another_folder()
    {
        using var repo = Repo();

        var page = GraphDocs.Render(At(repo, "graph.manifest.json"), At(repo, "fixtures"), At(repo, "docs/GRAPH.md"));

        Assert.Contains("| [Grow.cs](../src/Demo/Grow.cs) | [2 cases](../fixtures/grow/) |", page);
    }

    [Fact]
    public void A_current_page_with_working_links_has_no_problems()
    {
        using var repo = Repo();
        File.WriteAllText(At(repo, "GRAPH.md"), GraphDocs.Render(At(repo, "graph.manifest.json"), At(repo, "fixtures"), At(repo, "GRAPH.md")));

        Assert.Empty(GraphDocs.Check(At(repo, "graph.manifest.json"), At(repo, "fixtures"), At(repo, "GRAPH.md")));
    }

    [Fact]
    public void Line_endings_do_not_make_a_page_stale()
    {
        using var repo = Repo();
        var page = GraphDocs.Render(At(repo, "graph.manifest.json"), At(repo, "fixtures"), At(repo, "GRAPH.md"));
        File.WriteAllText(At(repo, "GRAPH.md"), page.Replace("\n", "\r\n"));

        Assert.Empty(GraphDocs.Check(At(repo, "graph.manifest.json"), At(repo, "fixtures"), At(repo, "GRAPH.md")));
    }

    [Fact]
    public void Reports_a_stale_page()
    {
        using var repo = Repo();
        File.WriteAllText(At(repo, "GRAPH.md"), GraphDocs.Render(At(repo, "graph.manifest.json"), At(repo, "fixtures"), At(repo, "GRAPH.md")));
        repo.Write("fixtures/grow/wide/input.json", "{}"); // a third case changes the count

        var problem = Assert.Single(GraphDocs.Check(At(repo, "graph.manifest.json"), At(repo, "fixtures"), At(repo, "GRAPH.md")));
        Assert.Contains("is stale", problem);
    }

    [Fact]
    public void Reports_a_missing_page()
    {
        using var repo = Repo();

        var problem = Assert.Single(GraphDocs.Check(At(repo, "graph.manifest.json"), At(repo, "fixtures"), At(repo, "GRAPH.md")));
        Assert.Contains("does not exist", problem);
    }

    [Fact]
    public void Reports_a_link_to_code_that_does_not_exist()
    {
        using var repo = Repo();
        File.WriteAllText(At(repo, "GRAPH.md"), GraphDocs.Render(At(repo, "graph.manifest.json"), At(repo, "fixtures"), At(repo, "GRAPH.md")));
        File.Delete(At(repo, "src/Demo/Grow.cs"));

        var problem = Assert.Single(GraphDocs.Check(At(repo, "graph.manifest.json"), At(repo, "fixtures"), At(repo, "GRAPH.md")));
        Assert.Contains("links to src/Demo/Grow.cs", problem);
    }

    [Fact]
    public void Does_not_check_web_links_or_anchors()
    {
        using var repo = new TempDir();
        var page = repo.Write("page.md", "[a](https://example.com/x) [b](#top) [c](missing.md)");

        Assert.Equal(["missing.md"], GraphDocs.BrokenLinks(page));
    }

    [Fact]
    public void Reports_a_stage_without_examples_and_a_folder_without_a_stage()
    {
        using var repo = Repo();
        Directory.Move(At(repo, "fixtures/grow"), At(repo, "fixtures/sprout"));
        File.WriteAllText(At(repo, "GRAPH.md"), GraphDocs.Render(At(repo, "graph.manifest.json"), At(repo, "fixtures"), At(repo, "GRAPH.md")));

        var problems = GraphDocs.Check(At(repo, "graph.manifest.json"), At(repo, "fixtures"), At(repo, "GRAPH.md"));

        Assert.Equal(2, problems.Count);
        Assert.Contains(problems, p => p.Contains("Stage 'grow' has no examples", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.Contains("sprout does not match any stage", StringComparison.Ordinal));
    }
}
