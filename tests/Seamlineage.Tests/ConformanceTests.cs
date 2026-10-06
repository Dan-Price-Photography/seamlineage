using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Seamlineage.Contracts;
using Seamlineage.Operators;
using Seamlineage.Testing;

namespace Seamlineage.Tests;

/// <summary>
/// The language-neutral conformance suite (spec/conformance/): every case under
/// spec/conformance/operators/&lt;operator&gt;/&lt;case&gt;/ run against Seamlineage.Operators. Judgments are plain
/// data in each item, named by the case's parameters, so a case needs no code: this class is the C# adapter.
/// </summary>
public class ConformanceTests
{
    private static readonly string Root = Path.Combine(RepoPaths.FindAbove("Seamlineage.slnx"), "spec", "conformance", "operators");

    public static TheoryData<string, string> Cases()
    {
        var data = new TheoryData<string, string>();
        foreach (var c in Fixtures.Discover(Root)) data.Add(c.Stage, c.Case);
        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Operator_output_is_json_equal_to_expected(string @operator, string @case)
    {
        var dir = Path.Combine(Root, @operator, @case);
        var input = JsonNode.Parse(File.ReadAllText(Path.Combine(dir, "input.json")))!.AsObject();
        var parameters = input["parameters"]!.AsObject();

        object output = @operator switch
        {
            "group-by" => GroupBy(Items(input["items"]), parameters),
            "order-by" => OrderBy(Groups(input["groups"]), parameters),
            "session" => Session(Groups(input["groups"]), parameters),
            "split-small-groups" => SplitSmallGroups(Groups(input["groups"]), parameters),
            _ => throw new CheckFailedException($"No operator named '{@operator}'."),
        };

        var actual = JsonNode.Parse(JsonSerializer.Serialize(new { groups = output }, WireJson.Options));
        var expected = JsonNode.Parse(File.ReadAllText(Path.Combine(dir, "expected.json")));
        if (!JsonNode.DeepEquals(expected, actual))
            throw new CheckFailedException(
                $"{@operator}/{@case} differs from expected.json:\n  {string.Join("\n  ", JsonDiff.Describe(expected, actual))}\n--- actual\n{actual}");
    }

    [Fact]
    public void The_suite_covers_every_operator()
    {
        Assert.Equal(["group-by", "order-by", "session", "split-small-groups"], Fixtures.Discover(Root).Select(c => c.Stage).Distinct());
    }

    // An item is a JSON object; a judgment's result for it is the field the parameters name.

    private static IReadOnlyList<IReadOnlyList<JsonObject>> GroupBy(List<JsonObject> items, JsonObject parameters)
    {
        var key = Field((string)parameters["key"]!);
        return Pipeline.Of<JsonObject>().GroupBy(key).Run(items);
    }

    private static IReadOnlyList<IReadOnlyList<JsonObject>> OrderBy(List<List<JsonObject>> groups, JsonObject parameters)
    {
        var by = parameters["by"]!.AsArray().Select(k => Field((string)k!)).ToList();
        var start = FromGroups();
        var pipeline = by switch
        {
            [var only] => start.OrderBy(Tagged(only)),
            [var first, var second] => start.OrderBy(Tagged(first), Tagged(second)),
            _ => throw new CheckFailedException("The C# order-by takes one or two keys."),
        };
        return Untag(pipeline.Run(Tagging(groups)));
    }

    private static IReadOnlyList<IReadOnlyList<JsonObject>> Session(List<List<JsonObject>> groups, JsonObject parameters)
    {
        var atField = (string)parameters["at"]!;
        var at = Judgment.Of(atField, "The item's time.", (Tag t) => Time(t.Item[atField]));
        var gap = TimeSpan.FromSeconds((double)parameters["maxGapSeconds"]!);
        var breakWhen = (parameters["breakWhen"]?.AsArray() ?? [])
            .Select(b => (string)b!)
            .Select(name => Judgment.Of(name, "Whether the item breaks the session.", (IReadOnlyList<Tag> _, Tag next) => (bool?)next.Item[name] == true))
            .ToArray();
        return Untag(FromGroups().Session(at, gap, breakWhen).Run(Tagging(groups)));
    }

    private static IReadOnlyList<Labelled<string, JsonObject>> SplitSmallGroups(List<List<JsonObject>> groups, JsonObject parameters)
    {
        var split = FromGroups().SplitSmallGroups((int)parameters["minimum"]!, (string)parameters["keptAs"]!, (string)parameters["splitAs"]!);
        return [.. split.Run(Tagging(groups)).Select(g => new Labelled<string, JsonObject>(g.Label, [.. g.Items.Select(t => t.Item)]))];
    }

    // The value of a field as a sortable, comparable key: text, a number, a boolean, or null when null or absent.
    private static Judgment<Func<JsonObject, object?>> Field(string name) =>
        Judgment.Of(name, $"The item's {name}.", (JsonObject item) => Key(item[name]));

    private static Judgment<Func<Tag, object?>> Tagged(Judgment<Func<JsonObject, object?>> field) =>
        Judgment.Of(field.Name, field.Description, (Tag t) => field.Apply(t.Item));

    private static object? Key(JsonNode? value) => value switch
    {
        null => null,
        JsonValue v when v.TryGetValue<string>(out var text) => text,
        JsonValue v when v.TryGetValue<bool>(out var flag) => flag,
        JsonValue v => v.GetValue<decimal>(),
        _ => throw new CheckFailedException($"A key must be text, a number, a boolean or null, not {value.ToJsonString()}."),
    };

    private static DateTime? Time(JsonNode? value) =>
        value is null ? null : DateTime.Parse((string)value!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    // The operators after group-by take groups. The adapter rebuilds them with group-by on each item's group index.
    private sealed record Tag(int Group, JsonObject Item);

    private static Pipeline<IReadOnlyList<Tag>, IReadOnlyList<IReadOnlyList<Tag>>> FromGroups() =>
        Pipeline.Of<Tag>().GroupBy(Judgment.Of("group", "The item's group in the input.", (Tag t) => t.Group));

    private static List<Tag> Tagging(List<List<JsonObject>> groups)
    {
        if (groups.Any(g => g.Count == 0)) throw new CheckFailedException("Input groups are never empty (spec/operators.md).");
        return [.. groups.SelectMany((g, i) => g.Select(item => new Tag(i, item)))];
    }

    private static IReadOnlyList<IReadOnlyList<JsonObject>> Untag(IReadOnlyList<IReadOnlyList<Tag>> groups) =>
        [.. groups.Select(g => (IReadOnlyList<JsonObject>)[.. g.Select(t => t.Item)])];

    private static List<JsonObject> Items(JsonNode? items) => [.. items!.AsArray().Select(i => i!.AsObject())];

    private static List<List<JsonObject>> Groups(JsonNode? groups) => [.. groups!.AsArray().Select(g => Items(g))];
}
