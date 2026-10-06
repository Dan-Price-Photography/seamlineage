using Seamlineage.Contracts;

namespace Harvest;

/// <summary>Logged picks → the ones that count, and a reason for each one that doesn't. A plain stage: one block of code.</summary>
public sealed class CheckPicks : IStage<PickLog, CheckedPicks>
{
    /// <summary>The varieties the orchard grows. Defined once, here.</summary>
    public static readonly IReadOnlySet<string> Varieties = new HashSet<string>(StringComparer.Ordinal) { "bramley", "cox", "gala", "russet" };

    public StageResult<CheckedPicks> Run(PickLog input, StageContext context)
    {
        var accepted = new List<Pick>();
        var rejected = new List<RejectedPick>();
        foreach (var pick in input.Picks)
        {
            RejectReason? reason = pick.WeightGrams <= 0 ? RejectReason.NoWeight
                : !Varieties.Contains(pick.Variety) ? RejectReason.UnknownVariety
                : null;
            if (reason is { } r) rejected.Add(new RejectedPick(pick.Id, r));
            else accepted.Add(pick);
        }
        return StageResult<CheckedPicks>.Pure(new CheckedPicks(accepted, rejected));
    }
}
