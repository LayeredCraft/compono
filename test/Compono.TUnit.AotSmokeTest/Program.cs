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

// PLAN-0040 Phase 1's own Native AOT gate (ADR-0041 Amendment 1): ConfigProfileBinder's
// ConstructorInfo.Invoke-based TConfig/TProfile construction needs the same real publish-and-run
// proof RowInvokerRegistry dispatch already got in Phase 0 - "likely AOT-safe because it's a
// non-generic, already-known Type" isn't good enough on its own.
internal sealed record ProfileConfig(int Seed);

internal sealed class ConfiguredProfile : ICompositionProfile
{
    private readonly ProfileConfig _config;

    public ConfiguredProfile(ProfileConfig config) => _config = config;

    public void Configure(CompositionBuilder builder) => builder.WithSeed(_config.Seed);
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

    // Exercises ComposeAttribute<TProfile, TConfig>.ApplyProfile -> ConfigProfileBinder.BindConfig/
    // BuildProfile, both ConstructorInfo.Invoke-based - the Phase 1 AOT gate this harness exists to
    // prove.
    [Compose<ConfiguredProfile, ProfileConfig>(12345)]
    public static void HandleWithConfiguredProfile(Widget widget, string leaf)
    {
    }
}

internal static class Program
{
    private static async Task<int> Main()
    {
        try
        {
            await RunRow(
                typeof(SmokeTestMethods).GetMethod(nameof(SmokeTestMethods.Handle))!,
                new ComposeAttribute(),
                "Compono.TUnit.ComposeAttribute");

            await RunRow(
                typeof(SmokeTestMethods).GetMethod(nameof(SmokeTestMethods.HandleWithConfiguredProfile))!,
                new ComposeAttribute<ConfiguredProfile, ProfileConfig>(12345),
                "Compono.TUnit.ComposeAttribute<TProfile, TConfig> (ConfigProfileBinder)");

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAIL: {ex}");
            return 1;
        }
    }

    private static async Task RunRow(MethodInfo method, ComposeAttribute attribute, string label)
    {
        var metadata = CreateDataGeneratorMetadata(method);

        var factories = new List<Func<Task<object?[]?>>>();
        await foreach (var factory in attribute.GetDataRowsAsync(metadata))
            factories.Add(factory);

        if (factories.Count != 1)
            throw new InvalidOperationException($"Expected exactly one data row, got {factories.Count}.");

        var data = await factories[0]();

        if (data is not [Widget { Name.Length: > 0 } widget, string { Length: > 0 } leaf])
            throw new InvalidOperationException($"Unexpected composed row: {(data is null ? "null" : string.Join(", ", data))}");

        Console.WriteLine($"PASS: {label} dispatch survived Native AOT - Widget.Name='{widget.Name}', leaf='{leaf}'.");
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
