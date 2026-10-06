using System.Globalization;
using System.Text.Json;
using Seamlineage.Contracts;

namespace Seamlineage.Operators;

/// <summary>A group of items and the label an operator gave it.</summary>
public sealed record Labelled<TLabel, T>(TLabel Label, IReadOnlyList<T> Items);

public static class SplitSmallGroupsOperator
{
    public const string Description =
        "Keeps each group of at least minimum items whole, labelled keptAs; splits each smaller group into groups of "
        + "one item, each labelled splitAs. Groups stay in order, and the items of a split group stay in its place.";

    public static Pipeline<TIn, IReadOnlyList<Labelled<TLabel, T>>> SplitSmallGroups<TIn, T, TLabel>(
        this Pipeline<TIn, IReadOnlyList<IReadOnlyList<T>>> pipeline, int minimum, TLabel keptAs, TLabel splitAs) =>
        pipeline.Then<IReadOnlyList<Labelled<TLabel, T>>>(
            new StepInfo(
                "split-small-groups",
                Description,
                [new("minimum", minimum.ToString(CultureInfo.InvariantCulture)), new("keptAs", Show(keptAs)), new("splitAs", Show(splitAs))],
                []),
            groups =>
            [
                .. groups.SelectMany(g => g.Count >= minimum
                    ? [new Labelled<TLabel, T>(keptAs, g)]
                    : g.Select(item => new Labelled<TLabel, T>(splitAs, [item]))),
            ]);

    // A label reads as it does on the wire: enum values in camelCase, like the manifest lists them.
    private static string Show<TLabel>(TLabel label) =>
        label is Enum ? JsonNamingPolicy.CamelCase.ConvertName(label.ToString()!) : Convert.ToString(label, CultureInfo.InvariantCulture) ?? "";
}
