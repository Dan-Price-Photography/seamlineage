namespace Seamlineage.Contracts;

/// <summary>A product-specific decision passed to a generic operator: a name and a one-line meaning.</summary>
public sealed record JudgmentInfo(string Name, string Description);

/// <summary>
/// One step of a composed stage: which generic operator, what it does, its parameters as a reviewer reads them
/// (judgments by name), and the judgments it uses.
/// </summary>
/// <param name="OperatorPhrase">
/// How the step reads as one line of the stage's pipeline text, as a template over its parameters: <c>{name}</c> is a
/// parameter's value and <c>[...]</c> is a part kept only when every parameter in it is given, e.g.
/// <c>"order each group by {by}"</c> (spec/manifest.md). Null: the tools write the operator's name and parameters.
/// </param>
public sealed record StepInfo(
    string Operator,
    string OperatorDescription,
    IReadOnlyList<KeyValuePair<string, string>> Parameters,
    IReadOnlyList<JudgmentInfo> Judgments,
    string? OperatorPhrase = null);

/// <summary>
/// A stage built from generic operators rather than written as one block of code. Its steps go into the manifest, so
/// the stage's logic, not just its input and output, shows in the manifest diff, the diagram and the pipeline text.
/// </summary>
public interface IComposedStage
{
    IReadOnlyList<StepInfo> Steps { get; }

    /// <summary>What the steps run over, as a reviewer reads it (e.g. "accepted picks"): the first line of the pipeline text.</summary>
    string Items => "items";
}
