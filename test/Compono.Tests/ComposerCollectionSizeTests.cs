namespace Compono.Tests;

/// <summary>
/// Exercises Milestone 3 Phase 3's collection-size configuration surface -
/// <see cref="CompositionBuilder.WithCollectionSize(int)"/> (global), the member-scoped
/// <c>.For&lt;T&gt;().Member(x => x.Y).WithCollectionSize(int)</c> override, and
/// <see cref="ICompositionContext.ResolveCollectionSize"/> - through the real
/// <see cref="Composer.Create(Action{CompositionBuilder})"/> path. See
/// <c>docs/adr/0020-composition-configuration-rules.md</c>.
/// </summary>
public sealed class ComposerCollectionSizeTests
{
    [Fact]
    public void WithCollectionSize_CalledTwice_ThrowsWithOneDuplicateConfigurationOptionErrorNamingBothSources()
    {
        var act = () => Composer.Create(builder => builder.WithCollectionSize(2).WithCollectionSize(5));

        var exception = act.Should().Throw<CompositionConfigurationException>().Which;
        exception.Errors.Should().ContainSingle();
        var error = exception.Errors[0].Should().BeOfType<CompositionConfigurationError.DuplicateConfigurationOption>().Which;
        error.OptionName.Should().Be("WithCollectionSize");
        error.Sources.Should().HaveCount(2);
    }

    [Fact]
    public void WithCollectionSize_Negative_ThrowsArgumentOutOfRangeException_ImmediatelyAtTheCallSite()
    {
        var act = () => new CompositionBuilder().WithCollectionSize(-1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void MemberScopedWithCollectionSize_Negative_ThrowsArgumentOutOfRangeException_ImmediatelyAtTheCallSite()
    {
        var act = () => new CompositionBuilder().For<Wrapper>().Member(x => x.ItemsA).WithCollectionSize(-1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void MemberScopedWithCollectionSize_CalledTwiceForTheSameMember_ThrowsWithOneDuplicateCollectionSizeOverrideErrorNamingBothSources()
    {
        var act = () => Composer.Create(builder => builder
            .For<Wrapper>().Member(x => x.ItemsA).WithCollectionSize(3)
            .For<Wrapper>().Member(x => x.ItemsA).WithCollectionSize(9));

        var exception = act.Should().Throw<CompositionConfigurationException>().Which;
        exception.Errors.Should().ContainSingle();
        var error = exception.Errors[0].Should().BeOfType<CompositionConfigurationError.DuplicateCollectionSizeOverride>().Which;
        error.DeclaringType.Should().Be(typeof(Wrapper));
        error.MemberName.Should().Be("ItemsA");
        error.Sources.Should().HaveCount(2);
    }

    [Fact]
    public void NoConfiguration_FallsBackToTheBuiltInSizeOfThree()
    {
        CollectionPlanCache<List<long>>.Instance = new SizeProbeListPlan();

        try
        {
            var composer = Composer.Create();

            var result = composer.Create<List<long>>();

            result.Should().HaveCount(3);
        }
        finally
        {
            CollectionPlanCache<List<long>>.Instance = null;
        }
    }

    [Fact]
    public void GlobalDefault_ChangesTheCollectionSize_ForARootLevelCollection()
    {
        CollectionPlanCache<List<long>>.Instance = new SizeProbeListPlan();

        try
        {
            var composer = Composer.Create(builder => builder.WithCollectionSize(7));

            var result = composer.Create<List<long>>();

            result.Should().HaveCount(7);
        }
        finally
        {
            CollectionPlanCache<List<long>>.Instance = null;
        }
    }

    [Fact]
    public void MemberScopedOverride_OverridesTheGlobalDefault_ForThatMemberOnly()
    {
        PlanCache<Wrapper>.Instance = new WrapperPlan();
        CollectionPlanCache<List<long>>.Instance = new SizeProbeListPlan();

        try
        {
            var composer = Composer.Create(builder => builder
                .WithCollectionSize(3)
                .For<Wrapper>().Member(x => x.ItemsA).WithCollectionSize(9));

            var wrapper = composer.Create<Wrapper>();

            wrapper.ItemsA.Should().HaveCount(9);
            wrapper.ItemsB.Should().HaveCount(3);
        }
        finally
        {
            PlanCache<Wrapper>.Instance = null;
            CollectionPlanCache<List<long>>.Instance = null;
        }
    }

    [Fact]
    public void MemberScopedOverride_DoesNotApply_WhenARequestForADifferentlyTypedMemberSharesTheSameName()
    {
        // Codex review: Conflicted legally has a property (`Value`, type object) and an unrelated
        // constructor parameter (`Value`, type List<long>) sharing the exact same case-sensitive name -
        // .Member(x => x.Value) captures the property (TMember = object), but the generated request for
        // the constructor parameter has RequestedType = List<long>. Before this fix,
        // CollectionSizePolicy matched on (DeclaringType, MemberName) alone and would have applied the
        // property's override to the unrelated collection parameter too.
        PlanCache<Conflicted>.Instance = new ConflictedPlan();
        CollectionPlanCache<List<long>>.Instance = new SizeProbeListPlan();

        try
        {
            var composer = Composer.Create(builder => builder
                .WithCollectionSize(3)
                .For<Conflicted>().Member(x => x.Value).WithCollectionSize(9));

            var result = composer.Create<Conflicted>();

            // The override targets the unrelated `object Value` property - the real List<long> "Value"
            // constructor parameter must fall through to the global default (3), never inherit the
            // property's override (9).
            result.CtorValue.Should().HaveCount(3);
        }
        finally
        {
            PlanCache<Conflicted>.Instance = null;
            CollectionPlanCache<List<long>>.Instance = null;
        }
    }

    private static CompositionRequestDescriptor Descriptor(int ordinal, string name) =>
        new(CompositionRequestKind.ConstructorParameter, ordinal, name, typeof(Wrapper), Nullability.NotNullable);

    private sealed record Wrapper(List<long> ItemsA, List<long> ItemsB);

    // A legal, if unusual, shape: a constructor parameter and a property sharing the exact same
    // case-sensitive name ("Value") but not a type - two entirely separate CLR symbols.
    private sealed class Conflicted
    {
        public Conflicted(List<long> Value) => CtorValue = Value;

        public List<long> CtorValue { get; }

        public object Value { get; init; } = new();
    }

    private sealed class ConflictedPlan : ICompositionPlan<Conflicted>
    {
        public Conflicted Compose(ICompositionContext context) =>
            new(context.Resolve<List<long>>(new CompositionRequestDescriptor(
                CompositionRequestKind.ConstructorParameter, 0, "Value", typeof(Conflicted), Nullability.NotNullable)));
    }

    private sealed class WrapperPlan : ICompositionPlan<Wrapper>
    {
        public Wrapper Compose(ICompositionContext context) =>
            new(
                context.Resolve<List<long>>(Descriptor(0, "ItemsA")),
                context.Resolve<List<long>>(Descriptor(1, "ItemsB")));
    }

    // Simulates the shape a real generated collection plan produces - reads context.ResolveCollectionSize()
    // instead of a hardcoded literal, per ADR-0020.
    private sealed class SizeProbeListPlan : ICompositionPlan<List<long>>
    {
        public List<long> Compose(ICompositionContext context)
        {
            var size = context.ResolveCollectionSize();
            var result = new List<long>(size);
            for (var i = 0; i < size; i++)
                result.Add(i);
            return result;
        }
    }
}
