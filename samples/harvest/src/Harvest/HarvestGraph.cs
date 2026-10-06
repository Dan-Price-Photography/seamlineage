using Seamlineage.Contracts;

namespace Harvest;

/// <summary>A toy product: an orchard's pick log, checked, grouped into baskets and weighed.</summary>
public static class HarvestGraph
{
    public static Graph Define() =>
        Graph.Start<PickLog>("harvest")
            .Then("check-picks",
                "Decides which logged picks count, and records a reason for every pick rejected: no weight, or a variety the orchard does not grow.",
                new CheckPicks())
            .Then("group-baskets",
                "Groups 3 or more picks from one row, each within 5 minutes of the last and of the same variety, into a basket; every other pick is loose, a group of one.",
                new GroupBaskets())
            .Then("weigh-baskets",
                "Totals the weight of each basket and of the whole harvest, loose picks included.",
                new WeighBaskets())
            .Build();
}
