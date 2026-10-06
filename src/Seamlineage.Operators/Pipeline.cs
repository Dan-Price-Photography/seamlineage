using Seamlineage.Contracts;

namespace Seamlineage.Operators;

/// <summary>
/// A chain of generic operators that both runs and describes itself: <see cref="Run"/> applies the steps in order,
/// and <see cref="Steps"/> lists them as data for the manifest. Built once; holds no state between runs.
/// </summary>
public sealed class Pipeline<TIn, TOut>
{
    private readonly Func<TIn, TOut> _run;

    internal Pipeline(Func<TIn, TOut> run, IReadOnlyList<StepInfo> steps)
    {
        _run = run;
        Steps = steps;
    }

    public IReadOnlyList<StepInfo> Steps { get; }

    public TOut Run(TIn input) => _run(input);

    internal Pipeline<TIn, TNext> Then<TNext>(StepInfo step, Func<TOut, TNext> next)
    {
        var run = _run;
        return new Pipeline<TIn, TNext>(input => next(run(input)), [.. Steps, step]);
    }
}

public static class Pipeline
{
    /// <summary>An empty pipeline over a list of items; add operators to it.</summary>
    public static Pipeline<IReadOnlyList<T>, IReadOnlyList<T>> Of<T>() => new(items => items, []);
}
