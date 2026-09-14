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
            // ADR-0067/PLAN-0067: null for plain [Compose]; non-null (with ConfigTypeName null) for
            // [Compose<TProfile>]; non-null (with ConfigTypeName set) for [Compose<TProfile, TConfig>].
            Profile = method.Profile is { } profile
                ? new
                {
                    ProfileTypeName = profile.FullyQualifiedProfileTypeName,
                    ConfigTypeName = profile.FullyQualifiedConfigTypeName,
                    // Each argument is wrapped in an explicit cast to its own selected constructor
                    // parameter's declared type in the template (PR #140 Codex review round 5) - the
                    // generated registration lives in the consumer's own assembly, so without a cast,
                    // ordinary C# overload resolution could pick a more-specific accessible sibling
                    // constructor Compono.Generators didn't actually select/validate.
                    ConfigArguments = profile.ConfigArguments.Select(a => new
                    {
                        a.RenderedLiteral,
                        a.FullyQualifiedParameterTypeName,
                    }).ToArray(),
                }
                : null,
            GeneratorVersion = GeneratorVersion.Current,
        };

        var source = TemplateHelper.Render("AotTheoryDataRowRegistration.scriban", model);
        var hintName = GeneratedFileNaming.HintNameFor($"{method.FullyQualifiedTestClassName}.{method.MethodName}");

        context.AddSource($"{hintName}.AotTheoryDataRowRegistration.g.cs", source);
    }
}
