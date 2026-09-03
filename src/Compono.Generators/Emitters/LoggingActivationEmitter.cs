using Compono.Generators.Models;
using Microsoft.CodeAnalysis;

namespace Compono.Generators.Emitters;

/// <summary>
/// Renders a <see cref="DiscoveredLoggingCategoryInfo"/> into a generated
/// <c>Compono.Logging.LoggingFactoryRegistry.Register&lt;T&gt;(...)</c> module-initializer call, per
/// docs/adr/0055-compono-logging-testing-support-package.md Amendments 1/3. Kept as its own,
/// narrowly scoped emitter - not folded into <see cref="CompositionPlanEmitter"/> or
/// <see cref="TestDoubleEmitter"/> - since it emits activation glue only, never composition-plan or
/// test-double behavior.
/// </summary>
internal static class LoggingActivationEmitter
{
    public static void Generate(SourceProductionContext context, DiscoveredLoggingCategoryInfo category)
    {
        var model = new
        {
            CategoryFullyQualifiedName = category.CategoryFullyQualifiedName,
            GeneratorVersion = GeneratorVersion.Current,
        };

        var source = TemplateHelper.Render("LoggingActivation.scriban", model);

        context.AddSource($"{GeneratedFileNaming.HintNameFor(category.CategoryFullyQualifiedName)}.LoggingActivation.g.cs", source);
    }
}
