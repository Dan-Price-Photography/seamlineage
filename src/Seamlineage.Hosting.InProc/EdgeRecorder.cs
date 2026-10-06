using System.Text.Json;
using Seamlineage.Contracts;

namespace Seamlineage.Hosting.InProc;

/// <summary>
/// Taps every edge (spec/recordings.md): appends each envelope as one JSON line to &lt;dir&gt;/&lt;NN&gt;-&lt;edge&gt;.jsonl.
/// Edge 00 is the graph input; edge N is stage N's output.
/// </summary>
public sealed class EdgeRecorder(string directory)
{
    private static readonly JsonSerializerOptions Compact = new(WireJson.Options) { WriteIndented = false };

    public string Directory { get; } = directory;

    public void Record(int index, string edge, Envelope<object> envelope)
    {
        System.IO.Directory.CreateDirectory(Directory);
        var path = Path.Combine(Directory, $"{index:00}-{edge}.jsonl");
        File.AppendAllText(path, JsonSerializer.Serialize(envelope, Compact) + "\n");
    }
}
