using System.Reflection;
using global::TUnit.Core;

namespace Compono.TUnit.Binding;

/// <summary>
/// One test method's cached binding plan - the reflected, immutable metadata the per-row binding
/// algorithm reads on every generated row. Computed exactly once per attribute instance, the first
/// time <c>GenerateDataSources</c> runs (<see cref="ComposeAttribute"/>'s caching), since the
/// <see cref="MethodMetadata"/> <see cref="Build"/> is given is stable for a given attribute
/// instance. Adapted from <c>Compono.XunitV3.Binding.BindingPlan</c> per
/// <c>docs/adr/0040-compono-tunit-package-design.md</c>'s binding-logic decision - TUnit hands a
/// data source <see cref="ParameterMetadata"/> (already carrying nullability/params/position), not
/// a raw <c>ParameterInfo</c>, so this is not a byte-for-byte port.
/// </summary>
internal sealed class BindingPlan
{
    /// <summary>
    /// The reason this method's signature is unsupported (a generic method, a <c>ref</c>/<c>out</c>/
    /// <c>in</c>/<c>params</c> parameter, or more than one <c>[Shared]</c> parameter of the same
    /// type), or <see langword="null"/> if the signature is supported. <see cref="ComposeAttribute"/>
    /// throws using this message - appending the row's seed - before any parameter is bound or
    /// composed.
    /// </summary>
    public required string? SignatureError { get; init; }

    /// <summary>
    /// Each parameter's cached binding metadata, in declaration order. Empty when
    /// <see cref="SignatureError"/> is set.
    /// </summary>
    public required IReadOnlyList<ParameterBindingPlan> Parameters { get; init; }

    /// <summary>
    /// Builds the binding plan for <paramref name="testInformation"/> - signature validation
    /// followed by, for a supported signature only, each parameter's descriptor and cached invoker
    /// delegates (<see cref="RowInvokers.Build"/>, one <c>MakeGenericMethod</c> per parameter).
    /// </summary>
    public static BindingPlan Build(MethodMetadata testInformation)
    {
        ArgumentNullException.ThrowIfNull(testInformation);

        var parameters = testInformation.Parameters;
        var signatureError = ValidateSignature(testInformation, parameters);

        if (signatureError is not null)
            return new BindingPlan { SignatureError = signatureError, Parameters = [] };

        var declaringType = testInformation.Class.Type;
        var plan = new List<ParameterBindingPlan>(parameters.Length);

        foreach (var parameter in parameters)
        {
            var descriptor = new CompositionRequestDescriptor(
                CompositionRequestKind.TestParameter,
                parameter.Position,
                parameter.Name,
                declaringType,
                parameter.IsNullable ? Nullability.Nullable : Nullability.NotNullable);

            var invokers = RowInvokers.Build(parameter.Type);

            plan.Add(new ParameterBindingPlan
            {
                Name = parameter.Name,
                ParameterType = parameter.Type,
                IsShared = parameter.ReflectionInfo.GetCustomAttributes(typeof(SharedAttribute), false).Length > 0,
                Descriptor = descriptor,
                ResolveInvoker = invokers.Resolve,
                ResolveSharedInvoker = invokers.ResolveShared,
                ShareExplicitInvoker = invokers.ShareExplicit,
            });
        }

        return new BindingPlan { SignatureError = null, Parameters = plan };
    }

    /// <summary>
    /// The dotted display name pre-composition exception messages use for
    /// <paramref name="testInformation"/>.
    /// </summary>
    internal static string MethodDisplayName(MethodMetadata testInformation) =>
        $"{testInformation.Class.Type.FullName}.{testInformation.Name}";

