namespace Seamlineage.Contracts;

/// <summary>A product-specific decision passed to a generic operator: a name and a one-line meaning.</summary>
public sealed record JudgmentInfo(string Name, string Description);

/// <summary>
/// One step of a composed stage: which generic operator, what it does, its parameters as a reviewer reads them
/// (judgments by name), and the judgments it uses.
/// </summary>
public sealed record StepInfo(
    string Operator,
    string OperatorDescription,
    IReadOnlyList<KeyValuePair<string, string>> Parameters,
    IReadOnlyList<JudgmentInfo> Judgments);

/// <summary>
/// A stage built from generic operators rather than written as one block of code. Its steps go into the manifest, so
/// the stage's logic, not just its input and output, shows in the manifest diff and the diagram.
/// </summary>
public interface IComposedStage
{
    IReadOnlyList<StepInfo> Steps { get; }
}
