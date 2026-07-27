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

        context.AddSource($"{type.TypeName}.CompositionPlan.g.cs", source);
    }
}
