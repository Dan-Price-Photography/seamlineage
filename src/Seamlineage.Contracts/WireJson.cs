using System.Text.Json;
using System.Text.Json.Serialization;

namespace Seamlineage.Contracts;

/// <summary>
/// The one serialization format for examples, recordings and the wire: camelCase names, camelCase enum strings,
/// ISO 8601 times (see spec/examples.md).
/// </summary>
public static class WireJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };
}
