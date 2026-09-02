using System.Reflection;

namespace Compono.MSTest.Binding;

/// <summary>
/// One test method's cached binding plan - the reflected, immutable metadata the per-row binding
/// algorithm reads on every <c>GetData</c> call. Computed exactly once per attribute instance, the
/// first time <c>GetData</c> runs (<see cref="ComposeAttribute"/>'s caching), since the
/// <see cref="System.Reflection.MethodInfo"/> <see cref="Build"/> is given is stable for a given
/// attribute instance. See ADR-0057's binding sections.
/// </summary>
internal sealed class BindingPlan
{
    /// <summary>
    /// The reason this method's signature is unsupported (more than one Compose-family attribute, a
    /// generic method, a <c>ref</c>/<c>out</c>/<c>in</c>/<c>params</c> parameter, a <c>ref struct</c>/
    /// pointer-typed parameter, or more than one <c>[Shared]</c> parameter of the same type), or
    /// <see langword="null"/> if the signature is supported. <c>GetData</c> throws using this message
    /// - appending the row's seed - before any parameter is bound or composed.
    /// </summary>
    public required string? SignatureError { get; init; }

    /// <summary>
    /// Each parameter's cached binding metadata, in declaration order. Empty when
    /// <see cref="SignatureError"/> is set.
    /// </summary>
    public required IReadOnlyList<ParameterBindingPlan> Parameters { get; init; }

    /// <summary>
    /// Builds the binding plan for <paramref name="testMethod"/> - signature validation followed by,
    /// for a supported signature only, each parameter's descriptor and cached invoker delegates
    /// (<see cref="RowInvokers.Build"/>).
    /// </summary>
    public static BindingPlan Build(MethodInfo testMethod)
    {
        ArgumentNullException.ThrowIfNull(testMethod);

        var parameters = testMethod.GetParameters();
        var signatureError = ValidateSignature(testMethod, parameters);

        if (signatureError is not null)
            return new BindingPlan { SignatureError = signatureError, Parameters = [] };

        // A test method is always declared on a type - there is no "global" test method shape.
        var declaringType = testMethod.DeclaringType!;
        var nullabilityContext = new NullabilityInfoContext();
        var plan = new List<ParameterBindingPlan>(parameters.Length);

        foreach (var parameter in parameters)
        {
            var name = parameter.Name ?? string.Empty;
            var descriptor = new CompositionRequestDescriptor(
                CompositionRequestKind.TestParameter,
                parameter.Position,
                name,
                declaringType,
                GetNullability(nullabilityContext, parameter));

            var invokers = RowInvokers.Build(parameter.ParameterType);

            plan.Add(new ParameterBindingPlan
            {
                Name = name,
                ParameterType = parameter.ParameterType,
                IsShared = parameter.GetCustomAttribute<SharedAttribute>() is not null,
                Descriptor = descriptor,
                ResolveInvoker = invokers.Resolve,
                ResolveSharedInvoker = invokers.ResolveShared,
                ShareExplicitInvoker = invokers.ShareExplicit,
            });
        }

        return new BindingPlan { SignatureError = null, Parameters = plan };
    }

    /// <summary>
    /// The dotted display name pre-composition exception messages use for <paramref name="testMethod"/>
    /// - shared with <see cref="ValidateSignature"/> so both halves of this package's diagnostics name
    /// a method identically.
    /// </summary>
    internal static string MethodDisplayName(MethodInfo testMethod) =>
        $"{testMethod.DeclaringType?.FullName}.{testMethod.Name}";

    private static string? ValidateSignature(MethodInfo testMethod, ParameterInfo[] parameters)
    {
        var methodDisplayName = MethodDisplayName(testMethod);

        // [AttributeUsage(AllowMultiple = false)] is enforced per exact attribute type by the
        // compiler, not across a base/derived family - [Compose], [Compose<TProfile>], and
        // [Compose<TProfile, TConfig>] are distinct types that each individually satisfy their own
        // AllowMultiple = false, so nothing stops stacking more than one Compose-family attribute on
        // the same method without this explicit check - matching Compono.XunitV3/Compono.TUnit.
        var composeAttributeCount = testMethod.GetCustomAttributes<ComposeAttribute>().Count();

        if (composeAttributeCount > 1)
            return $"More than one [Compose]/[Compose<TProfile>]/[Compose<TProfile, TConfig>] attribute on '{methodDisplayName}' - only one Compose-family attribute per test method is allowed.";

        if (testMethod.IsGenericMethodDefinition)
            return $"Compono.MSTest does not support generic test methods ('{methodDisplayName}').";

        foreach (var parameter in parameters)
        {
            if (parameter.ParameterType.IsByRef)
                return $"Compono.MSTest does not support ref/out/in parameters (parameter '{parameter.Name}' on '{methodDisplayName}').";

            if (parameter.GetCustomAttribute<ParamArrayAttribute>() is not null)
                return $"Compono.MSTest does not support params parameters (parameter '{parameter.Name}' on '{methodDisplayName}').";

            // ADR-0041's dispatch-eligibility guard, runtime side: a ref struct (e.g. Span<int>) or a
            // pointer type can never legally be a generic type argument to
            // CompositionRow.Resolve<T>()/etc. at all.
            if (parameter.ParameterType.IsByRefLike || parameter.ParameterType.IsPointer)
                return $"Compono.MSTest does not support ref struct or pointer-typed parameters (parameter '{parameter.Name}' on '{methodDisplayName}').";
        }

        var duplicateSharedType = parameters
            .Where(static parameter => parameter.GetCustomAttribute<SharedAttribute>() is not null)
            .GroupBy(static parameter => parameter.ParameterType)
            .FirstOrDefault(static group => group.Count() > 1);

        if (duplicateSharedType is not null)
        {
            var parameterNames = string.Join("', '", duplicateSharedType.Select(static parameter => parameter.Name));
            return $"More than one [Shared] parameter of type '{duplicateSharedType.Key}' ('{parameterNames}') on '{methodDisplayName}' - only one [Shared] parameter per type is allowed.";
        }

        return null;
    }

    private static Nullability GetNullability(NullabilityInfoContext nullabilityContext, ParameterInfo parameter)
    {
        if (Nullable.GetUnderlyingType(parameter.ParameterType) is not null)
            return Nullability.Nullable;

        if (parameter.ParameterType.IsValueType)
            return Nullability.NotNullable;

        return nullabilityContext.Create(parameter).ReadState == NullabilityState.Nullable
            ? Nullability.Nullable
            : Nullability.NotNullable;
    }
}
