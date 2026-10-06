namespace Seamlineage.Testing;

/// <summary>
/// Thrown when a Seamlineage check fails. Any test framework reports it as a failure with its message, so these
/// helpers work the same under xUnit, NUnit, MSTest or a plain console runner.
/// </summary>
public sealed class CheckFailedException(string message) : Exception(message);
