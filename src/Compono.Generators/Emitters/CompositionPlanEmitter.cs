using Compono.Generators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

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
            // A CLR metadata name (a constructor parameter or member on an external/library type,
            // per ADR-0004's discovery of types Compono doesn't own) isn't bound by C# identifier
            // syntax - it can legally contain a quote, backslash, or newline. NameLiteral is a full,
            // pre-quoted C# string literal (SymbolDisplay.FormatLiteral already includes the
            // surrounding quotes), so the template interpolates it directly rather than
            // hand-wrapping a raw name between quotes, which would emit invalid C# - or silently
            // change the diagnostic name - for a name FormatLiteral needs to escape.
            Parameters = type.Parameters.Select(p => new
            {
                p.FullyQualifiedTypeName,
                p.IsNullable,
                NameLiteral = SymbolDisplay.FormatLiteral(p.Name, quote: true),
                // Every constructor parameter belongs to the composed type's own selected constructor -
                // the declaring type is always the type this whole plan is being generated for.
                DeclaringType = type.FullyQualifiedName,
            }).ToArray(),
            RequiredMembers = type.RequiredMembers.Select(m => new
            {
                m.Name,
                m.FullyQualifiedTypeName,
                m.IsNullable,
                DisplayNameLiteral = SymbolDisplay.FormatLiteral(m.DisplayName, quote: true),
                DeclaringType = m.DeclaringTypeFullyQualifiedName,
            }).ToArray(),
            GeneratorVersion,
        };

        var source = TemplateHelper.Render("CompositionPlan.scriban", model);

        context.AddSource($"{GeneratedFileNaming.HintNameFor(type.FullyQualifiedName)}.CompositionPlan.g.cs", source);
    }
}
