using System.Reflection;
using Compono.TUnit;
using TUnit.Core;
using TUnit.Core.Enums;

namespace Compono.TUnit.AotSmokeTest;

// A real, custom composed type - needs both a generated PlanCache<Widget> entry (ordinary
// plan-generation discovery) and a generated RowInvokerRegistry registration (ADR-0041).
internal sealed class Widget
{
    public Widget(string name) => Name = name;

    public string Name { get; }
}

internal static class SmokeTestMethods
{
    // The real target of this whole harness: a real Compono.TUnit.ComposeAttribute-attributed method
    // parameter list containing both a custom composed type (Widget, needs a real PlanCache<Widget>
    // entry) and a provider-resolved leaf type (string, needs no PlanCache entry at all per
    // ADR-0041 Amendment 2 - the exact gap the amendment closed) - both need a real
    // RowInvokerRegistry registration to dispatch through CompositionRow.Resolve<T>() under Native
    // AOT with no MakeGenericMethod anywhere. Unlike test/Compono.AotSmokeTest (which stands in for
    // Compono.XunitV3.ComposeAttribute and dispatches through RowInvokerRegistry manually, proving
    // only the shared mechanism in isolation), this harness drives the *real*
    // Compono.TUnit.ComposeAttribute.GetDataRowsAsync - proving BindingPlan.Build and
    // Compono.TUnit.Binding.RowInvokers.Build themselves survive Native AOT, not just the registry
    // they call into.
    [Compose]
    public static void Handle(Widget widget, string leaf)
    {
    }
}

internal static class Program
{
    private static async Task<int> Main()
    {
        try
        {
            var method = typeof(SmokeTestMethods).GetMethod(nameof(SmokeTestMethods.Handle))!;
            var attribute = new ComposeAttribute();
            var metadata = CreateDataGeneratorMetadata(method);

            var factories = new List<Func<Task<object?[]?>>>();
            await foreach (var factory in attribute.GetDataRowsAsync(metadata))
                factories.Add(factory);

            if (factories.Count != 1)
                throw new InvalidOperationException($"Expected exactly one data row, got {factories.Count}.");

            var data = await factories[0]();

            if (data is not [Widget { Name.Length: > 0 } widget, string { Length: > 0 } leaf])
                throw new InvalidOperationException($"Unexpected composed row: {(data is null ? "null" : string.Join(", ", data))}");

            Console.WriteLine($"PASS: Compono.TUnit.ComposeAttribute dispatch survived Native AOT - Widget.Name='{widget.Name}', leaf='{leaf}'.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAIL: {ex}");
            return 1;
        }
    }

    // Hand-builds a real DataGeneratorMetadata/MethodMetadata from a real MethodInfo via reflection -
    // this harness has no TUnit source-generator wiring (a plain PackageReference doesn't produce
    // TUnit's own generated MethodMetadata), so it needs the same reflection-based construction
    // Compono.TUnit.Tests' DataGeneratorMetadataTestFactory/MethodMetadataTestFactory use for their
    // own unit tests, inlined here rather than shared since this project has no reference to that
    // test-only fixture assembly.
    private static DataGeneratorMetadata CreateDataGeneratorMetadata(MethodInfo method)
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

        var testInformation = MethodMetadataFactory.Create(
            method.Name,
            declaringType,
            method.ReturnType,
            classMetadata,
            method.GetGenericArguments().Length,
            parameters);

        return new DataGeneratorMetadata
        {
            TestBuilderContext = new TestBuilderContextAccessor(new TestBuilderContext { TestMetadata = testInformation }),
            MembersToGenerate = [],
            TestInformation = testInformation,
            Type = DataGeneratorType.TestParameters,
            TestSessionId = "aot-smoke-test-session",
            TestClassInstance = null,
            ClassInstanceArguments = null,
        };
    }

    private static bool IsNullable(NullabilityInfoContext context, ParameterInfo parameter)
    {
        if (parameter.ParameterType.IsValueType)
            return Nullable.GetUnderlyingType(parameter.ParameterType) is not null;

        var info = context.Create(parameter);
        return info.ReadState == NullabilityState.Nullable;
    }
}
