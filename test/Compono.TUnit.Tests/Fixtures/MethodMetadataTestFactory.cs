using System.Reflection;

namespace Compono.TUnit.Tests.Fixtures;

// Hand-builds a real MethodMetadata from a real MethodInfo via reflection, for BindingPlan.Build
// unit tests. Compono.TUnit.Tests has no source generator wiring at all (a plain ProjectReference
// doesn't propagate Compono.Generators as an analyzer - testing.md's established constraint), and
// TUnit's own generated MethodMetadata is only ever produced by TUnit's own source generator, never
// hand-constructible from a public API meant for test authors. This mirrors the reflection-based
// construction TUnit.Core.PropertyInjection.ClassMetadataHelper (internal to TUnit.Core) uses for
// its own non-source-generated fallback path, built from MethodMetadataFactory/ClassMetadata's own
// public factories.
internal static class MethodMetadataTestFactory
{
    public static MethodMetadata Create(MethodInfo method)
    {
        var declaringType = method.DeclaringType!;
        var nullabilityContext = new NullabilityInfoContext();

        var parameters = method.GetParameters()
            .Select((parameter, index) => new ParameterMetadata(parameter.ParameterType)
            {
                Name = parameter.Name ?? $"param{index}",
                TypeInfo = new ConcreteType(parameter.ParameterType),
                ReflectionInfo = parameter,
                Position = index,
                IsNullable = IsNullable(nullabilityContext, parameter),
            })
            .ToArray();

        var classMetadata = new ClassMetadata
        {
            Type = declaringType,
            TypeInfo = new ConcreteType(declaringType),
            Name = declaringType.Name,
            Namespace = declaringType.Namespace,
            Assembly = AssemblyMetadata.GetOrAdd(declaringType.Assembly.FullName ?? declaringType.Assembly.GetName().Name!, declaringType.Assembly.FullName ?? declaringType.Assembly.GetName().Name!),
            Parameters = [],
            Properties = [],
            Parent = null,
        };

        return MethodMetadataFactory.Create(
            method.Name,
            declaringType,
            method.ReturnType,
            classMetadata,
            method.GetGenericArguments().Length,
            parameters);
    }

    private static bool IsNullable(NullabilityInfoContext context, ParameterInfo parameter)
    {
        if (parameter.ParameterType.IsValueType)
            return Nullable.GetUnderlyingType(parameter.ParameterType) is not null;

        var info = context.Create(parameter);
        return info.ReadState == NullabilityState.Nullable;
    }
}
