using Seamlineage.Contracts;

namespace Seamlineage.Hosting.InProc;

/// <summary>Runs a graph in this process, recording every edge. The reference host every other host must match.</summary>
public sealed class InProcRunner(EdgeRecorder recorder, TimeProvider? time = null)
{
    private readonly TimeProvider _time = time ?? TimeProvider.System;

    public object Run(Graph graph, object input, string correlationId)
    {
        var context = new StageContext(correlationId, _time.GetUtcNow());
        recorder.Record(0, "input", Wrap(correlationId, "input", 1, input));

        var current = input;
        for (var i = 0; i < graph.Stages.Count; i++)
        {
            var stage = graph.Stages[i];
            var result = stage.Invoke(current, context);

            // No effect handlers exist yet. Failing loudly beats silently dropping a side effect.
            if (result.Effects.Count > 0)
            {
                var names = string.Join(", ", result.Effects.Select(e => e.GetType().Name).Distinct());
                throw new InvalidOperationException($"Stage '{stage.Name}' emitted effects with no handler: {names}");
            }

            current = result.Output;
            recorder.Record(i + 1, stage.Name, Wrap(correlationId, stage.Name, stage.SchemaVersion, current));
        }

        return current;
    }

    private static Envelope<object> Wrap(string correlationId, string edge, int schemaVersion, object payload) =>
        new(correlationId, $"{correlationId}:{edge}", OrderingKey: null, schemaVersion, payload);
}
