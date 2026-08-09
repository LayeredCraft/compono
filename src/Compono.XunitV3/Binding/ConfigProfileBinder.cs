using System.Reflection;

namespace Compono.XunitV3.Binding;

/// <summary>
/// Resolves and invokes the single constructors <see cref="ComposeAttribute{TProfile, TConfig}"/> uses
/// to build a <c>TConfig</c> from profile configuration arguments and a <c>TProfile</c> from that
/// <c>TConfig</c> - see
/// <c>docs/adr/0036-parameterized-composition-profile-selection.md</c>'s "Constructor contracts"
/// section. Deliberately narrow and deterministic: no "best constructor match" heuristic exists
/// anywhere here - an unsupported or ambiguous constructor shape is always a clear, named
/// <see cref="CompositionException"/>, never a guessed resolution.
/// </summary>
/// <remarks>
/// Every method here is reflection - <see cref="Type.GetConstructors()"/> and
/// <see cref="ConstructorInfo.Invoke(object?[])"/>, not the cached <see cref="MethodInfo.MakeGenericMethod"/>/
/// <see cref="Delegate.CreateDelegate(Type, MethodInfo)"/> shape <see cref="RowInvokers"/> uses. That
/// shape exists there to close a generic method over a parameter type known only at runtime, once per
/// parameter, so the per-row path never reflects again. <c>TConfig</c>/<c>TProfile</c> need no
/// equivalent: they're already compile-time-closed generic arguments on
/// <see cref="ComposeAttribute{TProfile, TConfig}"/> itself, and this binder's methods are only ever
/// called from <see cref="ComposeAttribute{TProfile, TConfig}.ApplyProfile"/> - itself only ever
/// invoked once per attribute instance, from inside the base <see cref="ComposeAttribute"/>'s existing
/// <c>Lazy&lt;Composer&gt;</c>-backed caching. That existing caching is what bounds this reflection to
/// once per attribute instance, never the repeated per-row <c>GetData</c> path - no separate caching
/// layer is needed here.
/// </remarks>
internal static class ConfigProfileBinder
{
    /// <summary>
    /// Binds <paramref name="configArguments"/> positionally to <paramref name="configType"/>'s single
    /// public constructor and invokes it - the same count/nullability/assignability validation
    /// <see cref="ComposeAttribute"/>'s own inline-value binding uses (ADR-0022), retargeted at a
    /// constructor's parameters instead of a test method's.
    /// </summary>
    /// <exception cref="CompositionException">
    /// <paramref name="configType"/> does not have exactly one public constructor; the supplied
    /// argument count doesn't match that constructor's parameter count; a supplied argument is
    /// <see langword="null"/> for a non-nullable parameter; or a supplied argument's type isn't
    /// assignable to its parameter's type.
    /// </exception>
    public static object BindConfig(Type configType, IReadOnlyList<object?> configArguments)
    {
        var constructor = ResolveSingleConstructor(configType);
        var parameters = constructor.GetParameters();

        if (configArguments.Count != parameters.Length)
        {
            throw new CompositionException(
                $"'{configType}' requires {parameters.Length} profile configuration argument(s), but {configArguments.Count} were supplied.");
        }

        var arguments = new object?[parameters.Length];
        var nullabilityContext = new NullabilityInfoContext();

        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            var value = configArguments[i];

            if (value is null)
            {
                if (!IsNullable(nullabilityContext, parameter))
                {
                    throw new CompositionException(
                        $"Profile configuration argument for parameter '{parameter.Name}' of '{configType}' is null, but the parameter is not nullable.");
                }

                arguments[i] = null;
                continue;
            }

            // A non-null Nullable<T> boxes as a boxed T, not a boxed Nullable<T> (a CLR
            // nullable-boxing rule) - unwrapping first is a no-op for a non-nullable parameter and
            // is what makes e.g. an int argument valid for an int? parameter. Same rule as
            // ComposeAttribute's own inline-value validation (ADR-0022).
            var underlyingType = Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType;

            if (!underlyingType.IsInstanceOfType(value))
            {
                throw new CompositionException(
                    $"Profile configuration argument for parameter '{parameter.Name}' of '{configType}' has type '{value.GetType()}', which is not assignable to '{parameter.ParameterType}'.");
            }

            arguments[i] = value;
        }

        return constructor.Invoke(arguments);
    }

    /// <summary>
    /// Constructs a <typeparamref name="TProfile"/> from <paramref name="config"/>, via
    /// <typeparamref name="TProfile"/>'s single public constructor accepting exactly one
    /// <typeparamref name="TConfig"/>-typed parameter.
    /// </summary>
    /// <exception cref="CompositionException">
    /// <typeparamref name="TProfile"/> does not have exactly one public constructor accepting exactly
    /// one <typeparamref name="TConfig"/>-typed parameter.
    /// </exception>
    public static TProfile BuildProfile<TProfile, TConfig>(object config)
        where TProfile : ICompositionProfile
    {
        var constructor = ResolveSingleProfileConstructor(typeof(TProfile), typeof(TConfig));

        return (TProfile)constructor.Invoke([config]);
    }

    private static ConstructorInfo ResolveSingleConstructor(Type type)
    {
        // An abstract type can still declare a public constructor (invoked only by a derived type's
        // own constructor chain) - GetConstructors would find it and this method would otherwise
        // hand it back as "the one constructor," but ConstructorInfo.Invoke on it throws
        // MemberAccessException ("Cannot create an abstract class"), not the documented
        // CompositionException - reject explicitly, before the constructor count check, so an
        // abstract TConfig fails with the same named diagnostic shape as every other unsupported
        // shape here (PR #65 review).
        if (type.IsAbstract)
        {
            throw new CompositionException(
                $"'{type}' is abstract and cannot be used as profile configuration - it must be a concrete, constructible type.");
        }

        var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);

        if (constructors.Length != 1)
        {
            throw new CompositionException(
                $"'{type}' must have exactly one public constructor to be used as profile configuration, but has {constructors.Length}.");
        }

        return constructors[0];
    }

    private static ConstructorInfo ResolveSingleProfileConstructor(Type profileType, Type configType)
    {
        // Same reasoning as ResolveSingleConstructor's abstract check above - an abstract TProfile
        // with a matching public constructor would otherwise reach ConstructorInfo.Invoke and throw
        // MemberAccessException instead of the documented CompositionException.
        if (profileType.IsAbstract)
        {
            throw new CompositionException(
                $"'{profileType}' is abstract and cannot be used as a profile - it must be a concrete, constructible type.");
        }

        var matching = profileType.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Where(constructor =>
            {
                var parameters = constructor.GetParameters();
                return parameters.Length == 1 && parameters[0].ParameterType == configType;
            })
            .ToArray();

        if (matching.Length != 1)
        {
            throw new CompositionException(
                $"'{profileType}' must have exactly one public constructor accepting a single '{configType}' parameter, but has {matching.Length}.");
        }

        return matching[0];
    }

    private static bool IsNullable(NullabilityInfoContext nullabilityContext, ParameterInfo parameter)
    {
        if (Nullable.GetUnderlyingType(parameter.ParameterType) is not null)
            return true;

        if (parameter.ParameterType.IsValueType)
            return false;

        return nullabilityContext.Create(parameter).ReadState == NullabilityState.Nullable;
    }
}
