using Seamlineage.Contracts;

namespace Seamlineage.Operators;

/// <summary>
/// A product-specific function handed to a generic operator, with a name and a one-line meaning. Operators only accept
/// judgments, never bare lambdas, so every custom decision in a composed stage shows up by name in the manifest.
/// </summary>
public sealed record Judgment<TFunc>(string Name, string Description, TFunc Apply) where TFunc : Delegate
{
    public JudgmentInfo Info => new(Name, Description);
}

public static class Judgment
{
    /// <summary>A judgment about one item, e.g. its key or its time.</summary>
    public static Judgment<Func<T, TResult>> Of<T, TResult>(string name, string description, Func<T, TResult> apply) =>
        new(name, description, apply);

    /// <summary>A judgment about the next item given the session so far, e.g. whether it breaks the session.</summary>
    public static Judgment<Func<IReadOnlyList<T>, T, bool>> Of<T>(string name, string description, Func<IReadOnlyList<T>, T, bool> apply) =>
        new(name, description, apply);
}
