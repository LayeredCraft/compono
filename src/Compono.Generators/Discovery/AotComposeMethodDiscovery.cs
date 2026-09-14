using Compono.Generators.Diagnostics;
using Compono.Generators.Emitters;
using Compono.Generators.Models;
using Compono.Generators.Types;
using Microsoft.CodeAnalysis;

namespace Compono.Generators.Discovery;

/// <summary>
/// Finds methods attributed <c>[Compose]</c>/<c>[Compose&lt;TProfile&gt;]</c>/<c>[Compose&lt;TProfile,
/// TConfig&gt;]</c> under <c>Compono.XunitV3.Aot</c>'s own metadata names and builds the per-method,
/// ordered-parameter (and, for the two profile-bearing forms, profile-selection) model
/// <see cref="Emitters.AotTheoryDataRowRegistrationEmitter"/> needs to emit a real
/// <c>RegisteredEngineConfig.RegisterTheoryDataRowFactory(...)</c> registration, per
/// ADR-0066/PLAN-0066 (plain <c>[Compose]</c>) and ADR-0067/PLAN-0067 (profile forms).
/// </summary>
/// <remarks>
/// Deliberately separate from <see cref="ComposeMethodDiscovery"/> - that discovery's own result
/// shape (<see cref="ComposeMethodDiscoveryResult"/>) is a compilation-wide, type-deduped worklist
/// for <c>PlanCache&lt;T&gt;</c>/<c>RowInvokerRegistry</c> emission (ADR-0041), which throws away
/// per-method identity and parameter order on purpose - exactly the two things this registration
/// needs to preserve, since every <c>[Compose]</c>-attributed method needs its own factory closure,
/// not a type-keyed dispatch table entry. <c>Compono.XunitV3.Aot.ComposeAttribute</c>'s three forms
/// are still also registered with <see cref="ComposeMethodDiscovery"/> separately (see
/// <c>ComponoIncrementalGenerator</c>) so their parameter types still get ordinary
/// <c>PlanCache&lt;T&gt;</c>/<c>RowInvokerRegistry</c> entries through the same, already-proven
/// mechanism the other four attribute families use - this discovery only adds the per-method
/// registration on top, it doesn't replace that.
/// </remarks>
internal static class AotComposeMethodDiscovery
{
    public const string AttributeMetadataName = "Compono.XunitV3.Aot.ComposeAttribute";

    /// <inheritdoc cref="ComposeMethodDiscovery.GenericAttributeMetadataName"/>
    /// <remarks><c>Compono.XunitV3.Aot.ComposeAttribute&lt;TProfile&gt;</c>'s own arity-suffixed form (ADR-0067/PLAN-0067).</remarks>
    public const string GenericAttributeMetadataName = "Compono.XunitV3.Aot.ComposeAttribute`1";

    /// <inheritdoc cref="ComposeMethodDiscovery.TwoTypeParameterAttributeMetadataName"/>
    /// <remarks><c>Compono.XunitV3.Aot.ComposeAttribute&lt;TProfile, TConfig&gt;</c>'s own arity-suffixed form (ADR-0067/PLAN-0067).</remarks>
    public const string TwoTypeParameterAttributeMetadataName = "Compono.XunitV3.Aot.ComposeAttribute`2";

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

            if (!ComposedTypeAnalyzer.IsRowInvokerShapeEligible(parameter.Type, context.SemanticModel.Compilation))
                return Unsupported(fullyQualifiedTestClassName, method.Name, location, methodDisplayName, $"parameter '{parameter.Name}' has a type that cannot be composed (an open generic parameter, ref struct, pointer, function pointer, or unsupported array shape)");

