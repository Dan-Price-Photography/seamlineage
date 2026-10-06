using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Seamlineage.Contracts;

/// <summary>
/// The one serialization format for examples, recordings, the manifest and the wire: camelCase names, camelCase enum
/// strings, ISO 8601 times, and minimal escaping (see spec/examples.md).
/// </summary>
public static class WireJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },

        // Escape only what JSON requires, so ' & < > and non-ASCII text read in a diff as written. The default encoder
        // escapes them for safe embedding in HTML, which wire JSON never is; "unsafe" refers only to that.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}
