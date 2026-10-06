namespace Seamlineage.Contracts;

public sealed record Versioned<T>(T Value, long Version);

/// <summary>
/// The only shared state a graph may use: keyed, versioned, compare-and-swap.
/// Contract only: no host implements it yet.
/// </summary>
public interface IStateStore
{
    Task<Versioned<T>?> GetAsync<T>(string key, CancellationToken ct = default);

    /// <returns>false if the stored version is not <paramref name="expectedVersion"/> (0 = must not exist).</returns>
    Task<bool> TryPutAsync<T>(string key, T value, long expectedVersion, CancellationToken ct = default);
}
