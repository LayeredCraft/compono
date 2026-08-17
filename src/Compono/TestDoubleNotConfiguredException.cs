namespace Compono;

/// <summary>
/// Thrown by a generated test double's configuration-required member when it's invoked before
/// <c>Configure().Member(...).Returns(...)</c>/<c>.Throws(...)</c> configures it - see ADR-0045.
/// </summary>
public sealed class TestDoubleNotConfiguredException : Exception
{
    /// <summary>Creates a <see cref="TestDoubleNotConfiguredException"/> with the given message.</summary>
    public TestDoubleNotConfiguredException(string message)
        : base(message)
    {
    }
}
