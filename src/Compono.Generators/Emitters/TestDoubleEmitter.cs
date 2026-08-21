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
                m.IsConfigurationRequired,
                m.IsOverloaded,
                m.IsEligibleForMatching,
                m.ExtensionReceiverName,
                m.GenericSuffix,
                m.ExtensionIsGeneric,
                m.ConstraintClausesText,
                m.OriginalName,
                Kind = m.Kind.ToString(),
                AccessorKind = m.AccessorKind.ToString(),
                Parameters = m.Parameters
                    .Select((p, i) => new
                    {
                        p.EscapedName,
                        p.FullyQualifiedTypeName,
                        p.RefKindPrefix,
                        p.IsParams,
                        p.DefaultValueExpression,
                        // A one-parameter member's call log is a plain List<T> - "(T)" isn't a tuple
                        // type in C#, it's just T in parentheses - so a single real parameter needs a
                        // different read expression ("call" itself) than a multi-parameter one
                        // ("call.Item1", "call.Item2", ...). Computed here rather than in the template
                        // so the arity branch lives in one place, in C#, not duplicated Scriban logic.
                        CallLogAccessExpression = m.Parameters.Count == 1 ? "call" : $"call.Item{i + 1}",
                    })
                    .ToArray(),
                OutParameterAssignments = m.OutParameterAssignments.ToArray(),
                MemberDescription = $"{m.DeclaringInterfaceFullyQualifiedName}.{m.OriginalName}",
                CallLogTypeText = m.Parameters.Count == 1
                    ? m.Parameters[0].FullyQualifiedTypeName
                    : $"({string.Join(", ", m.Parameters.Select(p => p.FullyQualifiedTypeName))})",
                CallLogConstructExpression = m.Parameters.Count == 1
                    ? m.Parameters[0].EscapedName
                    : $"({string.Join(", ", m.Parameters.Select(p => p.EscapedName))})",
            }).ToArray(),
            GeneratorVersion,
        };

        var source = TemplateHelper.Render("TestDouble.scriban", model);

        context.AddSource($"{GeneratedFileNaming.HintNameFor(testDouble.InterfaceFullyQualifiedName)}.TestDouble.g.cs", source);
    }
}
