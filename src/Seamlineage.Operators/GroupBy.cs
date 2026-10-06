using Seamlineage.Contracts;

namespace Seamlineage.Operators;

public static class GroupByOperator
{
    public const string Description = "Splits the items into groups that share a key, in the order each key first appears.";

    public const string Phrase = "group by {key}";

    public static Pipeline<TIn, IReadOnlyList<IReadOnlyList<T>>> GroupBy<TIn, T, TKey>(
        this Pipeline<TIn, IReadOnlyList<T>> pipeline, Judgment<Func<T, TKey>> key) =>
        pipeline.Then<IReadOnlyList<IReadOnlyList<T>>>(
            new StepInfo("group-by", Description, [new("key", key.Name)], [key.Info], Phrase),
            items => [.. items.GroupBy(key.Apply).Select(g => (IReadOnlyList<T>)[.. g])]);
}
