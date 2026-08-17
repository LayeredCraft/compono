using Compono.Generators.Models;
using Microsoft.CodeAnalysis;

namespace Compono.Generators.Emitters;

/// <summary>
/// Renders a <see cref="DiscoveredTestDoubleInfo"/> into the generated double type, its zero-argument
/// configuration extensions, its <c>Configure()</c> bridge, and its module-initializer registration -
/// one file, no namespace declaration (global namespace, ADR-0043 Amendment 11, Finding AA).
/// </summary>
internal static class TestDoubleEmitter
{
    // Same rationale as CompositionPlanEmitter.GeneratorVersion - read once from this assembly's own
    // metadata so the emitted GeneratedCodeAttribute stays accurate as the generator's version changes.
    private static readonly string GeneratorVersion =
        typeof(TestDoubleEmitter).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), inherit: false)
            .Cast<System.Reflection.AssemblyInformationalVersionAttribute>()
            .Select(a => a.InformationalVersion)
            .FirstOrDefault()
        ?? typeof(TestDoubleEmitter).Assembly.GetName().Version?.ToString()
        ?? "0.0.0";

    public static void Generate(SourceProductionContext context, DiscoveredTestDoubleInfo testDouble)
    {
        var model = new
        {
            InterfaceFullyQualifiedName = testDouble.InterfaceFullyQualifiedName,
            SafeIdentifier = testDouble.SafeIdentifier,
            Members = testDouble.Members.Select(m => new
            {
                m.FieldName,
                m.EscapedName,
                m.DeclaringInterfaceFullyQualifiedName,
                m.SlotTypeFullyQualifiedName,
                m.ReturnTypeFullyQualifiedName,
                m.IsVoid,
                m.DefaultExpression,
                m.HasConfigurationSurface,
                m.IsOverloaded,
                m.ExtensionReceiverName,
                m.GenericSuffix,
                m.ConstraintClausesText,
                m.OriginalName,
                Kind = m.Kind.ToString(),
                AccessorKind = m.AccessorKind.ToString(),
                Parameters = m.Parameters
                    .Select(p => new { p.EscapedName, p.FullyQualifiedTypeName, p.RefKindPrefix, p.IsParams, p.DefaultValueExpression })
                    .ToArray(),
                OutParameterAssignments = m.OutParameterAssignments.ToArray(),
                MemberDescription = $"{m.DeclaringInterfaceFullyQualifiedName}.{m.OriginalName}",
            }).ToArray(),
            GeneratorVersion,
        };

        var source = TemplateHelper.Render("TestDouble.scriban", model);

        context.AddSource($"{GeneratedFileNaming.HintNameFor(testDouble.InterfaceFullyQualifiedName)}.TestDouble.g.cs", source);
    }
}
