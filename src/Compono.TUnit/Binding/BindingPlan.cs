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
    // parameter (ByRef - ParameterMetadata has no ready-made equivalent). Deliberately does not
    // detect more than one Compose-family attribute stacked on the same method (Compono.XunitV3's
    // own such check) - DataGeneratorMetadata/MethodMetadata don't expose the method's own attribute
    // list at generation time the way a raw MethodInfo would; left as a known v1 scope reduction, not
    // a silently dropped requirement.
    private static string? ValidateSignature(MethodMetadata testInformation, ParameterMetadata[] parameters)
    {
        var methodDisplayName = MethodDisplayName(testInformation);

        if (testInformation.GenericTypeCount > 0)
            return $"Compono.TUnit does not support generic test methods ('{methodDisplayName}').";

        foreach (var parameter in parameters)
        {
            if (parameter.ReflectionInfo.ParameterType.IsByRef)
                return $"Compono.TUnit does not support ref/out/in parameters (parameter '{parameter.Name}' on '{methodDisplayName}').";

            if (parameter.IsParams)
                return $"Compono.TUnit does not support params parameters (parameter '{parameter.Name}' on '{methodDisplayName}').";
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
}
