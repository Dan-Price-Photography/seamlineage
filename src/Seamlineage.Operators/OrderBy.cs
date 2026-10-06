using Seamlineage.Contracts;

namespace Seamlineage.Operators;

public static class OrderByOperator
{
    public const string Description =
        "Sorts the items within each group by the keys in turn, keeping the original order for ties; an item with no "
        + "value for a key sorts after those with one, and text compares ordinally.";

    public static Pipeline<TIn, IReadOnlyList<IReadOnlyList<T>>> OrderBy<TIn, T, TKey>(
        this Pipeline<TIn, IReadOnlyList<IReadOnlyList<T>>> pipeline, Judgment<Func<T, TKey>> by) =>
        pipeline.Then(Step(by.Info), Sort(KeyComparer<T, TKey>(by.Apply)));

    public static Pipeline<TIn, IReadOnlyList<IReadOnlyList<T>>> OrderBy<TIn, T, TKey, TThen>(
        this Pipeline<TIn, IReadOnlyList<IReadOnlyList<T>>> pipeline, Judgment<Func<T, TKey>> by, Judgment<Func<T, TThen>> thenBy)
    {
        var first = KeyComparer<T, TKey>(by.Apply);
        var second = KeyComparer<T, TThen>(thenBy.Apply);
        var both = Comparer<T>.Create((a, b) => first.Compare(a, b) is var c and not 0 ? c : second.Compare(a, b));
        return pipeline.Then(Step(by.Info, thenBy.Info), Sort(both));
    }

    private static StepInfo Step(params JudgmentInfo[] keys) =>
        new("order-by", Description, [new("by", string.Join(", ", keys.Select(k => k.Name)))], keys);

    // Enumerable.Order is a stable sort.
    private static Func<IReadOnlyList<IReadOnlyList<T>>, IReadOnlyList<IReadOnlyList<T>>> Sort<T>(IComparer<T> comparer) =>
        groups => [.. groups.Select(g => (IReadOnlyList<T>)[.. g.Order(comparer)])];

    private static IComparer<T> KeyComparer<T, TKey>(Func<T, TKey> key) =>
        Comparer<T>.Create((a, b) => CompareKeys(key(a), key(b)));

    private static int CompareKeys<TKey>(TKey a, TKey b) => (a, b) switch
    {
        (null, null) => 0,
        (null, _) => 1,
        (_, null) => -1,
        (string x, string y) => string.CompareOrdinal(x, y),
        _ => Comparer<TKey>.Default.Compare(a, b),
    };
}
