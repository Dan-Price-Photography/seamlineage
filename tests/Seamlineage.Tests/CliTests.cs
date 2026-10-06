using Seamlineage.Cli;
using Seamlineage.Contracts;

namespace Seamlineage.Tests;

public class CliTests
{
    // A repo for the garden graph: manifest, code and an example for each stage.
    private static TempDir Repo()
    {
        var repo = new TempDir();
        repo.Write("graph.manifest.json", GraphManifest.Generate(Garden.Define()));
        repo.Write("src/Seamlineage.Tests/Sow.cs", "");
        repo.Write("src/Seamlineage.Tests/Count.cs", "");
        repo.Write("fixtures/sow/a/input.json", "{}");
        repo.Write("fixtures/count/a/input.json", "{}");
        return repo;
    }

    private static (int Exit, string Out, string Err) Run(params string[] args)
    {
        var (stdout, stderr) = (new StringWriter(), new StringWriter());
        var exit = CliApp.Run(args, stdout, stderr);
        return (exit, stdout.ToString(), stderr.ToString());
    }

    private static string[] Args(TempDir repo, string command, string outName = "GRAPH.md") =>
    [
        command,
        "--manifest", Path.Combine(repo.Path, "graph.manifest.json"),
        "--fixtures", Path.Combine(repo.Path, "fixtures"),
        "--out", Path.Combine(repo.Path, outName),
    ];

    [Fact]
    public void Graph_writes_the_page_drawn_from_the_manifest_without_the_products_assemblies()
    {
        using var repo = Repo();

        var (exit, _, _) = Run(Args(repo, "graph"));

        Assert.Equal(0, exit);
        var page = File.ReadAllText(Path.Combine(repo.Path, "GRAPH.md"));
        Assert.StartsWith("# garden graph\n", page);
        Assert.Contains("| **sow** | Decides how big each row is: 10 or more seeds is large. | [Sow.cs](src/Seamlineage.Tests/Sow.cs) | [1 case](fixtures/sow/) |", page);
    }

    [Fact]
    public void Check_passes_on_a_page_just_written()
    {
        using var repo = Repo();
        Run(Args(repo, "graph"));

        var (exit, stdout, _) = Run(Args(repo, "check"));

        Assert.Equal(0, exit);
        Assert.Contains("is current", stdout);
    }

    [Fact]
    public void Check_exits_1_when_the_page_is_stale()
    {
        using var repo = Repo();
        Run(Args(repo, "graph"));
        repo.Write("fixtures/sow/b/input.json", "{}");

        var (exit, _, stderr) = Run(Args(repo, "check"));

        Assert.Equal(1, exit);
        Assert.Contains("is stale. Regenerate it with: seamlineage graph", stderr);
    }

    [Fact]
    public void Check_exits_1_when_a_link_does_not_resolve()
    {
        using var repo = Repo();
        Run(Args(repo, "graph"));
        File.Delete(Path.Combine(repo.Path, "src/Seamlineage.Tests/Count.cs"));

        var (exit, _, stderr) = Run(Args(repo, "check"));

        Assert.Equal(1, exit);
        Assert.Contains("links to src/Seamlineage.Tests/Count.cs, which does not exist", stderr);
    }

    [Fact]
    public void Check_exits_1_when_a_stage_has_no_examples()
    {
        using var repo = Repo();
        Directory.Delete(Path.Combine(repo.Path, "fixtures/count"), recursive: true);
        Run(Args(repo, "graph"));

        var (exit, _, stderr) = Run(Args(repo, "check"));

        Assert.Equal(1, exit);
        Assert.Contains("Stage 'count' has no examples", stderr);
    }

    [Fact]
    public void Graph_links_relative_to_a_page_written_elsewhere()
    {
        using var repo = Repo();

        Run(Args(repo, "graph", outName: "docs/GRAPH.md"));

        var page = File.ReadAllText(Path.Combine(repo.Path, "docs", "GRAPH.md"));
        Assert.Contains("[Sow.cs](../src/Seamlineage.Tests/Sow.cs) | [1 case](../fixtures/sow/)", page);
        Assert.Equal(0, Run(Args(repo, "check", outName: "docs/GRAPH.md")).Exit);
    }

    [Fact]
    public void Works_without_an_examples_folder()
    {
        using var repo = Repo();
        var args = new[] { "graph", "--manifest", Path.Combine(repo.Path, "graph.manifest.json"), "--out", Path.Combine(repo.Path, "GRAPH.md") };

        Assert.Equal(0, Run(args).Exit);
        Assert.Contains("| none |", File.ReadAllText(Path.Combine(repo.Path, "GRAPH.md")));
    }

    [Fact]
    public void Exits_2_with_usage_for_an_unknown_command_or_option()
    {
        Assert.Equal(2, Run("draw").Exit);
        var (exit, _, stderr) = Run("graph", "--colour", "blue");
        Assert.Equal(2, exit);
        Assert.Contains("Unknown option '--colour'", stderr);
        Assert.Contains("usage: seamlineage", stderr);
    }

    [Fact]
    public void Exits_2_when_the_manifest_is_missing_or_invalid()
    {
        using var repo = new TempDir();
        var missing = Run("graph", "--manifest", Path.Combine(repo.Path, "none.json"));
        var invalid = Run("graph", "--manifest", repo.Write("bad.json", """{ "graph": "x" }"""), "--out", Path.Combine(repo.Path, "GRAPH.md"));

        Assert.Equal(2, missing.Exit);
        Assert.Contains("No manifest at", missing.Err);
        Assert.Equal(2, invalid.Exit);
        Assert.Contains("no 'input' field", invalid.Err);
    }

    [Fact]
    public void Help_lists_the_commands()
    {
        var (exit, stdout, _) = Run("--help");

        Assert.Equal(0, exit);
        Assert.Contains("  graph ", stdout);
        Assert.Contains("  check ", stdout);
    }
}
