namespace Compono.Tests;

/// <summary>
/// Exercises Milestone 3 Phase 3's public type/member configuration-rule surface -
/// <see cref="CompositionBuilder.For{T}"/>, <see cref="CompositionTypeRuleBuilder{T}"/>, and
/// <see cref="CompositionMemberRuleBuilder{TParent, TMember}"/> - through the real
/// <see cref="Composer.Create(Action{CompositionBuilder})"/> path. See
/// <c>docs/adr/0020-composition-configuration-rules.md</c>.
/// </summary>
public sealed class ComposerConfigurationRuleTests
{
    [Fact]
    public void MemberRule_WinsOverTypeRule_ForTheSameEffectiveRequest()
    {
        PlanCache<Customer>.Instance = new CustomerPlan();

        try
        {
            var composer = Composer.Create(builder => builder
                .For<string>().Use("from-type-rule")
                .For<Customer>().Member(x => x.Email).Use("from-member-rule"));

            var customer = composer.Create<Customer>();

            customer.Email.Should().Be("from-member-rule");
        }
        finally
        {
            PlanCache<Customer>.Instance = null;
        }
    }

    [Fact]
    public void TwoMemberRules_DifferentDeclaringTypes_SameMemberNameAndType_DoNotCollide()
    {
        PlanCache<Customer>.Instance = new CustomerPlan();
        PlanCache<Vendor>.Instance = new VendorPlan();

        try
        {
            var composer = Composer.Create(builder => builder
                .For<Customer>().Member(x => x.Email).Use("customer-email")
                .For<Vendor>().Member(x => x.Email).Use("vendor-email"));

            var customer = composer.Create<Customer>();
            var vendor = composer.Create<Vendor>();

            customer.Email.Should().Be("customer-email");
            vendor.Email.Should().Be("vendor-email");
        }
        finally
        {
            PlanCache<Customer>.Instance = null;
            PlanCache<Vendor>.Instance = null;
        }
    }

    [Fact]
    public void TypeRule_ExactTypeMatchOnly_DoesNotSatisfyAnAssignableConcreteType()
    {
        PlanCache<Holder>.Instance = new HolderPlan();

        try
        {
            var composer = Composer.Create(builder => builder.For<IClock>().Use(_ => new FakeClock()));

            // Holder requests a concrete SystemClock, not IClock - the type rule for IClock must not
            // apply, so this falls through to the "nothing could satisfy this" failure instead.
            var act = () => composer.Create<Holder>();

            act.Should().Throw<CompositionException>().WithMessage("*SystemClock*");
        }
        finally
        {
            PlanCache<Holder>.Instance = null;
        }
    }

    [Fact]
    public void Member_MalformedExpression_ThrowsImmediately_AtTheCallSite()
    {
        var act = () => Composer.Create(builder => builder.For<Customer>().Member(x => x.Email.Length));

        act.Should().Throw<ArgumentException>().WithParameterName("member");
    }

    [Fact]
    public void For_DuplicateTypeRule_ThrowsWithOneDuplicateRuleErrorNamingBothSources()
    {
        var act = () => Composer.Create(builder => builder
            .For<string>().Use("first")
            .For<string>().Use("second"));

        var exception = act.Should().Throw<CompositionConfigurationException>().Which;
        exception.Errors.Should().ContainSingle();
        var error = exception.Errors[0].Should().BeOfType<CompositionConfigurationError.DuplicateRule>().Which;
        error.RuleType.Should().Be(typeof(string));
        error.MemberName.Should().BeNull();
        error.Sources.Should().HaveCount(2);
    }

    [Fact]
    public void Member_DuplicateMemberRule_ThrowsWithOneDuplicateRuleErrorNamingBothSources()
    {
        var act = () => Composer.Create(builder => builder
            .For<Customer>().Member(x => x.Email).Use("first")
            .For<Customer>().Member(x => x.Email).Use("second"));

        var exception = act.Should().Throw<CompositionConfigurationException>().Which;
        exception.Errors.Should().ContainSingle();
        var error = exception.Errors[0].Should().BeOfType<CompositionConfigurationError.DuplicateRule>().Which;
        error.RuleType.Should().Be(typeof(Customer));
        error.MemberName.Should().Be("Email");
        error.Sources.Should().HaveCount(2);
    }

