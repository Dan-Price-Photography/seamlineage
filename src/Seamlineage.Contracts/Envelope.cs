namespace Seamlineage.Contracts;

/// <summary>
/// What travels across every edge of a graph. Hosts create envelopes; stages never see them.
/// Delivery is assumed at-least-once, so <see cref="IdempotencyKey"/> must be stable across retries.
/// </summary>
public sealed record Envelope<T>(
    string CorrelationId,
    string IdempotencyKey,
    string? OrderingKey,
    int SchemaVersion,
    T Payload);
