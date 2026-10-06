namespace Seamlineage.Contracts;

/// <summary>One column of an example table: its heading, and a dot-path to its value (spec/examples.md).</summary>
public sealed record ExampleColumn(string Label, string Path);

/// <summary>
/// How a row's outcome is read from expected.json: <paramref name="From"/> names the list of output records; a row
/// matches the record holding a member (each item of <paramref name="Members"/>, or the record itself when that is
/// null) whose <paramref name="Match"/> value equals the row's; <paramref name="Columns"/> are read from that record.
/// </summary>
public sealed record ExampleOutcome(string From, string Match, IReadOnlyList<ExampleColumn> Columns, string? Members = null);

/// <summary>
/// How a stage's examples read as tables of input → outcome instead of JSON: one row per item of the
/// <paramref name="Rows"/> list in input.json, with <paramref name="Columns"/> read from each item, then its
/// <paramref name="Outcome"/> columns. Paths are dot-paths (spec/examples.md); no expressions.
/// </summary>
public sealed record ExampleView(string Rows, IReadOnlyList<ExampleColumn> Columns, ExampleOutcome? Outcome = null);

/// <summary>A stage that declares how its examples are tabulated; the view is written into the manifest.</summary>
public interface IHasExampleView
{
    ExampleView ExampleView { get; }
}
