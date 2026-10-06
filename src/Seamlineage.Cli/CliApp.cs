using System.Reflection;
using Seamlineage.Docs;

namespace Seamlineage.Cli;

/// <summary>
/// The shared <c>seamlineage</c> command line. It reads only a manifest and an examples folder, never a product's
/// assemblies, so it serves any implementation language. Each command is one entry in <see cref="Commands"/>; new
/// views (pipeline text, example tables, a per-PR change diagram) are added as new commands or as new
/// <see cref="IDiagramSection"/>s of the page.
/// </summary>
public static class CliApp
{
    private sealed record Command(string Summary, string[] Options, Func<IReadOnlyDictionary<string, string>, TextWriter, TextWriter, int> Run);

    private static readonly Dictionary<string, Command> Commands = new(StringComparer.Ordinal)
    {
        ["graph"] = new("Write GRAPH.md from a manifest and its examples.", ["manifest", "fixtures", "out"], Graph),
        ["check"] = new(
            "Exit 1 if GRAPH.md is stale, a link in it does not resolve, or (with --fixtures) a stage has no examples or an examples folder names no stage.",
            ["manifest", "fixtures", "out"],
            Check),
    };

    public static int Run(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args is [] or ["help" or "-h" or "--help"])
        {
            stdout.Write(Usage());
            return 0;
        }
        if (args is ["--version"])
        {
            stdout.WriteLine(typeof(CliApp).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown");
            return 0;
        }
        if (!Commands.TryGetValue(args[0], out var command))
        {
            stderr.WriteLine($"Unknown command '{args[0]}'.");
            stderr.Write(Usage());
            return 2;
        }
        if (ParseOptions(args[1..], command.Options, out var options) is { } error)
        {
            stderr.WriteLine(error);
            stderr.Write(Usage());
            return 2;
        }

        try
        {
            return command.Run(options, stdout, stderr);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or System.Text.Json.JsonException or UnauthorizedAccessException)
        {
            stderr.WriteLine($"seamlineage {args[0]}: {ex.Message}");
            return 2;
        }
    }

    private static int Graph(IReadOnlyDictionary<string, string> options, TextWriter stdout, TextWriter stderr)
    {
        var (manifest, fixtures, outPath) = Paths(options);
        var page = GraphDocs.Render(manifest, fixtures, outPath);
        if (Path.GetDirectoryName(Path.GetFullPath(outPath)) is { } dir) Directory.CreateDirectory(dir);
        File.WriteAllText(outPath, page);
        stdout.WriteLine($"Wrote {outPath}");
        return 0;
    }

    private static int Check(IReadOnlyDictionary<string, string> options, TextWriter stdout, TextWriter stderr)
    {
        var (manifest, fixtures, outPath) = Paths(options);
        var problems = GraphDocs.Check(manifest, fixtures, outPath);
        foreach (var problem in problems) stderr.WriteLine(problem);
        if (problems.Count > 0) return 1;
        stdout.WriteLine($"{outPath} is current and every link resolves.");
        return 0;
    }

    private static (string Manifest, string? Fixtures, string Out) Paths(IReadOnlyDictionary<string, string> options)
    {
        var manifest = options.GetValueOrDefault("manifest", "graph.manifest.json");
        if (!File.Exists(manifest)) throw new FileNotFoundException($"No manifest at {manifest}.");
        var fixtures = options.GetValueOrDefault("fixtures");
        if (fixtures is not null && !Directory.Exists(fixtures)) throw new DirectoryNotFoundException($"No examples folder at {fixtures}.");
        return (manifest, fixtures, options.GetValueOrDefault("out", "GRAPH.md"));
    }

    private static string? ParseOptions(string[] args, string[] allowed, out Dictionary<string, string> options)
    {
        options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < args.Length; i++)
        {
            var name = args[i].StartsWith("--", StringComparison.Ordinal) ? args[i][2..] : null;
            if (name is null || !allowed.Contains(name)) return $"Unknown option '{args[i]}'.";
            if (i + 1 >= args.Length) return $"Option '{args[i]}' needs a value.";
            options[name] = args[++i];
        }
        return null;
    }

    private static string Usage()
    {
        var lines = new List<string>
        {
            "seamlineage: generate and check the reviewable views of a Seamlineage graph from its manifest.",
            "",
            "usage: seamlineage <command> [--manifest graph.manifest.json] [--fixtures <dir>] [--out GRAPH.md]",
            "",
            "commands:",
        };
        lines.AddRange(Commands.Select(c => $"  {c.Key,-8}{c.Value.Summary}"));
        lines.AddRange([
            "",
            "options:",
            "  --manifest  the manifest to read (default graph.manifest.json)",
            "  --fixtures  the examples folder, fixtures/<stage>/<case>/ (optional; without it no examples are linked)",
            "  --out       the page to write or check (default GRAPH.md); links in it are relative to it",
            "",
        ]);
        return string.Join("\n", lines);
    }
}
