using System.Text.Json;
using Seamlineage.Contracts;

namespace Seamlineage.Hosting.InProc;

/// <summary>
/// The command line of a product's in-process host, so a product needs only a one-line Program.cs:
/// <c>return InProcHost.Main(args, MyGraph.Define());</c>
/// <list type="bullet">
/// <item><c>manifest [path]</c> writes graph.manifest.json (then <c>seamlineage graph</c> draws GRAPH.md from it).</item>
/// <item><c>run &lt;input.json&gt; [recordingsRoot]</c> runs the graph on an input and records every edge.</item>
/// <item><c>promote &lt;recordingDir&gt; &lt;stage&gt; &lt;case&gt; [fixturesRoot]</c> turns a recorded stage into an example.</item>
/// </list>
/// </summary>
public static class InProcHost
{
    public const string Usage =
        "usage: manifest [path] | run <input.json> [recordingsRoot] | promote <recordingDir> <stage> <caseName> [fixturesRoot]";

    public static int Main(string[] args, Graph graph, TextWriter? stdout = null, TextWriter? stderr = null, Func<Type, string>? codePath = null)
    {
        stdout ??= Console.Out;
        stderr ??= Console.Error;
        switch (args)
        {
            case ["manifest", .. var rest] when rest.Length <= 1:
            {
                var path = rest.FirstOrDefault() ?? "graph.manifest.json";
                File.WriteAllText(path, GraphManifest.Generate(graph, codePath));
                stdout.WriteLine($"Wrote {path}");
                return 0;
            }
            case ["run", var inputPath, .. var rest] when rest.Length <= 1:
            {
                var runId = $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..6]}";
                var recordings = Path.Combine(rest.FirstOrDefault() ?? "recordings", runId);
                var input = JsonSerializer.Deserialize(File.ReadAllText(inputPath), graph.InputType, WireJson.Options)
                    ?? throw new InvalidDataException($"{inputPath} holds no {graph.InputType.Name}.");
                new InProcRunner(new EdgeRecorder(recordings)).Run(graph, input, runId);
                stdout.WriteLine($"Ran {graph.Stages.Count} stages. Recorded to {recordings}");
                return 0;
            }
            case ["promote", var recordingDir, var stage, var caseName, .. var rest] when rest.Length <= 1:
            {
                var fixtures = rest.FirstOrDefault() ?? "fixtures";
                FixturePromoter.Promote(recordingDir, stage, caseName, fixtures);
                stdout.WriteLine($"Wrote {fixtures}/{stage}/{caseName}/. Check expected.json before committing: it records what the code did.");
                return 0;
            }
            default:
                stderr.WriteLine(Usage);
                return 2;
        }
    }
}
