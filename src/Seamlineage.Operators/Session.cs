using System.Globalization;
using Seamlineage.Contracts;

namespace Seamlineage.Operators;

public static class SessionOperator
{
    public const string Description =
        "Splits each ordered group into sessions: an item joins the current session when its time is at most maxGap "
        + "after the time of the previous item and no breakWhen judgment holds; otherwise it starts a new one. "
        + "An item with no time is a session of its own.";

    /// <param name="at">The item's time; null means it can't be placed, so it stands alone.</param>
    /// <param name="breakWhen">Each is asked (session so far, next item); any true starts a new session.</param>
    public static Pipeline<TIn, IReadOnlyList<IReadOnlyList<T>>> Session<TIn, T>(
        this Pipeline<TIn, IReadOnlyList<IReadOnlyList<T>>> pipeline,
        Judgment<Func<T, DateTime?>> at,
        TimeSpan maxGap,
        params Judgment<Func<IReadOnlyList<T>, T, bool>>[] breakWhen)
    {
        List<KeyValuePair<string, string>> parameters = [new("at", at.Name), new("maxGap", Seconds(maxGap))];
        if (breakWhen.Length > 0) parameters.Add(new("breakWhen", string.Join(", ", breakWhen.Select(b => b.Name))));
        var step = new StepInfo("session", Description, parameters, [at.Info, .. breakWhen.Select(b => b.Info)]);

        return pipeline.Then(step, groups =>
        {
            var sessions = new List<IReadOnlyList<T>>();
            foreach (var group in groups)
            {
                var current = new List<T>();
                DateTime? previous = null;
                foreach (var item in group)
                {
                    var time = at.Apply(item);
                    var joins = current.Count > 0
                        && time is { } t && previous is { } p && t - p <= maxGap
                        && !breakWhen.Any(b => b.Apply(current, item));
                    if (!joins && current.Count > 0)
                    {
                        sessions.Add(current);
                        current = [];
                    }
                    current.Add(item);
                    previous = time;
                }
                if (current.Count > 0) sessions.Add(current);
            }
            return (IReadOnlyList<IReadOnlyList<T>>)sessions;
        });
    }

    private static string Seconds(TimeSpan span) => span.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture) + " s";
}
