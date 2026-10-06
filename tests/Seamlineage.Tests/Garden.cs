using Seamlineage.Contracts;

namespace Seamlineage.Tests;

/// <summary>A tiny synthetic graph used across the tests: seeds are sown, then counted.</summary>
public static class Garden
{
    public sealed record Seed(string Name, int Count);

    public sealed record Packet(IReadOnlyList<Seed> Seeds);

    public enum Size { Small, Large }

    public sealed record Sown(string Name, Size Size);

    public sealed record Bed(IReadOnlyList<Sown> Rows);

    public sealed record Tally(int Rows, int Large);

    public sealed class Sow : IStage<Packet, Bed>
    {
        public StageResult<Bed> Run(Packet input, StageContext context) =>
            StageResult<Bed>.Pure(new Bed([.. input.Seeds.Select(s => new Sown(s.Name, s.Count >= 10 ? Size.Large : Size.Small))]));
    }

    public sealed class Count : IStage<Bed, Tally>
    {
        public StageResult<Tally> Run(Bed input, StageContext context) =>
            StageResult<Tally>.Pure(new Tally(input.Rows.Count, input.Rows.Count(r => r.Size == Size.Large)));
    }

    public static Graph Define() =>
        Graph.Start<Packet>("garden")
            .Then("sow", "Decides how big each row is: 10 or more seeds is large.", new Sow())
            .Then("count", "Counts the rows, and the large ones.", new Count(), schemaVersion: 2)
            .Build();

    public static readonly Packet Sample = new([new Seed("pea", 12), new Seed("bean", 3)]);
}
