namespace Compono.Tests;

public sealed class RowInvokerRegistryTests
{
    [Fact]
    public void TryGet_ReturnsFalse_WhenNoEntryIsRegisteredForTheType()
    {
        var found = RowInvokerRegistry.TryGet(typeof(RowInvokerRegistryTests), out _, out _, out _);

        found.Should().BeFalse();
    }

    [Fact]
    public void Register_ThenTryGet_ReturnsWorkingDispatchDelegates()
    {
        RowInvokerRegistry.Register(
            typeof(int),
            static (row, in descriptor) => row.Resolve<int>(descriptor),
            static (row, in descriptor) => row.ResolveShared<int>(descriptor),
            static (row, in descriptor, value) => row.ShareExplicit(descriptor, (int)value!));
        var row = Composer.Create().CreateRow(typeof(RowInvokerRegistryTests));

        var found = RowInvokerRegistry.TryGet(typeof(int), out var resolve, out _, out _);
        var value = resolve(row, Descriptor(typeof(RowInvokerRegistryTests)));

        found.Should().BeTrue();
        value.Should().BeOfType<int>();
    }

    [Fact]
    public void Register_IsIdempotent_WhenTwoSimulatedConsumingAssembliesRegisterTheSameType()
    {
        // Two "consuming assemblies" each running their own generated module initializer for the
        // same parameter type - functionally interchangeable delegate sets (same shape, both close
        // over the same T), exactly what makes GetOrAdd-style idempotent registration safe here
        // (ADR-0041 Amendment 3): there's no "which one is correct" ambiguity to resolve, unlike
        // PlanCache<T>'s own genuine cross-assembly collision.
        var firstAssemblyRegisters = () => RowInvokerRegistry.Register(
            typeof(string),
            static (row, in descriptor) => row.Resolve<string>(descriptor),
            static (row, in descriptor) => row.ResolveShared<string>(descriptor),
            static (row, in descriptor, value) => row.ShareExplicit(descriptor, (string)value!));

        var secondAssemblyRegisters = () => RowInvokerRegistry.Register(
            typeof(string),
            static (row, in descriptor) => row.Resolve<string>(descriptor),
            static (row, in descriptor) => row.ResolveShared<string>(descriptor),
            static (row, in descriptor, value) => row.ShareExplicit(descriptor, (string)value!));

        firstAssemblyRegisters();
        secondAssemblyRegisters();
        var row = Composer.Create().CreateRow(typeof(RowInvokerRegistryTests));

        var found = RowInvokerRegistry.TryGet(typeof(string), out var resolve, out _, out _);
        var value = resolve(row, Descriptor(typeof(RowInvokerRegistryTests)));

        firstAssemblyRegisters.Should().NotThrow();
        secondAssemblyRegisters.Should().NotThrow();
        found.Should().BeTrue();
        value.Should().BeOfType<string>();
    }

    private static CompositionRequestDescriptor Descriptor(Type declaringType) =>
        new(CompositionRequestKind.TestParameter, 0, "value", declaringType, Nullability.NotNullable);
}
