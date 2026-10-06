namespace Seamlineage.Contracts;

/// <summary>
/// One node of a graph, type-erased so hosts can run any graph generically. <see cref="Steps"/> is set when the stage
/// is composed from generic operators (<see cref="IComposedStage"/>), and null for a plain stage.
/// </summary>
public sealed record StageNode(
    string Name,
    string Description,
    Type StageType,
    Type InputType,
    Type OutputType,
    int SchemaVersion,
    Func<object, StageContext, StageResult<object>> Invoke,
    IReadOnlyList<StepInfo>? Steps = null);

/// <summary>A linear pipeline: the product's description of what happens, independent of where it runs.</summary>
public sealed record Graph(string Name, Type InputType, IReadOnlyList<StageNode> Stages)
{
    public static GraphBuilder<T> Start<T>(string name) => new(name, typeof(T), []);
}

public sealed class GraphBuilder<TCurrent>
{
    private readonly string _name;
    private readonly Type _inputType;
    private readonly List<StageNode> _stages;

    internal GraphBuilder(string name, Type inputType, List<StageNode> stages)
    {
        _name = name;
        _inputType = inputType;
        _stages = stages;
    }

    /// <param name="name">kebab-case; also the folder name of the stage's examples.</param>
    /// <param name="description">One sentence a reviewer reads in the manifest: what this stage decides.</param>
    public GraphBuilder<TNext> Then<TNext>(string name, string description, IStage<TCurrent, TNext> stage, int schemaVersion = 1)
        where TNext : notnull
    {
        var node = new StageNode(name, description, stage.GetType(), typeof(TCurrent), typeof(TNext), schemaVersion, (input, ctx) =>
        {
            var result = stage.Run((TCurrent)input, ctx);
            return new StageResult<object>(result.Output, result.Effects);
        }, (stage as IComposedStage)?.Steps);
        return new GraphBuilder<TNext>(_name, _inputType, [.. _stages, node]);
    }

    public Graph Build() => new(_name, _inputType, _stages);
}
