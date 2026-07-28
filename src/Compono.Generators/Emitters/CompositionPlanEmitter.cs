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
    // Read once from this assembly's own metadata rather than hard-coded, so the emitted
    // GeneratedCodeAttribute stays accurate as the generator's version changes instead of quietly
    // going stale. Falls back to the assembly version if no informational version is set (e.g. no
    // real release versioning wired up yet).
    private static readonly string GeneratorVersion =
        typeof(CompositionPlanEmitter).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), inherit: false)
            .Cast<System.Reflection.AssemblyInformationalVersionAttribute>()
            .Select(a => a.InformationalVersion)
            .FirstOrDefault()
        ?? typeof(CompositionPlanEmitter).Assembly.GetName().Version?.ToString()
        ?? "0.0.0";

    public static void Generate(SourceProductionContext context, DiscoveredTypeInfo type)
    {
        var model = new
        {
            Namespace = type.Namespace,
            PlanClassName = type.PlanClassName,
            FullyQualifiedName = type.FullyQualifiedName,
            Parameters = type.Parameters.Select(p => new { p.FullyQualifiedTypeName, p.IsNullable }).ToArray(),
            RequiredMembers = type.RequiredMembers.Select(m => new { m.Name, m.FullyQualifiedTypeName, m.IsNullable }).ToArray(),
            GeneratorVersion,
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
