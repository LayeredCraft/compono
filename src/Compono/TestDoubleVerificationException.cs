namespace Compono;

/// <summary>
/// Thrown by <see cref="CallVerifier"/> when a generated test double's member was called a different
/// number of times than expected.
/// </summary>
public sealed class TestDoubleVerificationException : Exception
{
    /// <summary>Creates a <see cref="TestDoubleVerificationException"/> with the given message.</summary>
    public TestDoubleVerificationException(string message)
        : base(message)
    {
    }
}
