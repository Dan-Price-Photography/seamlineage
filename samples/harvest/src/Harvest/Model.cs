namespace Harvest;

// The data on the harvest graph's edges. Every type here appears in graph.manifest.json.

/// <summary>What the pickers logged: one entry per piece of fruit picked.</summary>
public sealed record PickLog(IReadOnlyList<Pick> Picks);

/// <param name="Tree">Which tree, as "row-N/tree-N".</param>
/// <param name="PickedAt">The picker's watch time; null if they forgot to note it.</param>
public sealed record Pick(string Id, string Tree, string Variety, int WeightGrams, DateTime? PickedAt);

public enum RejectReason { NoWeight, UnknownVariety }

public sealed record RejectedPick(string Id, RejectReason Reason);

public sealed record CheckedPicks(IReadOnlyList<Pick> Accepted, IReadOnlyList<RejectedPick> Rejected);

public enum BasketKind { Basket, Loose }

/// <param name="Key">The id of the basket's first pick.</param>
public sealed record Basket(string Key, BasketKind Kind, IReadOnlyList<Pick> Picks);

public sealed record Baskets(IReadOnlyList<Basket> Groups);

public sealed record BasketWeight(string Key, BasketKind Kind, int Picks, long Grams);

public sealed record HarvestReport(IReadOnlyList<BasketWeight> Baskets, long TotalGrams);
