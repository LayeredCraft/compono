using Compono.Generators.Diagnostics;
using Compono.Generators.Models;
using Compono.Generators.Types;
using Microsoft.CodeAnalysis;

namespace Compono.Generators.Discovery;

/// <summary>
/// Finds methods attributed <c>[Compose]</c> under <c>Compono.XunitV3.Aot</c>'s own metadata name
/// and builds the per-method, ordered-parameter model
/// <see cref="Emitters.AotTheoryDataRowRegistrationEmitter"/> needs to emit a real
/// <c>RegisteredEngineConfig.RegisterTheoryDataRowFactory(...)</c> registration, per
/// ADR-0066/PLAN-0066.
/// </summary>
/// <remarks>
/// Deliberately separate from <see cref="ComposeMethodDiscovery"/> - that discovery's own result
/// shape (<see cref="ComposeMethodDiscoveryResult"/>) is a compilation-wide, type-deduped worklist
/// for <c>PlanCache&lt;T&gt;</c>/<c>RowInvokerRegistry</c> emission (ADR-0041), which throws away
/// per-method identity and parameter order on purpose - exactly the two things this registration
/// needs to preserve, since every <c>[Compose]</c>-attributed method needs its own factory closure,
/// not a type-keyed dispatch table entry. <c>Compono.XunitV3.Aot.ComposeAttribute</c> is still also
/// registered with <see cref="ComposeMethodDiscovery"/> separately (see
/// <c>ComponoIncrementalGenerator</c>) so its parameter types still get ordinary
/// <c>PlanCache&lt;T&gt;</c>/<c>RowInvokerRegistry</c> entries through the same, already-proven
/// mechanism the other four attribute families use - this discovery only adds the per-method
/// registration on top, it doesn't replace that.
/// </remarks>
internal static class AotComposeMethodDiscovery
{
    public const string AttributeMetadataName = "Compono.XunitV3.Aot.ComposeAttribute";

    public static AotComposeMethodInfo? TransformMethod(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        if (context.TargetSymbol is not IMethodSymbol method)
            return null;

        var declaringType = method.ContainingType;
        var fullyQualifiedTestClassName = declaringType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var methodDisplayName = $"{declaringType.ToDisplayString()}.{method.Name}";
        var location = LocationInfo.From(method);

        // No runtime BindingPlan.ValidateSignature-equivalent exists for this attribute family
        // (ComposeAttribute is a marker only, per RESEARCH-0032 §2/§9) - an unsupported shape has to
        // be caught here, at compile time, or it silently never gets a registration at all (the test
        // method simply never gets discovered by xUnit's AOT pipeline, with no diagnostic anywhere).
        if (method.IsGenericMethod)
            return Unsupported(fullyQualifiedTestClassName, method.Name, location, methodDisplayName, "generic test methods are not supported");

        var parameters = new List<AotComposeParameterInfo>(method.Parameters.Length);

        foreach (var parameter in method.Parameters)
        {
            if (parameter.RefKind != RefKind.None)
                return Unsupported(fullyQualifiedTestClassName, method.Name, location, methodDisplayName, $"parameter '{parameter.Name}' is ref/out/in, which is not supported");

            if (parameter.IsParams)
                return Unsupported(fullyQualifiedTestClassName, method.Name, location, methodDisplayName, $"parameter '{parameter.Name}' is a params parameter, which is not supported");

            parameters.Add(new AotComposeParameterInfo(
                parameter.Name,
                parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                parameter.Ordinal,
                IsNullable(parameter.Type)));
        }

        return new AotComposeMethodInfo(
            fullyQualifiedTestClassName,
            method.Name,
            parameters.ToEquatableArray(),
            Array.Empty<DiagnosticInfo>().ToEquatableArray());
    }

    private static AotComposeMethodInfo Unsupported(
        string fullyQualifiedTestClassName,
        string methodName,
        LocationInfo? location,
        string methodDisplayName,
        string reason) =>
            new(
                fullyQualifiedTestClassName,
                methodName,
                Array.Empty<AotComposeParameterInfo>().ToEquatableArray(),
                new[] { new DiagnosticInfo(DiagnosticDescriptors.UnsupportedAotComposeMethodSignature, location, methodDisplayName, reason) }.ToEquatableArray());

    // Mirrors Compono.XunitV3.Binding.BindingPlan.GetNullability's runtime logic exactly, using
    // Roslyn symbols in place of System.Reflection - this generator has no access to (and must never
    // need) a runtime NullabilityInfoContext.
    private static bool IsNullable(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T })
            return true;

        if (type.IsValueType)
            return false;

        return type.NullableAnnotation == NullableAnnotation.Annotated;
    }
}
