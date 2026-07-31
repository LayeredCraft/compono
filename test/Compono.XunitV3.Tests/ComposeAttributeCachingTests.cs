using Compono.XunitV3.Binding;
using Compono.XunitV3.Tests.Fixtures;
using Xunit.Sdk;

namespace Compono.XunitV3.Tests;

public sealed class ComposeAttributeCachingTests
{
    [Fact]
    public void GetComposer_ReturnsTheSameInstance_AcrossRepeatedCalls()
    {
        var attribute = new ComposeAttribute();

        var first = attribute.GetComposer();
        var second = attribute.GetComposer();

        second.Should().BeSameAs(first);
    }

    [Fact]
    public void GetComposer_AppliesTheConfiguredProfile()
    {
        var attribute = new ComposeAttribute<SampleTestMethods.TestProfile>();

        var composer = attribute.GetComposer();
        var row = composer.CreateRow(typeof(ComposeAttributeCachingTests));
        var descriptor = new CompositionRequestDescriptor(
            CompositionRequestKind.TestParameter, ordinal: 0, name: "value", declaringType: typeof(ComposeAttributeCachingTests), Nullability.NotNullable);

        row.Resolve<string>(descriptor).Should().Be("from-profile");
    }

    [Fact]
    public void GetComposer_UsesTheConfiguredSeed_ForEveryRow()
    {
        var attribute = new ComposeAttribute { Seed = 8492173 };

        var row = attribute.GetComposer().CreateRow(typeof(ComposeAttributeCachingTests));

        row.Seed.Should().Be(8492173);
    }

    [Fact]
    public void Seed_DefaultsToZero_WhenNeverSet()
    {
        var attribute = new ComposeAttribute();

        attribute.Seed.Should().Be(0);
    }

    [Fact]
    public void InlineValues_SingleNullArgument_TreatedAsOneSuppliedNullValue()
    {
        // [Compose(null)] binds in the C# compiler's non-expanded params form - the constructor
        // receives a null array, not a one-element array containing null - so the constructor must
        // recover the author's actual intent (a single supplied null value) instead of throwing on a
        // null inlineValues array (PR #23 review). The null-forgiving operator here simulates exactly
        // that compiler-produced null array from ordinary C# code, which can't itself pass a null
        // array literal to a `params object?[]` parameter without it.
        var attribute = new ComposeAttribute((object?[])null!);

        attribute.InlineValues.Should().Equal([null]);
    }

    [Fact]
    public void InlineValues_SingleReferenceArrayArgument_TreatedAsOneSuppliedArrayValue()
    {
        // [Compose(new string[] { "a", "b" })] also binds in the C# compiler's non-expanded params
        // form - string[] is covariantly convertible to object?[], so the constructor receives that
        // exact string[] instance (runtime type string[], not object[]) rather than a 2-element
        // object?[] (PR #24 review). Must be recovered the same way the single-null case is: as one
        // supplied array value, not two separate inline values.
        var tags = new[] { "a", "b" };
        var attribute = new ComposeAttribute(tags);

        attribute.InlineValues.Should().Equal([tags]);
    }

    [Fact]
    public void EnsureBindingPlan_ReturnsTheSameInstance_AcrossRepeatedCalls()
    {
        var attribute = new ComposeAttribute();
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.Simple))!;

        var first = attribute.EnsureBindingPlan(method);
        var second = attribute.EnsureBindingPlan(method);

        second.Should().BeSameAs(first);
    }

    [Fact]
    public void EnsureBindingPlan_ReusesTheCachedInvokerDelegates_AcrossRepeatedCalls()
    {
        var attribute = new ComposeAttribute();
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.Simple))!;

        var first = attribute.EnsureBindingPlan(method);
        var second = attribute.EnsureBindingPlan(method);

        second.Parameters[0].ResolveInvoker.Should().BeSameAs(first.Parameters[0].ResolveInvoker);
        second.Parameters[0].ResolveSharedInvoker.Should().BeSameAs(first.Parameters[0].ResolveSharedInvoker);
        second.Parameters[0].ShareExplicitInvoker.Should().BeSameAs(first.Parameters[0].ShareExplicitInvoker);
    }

    [Fact]
    public async Task GetData_NeverRebuildsTheBindingPlan_AcrossRepeatedCalls()
    {
        var attribute = new ComposeAttribute();
        var method = typeof(SampleTestMethods).GetMethod(nameof(SampleTestMethods.Simple))!;
        var tracker = new DisposalTracker();
        var plansObservedAfterEachCall = new List<BindingPlan>();

        for (var i = 0; i < 5; i++)
        {
            await attribute.GetData(method, tracker);
            plansObservedAfterEachCall.Add(attribute.EnsureBindingPlan(method));
        }

        plansObservedAfterEachCall.Should().OnlyContain(plan => ReferenceEquals(plan, plansObservedAfterEachCall[0]));
    }
}
