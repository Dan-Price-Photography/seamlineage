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
        WriteAll(repo);

        Assert.Empty(GraphDocs.Check(At(repo, "graph.manifest.json"), At(repo, "fixtures"), At(repo, "GRAPH.md")));
    }

    [Fact]
    public void Line_endings_do_not_make_a_page_stale()
    {
        using var repo = Repo();
        foreach (var (path, content) in GraphDocs.RenderAll(At(repo, "graph.manifest.json"), At(repo, "fixtures"), At(repo, "GRAPH.md")))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content.Replace("\n", "\r\n"));
        }

        Assert.Empty(GraphDocs.Check(At(repo, "graph.manifest.json"), At(repo, "fixtures"), At(repo, "GRAPH.md")));
    }

    [Fact]
    public void Reports_a_stale_page()
    {
        using var repo = Repo();
        WriteAll(repo);
        repo.Write("fixtures/grow/wide/input.json", "{}"); // a third case changes the count, and adds a table to the stage page

        var problems = GraphDocs.Check(At(repo, "graph.manifest.json"), At(repo, "fixtures"), At(repo, "GRAPH.md"));
        Assert.Contains(problems, p => p.Contains("GRAPH.md is stale", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.Contains("grow.md is stale", StringComparison.Ordinal));
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
        WriteAll(repo);
        File.Delete(At(repo, "src/Demo/Grow.cs"));

        var problems = GraphDocs.Check(At(repo, "graph.manifest.json"), At(repo, "fixtures"), At(repo, "GRAPH.md"));
        Assert.Contains(problems, p => p.Contains("GRAPH.md links to src/Demo/Grow.cs, which does not exist", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.Replace('\\', '/').Contains("graph/grow.md links to ../src/Demo/Grow.cs, which does not exist", StringComparison.Ordinal));
    }

    private static void WriteAll(TempDir repo, string outName = "GRAPH.md")
    {
        foreach (var (path, content) in GraphDocs.RenderAll(At(repo, "graph.manifest.json"), At(repo, "fixtures"), At(repo, outName)))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }
    }

    [Fact]
    public void Renders_the_graph_page_then_one_page_per_stage_in_a_graph_folder_beside_it()
    {
        using var repo = Repo();

        var files = GraphDocs.RenderAll(At(repo, "graph.manifest.json"), At(repo, "fixtures"), At(repo, "GRAPH.md"));

        Assert.Equal([At(repo, "GRAPH.md"), Path.Combine(repo.Path, "graph", "grow.md")], files.Keys);
        Assert.Contains("| Code | [Grow.cs](../src/Demo/Grow.cs) |", files[Path.Combine(repo.Path, "graph", "grow.md")]);
        Assert.Contains("### [short](../fixtures/grow/short/)", files[Path.Combine(repo.Path, "graph", "grow.md")]);
    }

    [Fact]
    public void Stage_pages_link_relative_to_a_graph_page_in_another_folder()
    {
        using var repo = Repo();

        var files = GraphDocs.RenderAll(At(repo, "graph.manifest.json"), At(repo, "fixtures"), At(repo, "docs/GRAPH.md"));

        Assert.Contains("| Code | [Grow.cs](../../src/Demo/Grow.cs) |", files[Path.Combine(repo.Path, "docs", "graph", "grow.md")]);
    }

    [Fact]
    public void Current_pages_with_working_links_have_no_problems()
    {
        using var repo = Repo();
        WriteAll(repo);

        Assert.Empty(GraphDocs.Check(At(repo, "graph.manifest.json"), At(repo, "fixtures"), At(repo, "GRAPH.md")));
    }

    [Fact]
    public void Reports_a_stale_or_missing_stage_page()
    {
        using var repo = Repo();
        WriteAll(repo);
        File.AppendAllText(At(repo, "graph/grow.md"), "edited\n");

        Assert.Contains("graph/grow.md is stale", Assert.Single(GraphDocs.Check(At(repo, "graph.manifest.json"), At(repo, "fixtures"), At(repo, "GRAPH.md"))).Replace('\\', '/'));

        File.Delete(At(repo, "graph/grow.md"));
        var problems = GraphDocs.Check(At(repo, "graph.manifest.json"), At(repo, "fixtures"), At(repo, "GRAPH.md"));
        Assert.Contains(problems, p => p.Replace('\\', '/').Contains("graph/grow.md does not exist", StringComparison.Ordinal));
    }

    [Fact]
    public void Reports_a_page_in_the_graph_folder_that_is_no_longer_a_stage()
    {
        using var repo = Repo();
        WriteAll(repo);
        repo.Write("graph/sprout.md", "# sprout\n");

        var problem = Assert.Single(GraphDocs.Check(At(repo, "graph.manifest.json"), At(repo, "fixtures"), At(repo, "GRAPH.md")));
        Assert.Contains("graph/sprout.md is not the page of any stage", problem.Replace('\\', '/'));
    }

    [Fact]
    public void Reports_an_example_view_that_does_not_fit_a_case()
    {
        using var repo = new TempDir();
        repo.Write("graph.manifest.json", Manifest.Replace(
            "\"code\": \"src/Demo/Grow.cs\" }",
            "\"code\": \"src/Demo/Grow.cs\", \"exampleView\": { \"rows\": \"seeds\", \"columns\": [] } }"));
        repo.Write("src/Demo/Grow.cs", "// grow");
        repo.Write("fixtures/grow/tall/input.json", """{ "name": "oak" }""");
        repo.Write("fixtures/grow/tall/expected.json", """{ "height": 3 }""");
        WriteAll(repo);

        var problem = Assert.Single(GraphDocs.Check(At(repo, "graph.manifest.json"), At(repo, "fixtures"), At(repo, "GRAPH.md")));
        Assert.Contains("grow/tall: the example view's rows path 'seeds' is not a list in input.json", problem);
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
        WriteAll(repo);

        var problems = GraphDocs.Check(At(repo, "graph.manifest.json"), At(repo, "fixtures"), At(repo, "GRAPH.md"));

        Assert.Equal(2, problems.Count);
        Assert.Contains(problems, p => p.Contains("Stage 'grow' has no examples", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.Contains("sprout does not match any stage", StringComparison.Ordinal));
    }
}
