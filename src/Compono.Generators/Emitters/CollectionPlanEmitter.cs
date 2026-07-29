using Compono.Generators.Models;
using Microsoft.CodeAnalysis;

namespace Compono.Generators.Emitters;

/// <summary>
/// Renders a <see cref="DiscoveredCollectionInfo"/> into a generated
/// <c>ICompositionPlan&lt;TCollection&gt;</c> class + <c>CollectionPlanCache&lt;TCollection&gt;</c>
/// module-initializer registration, per
/// <c>docs/adr/0014-generator-emitted-collection-plans.md</c>.
/// </summary>
internal static class CollectionPlanEmitter
{
    private static readonly string GeneratorVersion =
        typeof(CollectionPlanEmitter).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), inherit: false)
            .Cast<System.Reflection.AssemblyInformationalVersionAttribute>()
            .Select(a => a.InformationalVersion)
            .FirstOrDefault()
        ?? typeof(CollectionPlanEmitter).Assembly.GetName().Version?.ToString()
        ?? "0.0.0";

    public static void Generate(SourceProductionContext context, DiscoveredCollectionInfo collection)
    {
        var model = new
        {
            Shape = collection.Shape.ToString(),
            CollectionType = collection.FullyQualifiedCollectionTypeName,
            ElementType = collection.ElementFullyQualifiedTypeName,
            ElementNullability = collection.ElementIsNullable ? "global::Compono.Nullability.Nullable" : "global::Compono.Nullability.NotNullable",
            KeyType = collection.KeyFullyQualifiedTypeName,
            KeyNullability = collection.KeyIsNullable ? "global::Compono.Nullability.Nullable" : "global::Compono.Nullability.NotNullable",
            GeneratorVersion,
        };

        var source = TemplateHelper.Render("CollectionPlan.scriban", model);

        context.AddSource($"{GeneratedFileNaming.HintNameFor(collection.FullyQualifiedCollectionTypeName)}.CollectionPlan.g.cs", source);
    }
}