            parameters.Add(new AotComposeParameterInfo(
                parameter.Name,
                parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                parameter.Ordinal,
                IsNullable(parameter.Type)));
        }

        // context.Attributes contains exactly one match - ForAttributeWithMetadataName matches an
        // attribute usage's own attribute-class metadata name exactly, and [AttributeUsage(AllowMultiple
        // = false)] rules out a second usage of the same closed form on one method.
        var attributeData = context.Attributes[0];
        var profileDiagnostics = new List<DiagnosticInfo>();

        var profile = attributeData.AttributeClass!.Arity switch
        {
            0 => null,
            1 => BuildOneTypeParameterProfile(attributeData),
            2 => BuildTwoTypeParameterProfile(attributeData, methodDisplayName, location, context.SemanticModel.Compilation, profileDiagnostics),
            var arity => throw new NotSupportedException($"Unsupported Compono.XunitV3.Aot.ComposeAttribute arity '{arity}'."),
        };

        if (profileDiagnostics.Count > 0)
        {
            return new AotComposeMethodInfo(
                fullyQualifiedTestClassName,
                method.Name,
                Array.Empty<AotComposeParameterInfo>().ToEquatableArray(),
                profileDiagnostics.ToEquatableArray());
        }

        return new AotComposeMethodInfo(
            fullyQualifiedTestClassName,
            method.Name,
            parameters.ToEquatableArray(),
            Array.Empty<DiagnosticInfo>().ToEquatableArray(),
            profile);
    }

    // [Compose<TProfile>] - TProfile : ICompositionProfile, new() is enforced by the C# compiler at
    // the use site (a generic-attribute constraint, like any other closed generic type), so there is
    // no additional compile-time check to perform here at all - identical to
    // Compono.XunitV3.ComposeAttribute<TProfile>'s own "nothing left to validate" remarks.
    private static AotProfileInfo BuildOneTypeParameterProfile(AttributeData attributeData)
    {
        var profileType = attributeData.AttributeClass!.TypeArguments[0];

        return new AotProfileInfo(
            profileType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            null,
            EquatableArray<AotProfileConfigArgumentInfo>.Empty);
    }

    // [Compose<TProfile, TConfig>] - performs, at compile time, the same three checks
    // Compono.XunitV3.Binding.ConfigProfileBinder performs at runtime (ADR-0036): TConfig has exactly
    // one public constructor (CMP0041); TProfile has exactly one public constructor accepting exactly
    // one TConfig-typed parameter (CMP0042); the supplied profile configuration arguments match that
    // constructor's parameters (CMP0043). Any failure appends to diagnostics and this method returns a
    // value the caller discards (the diagnostics list being non-empty is what matters, per
    // TransformMethod's own check).
    private static AotProfileInfo? BuildTwoTypeParameterProfile(
        AttributeData attributeData,
        string methodDisplayName,
        LocationInfo? location,
        Compilation compilation,
        List<DiagnosticInfo> diagnostics)
    {
        var profileType = (INamedTypeSymbol)attributeData.AttributeClass!.TypeArguments[0];
        var configType = (INamedTypeSymbol)attributeData.AttributeClass!.TypeArguments[1];
        var profileDisplayName = profileType.ToDisplayString();
        var configDisplayName = configType.ToDisplayString();

        var configConstructors = configType.IsAbstract
            ? Array.Empty<IMethodSymbol>()
            : configType.Constructors.Where(static c => c.DeclaredAccessibility == Accessibility.Public).ToArray();

        if (configConstructors.Length != 1)
        {
            diagnostics.Add(new DiagnosticInfo(
                DiagnosticDescriptors.InvalidProfileConfigConstructorShape,
                location,
                configDisplayName,
                profileDisplayName,
                methodDisplayName,
                configConstructors.Length));

            return null;
        }

        var profileConstructors = profileType.IsAbstract
            ? Array.Empty<IMethodSymbol>()
            : profileType.Constructors
                .Where(c => c.DeclaredAccessibility == Accessibility.Public
                    && c.Parameters.Length == 1
                    && SymbolEqualityComparer.Default.Equals(c.Parameters[0].Type, configType))
                .ToArray();

        if (profileConstructors.Length != 1)
        {
            diagnostics.Add(new DiagnosticInfo(
                DiagnosticDescriptors.InvalidProfileConstructorShape,
                location,
                profileDisplayName,
                configDisplayName,
                methodDisplayName,
                profileConstructors.Length));

            return null;
        }

        var configConstructor = configConstructors[0];
        var configParameters = configConstructor.Parameters;
        var suppliedArguments = NormalizeConstructorArguments(attributeData);

        if (suppliedArguments.Count != configParameters.Length)
        {
            diagnostics.Add(new DiagnosticInfo(
                DiagnosticDescriptors.ProfileConfigArgumentMismatch,
                location,
                profileDisplayName,
                configDisplayName,
                methodDisplayName,
                $"'{configDisplayName}' requires {configParameters.Length} argument(s), but {suppliedArguments.Count} were supplied"));

            return null;
        }

        var renderedArguments = new List<AotProfileConfigArgumentInfo>(configParameters.Length);

        for (var i = 0; i < configParameters.Length; i++)
        {
            var parameter = configParameters[i];
            var argument = suppliedArguments[i];

            switch (TypedConstantMatcher.Validate(argument, parameter, compilation))
            {
                case TypedConstantValidation.NullNotAllowed:
                    diagnostics.Add(new DiagnosticInfo(
                        DiagnosticDescriptors.ProfileConfigArgumentMismatch,
                        location,
                        profileDisplayName,
                        configDisplayName,
                        methodDisplayName,
                        $"argument for parameter '{parameter.Name}' is null, but the parameter is not nullable"));
                    return null;

                case TypedConstantValidation.TypeMismatch:
                    diagnostics.Add(new DiagnosticInfo(
                        DiagnosticDescriptors.ProfileConfigArgumentMismatch,
                        location,
                        profileDisplayName,
                        configDisplayName,
                        methodDisplayName,
                        $"argument for parameter '{parameter.Name}' is not assignable to '{parameter.Type.ToDisplayString()}'"));
                    return null;
            }

            renderedArguments.Add(new AotProfileConfigArgumentInfo(TypedConstantLiteralRenderer.Render(argument, parameter.Type)));
        }

        return new AotProfileInfo(
            profileType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            configType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            renderedArguments.ToEquatableArray());
    }

    // Compono.XunitV3.Aot.ComposeAttribute<TProfile, TConfig>'s params object?[] configArguments
    // constructor parameter is a single TypedConstant of Kind = Array in AttributeData.
    // ConstructorArguments, whose own .Values are the actual per-argument constants - mirrors
    // Compono.XunitV3.ComposeAttribute.NormalizeParamsArguments's null-array handling (a bare
    // [Compose<P, C>(null)] use site binds to the params parameter in non-expanded form, i.e. the
    // whole array is null, which is normalized to "one supplied argument, whose value is null" to
    // match the author's evident intent - see that method's own remarks for the full rationale).
    // NOT a parity gap with Compono.XunitV3.ComposeAttribute<TProfile, TConfig> (confirmed by direct
    // Roslyn investigation during Phase 2 review): a single argument more specifically typed than
    // object?[] (e.g. new string[] { "a", "b" }, intended as one inline array-typed value for a
    // TConfig constructor parameter of that array type) is not legal C# attribute-argument syntax at
    // all - CS0182 ("An attribute argument must be a constant expression, typeof expression or array
    // creation expression of an attribute parameter type") rejects it at compile time, for either
    // package, before either package's own binding logic ever runs. NormalizeParamsArguments's own
    // "single reference-array argument" handling in Compono.XunitV3.ComposeAttribute exists only for
    // that constructor's own direct-construction unit-test coverage (ComposeAttributeCachingTests'
    // InlineValues_SingleReferenceArrayArgument_TreatedAsOneSuppliedArrayValue calls the constructor
    // directly in C#, never through a real [Compose(...)] attribute annotation) - it is not a shape
    // reachable through genuine attribute usage either. Every array-typed argument that CAN legally
    // reach this method (one whose type is exactly object?[]/object[], matching this attribute's own
    // declared parameter type) is therefore always the ordinary N-separate-values case .Values already
    // handles correctly - there is no unresolved disambiguation left to do here.
    private static IReadOnlyList<TypedConstant> NormalizeConstructorArguments(AttributeData attributeData)
    {
        if (attributeData.ConstructorArguments.Length == 0)
            return [];

        var paramsArray = attributeData.ConstructorArguments[0];

        return paramsArray.IsNull ? [paramsArray] : paramsArray.Values;
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
