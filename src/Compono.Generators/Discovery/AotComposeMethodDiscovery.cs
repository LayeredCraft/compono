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
        var compilation = context.SemanticModel.Compilation;

        // PR #140 Codex review: [Compose<TProfile>] and [Compose<TProfile, TConfig>] are distinct
        // attribute types sharing no common base class (ADR-0067 Amendment 1 - each derives directly
        // from Xunit.v3.DataAttribute), so nothing stops stacking two different forms on the same
        // method the way BindingPlan.ValidateSignature's single GetCustomAttributes<ComposeAttribute>()
        // query catches this for Compono.XunitV3's own three forms. Left unchecked, each would
        // independently reach the emitter below and both call AddSource with the identical
        // class-and-method-derived hint name, throwing inside the generator (crashing the whole
        // compilation) instead of producing an actionable diagnostic - caught here, first, before any
        // other processing.
        if (CountAotComposeAttributes(method) > 1)
        {
            return new AotComposeMethodInfo(
                fullyQualifiedTestClassName,
                method.Name,
                Array.Empty<AotComposeParameterInfo>().ToEquatableArray(),
                new[] { new DiagnosticInfo(DiagnosticDescriptors.MultipleAotComposeAttributes, location, methodDisplayName) }.ToEquatableArray());
        }

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

            if (!ComposedTypeAnalyzer.IsRowInvokerShapeEligible(parameter.Type, compilation))
                return Unsupported(fullyQualifiedTestClassName, method.Name, location, methodDisplayName, $"parameter '{parameter.Name}' has a type that cannot be composed (an open generic parameter, ref struct, pointer, function pointer, or unsupported array shape)");

            parameters.Add(new AotComposeParameterInfo(
                parameter.Name,
                parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                parameter.Ordinal,
                IsNullable(parameter.Type)));
        }

        // context.Attributes contains exactly one match - ForAttributeWithMetadataName matches an
        // attribute usage's own attribute-class metadata name exactly, and [AttributeUsage(AllowMultiple
        // = false)] rules out a second usage of the same closed form on one method (the
        // CountAotComposeAttributes check above rules out a second usage of a *different* form).
        var attributeData = context.Attributes[0];
        var profileDiagnostics = new List<DiagnosticInfo>();

        var profile = attributeData.AttributeClass!.Arity switch
        {
            0 => null,
            1 => BuildOneTypeParameterProfile(attributeData, methodDisplayName, location, compilation, profileDiagnostics),
            2 => BuildTwoTypeParameterProfile(attributeData, methodDisplayName, location, compilation, profileDiagnostics),
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
    // the use site (a generic-attribute constraint, like any other closed generic type: it also rules
    // out an abstract or non-named TProfile, since neither can satisfy new()), so the only remaining
    // compile-time check this form needs is accessibility (CMP0044) - a private/protected TProfile
    // nested inside the attributed method's own containing type is legal at the [Compose<TProfile>]
    // use site but not from the generated top-level AddProfile<TProfile>() call.
    private static AotProfileInfo? BuildOneTypeParameterProfile(
        AttributeData attributeData,
        string methodDisplayName,
        LocationInfo? location,
        Compilation compilation,
        List<DiagnosticInfo> diagnostics)
    {
        var profileType = attributeData.AttributeClass!.TypeArguments[0];

        if (!compilation.IsSymbolAccessibleWithin(profileType, compilation.Assembly))
        {
            diagnostics.Add(InaccessibleSymbolDiagnostic(profileType, methodDisplayName, location, "the TProfile type argument"));
            return null;
        }

        // PR #140 Codex review round 9: the `new()` constraint guarantees a public parameterless
        // constructor exists, but not that it's safe to actually call - the generated top-level
        // registration calls `Composer.Create(b => b.AddProfile<TProfile>())`, and `AddProfile<T>()`'s
        // own generic `new T()` construction closes over the real TProfile at the *generated call site*,
        // so a [RequiresDynamicCode]/[RequiresUnreferencedCode]/[RequiresAssemblyFiles]-marked
        // constructor surfaces its IL3050/IL2026/IL3002 warning there - confirmed by direct probe that
        // this is how the trim/AOT analyzer treats a closed generic `where T : new()` construction
        // (unlike CMP0047's family - Obsolete(error:true)/Experimental/CompilerFeatureRequired - which a
        // second probe confirmed do NOT surface through generic `new T()` at all, since those are
        // compiler-enforced checks against a concrete member, not analyzer-surfaced hints, so they don't
        // need a check here).
        if (profileType is INamedTypeSymbol { InstanceConstructors: var profileTypeConstructors }
            && profileTypeConstructors.FirstOrDefault(static c => c.Parameters.Length == 0) is { } parameterlessConstructor
            && HasProhibitedAotAttribute(parameterlessConstructor))
        {
            diagnostics.Add(new DiagnosticInfo(
                DiagnosticDescriptors.ProfileConstructorRequiresAotUnsafeFeature,
                location,
                profileType.ToDisplayString(),
                methodDisplayName));
            return null;
        }

        return new AotProfileInfo(
            profileType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            null,
            EquatableArray<AotProfileConfigArgumentInfo>.Empty);
    }

    // [Compose<TProfile, TConfig>] - performs, at compile time, the same three checks
    // Compono.XunitV3.Binding.ConfigProfileBinder performs at runtime (ADR-0036): TConfig has exactly
    // one public constructor (CMP0041); TProfile has exactly one public constructor accepting exactly
    // one TConfig-typed parameter (CMP0042); the supplied profile configuration arguments match that
    // constructor's parameters (CMP0043) - plus three further checks PR #140's Codex review found the
    // initial pass missing: both types (and any typeof/enum-typed argument's own type) must be
    // accessible from the generated top-level registration (CMP0044); TProfile/TConfig are
    // unconstrained/only interface-constrained, so a non-named type argument (e.g. TConfig = string[])
    // must not reach an unconditional INamedTypeSymbol cast (guarded below rather than crashing); and
    // the selected constructors must not leave a required member unsatisfied (CMP0046). Any failure
    // appends to diagnostics and this method returns a value the caller discards (the diagnostics list
    // being non-empty is what matters, per TransformMethod's own check).
    private static AotProfileInfo? BuildTwoTypeParameterProfile(
        AttributeData attributeData,
        string methodDisplayName,
        LocationInfo? location,
        Compilation compilation,
        List<DiagnosticInfo> diagnostics)
    {
        var profileTypeArgument = attributeData.AttributeClass!.TypeArguments[0];
        var configTypeArgument = attributeData.AttributeClass!.TypeArguments[1];
        var profileDisplayName = profileTypeArgument.ToDisplayString();
        var configDisplayName = configTypeArgument.ToDisplayString();

        // A non-named TConfig (e.g. string[], legal since TConfig carries no constraint at all) or
        // TProfile (practically unreachable in ordinary use - ICompositionProfile is an interface
        // constraint, which only a named type can satisfy - but guarded the same way rather than
        // trusting that) structurally has zero usable constructors, reported through the same CMP0041/
        // CMP0042 "wrong constructor count" diagnostics an abstract type already uses, rather than an
        // unconditional cast throwing InvalidCastException inside the generator.
        //
        // PR #140 Codex review round 3: counting must match
        // Compono.XunitV3.Binding.ConfigProfileBinder.ResolveSingleConstructor's exact two-step shape -
        // it counts *every* public constructor first (regardless of parameter kind; a real
        // Type.GetConstructors(Public|Instance) call, confirmed by direct probe, returns a ref/out/in-
        // parameter constructor right alongside an ordinary one), and only rejects "ambiguous" at that
        // raw count. Filtering ref/out/in constructors out *before* counting (the round-2 fix) let a
        // TConfig with one ordinary and one ref/out/in constructor silently succeed here while JIT-mode
        // would reject it outright as ambiguous ("has 2") - a real, confirmed parity divergence, not
        // just a wording nicety (ADR-0067's own design intent: "performs, at compile time, the same
        // three checks ConfigProfileBinder performs at runtime"). Restructured into the same two
        // sequential gates JIT-mode has: raw-count ambiguity first, then (only once exactly one
        // constructor exists) whether that sole constructor is actually usable for AOT's direct-
        // construction codegen - a ref/out/in parameter still can't be satisfied by a generated literal
        // argument (no addressable variable to pass by reference, unlike JIT's reflection-based
        // ConstructorInfo.Invoke, confirmed by direct probe to actually succeed there), so it's still
        // rejected, just at the second gate rather than folded into the first.
        var configType = configTypeArgument as INamedTypeSymbol;
        // PR #140 Codex review round 14: computed unconditionally (not gated on `IsAbstract: false`) so
        // an abstract TConfig's diagnostic can still report its true declared public-constructor count -
        // round 13 added an explicit "public constructor(s)" noun to the raw-ambiguity gate's message,
        // which turned the pre-existing "abstract types report as having 0" shortcut below (`
        // allConfigConstructors`, still correctly forced to empty for the *usability* gate - an abstract
        // type can never be `new`'d regardless of how many constructors it declares) into an outright
        // false claim for an abstract type that actually has one or more public constructors.
        var rawConfigConstructors = configType?.Constructors
            // A struct's own compiler-synthesized parameterless constructor (no constructor explicitly
            // declared) is real, discoverable Roslyn metadata (INamedTypeSymbol.Constructors includes
            // it, IsImplicitlyDeclared = true, confirmed by direct probe) but is *not* reflectable -
            // Type.GetConstructors(Public|Instance) returns zero constructors for exactly this shape,
            // confirmed by direct probe - so JIT-mode's ConfigProfileBinder would reject this same
            // TConfig with "has 0", not silently succeed the way an unfiltered Roslyn count would.
            // PR #140 Codex review round 4: this exclusion must be scoped to value types only - a
            // *class*'s own implicit parameterless constructor is also IsImplicitlyDeclared = true in
            // Roslyn, but (unlike a struct's) it genuinely is real, reflectable IL
            // (Type.GetConstructors(Public|Instance) returns 1 for a no-explicit-ctor class, confirmed
            // by a direct probe) - excluding it too would have made an entirely ordinary `class Config
            // { }` fail CMP0041 with "has 0" when JIT-mode succeeds. `configType.IsValueType` is the
            // exact condition that distinguishes the two cases.
            .Where(c => c.DeclaredAccessibility == Accessibility.Public
                && !(configType.IsValueType && c.IsImplicitlyDeclared))
            .ToArray() ?? Array.Empty<IMethodSymbol>();

        // The *usability* gate below still needs abstract forced to zero regardless of `rawConfigConstructors`
        // - an abstract type can never be constructed via `new T(...)` no matter how many constructors it
        // declares, so it must always fail this gate, but with the message now reporting the true count.
        var allConfigConstructors = configType is { IsAbstract: false } ? rawConfigConstructors : Array.Empty<IMethodSymbol>();

        if (allConfigConstructors.Length != 1)
        {
            diagnostics.Add(new DiagnosticInfo(
                DiagnosticDescriptors.InvalidProfileConfigConstructorShape,
                location,
                configDisplayName,
                profileDisplayName,
                methodDisplayName,
                rawConfigConstructors.Length,
                // PR #140 Codex review round 13: this raw-ambiguity gate's count is the raw public-
                // constructor count, BEFORE the second gate's usability filtering - a TConfig with two
                // public constructors, one ordinary and one disqualified by a by-ref/dynamic parameter,
                // still hits this gate with count 2, neither of which is actually confirmed usable. The
                // noun phrase reflects that: "public constructor(s)", not "usable public constructor(s)".
                "public constructor(s)"));

            return null;
        }

        // Second gate: the sole constructor exists (JIT-mode would select it too) but isn't usable for
        // AOT's direct `new TConfig(literalArgs)` construction if it has a ref/out/in parameter, or a
        // `dynamic`-typed parameter - reported the same way an abstract/non-named type argument is
        // (zero *usable* constructors), consistent with that existing convention rather than a new
        // diagnostic just for either shape. PR #140 Codex review round 6: a `dynamic` parameter passes
        // TypedConstantMatcher.Validate (ClassifyConversion treats string->dynamic as an implicit
        // reference conversion, confirmed by direct probe) and would generate a `(dynamic)"literal"`
        // cast, invoking the C# runtime dynamic binder - not Native-AOT/trim-safe, violating ADR-0067's
        // zero-reflection guarantee. Unlike ref/out/in, JIT-mode's reflection-based
        // ConstructorInfo.Invoke *can* satisfy a dynamic parameter (it's just object at the metadata
        // level), so this is an AOT-only restriction, not a JIT-parity gap - still folded into the same
        // "0 usable constructors" gate rather than a new diagnostic. PR #140 Codex review round 8: a
        // constructor marked [RequiresDynamicCode]/[RequiresUnreferencedCode] compiles and runs under
        // JIT (ConstructorInfo.Invoke doesn't care), but a `PublishAot=true` consumer of the generated
        // direct `new T(...)` call gets a real IL3050/IL2026 warning (confirmed by direct probe) -
        // exactly the trim/AOT-unsafety this attribute exists to flag, directly contradicting this
        // package's zero-reflection/AOT-safety guarantee. Same tier as `dynamic`: excluded from the
        // usable-constructor set rather than emitting code that's merely *likely* to work.
        var configConstructors = allConfigConstructors[0].Parameters.All(static p =>
            p.RefKind == RefKind.None && p.Type.TypeKind != TypeKind.Dynamic)
            && !HasProhibitedAotAttribute(allConfigConstructors[0])
            ? allConfigConstructors
            : Array.Empty<IMethodSymbol>();

        if (configConstructors.Length != 1)
        {
            diagnostics.Add(new DiagnosticInfo(
                DiagnosticDescriptors.InvalidProfileConfigConstructorShape,
                location,
                configDisplayName,
                profileDisplayName,
                methodDisplayName,
                configConstructors.Length,
                // This gate's count IS the post-filtering usable count (the sole raw candidate, filtered
                // to 0 if unusable) - "usable public constructor(s)" is accurate here.
                "usable public constructor(s) (a constructor with a ref/out/in parameter, a " +
                "dynamic-typed parameter, or one marked " +
                "[RequiresDynamicCode]/[RequiresUnreferencedCode]/[RequiresAssemblyFiles] does not " +
                "count as usable)"));

            return null;
        }

        if (!compilation.IsSymbolAccessibleWithin(configType!, compilation.Assembly))
        {
            diagnostics.Add(InaccessibleSymbolDiagnostic(configType!, methodDisplayName, location, "the TConfig type argument"));
            return null;
        }

        var profileType = profileTypeArgument as INamedTypeSymbol;
        var profileConstructors = profileType is { IsAbstract: false }
            ? profileType.Constructors
                .Where(c => c.DeclaredAccessibility == Accessibility.Public
                    && c.Parameters.Length == 1
                    // PR #140 Codex review round 2: IParameterSymbol.Type strips ref/out/in - a
                    // Profile(ref TConfig)/Profile(out TConfig) constructor would otherwise match here
                    // and the generated `new TProfile(profileConfig)` call (no ref/out keyword) fails
                    // with CS1620; Profile(in TConfig) would instead *compile* (the `in` modifier is
                    // call-site-optional) but that's a real behavioral divergence from Compono.XunitV3's
                    // JIT-mode binder, which sees the by-ref runtime Type (TConfig&) and never matches
                    // it as this exact shape at all - excluded uniformly here for both reasons.
                    && c.Parameters[0].RefKind == RefKind.None
                    && SymbolEqualityComparer.Default.Equals(c.Parameters[0].Type, configType)
                    // PR #140 Codex review round 8: same RequiresDynamicCode/RequiresUnreferencedCode
                    // exclusion as configConstructors above, applied to the TProfile constructor too.
                    && !HasProhibitedAotAttribute(c))
                .ToArray()
            : Array.Empty<IMethodSymbol>();

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

        if (!compilation.IsSymbolAccessibleWithin(profileType!, compilation.Assembly))
        {
            diagnostics.Add(InaccessibleSymbolDiagnostic(profileType!, methodDisplayName, location, "the TProfile type argument"));
            return null;
        }

        var configConstructor = configConstructors[0];
        var profileConstructor = profileConstructors[0];

        if (!ConstructorSatisfiesRequiredMembers(configType!, configConstructor))
        {
            diagnostics.Add(RequiredMembersDiagnostic(configType!, configConstructor, methodDisplayName, location));
            return null;
        }

        if (!ConstructorSatisfiesRequiredMembers(profileType!, profileConstructor))
        {
            diagnostics.Add(RequiredMembersDiagnostic(profileType!, profileConstructor, methodDisplayName, location));
            return null;
        }

        if (ProhibitedCallSiteAttribute(configConstructor) is { } configProhibitedAttribute)
        {
            diagnostics.Add(ProhibitedConstructorDiagnostic(configType!, methodDisplayName, location, configProhibitedAttribute));
            return null;
        }

        if (ProhibitedCallSiteAttribute(profileConstructor) is { } profileProhibitedAttribute)
        {
            diagnostics.Add(ProhibitedConstructorDiagnostic(profileType!, methodDisplayName, location, profileProhibitedAttribute));
            return null;
        }

        if (FindHigherPriorityAccessibleSibling(configType!, configConstructor, compilation) is { } configSupersedingCtor)
        {
            diagnostics.Add(SupersededByPriorityDiagnostic(configType!, configSupersedingCtor, methodDisplayName, location));
            return null;
        }

        if (FindHigherPriorityAccessibleSibling(profileType!, profileConstructor, compilation) is { } profileSupersedingCtor)
        {
            diagnostics.Add(SupersededByPriorityDiagnostic(profileType!, profileSupersedingCtor, methodDisplayName, location));
            return null;
        }

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

            // A typeof(...)/enum-typed argument embeds a reference to another type in the rendered
            // literal (typeof(global::Ns.SomeType), (global::Ns.SomeEnum)1) - that type needs the same
            // top-level-accessibility guarantee TProfile/TConfig themselves just got, or the generated
            // registration fails with CS0122 the same way (PR #140 Codex review). Walks into array
            // elements too (PR #140 round 4 evidence: an array of a private nested enum/Type value has
            // Kind = Array at the top level - EmbeddedTypes recurses so an inaccessible type nested
            // inside an array argument is caught the same way a top-level one already was).
            foreach (var embeddedType in EmbeddedTypes(argument))
            {
                if (!compilation.IsSymbolAccessibleWithin(embeddedType, compilation.Assembly))
                {
                    diagnostics.Add(InaccessibleSymbolDiagnostic(embeddedType, methodDisplayName, location, $"the profile configuration argument for parameter '{parameter.Name}'"));
                    return null;
                }
            }

            renderedArguments.Add(new AotProfileConfigArgumentInfo(
                TypedConstantLiteralRenderer.Render(argument, parameter.Type),
                parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
        }

        return new AotProfileInfo(
            profileType!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            configType!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
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

    // PR #140 Codex review: [Compose<TProfile>]/[Compose<TProfile, TConfig>] are independent marker
    // types (ADR-0067 Amendment 1) with no shared base class for a single GetAttributes<T>() query to
    // catch, so this compares each attribute's own metadata name against all three forms directly -
    // the compile-time equivalent of Compono.XunitV3.Binding.BindingPlan.ValidateSignature's runtime
    // GetCustomAttributes<ComposeAttribute>().Count() check.
    private static int CountAotComposeAttributes(IMethodSymbol method) =>
        method.GetAttributes().Count(a => a.AttributeClass is { } attributeClass && IsAotComposeAttribute(attributeClass));

    private static bool IsAotComposeAttribute(INamedTypeSymbol attributeClass)
    {
        var metadataName = $"{attributeClass.ContainingNamespace.ToDisplayString()}.{attributeClass.MetadataName}";

        return metadataName is AttributeMetadataName or GenericAttributeMetadataName or TwoTypeParameterAttributeMetadataName;
    }

    private static DiagnosticInfo InaccessibleSymbolDiagnostic(ITypeSymbol type, string methodDisplayName, LocationInfo? location, string role) =>
        new(
            DiagnosticDescriptors.InaccessibleProfileSymbol,
            location,
            type.ToDisplayString(),
            methodDisplayName,
            role);

    // PR #140 Codex review round 4: recurses into array elements, not just the top-level constant -
    // typeof(...)/enum-typed values embedded inside an array argument (e.g. new PrivateEnum[] { ... })
    // need the same accessibility check a top-level typeof(...)/enum argument already gets.
    // PR #140 Codex review round 5: a null array constant (e.g. the whole params array is null, per
    // NormalizeConstructorArguments' own null-array handling for a bare [Compose<P, C>(null)] use
    // site) still reports Kind = Array, but its .Values throws NullReferenceException - confirmed by
    // a direct probe, not merely an empty-array edge case. Guarded first, uniformly, since IsNull is
    // meaningful for every Kind this switch handles, not just Array.
    private static IEnumerable<ITypeSymbol> EmbeddedTypes(TypedConstant constant)
    {
        if (constant.IsNull)
            yield break;

        switch (constant.Kind)
        {
            case TypedConstantKind.Type:
                yield return (ITypeSymbol)constant.Value!;
                break;

            case TypedConstantKind.Enum when constant.Type is not null:
                yield return constant.Type;
                break;

            case TypedConstantKind.Array:
                // PR #140 Codex review round 5 (finding not caught until round 6's re-check): the
                // array's own *declared element type* is embedded in the rendered literal
                // (`new global::Ns.PrivateEnum[] { ... }`) regardless of whether the array actually
                // has any elements - an empty array of a private type (`new PrivateEnum[] { }`) has
                // no Values to recurse into at all, so the per-element walk alone never catches it.
                // Checked independently of the per-element recursion below (which instead catches a
                // *value* embedding some other type, e.g. a typeof(...) element inside a Type[] array
                // whose own declared element type - System.Type - is always accessible).
                if (constant.Type is IArrayTypeSymbol arrayType)
                    yield return arrayType.ElementType;

                foreach (var element in constant.Values)
                foreach (var embeddedType in EmbeddedTypes(element))
                    yield return embeddedType;
                break;
        }
    }

    // The compile-time counterpart to RequiredMemberCollector's own [SetsRequiredMembers] check
    // (used for core Compono's ordinary composed-type construction) - deliberately narrower: this
    // attribute family constructs TConfig/TProfile directly from literal attribute arguments, not
    // through Compono's provider pipeline, so there is no sensible composed value to auto-supply a
    // required member with the way core construction does. An unsatisfied required member is
    // reported as a diagnostic (CMP0046) rather than attempted.
    private static bool ConstructorSatisfiesRequiredMembers(INamedTypeSymbol type, IMethodSymbol constructor)
    {
        if (constructor.GetAttributes().Any(static a =>
                a.AttributeClass?.ToDisplayString() == "System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute"))
            return true;

        for (var current = type; current is not null; current = current.BaseType)
        {
            foreach (var member in current.GetMembers())
            {
                if (member is IPropertySymbol { IsRequired: true } or IFieldSymbol { IsRequired: true })
                    return false;
            }
        }

        return true;
    }

    private static DiagnosticInfo RequiredMembersDiagnostic(INamedTypeSymbol type, IMethodSymbol constructor, string methodDisplayName, LocationInfo? location)
    {
        var unsatisfiedMember = FindFirstUnsatisfiedRequiredMember(type);

        return new DiagnosticInfo(
            DiagnosticDescriptors.ProfileConstructorRequiredMembersUnsatisfied,
            location,
            type.ToDisplayString(),
            unsatisfiedMember,
            methodDisplayName);
    }

    private static string FindFirstUnsatisfiedRequiredMember(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            foreach (var member in current.GetMembers())
            {
                if (member is IPropertySymbol { IsRequired: true } or IFieldSymbol { IsRequired: true })
                    return member.Name;
            }
        }

        return "(unknown)";
    }

    // PR #140 Codex review round 6 (CMP0047) - a constructor marked [Obsolete("...", error: true)] is
    // otherwise a perfectly normal, selectable constructor (shape/accessibility/required-members all
    // pass), but Compono.Generators' generated registration calls it directly (`new T(...)`), which the
    // compiler rejects with CS0619 for this attribute shape specifically - confirmed by direct compile
    // probe. `error: false` (the default, a mere warning) is left alone: CS0618 does not block
    // compilation, so the generated registration still builds. PR #140 Codex review round 7: generalized
    // to also detect [System.Diagnostics.CodeAnalysis.Experimental("...")] - confirmed by a direct
    // compile probe to be a second, independent standard attribute where any *use* of the marked
    // constructor is always a compiler error (the attribute's own diagnostic ID, e.g. EXP001, at default
    // severity Error - there is no "off" mode the way Obsolete has `error: false`). Returns the rendered
    // attribute syntax to name in the diagnostic message, or null if the constructor carries neither.
    private static string? ProhibitedCallSiteAttribute(IMethodSymbol constructor)
    {
        foreach (var attribute in constructor.GetAttributes())
        {
            switch (attribute.AttributeClass?.ToDisplayString())
            {
                case "System.ObsoleteAttribute" when attribute.ConstructorArguments is [_, { Value: true }]:
                    return "[Obsolete(error: true)]";

                case "System.Diagnostics.CodeAnalysis.ExperimentalAttribute":
                    var diagnosticId = attribute.ConstructorArguments is [{ Value: string id }] ? id : "...";
                    return $"[Experimental(\"{diagnosticId}\")]";

                // PR #140 Codex review round 8: [CompilerFeatureRequired("...")] with the default
                // IsOptional = false is a third independent attribute in this same bucket - source code
                // can never apply it directly (CS8335 blocks that), but a constructor from a *referenced*
                // assembly (e.g. compiled by a future/different compiler, or IL-emitted) can carry it,
                // and Roslyn still reports it via GetAttributes() on the imported symbol (confirmed by a
                // direct probe: built such a constructor via PersistedAssemblyBuilder, referenced it, and
                // confirmed both that a real C# compiler rejects `new T(...)` with CS9041, and that this
                // generator's own Roslyn APIs see the attribute on the imported symbol identically to any
                // other constructor attribute).
                case "System.Runtime.CompilerServices.CompilerFeatureRequiredAttribute":
                    var isOptional = attribute.NamedArguments
                        .FirstOrDefault(static na => na.Key == "IsOptional").Value is { Value: true };
                    if (isOptional)
                        continue;
                    // Best-effort: the feature name renders correctly against a real compiled reference
                    // assembly, but falls back to "..." rather than failing when it can't be read (e.g.
                    // an IL-emitted PersistedAssemblyBuilder reference, as this file's own test coverage
                    // uses) - this is purely cosmetic, the rejection itself doesn't depend on the name.
                    var featureName = attribute.ConstructorArguments.Length == 1 && attribute.ConstructorArguments[0].Value is string feature
                        ? feature
                        : "...";
                    return $"[CompilerFeatureRequired(\"{featureName}\")]";
            }
        }

        return null;
    }

    // PR #140 Codex review round 8 - a constructor marked [RequiresDynamicCode]/[RequiresUnreferencedCode]
    // compiles and runs fine under ordinary JIT execution (this is purely an analyzer-surfaced hint, not
    // a compiler-enforced restriction the way the ProhibitedCallSiteAttribute family is), but using it
    // from Compono.Generators' generated direct `new T(...)` call produces a real IL3050/IL2026 warning
    // for any consumer with trim/AOT analysis enabled (confirmed by direct probe) - exactly the failure
    // mode these attributes exist to flag, so it's excluded from the usable-constructor set entirely
    // rather than emitted and hoped clean. PR #140 Codex review round 9: generalized to also detect
    // [RequiresAssemblyFiles] - a third, independent member of the same "analyzer-surfaced AOT/trim/
    // single-file hazard" family (produces IL3002 rather than IL3050/IL2026), confirmed reachable the
    // same way by direct probe.
    private static bool HasProhibitedAotAttribute(IMethodSymbol constructor) =>
        constructor.GetAttributes().Any(static a => a.AttributeClass?.ToDisplayString() is
            "System.Diagnostics.CodeAnalysis.RequiresDynamicCodeAttribute" or
            "System.Diagnostics.CodeAnalysis.RequiresUnreferencedCodeAttribute" or
            "System.Diagnostics.CodeAnalysis.RequiresAssemblyFilesAttribute");

    private static DiagnosticInfo ProhibitedConstructorDiagnostic(INamedTypeSymbol type, string methodDisplayName, LocationInfo? location, string prohibitedAttribute) =>
        new(
            DiagnosticDescriptors.ProhibitedProfileConstructor,
            location,
            type.ToDisplayString(),
            methodDisplayName,
            prohibitedAttribute);

    // PR #140 Codex review round 8 (CMP0048) - round 5's explicit-cast fix for the overload-hijack
    // finding only defends against ordinary overload resolution; it does nothing against
    // [OverloadResolutionPriority], which prunes candidates to the highest-priority group *before*
    // applicability/conversion quality is even compared - confirmed by direct probe that a higher-
    // priority accessible sibling constructor still wins even when the call site's argument is cast to
    // the selected constructor's own parameter type. Conservative by design (predictability over magic,
    // ADR-0001): any accessible sibling with an explicit priority strictly greater than the selected
    // constructor's own (default 0 if absent) is treated as a potential supersession, regardless of
    // whether its parameter shape would actually be applicable to the rendered arguments - correctly
    // computing real C# overload applicability at compile time here would mean reimplementing overload
    // resolution itself, which this generator has consistently avoided elsewhere (round 3's two-gate
    // restructure took the same "diagnostic over cleverness" position for ref/out/in ambiguity).
    private static IMethodSymbol? FindHigherPriorityAccessibleSibling(INamedTypeSymbol type, IMethodSymbol selected, Compilation compilation)
    {
        var selectedPriority = OverloadResolutionPriorityOf(selected);

        foreach (var candidate in type.Constructors)
        {
            if (SymbolEqualityComparer.Default.Equals(candidate, selected))
                continue;

            if (OverloadResolutionPriorityOf(candidate) <= selectedPriority)
                continue;

            if (compilation.IsSymbolAccessibleWithin(candidate, compilation.Assembly))
                return candidate;
        }

        return null;
    }

    private static int OverloadResolutionPriorityOf(IMethodSymbol constructor)
    {
        foreach (var attribute in constructor.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() == "System.Runtime.CompilerServices.OverloadResolutionPriorityAttribute"
                && attribute.ConstructorArguments is [{ Value: int priority }])
                return priority;
        }

        return 0;
    }

    private static DiagnosticInfo SupersededByPriorityDiagnostic(INamedTypeSymbol type, IMethodSymbol supersedingConstructor, string methodDisplayName, LocationInfo? location) =>
        new(
            DiagnosticDescriptors.ProfileConstructorSupersededByPriority,
            location,
            type.ToDisplayString(),
            methodDisplayName,
            supersedingConstructor.ToDisplayString());

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
