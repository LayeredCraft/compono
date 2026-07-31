using Compono.Xunit.Binding;

namespace Compono.Xunit.Tests;

public sealed class RowInvokersTests
{
    [Fact]
    public void Resolve_ComposesAValue_ForAReferenceTypedParameter()
    {
        var invokers = RowInvokers.Build(typeof(string));
        var row = Composer.Create().CreateRow(typeof(RowInvokersTests));
        var descriptor = Descriptor(typeof(RowInvokersTests));

        var value = invokers.Resolve(row, descriptor);

        value.Should().BeOfType<string>();
    }

    [Fact]
    public void Resolve_ComposesAValue_ForAValueTypedParameter()
    {
        var invokers = RowInvokers.Build(typeof(int));
        var row = Composer.Create().CreateRow(typeof(RowInvokersTests));
        var descriptor = Descriptor(typeof(RowInvokersTests));

        var value = invokers.Resolve(row, descriptor);

        value.Should().BeOfType<int>();
    }

    [Fact]
    public void ResolveShared_MakesTheValueVisible_ToALaterOrdinaryResolve()
    {
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
        var invokers = RowInvokers.Build(typeof(int));
        var row = Composer.Create().CreateRow(typeof(RowInvokersTests));
        var sharedDescriptor = Descriptor(typeof(RowInvokersTests), ordinal: 0);
        var laterDescriptor = Descriptor(typeof(RowInvokersTests), ordinal: 1);

        invokers.ShareExplicit(row, sharedDescriptor, 42);
        var later = invokers.Resolve(row, laterDescriptor);

        later.Should().Be(42);
    }

    private static CompositionRequestDescriptor Descriptor(Type declaringType, int ordinal = 0) =>
        new(CompositionRequestKind.TestParameter, ordinal, "value", declaringType, Nullability.NotNullable);
}
