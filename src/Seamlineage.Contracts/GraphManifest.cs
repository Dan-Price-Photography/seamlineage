using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Seamlineage.Contracts;

/// <summary>
/// Renders a graph as reviewable JSON (spec/manifest.md): stages in order, and the wire shape of every type crossing
/// an edge. Committed as graph.manifest.json so a PR shows what changed in the product's structure, not just its code.
/// </summary>
public static class GraphManifest
{
    // Stable across machines: the same bytes on Windows and Linux.
    private static readonly JsonSerializerOptions Output = new(WireJson.Options) { NewLine = "\n" };

    /// <summary>
    /// The default place of a stage's code: one stage class per file, <c>src/&lt;assembly&gt;/&lt;Class&gt;.cs</c>,
    /// relative to the directory the manifest is written to. Tools check the file exists.
    /// </summary>
    public static string ConventionalCodePath(Type stageType) =>
        $"src/{stageType.Assembly.GetName().Name}/{stageType.Name}.cs";

    /// <param name="codePath">Where each stage class's source lives; defaults to <see cref="ConventionalCodePath"/>.</param>
    public static string Generate(Graph graph, Func<Type, string>? codePath = null)
    {
        codePath ??= ConventionalCodePath;
        var types = new SortedDictionary<string, JsonNode>(StringComparer.Ordinal);
        var operators = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var stages = new JsonArray();

        Describe(graph.InputType, types);
        foreach (var stage in graph.Stages)
        {
            var node = new JsonObject
            {
                ["name"] = stage.Name,
                ["description"] = stage.Description,
                ["input"] = TypeName(stage.InputType),
                ["output"] = TypeName(stage.OutputType),
                ["schemaVersion"] = stage.SchemaVersion,
                ["code"] = codePath(stage.StageType),
            };
            if (stage.Steps is { } steps) DescribeSteps(steps, node, operators);
            stages.Add(node);
            Describe(stage.OutputType, types);
        }

        var manifest = new JsonObject
        {
            ["graph"] = graph.Name,
            ["input"] = TypeName(graph.InputType),
            ["stages"] = stages,
        };
        if (operators.Count > 0)
            manifest["operators"] = new JsonObject(operators.Select(o => KeyValuePair.Create(o.Key, (JsonNode?)o.Value)));
        manifest["types"] = new JsonObject(types.Select(t => KeyValuePair.Create(t.Key, (JsonNode?)t.Value)));
        return manifest.ToJsonString(Output) + "\n";
    }

    // A composed stage's logic as data: its steps in order, each judgment it uses once, and (graph-wide) each operator.
    private static void DescribeSteps(IReadOnlyList<StepInfo> steps, JsonObject stage, IDictionary<string, string> operators)
    {
        var judgments = new JsonObject();
        stage["steps"] = new JsonArray(steps.Select(step =>
        {
            operators[step.Operator] = step.OperatorDescription;
            foreach (var judgment in step.Judgments.Where(j => !judgments.ContainsKey(j.Name)))
                judgments[judgment.Name] = judgment.Description;
            return (JsonNode)new JsonObject
            {
                ["operator"] = step.Operator,
                ["parameters"] = new JsonObject(step.Parameters.Select(p => KeyValuePair.Create(p.Key, (JsonNode?)p.Value))),
            };
        }).ToArray());
        stage["judgments"] = judgments;
    }

    private static void Describe(Type type, IDictionary<string, JsonNode> types)
    {
        type = Unwrap(type);
        if (IsScalar(type) || types.ContainsKey(type.Name)) return;

        if (type.IsEnum)
        {
            types[type.Name] = new JsonArray(Enum.GetNames(type).Select(n => (JsonNode)JsonNamingPolicy.CamelCase.ConvertName(n)).ToArray());
            return;
        }

        var shape = new JsonObject();
        types[type.Name] = shape; // registered before recursing, so self-references terminate
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(p => p.GetIndexParameters().Length == 0 && p.Name != "EqualityContract")
                     .OrderBy(p => p.MetadataToken))
        {
            shape[JsonNamingPolicy.CamelCase.ConvertName(property.Name)] = TypeName(property);
            Describe(property.PropertyType, types);
        }
    }

    private static string TypeName(PropertyInfo property)
    {
        var name = TypeName(property.PropertyType);
        var nullable = !property.PropertyType.IsValueType
            && new NullabilityInfoContext().Create(property).ReadState == NullabilityState.Nullable;
        return nullable ? name + "?" : name;
    }

    private static string TypeName(Type type)
    {
        if (Nullable.GetUnderlyingType(type) is { } inner) return TypeName(inner) + "?";
        if (ElementType(type) is { } element) return TypeName(element) + "[]";
        if (type.IsEnum) return type.Name; // enums report their underlying TypeCode (Int32)
        return Type.GetTypeCode(type) switch
        {
            TypeCode.String => "string",
            TypeCode.Int32 => "int",
            TypeCode.Int64 => "long",
            TypeCode.Boolean => "bool",
            TypeCode.Double => "double",
            TypeCode.Decimal => "decimal",
            _ when type == typeof(DateTimeOffset) => "datetime",
            _ when type == typeof(DateTime) => "localdatetime", // a wall-clock time with no offset
            _ when type == typeof(Guid) => "guid",
            _ => type.Name,
        };
    }

    private static Type Unwrap(Type type) =>
        Nullable.GetUnderlyingType(type) ?? (ElementType(type) is { } element ? Unwrap(element) : type);

    private static Type? ElementType(Type type) =>
        type == typeof(string) ? null
        : type.IsArray ? type.GetElementType()
        : type.GetInterfaces().Append(type)
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            ?.GetGenericArguments()[0];

    private static bool IsScalar(Type type) =>
        type.IsPrimitive || type == typeof(string) || type == typeof(decimal)
        || type == typeof(DateTimeOffset) || type == typeof(DateTime) || type == typeof(Guid);
}