    // Mirrors Compono.XunitV3's own "Async and Unsupported Shapes" validation, adapted to what
    // ParameterMetadata already computes (IsParams) versus what still needs one reflection call per
    // parameter (ByRef - ParameterMetadata has no ready-made equivalent).
    private static string? ValidateSignature(MethodMetadata testInformation, ParameterMetadata[] parameters)
    {
        var methodDisplayName = MethodDisplayName(testInformation);

        // [AttributeUsage(AllowMultiple = false)] is enforced per exact attribute type by the
        // compiler, not across a base/derived family - [Compose], [Compose<TProfile>], and
        // [Compose<TProfile, TConfig>] (or two differently-closed forms of either generic one) are
        // distinct types that each individually satisfy their own AllowMultiple = false, so nothing
        // stops stacking more than one Compose-family attribute on the same method without this
        // explicit check. Mirrors Compono.XunitV3.Binding.BindingPlan's identical check, adapted to
        // what MethodMetadata/ParameterMetadata expose: a parameter's ReflectionInfo.Member is the
        // declaring MethodInfo whenever the method has at least one parameter; a zero-parameter
        // method needs its own lookup instead (see ResolveMethodInfo below - filtered by name,
        // parameter count, and generic arity together, not parameter count alone).
        var method = ResolveMethodInfo(testInformation, parameters);
        var composeAttributeCount = method?.GetCustomAttributes<ComposeAttribute>(inherit: false).Count() ?? 0;

        if (composeAttributeCount > 1)
            return $"More than one [Compose]/[Compose<TProfile>]/[Compose<TProfile, TConfig>] attribute on '{methodDisplayName}' - only one Compose-family attribute per test method is allowed.";

        if (testInformation.GenericTypeCount > 0)
            return $"Compono.TUnit does not support generic test methods ('{methodDisplayName}').";

        foreach (var parameter in parameters)
        {
            if (parameter.ReflectionInfo.ParameterType.IsByRef)
                return $"Compono.TUnit does not support ref/out/in parameters (parameter '{parameter.Name}' on '{methodDisplayName}').";

            if (parameter.IsParams)
                return $"Compono.TUnit does not support params parameters (parameter '{parameter.Name}' on '{methodDisplayName}').";

            // ADR-0041's dispatch-eligibility guard, runtime side: a ref struct (e.g. Span<int>) or a
            // pointer type can never legally be a generic type argument to
            // CompositionRow.Resolve<T>()/etc. at all - the generator's own guard (ComposedTypeAnalyzer.
            // IsRowInvokerShapeEligible) already refuses to emit a RowInvokerRegistry registration for
            // one, so RowInvokers.Build would otherwise fail with an unhelpful "no dispatch registered"
            // message. Mirrors Compono.XunitV3.Binding.BindingPlan's identical check - not a new
            // investigation.
            if (parameter.ReflectionInfo.ParameterType.IsByRefLike || parameter.ReflectionInfo.ParameterType.IsPointer)
                return $"Compono.TUnit does not support ref struct or pointer-typed parameters (parameter '{parameter.Name}' on '{methodDisplayName}').";
        }

        var duplicateSharedType = parameters
            .Where(static parameter => parameter.ReflectionInfo.GetCustomAttributes(typeof(SharedAttribute), false).Length > 0)
            .GroupBy(static parameter => parameter.Type)
            .FirstOrDefault(static group => group.Count() > 1);

        if (duplicateSharedType is not null)
        {
            var parameterNames = string.Join("', '", duplicateSharedType.Select(static parameter => parameter.Name));
            return $"More than one [Shared] parameter of type '{duplicateSharedType.Key}' ('{parameterNames}') on '{methodDisplayName}' - only one [Shared] parameter per type is allowed.";
        }

        return null;
    }

    // A parameter's own ReflectionInfo.Member is always the declaring MethodInfo - the cheapest
    // possible lookup, and correct even for an overloaded method name, since it's the exact
    // MethodInfo TUnit itself resolved this parameter from. A zero-parameter method has no
    // parameter to read that from, so falls back to a direct lookup instead - but
    // Type.GetMethod(name, Type.EmptyTypes) matches by parameter *types* only, not generic arity, so
    // a class declaring both a zero-parameter Run() and a zero-parameter-but-generic Run<T>() throws
    // AmbiguousMatchException instead of returning either - before the generic-method check above
    // even gets a chance to produce its own clear CompositionException (Codex review). Filtering
    // GetMethods() by both zero declared parameters and testInformation.GenericTypeCount (this
    // specific test's own arity, whether zero or not) disambiguates that case the same way the
    // compiler already did to produce this exact MethodMetadata.
    private static MethodInfo? ResolveMethodInfo(MethodMetadata testInformation, ParameterMetadata[] parameters)
    {
        if (parameters.Length > 0)
            return parameters[0].ReflectionInfo.Member as MethodInfo;

        return testInformation.Class.Type
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .FirstOrDefault(candidate =>
                candidate.Name == testInformation.Name &&
                candidate.GetParameters().Length == 0 &&
                (candidate.IsGenericMethodDefinition ? candidate.GetGenericArguments().Length : 0) == testInformation.GenericTypeCount);
    }
}
