using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace Compono.NUnit.Binding;

/// <summary>
/// Resolves and invokes the single constructors <see cref="ComposeAttribute{TProfile, TConfig}"/> uses
/// to build a <c>TConfig</c> from profile configuration arguments and a <c>TProfile</c> from that
/// <c>TConfig</c> - a package-local port of <c>Compono.XunitV3</c>'s own binder, matching ADR-0059's
/// "same Compono-facing attribute family and semantics" decision for this form. Deliberately narrow
/// and deterministic: no "best constructor match" heuristic exists anywhere here - an unsupported or
/// ambiguous constructor shape is always a clear, named <see cref="CompositionException"/>, never a
/// guessed resolution.
/// </summary>
/// <remarks>
/// Every method here is reflection - <see cref="Type.GetConstructors()"/> and
/// <see cref="ConstructorInfo.Invoke(object?[])"/>, not the cached dispatch shape
/// <see cref="RowInvokers"/> uses. <c>TConfig</c>/<c>TProfile</c> need no equivalent: they're already
/// compile-time-closed generic arguments on <see cref="ComposeAttribute{TProfile, TConfig}"/> itself,
/// and this binder's methods are only ever called from
/// <see cref="ComposeAttribute{TProfile, TConfig}.ApplyProfile"/> - itself only ever invoked once per
/// attribute instance, from inside the base <see cref="ComposeAttribute"/>'s existing
/// <c>Lazy&lt;Composer&gt;</c>-backed caching, never the repeated per-row <c>BuildFrom</c> path.
/// </remarks>
internal static class ConfigProfileBinder
{
    /// <summary>
    /// Binds <paramref name="configArguments"/> positionally to <paramref name="configType"/>'s single
    /// public constructor and invokes it.
    /// </summary>
    /// <exception cref="CompositionException">
    /// <paramref name="configType"/> does not have exactly one public constructor; the supplied
    /// argument count doesn't match that constructor's parameter count; a supplied argument is
    /// <see langword="null"/> for a non-nullable parameter; or a supplied argument's type isn't
    /// assignable to its parameter's type.
    /// </exception>
    public static object BindConfig(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type configType,
        IReadOnlyList<object?> configArguments)
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
            var nullability = IsNullable(nullabilityContext, parameter) ? Nullability.Nullable : Nullability.NotNullable;

            switch (PositionalArgumentBinder.Validate(parameter.ParameterType, nullability, value))
            {
                case PositionalArgumentValidation.NullNotAllowed:
                    throw new CompositionException(
                        $"Profile configuration argument for parameter '{parameter.Name}' of '{configType}' is null, but the parameter is not nullable.");

                case PositionalArgumentValidation.TypeMismatch:
                    throw new CompositionException(
                        $"Profile configuration argument for parameter '{parameter.Name}' of '{configType}' has type '{value!.GetType()}', which is not assignable to '{parameter.ParameterType}'.");
            }

            arguments[i] = value;
        }

        return Invoke(constructor, arguments);
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
    public static TProfile BuildProfile<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TProfile, TConfig>(object config)
        where TProfile : ICompositionProfile
    {
        var constructor = ResolveSingleProfileConstructor(typeof(TProfile), typeof(TConfig));

        return (TProfile)Invoke(constructor, [config]);
    }

    // ConstructorInfo.Invoke wraps any exception the constructor body itself throws in a
    // TargetInvocationException - without unwrapping it here, ApplyProfile's own
    // catch (CompositionException) could never observe it. ExceptionDispatchInfo.Capture(...).Throw()
    // re-throws the inner exception with its original stack trace preserved.
    private static object Invoke(ConstructorInfo constructor, object?[] arguments)
    {
        try
        {
            return constructor.Invoke(arguments);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw; // Unreachable - Throw() always throws; satisfies every code path returning a value.
        }
    }

    private static ConstructorInfo ResolveSingleConstructor(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type type)
    {
        // An abstract type can still declare a public constructor (invoked only by a derived type's
        // own constructor chain) - reject explicitly, before the constructor count check, so an
        // abstract TConfig fails with the same named diagnostic shape as every other unsupported
        // shape here.
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

    private static ConstructorInfo ResolveSingleProfileConstructor(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type profileType,
        Type configType)
    {
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
