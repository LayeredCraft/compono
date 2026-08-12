using System.Reflection;
using Compono.XunitV3.Binding;

namespace Compono.XunitV3.Tests;

public sealed class RowInvokersTests
{
    [Fact]
    public void Resolve_ComposesAValue_ForAReferenceTypedParameter()
    {
        Register(typeof(string), static (row, in descriptor) => row.Resolve<string>(descriptor),
            static (row, in descriptor) => row.ResolveShared<string>(descriptor),
            static (row, in descriptor, value) => row.ShareExplicit(descriptor, (string)value!));
        var invokers = RowInvokers.Build(typeof(string));
        var row = Composer.Create().CreateRow(typeof(RowInvokersTests));
        var descriptor = Descriptor(typeof(RowInvokersTests));

        var value = invokers.Resolve(row, descriptor);

        value.Should().BeOfType<string>();
    }

    [Fact]
    public void Resolve_ComposesAValue_ForAValueTypedParameter()
    {
        Register(typeof(int), static (row, in descriptor) => row.Resolve<int>(descriptor),
            static (row, in descriptor) => row.ResolveShared<int>(descriptor),
            static (row, in descriptor, value) => row.ShareExplicit(descriptor, (int)value!));
        var invokers = RowInvokers.Build(typeof(int));
        var row = Composer.Create().CreateRow(typeof(RowInvokersTests));
        var descriptor = Descriptor(typeof(RowInvokersTests));

        var value = invokers.Resolve(row, descriptor);

        value.Should().BeOfType<int>();
    }

    [Fact]
    public void ResolveShared_MakesTheValueVisible_ToALaterOrdinaryResolve()
    {
        Register(typeof(string), static (row, in descriptor) => row.Resolve<string>(descriptor),
            static (row, in descriptor) => row.ResolveShared<string>(descriptor),
            static (row, in descriptor, value) => row.ShareExplicit(descriptor, (string)value!));
        var invokers = RowInvokers.Build(typeof(string));
        var row = Composer.Create().CreateRow(typeof(RowInvokersTests));
        var sharedDescriptor = Descriptor(typeof(RowInvokersTests), ordinal: 0);
        var laterDescriptor = Descriptor(typeof(RowInvokersTests), ordinal: 1);

        var shared = invokers.ResolveShared(row, sharedDescriptor);
        var later = invokers.Resolve(row, laterDescriptor);

        later.Should().Be(shared);
    }

    [Fact]
    public void ShareExplicit_StoresTheValue_ForAValueTypedParameter()
    {
        Register(typeof(int), static (row, in descriptor) => row.Resolve<int>(descriptor),
            static (row, in descriptor) => row.ResolveShared<int>(descriptor),
            static (row, in descriptor, value) => row.ShareExplicit(descriptor, (int)value!));
        var invokers = RowInvokers.Build(typeof(int));
        var row = Composer.Create().CreateRow(typeof(RowInvokersTests));
        var sharedDescriptor = Descriptor(typeof(RowInvokersTests), ordinal: 0);
        var laterDescriptor = Descriptor(typeof(RowInvokersTests), ordinal: 1);

        invokers.ShareExplicit(row, sharedDescriptor, 42);
        var later = invokers.Resolve(row, laterDescriptor);

        later.Should().Be(42);
    }

    [Fact]
    public void Build_ThrowsACompositionException_WhenNoDispatchIsRegisteredForTheType()
    {
        // No RowInvokerRegistry.Register call for this marker type anywhere - proves Build reports a
        // clear, diagnosable failure instead of silently falling back to reflection (the old
        // MakeGenericMethod-based design's only option) when the registry has nothing for a type.
        var act = () => RowInvokers.Build(typeof(UnregisteredMarker));

        act.Should().Throw<CompositionException>()
            .WithMessage("*No row-binding dispatch is registered*");
    }

    [Fact]
    public void RowInvokers_CachesNoMethodInfoFields_ProvingNoReflectionBasedDispatchPathRemains()
    {
        // The old MakeGenericMethod-based design cached three static MethodInfo fields
        // (ResolveMethod/ResolveSharedMethod/ShareExplicitMethod) to close per parameter type -
        // RowInvokers now does an ordinary Type-keyed dictionary lookup via RowInvokerRegistry, so no
        // reflection metadata is cached (or needed) here at all.
        var fields = typeof(RowInvokers).GetFields(BindingFlags.NonPublic | BindingFlags.Static);

        fields.Should().BeEmpty();
    }

    private static void Register(Type type, ResolveInvoker resolve, ResolveSharedInvoker resolveShared, ShareExplicitInvoker shareExplicit) =>
        RowInvokerRegistry.Register(type, resolve, resolveShared, shareExplicit);

    private static CompositionRequestDescriptor Descriptor(Type declaringType, int ordinal = 0) =>
        new(CompositionRequestKind.TestParameter, ordinal, "value", declaringType, Nullability.NotNullable);

    private sealed class UnregisteredMarker;
}
