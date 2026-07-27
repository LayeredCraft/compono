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

        // Hint names must be unique across the whole generator run - AddSource throws if two calls
        // use the same one. TypeName alone collides for same-simple-name types in different
        // namespaces (Sales.Customer vs. Support.Customer); FullyQualifiedName doesn't, since it
        // includes the namespace. Still needs sanitizing - a hint name isn't a real file path, but
        // generic-type syntax (angle brackets, commas) isn't safe to put in one unescaped.
        context.AddSource($"{SanitizeHintName(type.FullyQualifiedName)}.CompositionPlan.g.cs", source);
    }

    private static string SanitizeHintName(string fullyQualifiedName)
    {
        var builder = new StringBuilder(fullyQualifiedName.Length);

        foreach (var c in fullyQualifiedName)
            builder.Append(char.IsLetterOrDigit(c) || c == '.' ? c : '_');

        return builder.ToString();
    }
}
