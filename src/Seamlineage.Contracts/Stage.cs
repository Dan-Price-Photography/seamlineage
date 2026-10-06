namespace Seamlineage.Contracts;

/// <summary>Everything non-deterministic a stage may use. Hosts supply it, so runs are replayable.</summary>
public sealed record StageContext(string CorrelationId, DateTimeOffset Now);

/// <summary>A side effect described as data. Stages return effects; hosts execute them.</summary>
public abstract record Effect;

public sealed record StageResult<TOut>(TOut Output, IReadOnlyList<Effect> Effects)
{
    public static StageResult<TOut> Pure(TOut output) => new(output, []);
}

/// <summary>A pure transform. No I/O, no clock, no randomness: only <paramref name="input"/> and the context.</summary>
public interface IStage<in TIn, TOut>
{
    StageResult<TOut> Run(TIn input, StageContext context);
}
