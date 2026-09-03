namespace Compono.Generators.Emitters;

/// <summary>
/// The single FNV-1a implementation every generator-emitted naming scheme hashes through -
/// <see cref="GeneratedFileNaming"/>, <see cref="TestDoubleIdentifierNaming"/>, and
/// <see cref="TestDoubleOverloadIdentity"/> each need a deterministic, cross-build-stable hash of a
/// raw identity string (never <c>string.GetHashCode()</c>, which is randomized per process on modern
/// runtimes and would churn hint names/generated type names between builds) and previously each
/// carried their own byte-for-byte copy of it (PLAN-0061 Phase 1).
/// </summary>
internal static class StableHash
{
    public static string Compute(string value)
    {
        const uint offsetBasis = 2166136261;
        const uint prime = 16777619;

        var hash = offsetBasis;

        foreach (var c in value)
            hash = (hash ^ c) * prime;

        return hash.ToString("x8");
    }
}
