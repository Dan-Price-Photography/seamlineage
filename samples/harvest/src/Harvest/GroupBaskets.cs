using Seamlineage.Contracts;
using Seamlineage.Operators;

namespace Harvest;

/// <summary>
/// Picks → baskets, composed from generic operators: group by row, put in picking order, split into sessions, keep
/// sessions of 3 or more as baskets and split the rest into loose picks. The steps and the judgments below are listed
/// in graph.manifest.json and drawn in GRAPH.md; this class only names the judgments and shapes the output.
/// </summary>
public sealed class GroupBaskets : IStage<CheckedPicks, Baskets>, IComposedStage, IHasExampleView
{
    // Judgments first: the pipeline below reads them when it is built.

    private static readonly Judgment<Func<Pick, string>> Row = Judgment.Of(
        "row", "The row part of the tree id (row-3 of row-3/tree-12), so a basket never spans rows.",
        (Pick p) => p.Tree.IndexOf('/') is var slash and >= 0 ? p.Tree[..slash] : p.Tree);

    private static readonly Judgment<Func<Pick, DateTime?>> PickedAt = Judgment.Of(
        "picked-at", "When the fruit was picked by the picker's watch; none if it was not noted.",
        (Pick p) => p.PickedAt);

    private static readonly Judgment<Func<Pick, string>> Id = Judgment.Of(
        "id", "The pick's id, which keeps log order among picks noted at the same minute.",
        (Pick p) => p.Id);

    private static readonly Judgment<Func<IReadOnlyList<Pick>, Pick, bool>> VarietyChanges = Judgment.Of(
        "variety-changes", "The pick is a different variety from the one before it, so a basket holds one variety.",
        (IReadOnlyList<Pick> basket, Pick next) => next.Variety != basket[^1].Variety);

    private static readonly Pipeline<IReadOnlyList<Pick>, IReadOnlyList<Labelled<BasketKind, Pick>>> Grouping =
        Pipeline.Of<Pick>("accepted picks")
            .GroupBy(Row)
            .OrderBy(PickedAt, Id)
            .Session(PickedAt, maxGap: TimeSpan.FromMinutes(5), VarietyChanges)
            .SplitSmallGroups(minimum: 3, keptAs: BasketKind.Basket, splitAs: BasketKind.Loose);

    public IReadOnlyList<StepInfo> Steps => Grouping.Steps;

    public string Items => Grouping.Items;

    /// <summary>Each example as a table: one row per accepted pick, then the basket it ended up in and that basket's kind.</summary>
    public ExampleView ExampleView { get; } = new(
        Rows: "accepted",
        Columns: [new("pick", "id"), new("tree", "tree"), new("variety", "variety"), new("picked at", "pickedAt")],
        Outcome: new(From: "groups", Match: "id", Columns: [new("basket", "key"), new("kind", "kind")], Members: "picks"));

    public StageResult<Baskets> Run(CheckedPicks input, StageContext context) =>
        StageResult<Baskets>.Pure(new Baskets(
            [.. Grouping.Run(input.Accepted)
                .Select(g => new Basket(g.Items[0].Id, g.Label, g.Items))
                .OrderBy(g => g.Key, StringComparer.Ordinal)]));
}
