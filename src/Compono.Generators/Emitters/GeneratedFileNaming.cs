using System.Text;

namespace Compono.Generators.Emitters;

/// <summary>
/// The <c>AddSource</c> hint-name scheme shared by every emitter: a sanitized, readable fully
/// qualified name plus a stable hash of the raw pre-sanitization identity, per
/// <c>coding-standards.md</c>'s "Generated code" section ("Hint names are readable +
/// stable-hash-suffixed").
/// </summary>
internal static class GeneratedFileNaming
{
    public static string HintNameFor(string fullyQualifiedName)
    {
        const string globalPrefix = "global::";
        var readable = fullyQualifiedName.StartsWith(globalPrefix, StringComparison.Ordinal)
            ? fullyQualifiedName.Substring(globalPrefix.Length)
            : fullyQualifiedName;

        var builder = new StringBuilder(readable.Length + 9);

        foreach (var c in readable)
            builder.Append(char.IsLetterOrDigit(c) || c == '.' ? c : '_');

        return builder.Append('_').Append(StableHash(fullyQualifiedName)).ToString();
    }

    // FNV-1a, not string.GetHashCode() - the latter is randomized per process on modern runtimes,
    // and a hint name that changes between builds would defeat incremental caching and churn
    // EmitCompilerGeneratedFiles output paths.
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