    [Fact]
    public void MemberRuleAndTypeRule_ForTheSameEffectiveRequest_AreNotAConflict()
    {
        PlanCache<Customer>.Instance = new CustomerPlan();

        try
        {
            var act = () => Composer.Create(builder => builder
                .For<string>().Use("from-type-rule")
                .For<Customer>().Member(x => x.Email).Use("from-member-rule"));

            act.Should().NotThrow();
        }
        finally
        {
            PlanCache<Customer>.Instance = null;
        }
    }

    [Fact]
    public async Task SelfReferencingTypeRule_FailsWithADiagnosableException_InsteadOfInvokingItselfForever()
    {
        var composer = Composer.Create(builder => builder.For<Recursive>().Use(ctx => ctx.Resolve<Recursive>()));

        // Same bounded-race shape as ComposerRegistrationTests' identical registration regression -
        // a factory-reentrance guard regression here would recurse to StackOverflowException or hang.
        var task = Task.Run(() => composer.Create<Recursive>());
        var completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)) == task;

        completed.Should().BeTrue("a factory-reentrance regression would hang instead of throwing");
        var rethrow = async () => await task;
        await rethrow.Should().ThrowAsync<CompositionException>().WithMessage("*factory*");
    }

    [Fact]
    public void RuleThatLegitimatelyTerminatesASelfReferencingGraph_Succeeds()
    {
        PlanCache<Node>.Instance = new RecursingNodePlan();

        try
        {
            var composer = Composer.Create(builder => builder.For<Node>().Member(x => x.Child).Use(_ => new Node(null)));

            var act = () => composer.Create<Node>();

            act.Should().NotThrow();
        }
        finally
        {
            PlanCache<Node>.Instance = null;
        }
    }

    private static CompositionRequestDescriptor Descriptor(CompositionRequestKind kind, int ordinal, string name, Type declaringType) =>
        new(kind, ordinal, name, declaringType, Nullability.NotNullable);

    private sealed record Customer(string Email);

    private sealed record Vendor(string Email);

    private sealed record Holder(SystemClock Clock);

    private sealed record Recursive;

    private sealed class Node
    {
        public Node(Node? child) => Child = child;

        public Node? Child { get; }
    }

    private interface IClock;

    private sealed class FakeClock : IClock;

    private sealed class SystemClock : IClock;

    private sealed class CustomerPlan : ICompositionPlan<Customer>
    {
        public Customer Compose(ICompositionContext context) =>
            new(context.Resolve<string>(Descriptor(CompositionRequestKind.ConstructorParameter, 0, "Email", typeof(Customer))));
    }

    private sealed class VendorPlan : ICompositionPlan<Vendor>
    {
        public Vendor Compose(ICompositionContext context) =>
            new(context.Resolve<string>(Descriptor(CompositionRequestKind.ConstructorParameter, 0, "Email", typeof(Vendor))));
    }

    private sealed class HolderPlan : ICompositionPlan<Holder>
    {
        public Holder Compose(ICompositionContext context) =>
            new(context.Resolve<SystemClock>(Descriptor(CompositionRequestKind.ConstructorParameter, 0, "Clock", typeof(Holder))));
    }

    // Simulates the shape a real generated Node plan would produce for a self-referencing type: the
    // Child constructor parameter is requested via a descriptor carrying Node's own DeclaringType, per
    // ADR-0020 - a terminating .Member(x => x.Child).Use(...) rule must match this exactly the same
    // way it would against real generated code.
    private sealed class RecursingNodePlan : ICompositionPlan<Node>
    {
        public Node Compose(ICompositionContext context) =>
            new(context.Resolve<Node?>(Descriptor(CompositionRequestKind.ConstructorParameter, 0, "Child", typeof(Node))));
    }
}
