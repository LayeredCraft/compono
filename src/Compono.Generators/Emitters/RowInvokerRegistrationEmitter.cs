using Compono.Generators.Models;
using Microsoft.CodeAnalysis;

namespace Compono.Generators.Emitters;

/// <summary>
/// Renders a <see cref="RowInvokerTypeInfo"/> into a generated <c>RowInvokerRegistry.Register(...)</c>
/// module-initializer call, per ADR-0041 Amendment 2.
/// </summary>
internal static class RowInvokerRegistrationEmitter
{
    private static readonly string GeneratorVersion =
        typeof(RowInvokerRegistrationEmitter).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), inherit: false)
            .Cast<System.Reflection.AssemblyInformationalVersionAttribute>()
            .Select(a => a.InformationalVersion)
            .FirstOrDefault()
        ?? typeof(RowInvokerRegistrationEmitter).Assembly.GetName().Version?.ToString()
        ?? "0.0.0";

    public static void Generate(SourceProductionContext context, RowInvokerTypeInfo type)
    {
        var model = new
        {
            FullyQualifiedTypeName = type.FullyQualifiedTypeName,
            GeneratorVersion,
        };

        var source = TemplateHelper.Render("RowInvokerRegistration.scriban", model);

        context.AddSource($"{GeneratedFileNaming.HintNameFor(type.FullyQualifiedTypeName)}.RowInvokerRegistration.g.cs", source);
    }
}
