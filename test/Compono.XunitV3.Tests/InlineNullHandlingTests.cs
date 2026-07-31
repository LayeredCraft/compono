using Compono.XunitV3.Tests.Fixtures;
using Xunit.Sdk;

namespace Compono.XunitV3.Tests;

// The four null/non-null-parameter combinations Phase 3's plan requires, each covered for both an
// ordinary parameter and an inline-[Shared] parameter - proving rejection happens before
// ShareExplicit's invoker is ever reached (a rejected inline value fails GetData's own
// pre-composition validation loop, which runs before either binding loop touches a parameter).
public sealed class InlineNullHandlingTests
{
    [Fact]
    public async Task GetData_AcceptsANullInlineValue_ForANullableReferenceParameter()
    {
        var attribute = new ComposeAttribute((string?)null);
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithNullableReferenceParameter))!;

        var rows = await attribute.GetData(method, new DisposalTracker());

        rows.Single().GetData()[0].Should().BeNull();
    }

    [Fact]
    public async Task GetData_AcceptsANullInlineValue_ForANullableValueParameter()
    {
        var attribute = new ComposeAttribute((int?)null);
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithNullableValueParameter))!;

        var rows = await attribute.GetData(method, new DisposalTracker());

        rows.Single().GetData()[0].Should().BeNull();
    }

    [Fact]
    public async Task GetData_RejectsANullInlineValue_ForANonNullableReferenceParameter()
    {
        var attribute = new ComposeAttribute((object?)null);
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithNonNullableReferenceParameter))!;

        var act = () => attribute.GetData(method, new DisposalTracker()).AsTask();

        await act.Should().ThrowAsync<CompositionException>()
            .WithMessage("*value*");
    }

    [Fact]
    public async Task GetData_RejectsANullInlineValue_ForANonNullableValueParameter()
    {
        var attribute = new ComposeAttribute((object?)null);
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithNonNullableValueParameter))!;

        var act = () => attribute.GetData(method, new DisposalTracker()).AsTask();

        await act.Should().ThrowAsync<CompositionException>()
            .WithMessage("*value*");
    }

    [Fact]
    public async Task GetData_AcceptsANullInlineValue_ForASharedNullableReferenceParameter()
    {
        var attribute = new ComposeAttribute((string?)null);
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithSharedNullableReferenceParameter))!;

        var rows = await attribute.GetData(method, new DisposalTracker());

        rows.Single().GetData()[0].Should().BeNull();
    }

    [Fact]
    public async Task GetData_AcceptsANullInlineValue_ForASharedNullableValueParameter()
    {
        var attribute = new ComposeAttribute((int?)null);
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithSharedNullableValueParameter))!;

        var rows = await attribute.GetData(method, new DisposalTracker());

        rows.Single().GetData()[0].Should().BeNull();
    }

    [Fact]
    public async Task GetData_RejectsANullInlineValue_ForASharedNonNullableReferenceParameter()
    {
        var attribute = new ComposeAttribute((object?)null);
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithSharedNonNullableReferenceParameter))!;

        var act = () => attribute.GetData(method, new DisposalTracker()).AsTask();

        await act.Should().ThrowAsync<CompositionException>()
            .WithMessage("*value*");
    }

    [Fact]
    public async Task GetData_RejectsANullInlineValue_ForASharedNonNullableValueParameter()
    {
        var attribute = new ComposeAttribute((object?)null);
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithSharedNonNullableValueParameter))!;

        var act = () => attribute.GetData(method, new DisposalTracker()).AsTask();

        await act.Should().ThrowAsync<CompositionException>()
            .WithMessage("*value*");
    }

    [Fact]
    public async Task GetData_AcceptsANonNullInlineValue_ForANullableValueParameter()
    {
        // The regression test for the boxed-T-vs-boxed-Nullable<T> CLR bug: a non-null int arrives
        // boxed as a boxed int, not a boxed int? - without the Nullable.GetUnderlyingType unwrap this
        // is wrongly rejected against the raw int? parameter type.
        var attribute = new ComposeAttribute(42);
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.WithNullableValueParameter))!;

        var rows = await attribute.GetData(method, new DisposalTracker());

        rows.Single().GetData()[0].Should().Be(42);
    }
}
