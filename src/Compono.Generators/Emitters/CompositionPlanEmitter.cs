using System.Text;
using Compono.Generators.Models;
using Microsoft.CodeAnalysis;

namespace Compono.Generators.Emitters;

/// <summary>
/// Renders a <see cref="DiscoveredTypeInfo"/> into the generated <c>ICompositionPlan&lt;T&gt;</c>
/// class + module-initializer registration, per
/// <c>docs/adr/0005-generator-implementation-conventions.md</c>.
/// </summary>
internal static class CompositionPlanEmitter
{
    public static void Generate(SourceProductionContext context, DiscoveredTypeInfo type)
    {
        var model = new
        {
            Namespace = type.Namespace,
            PlanClassName = type.PlanClassName,
            FullyQualifiedName = type.FullyQualifiedName,
            Parameters = type.Parameters.Select(p => new { FullyQualifiedTypeName = p.FullyQualifiedTypeName }),
        };

        var source = TemplateHelper.Render("CompositionPlan.scriban", model);

        context.AddSource($"{HintNameFor(type.FullyQualifiedName)}.CompositionPlan.g.cs", source);
    }

    // Hint names must be unique across the whole generator run - AddSource throws if two calls use
    // the same one. The readable part is the sanitized fully-qualified name (TypeName alone collides
    // for same-simple-name types in different namespaces), but sanitization is lossy - `N.Foo<int>`
    // and a literal type named `N.Foo_int_` sanitize identically - so a stable hash of the *raw*,
    // pre-sanitization identity is appended to guarantee uniqueness regardless.
    private static string HintNameFor(string fullyQualifiedName)
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
