using System.Text;

namespace Compono.Generators.Emitters;

/// <summary>
/// Identifier-safe sibling to <see cref="GeneratedFileNaming"/>. <see cref="GeneratedFileNaming.HintNameFor"/>'s
/// sanitized output deliberately preserves dots - correct for an <c>AddSource</c> hint name (a file
/// name, which can contain dots), wrong for a C# type identifier, where a dot is illegal.
/// <see cref="GeneratedFileNaming"/> itself is intentionally left unmodified by this - this is a
/// sibling helper, not a change to it. Reuses the same FNV-1a hash algorithm over the original,
/// unsanitized fully qualified name for the collision-safe suffix. See ADR-0043 Amendment 5, Finding J.
/// </summary>
internal static class TestDoubleIdentifierNaming
{
    public static string SafeIdentifierFor(string fullyQualifiedName)
    {
        const string globalPrefix = "global::";
        var readable = fullyQualifiedName.StartsWith(globalPrefix, StringComparison.Ordinal)
            ? fullyQualifiedName.Substring(globalPrefix.Length)
            : fullyQualifiedName;

        var builder = new StringBuilder(readable.Length + 9);

        foreach (var c in readable)
            builder.Append(char.IsLetterOrDigit(c) ? c : '_');

        return builder.Append('_').Append(StableHash(fullyQualifiedName)).ToString();
    }

    // Same FNV-1a algorithm as GeneratedFileNaming.StableHash, over the same raw, unsanitized input -
    // never string.GetHashCode(), which is randomized per process on modern runtimes and would churn
    // generated type names between builds.
    private static string StableHash(string value)
    {
        const uint offsetBasis = 2166136261;
        const uint prime = 16777619;

        var hash = offsetBasis;

        foreach (var c in value)
            hash = (hash ^ c) * prime;

        return hash.ToString("x8");
    }
}
