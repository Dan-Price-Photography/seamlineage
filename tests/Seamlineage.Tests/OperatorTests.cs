using Seamlineage.Contracts;
using Seamlineage.Operators;

namespace Seamlineage.Tests;

/// <summary>The generic building blocks, on plain synthetic data: each does one thing, described as data.</summary>
public class OperatorTests
{
    private sealed record Event(string Name, string Room, DateTime? At = null, int? Size = null);

    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0);

    private static readonly Judgment<Func<Event, string>> Room = Judgment.Of("room", "The room the event is in.", (Event e) => e.Room);
    private static readonly Judgment<Func<Event, DateTime?>> At = Judgment.Of("at", "When the event happened.", (Event e) => e.At);
    private static readonly Judgment<Func<Event, string>> Name = Judgment.Of("name", "The event's name.", (Event e) => e.Name);
    private static readonly Judgment<Func<Event, int?>> Size = Judgment.Of("size", "How big the event is.", (Event e) => e.Size);

    private static readonly Judgment<Func<IReadOnlyList<Event>, Event, bool>> Shrinks = Judgment.Of(
        "shrinks", "The event is smaller than the one before.", (IReadOnlyList<Event> session, Event next) => next.Size < session[^1].Size);

    private static List<List<string>> Names(IReadOnlyList<IReadOnlyList<Event>> groups) =>
        groups.Select(g => g.Select(e => e.Name).ToList()).ToList();

    [Fact]
    public void Group_by_splits_items_by_key_in_order_of_first_appearance()
    {
        var groups = Pipeline.Of<Event>().GroupBy(Room).Run([new("a", "x"), new("b", "y"), new("c", "x")]);

        Assert.Equal([["a", "c"], ["b"]], Names(groups));
    }

    [Fact]
    public void Order_by_sorts_within_each_group_and_leaves_groups_in_place()
    {
        var groups = Pipeline.Of<Event>().GroupBy(Room).OrderBy(At).Run([
            new("x2", "x", T0.AddSeconds(2)), new("y1", "y", T0.AddSeconds(1)), new("x1", "x", T0.AddSeconds(1)),
        ]);

        Assert.Equal([["x1", "x2"], ["y1"]], Names(groups));
    }

    [Fact]
    public void Order_by_puts_items_with_no_value_last_and_breaks_ties_with_the_next_key()
    {
        var groups = Pipeline.Of<Event>().GroupBy(Room).OrderBy(At, Name).Run([
            new("none", "x"), new("b", "x", T0), new("a", "x", T0), new("early", "x", T0.AddSeconds(-1)),
        ]);

        Assert.Equal([["early", "a", "b", "none"]], Names(groups));
    }

    [Fact]
    public void Order_by_compares_text_ordinally_not_by_culture()
    {
        var groups = Pipeline.Of<Event>().GroupBy(Room).OrderBy(Name).Run([new("b", "x"), new("_", "x"), new("B", "x")]);

        Assert.Equal([["B", "_", "b"]], Names(groups)); // ordinal: 'B' (66) < '_' (95) < 'b' (98)
    }

    [Fact]
    public void Session_joins_items_at_most_the_gap_apart_and_splits_at_a_longer_gap()
    {
        var groups = Pipeline.Of<Event>().GroupBy(Room).Session(At, TimeSpan.FromSeconds(2)).Run([
            new("a", "x", T0), new("b", "x", T0.AddSeconds(2)), new("c", "x", T0.AddSeconds(4.001)),
        ]);

        Assert.Equal([["a", "b"], ["c"]], Names(groups));
    }

    [Fact]
    public void Session_never_joins_items_across_groups()
    {
        var groups = Pipeline.Of<Event>().GroupBy(Room).Session(At, TimeSpan.FromSeconds(2)).Run([
            new("x1", "x", T0), new("y1", "y", T0), new("x2", "x", T0),
        ]);

        Assert.Equal([["x1", "x2"], ["y1"]], Names(groups));
    }

    [Fact]
    public void Session_puts_an_item_with_no_time_in_a_session_of_its_own()
    {
        var groups = Pipeline.Of<Event>().GroupBy(Room).Session(At, TimeSpan.FromSeconds(2)).Run([
            new("a", "x", T0), new("none", "x"), new("b", "x", T0),
        ]);

        Assert.Equal([["a"], ["none"], ["b"]], Names(groups));
    }

    [Fact]
    public void Session_starts_a_new_session_when_a_break_judgment_holds()
    {
        var groups = Pipeline.Of<Event>().GroupBy(Room).Session(At, TimeSpan.FromSeconds(2), Shrinks).Run([
            new("a", "x", T0, 1), new("b", "x", T0, 2), new("c", "x", T0, 1), new("d", "x", T0, 3),
        ]);

        Assert.Equal([["a", "b"], ["c", "d"]], Names(groups));
    }

    [Fact]
    public void Session_passes_the_whole_session_so_far_to_a_break_judgment()
    {
        var seen = new List<int>();
        var spy = Judgment.Of("spy", "Records how long the session is.", (IReadOnlyList<Event> session, Event next) =>
        {
            seen.Add(session.Count);
            return false;
        });

        Pipeline.Of<Event>().GroupBy(Room).Session(At, TimeSpan.FromSeconds(2), spy).Run([
            new("a", "x", T0), new("b", "x", T0), new("c", "x", T0),
        ]);

        Assert.Equal([1, 2], seen);
    }

    private enum Turnout { Crowd, Loner }

    // "label: names", so a whole group compares as one value.
    private static List<string> Labelled(IReadOnlyList<Labelled<Turnout, Event>> groups) =>
        groups.Select(g => $"{g.Label}: {string.Join(" ", g.Items.Select(e => e.Name))}").ToList();

    [Fact]
    public void Split_small_groups_keeps_a_group_of_the_minimum_whole_and_labels_it()
    {
        var groups = Pipeline.Of<Event>().GroupBy(Room).SplitSmallGroups(minimum: 2, keptAs: Turnout.Crowd, splitAs: Turnout.Loner)
            .Run([new("x1", "x"), new("x2", "x"), new("y1", "y"), new("y2", "y"), new("y3", "y")]);

        Assert.Equal(["Crowd: x1 x2", "Crowd: y1 y2 y3"], Labelled(groups));
    }

    [Fact]
    public void Split_small_groups_turns_a_smaller_group_into_groups_of_one_in_place()
    {
        var groups = Pipeline.Of<Event>().GroupBy(Room).SplitSmallGroups(minimum: 3, keptAs: Turnout.Crowd, splitAs: Turnout.Loner)
            .Run([new("x1", "x"), new("y1", "y"), new("x2", "x"), new("z1", "z"), new("z2", "z"), new("z3", "z")]);

        Assert.Equal(
            ["Loner: x1", "Loner: x2", "Loner: y1", "Crowd: z1 z2 z3"],
            Labelled(groups));
    }

    [Fact]
    public void Split_small_groups_labels_by_size_so_a_big_enough_group_of_one_is_kept()
    {
        var groups = Pipeline.Of<Event>().GroupBy(Room).SplitSmallGroups(minimum: 1, keptAs: Turnout.Crowd, splitAs: Turnout.Loner)
            .Run([new("x1", "x")]);

        Assert.Equal(["Crowd: x1"], Labelled(groups));
    }

    [Fact]
    public void A_pipeline_lists_its_steps_in_order_with_their_parameters_and_judgments()
    {
        var pipeline = Pipeline.Of<Event>()
            .GroupBy(Room)
            .OrderBy(At, Name)
            .Session(At, TimeSpan.FromSeconds(2.5), Shrinks)
            .SplitSmallGroups(minimum: 3, keptAs: Turnout.Crowd, splitAs: Turnout.Loner);

        Assert.Equal(["group-by", "order-by", "session", "split-small-groups"], pipeline.Steps.Select(s => s.Operator));
        Assert.Equal([new("key", "room")], pipeline.Steps[0].Parameters);
        Assert.Equal([new("by", "at, then name")], pipeline.Steps[1].Parameters);
        Assert.Equal(
            [new("at", "at"), new("maxGap", "2.5 s"), new KeyValuePair<string, string>("breakWhen", "shrinks")],
            pipeline.Steps[2].Parameters);
        Assert.Equal(
            [new("minimum", "3"), new("keptAs", "crowd"), new KeyValuePair<string, string>("splitAs", "loner")],
            pipeline.Steps[3].Parameters);
        Assert.Equal(
            [new JudgmentInfo("at", "When the event happened."), new JudgmentInfo("shrinks", "The event is smaller than the one before.")],
            pipeline.Steps[2].Judgments);
        Assert.All(pipeline.Steps, s => Assert.False(string.IsNullOrWhiteSpace(s.OperatorDescription)));
    }

    [Fact]
    public void Session_lists_no_break_parameter_when_it_has_no_break_judgments()
    {
        var step = Pipeline.Of<Event>().GroupBy(Room).Session(At, TimeSpan.FromSeconds(2)).Steps[^1];

        Assert.Equal([new("at", "at"), new KeyValuePair<string, string>("maxGap", "2 s")], step.Parameters);
    }

    [Fact]
    public void Session_writes_whole_minutes_and_hours_as_such_and_anything_else_in_seconds()
    {
        string Gap(TimeSpan gap) => Pipeline.Of<Event>().GroupBy(Room).Session(At, gap).Steps[^1].Parameters[1].Value;

        Assert.Equal("5 min", Gap(TimeSpan.FromMinutes(5)));
        Assert.Equal("2 h", Gap(TimeSpan.FromHours(2)));
        Assert.Equal("90 s", Gap(TimeSpan.FromSeconds(90)));
        Assert.Equal("0.25 s", Gap(TimeSpan.FromMilliseconds(250)));
        Assert.Equal("0 s", Gap(TimeSpan.Zero));
    }

    [Fact]
    public void Session_joins_several_break_judgments_with_or()
    {
        var other = Judgment.Of("grows", "The event is bigger than the one before.", (IReadOnlyList<Event> s, Event next) => next.Size > s[^1].Size);

        var step = Pipeline.Of<Event>().GroupBy(Room).Session(At, TimeSpan.FromSeconds(2), Shrinks, other).Steps[^1];

        Assert.Equal("shrinks or grows", step.Parameters.Single(p => p.Key == "breakWhen").Value);
    }

    [Fact]
    public void Each_operator_declares_a_phrase_that_names_only_its_own_parameters()
    {
        var steps = Pipeline.Of<Event>()
            .GroupBy(Room)
            .OrderBy(At, Name)
            .Session(At, TimeSpan.FromMinutes(5), Shrinks)
            .SplitSmallGroups(minimum: 3, keptAs: Turnout.Crowd, splitAs: Turnout.Loner)
            .Steps;

        Assert.Equal(
            [
                "group by {key}",
                "order each group by {by}",
                "start a new session when: more than {maxGap} since the previous {at}[, or {breakWhen}]",
                "keep groups of {minimum} or more whole as {keptAs}; split the rest into groups of one, each {splitAs}",
            ],
            steps.Select(s => s.OperatorPhrase));
        foreach (var step in steps)
        foreach (var name in System.Text.RegularExpressions.Regex.Matches(step.OperatorPhrase!, @"\{(\w+)\}").Select(m => m.Groups[1].Value))
            Assert.Contains(name, new[] { "key", "by", "at", "maxGap", "breakWhen", "minimum", "keptAs", "splitAs" });
    }

    [Fact]
    public void A_pipeline_names_the_items_it_runs_over()
    {
        Assert.Equal("items", Pipeline.Of<Event>().GroupBy(Room).Items);
        Assert.Equal("events", Pipeline.Of<Event>("events").GroupBy(Room).OrderBy(At).Items);
    }

    [Fact]
    public void Steps_are_described_without_running_anything()
    {
        var pipeline = Pipeline.Of<Event>().GroupBy(Judgment.Of("boom", "Throws.", string (Event _) => throw new InvalidOperationException()));

        Assert.Single(pipeline.Steps);
        Assert.Throws<InvalidOperationException>(() => pipeline.Run([new("a", "x")]));
    }

    [Fact]
    public void Order_by_accepts_numeric_keys()
    {
        var groups = Pipeline.Of<Event>().GroupBy(Room).OrderBy(Size).Run([new("big", "x", Size: 10), new("small", "x", Size: 2)]);

        Assert.Equal([["small", "big"]], Names(groups));
    }
}
