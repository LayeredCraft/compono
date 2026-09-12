using Compono.Generators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Compono.Generators.Emitters;

/// <summary>
/// Renders an <see cref="AotComposeMethodInfo"/> into a generated
/// <c>RegisteredEngineConfig.RegisterTheoryDataRowFactory(...)</c> module-initializer registration,
/// per ADR-0066/PLAN-0066 - the xUnit v3 Native AOT counterpart to
/// <see cref="RowInvokerRegistrationEmitter"/>'s reflection-mode dispatch registration. Proven
/// end to end (real <c>dotnet publish -p:PublishAot=true</c> + native-binary execution) by
/// RESEARCH-0032 §9's hand-written stand-in before this emitter existed.
/// </summary>
internal static class AotTheoryDataRowRegistrationEmitter
{
    public static void Generate(SourceProductionContext context, AotComposeMethodInfo method)
    {
        var model = new
        {
            FullyQualifiedTestClassName = method.FullyQualifiedTestClassName,
            MethodNameLiteral = SymbolDisplay.FormatLiteral(method.MethodName, quote: true),
            TestClassIndexLiteral = SymbolDisplay.FormatLiteral(method.FullyQualifiedTestClassName, quote: true),
            Parameters = method.Parameters.Select(p => new
            {
                p.FullyQualifiedTypeName,
                p.IsNullable,
                p.Ordinal,
                LocalName = $"value{p.Ordinal}",
                NameLiteral = SymbolDisplay.FormatLiteral(p.Name, quote: true),
            }).ToArray(),
            GeneratorVersion = GeneratorVersion.Current,
        };

        var source = TemplateHelper.Render("AotTheoryDataRowRegistration.scriban", model);
        var hintName = GeneratedFileNaming.HintNameFor($"{method.FullyQualifiedTestClassName}.{method.MethodName}");

        context.AddSource($"{hintName}.AotTheoryDataRowRegistration.g.cs", source);
    }
}
