using Seamlineage.Contracts;

namespace Harvest;

/// <summary>Baskets → the weight of each, and of the whole harvest.</summary>
public sealed class WeighBaskets : IStage<Baskets, HarvestReport>
{
    public StageResult<HarvestReport> Run(Baskets input, StageContext context)
    {
        var weights = input.Groups
            .Select(b => new BasketWeight(b.Key, b.Kind, b.Picks.Count, b.Picks.Sum(p => (long)p.WeightGrams)))
            .ToList();
        return StageResult<HarvestReport>.Pure(new HarvestReport(weights, weights.Sum(w => w.Grams)));
    }
}
