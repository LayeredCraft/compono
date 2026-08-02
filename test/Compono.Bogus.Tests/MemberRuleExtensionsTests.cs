namespace Compono.Bogus.Tests;

/// <summary>
/// <see cref="MemberRuleExtensions.UseBogus{TParent, TMember}"/> - the explicit member-rule sugar over
/// <see cref="CompositionMemberRuleBuilder{TParent, TMember}.Use(Func{ICompositionContext, TMember})"/>
/// - PLAN-0006 Phase 3. See <c>docs/adr/0027-compono-bogus-package-design.md</c>.
/// </summary>
public sealed class MemberRuleExtensionsTests
{
    [Fact]
    public void UseBogus_OverridesTheConventionProvider_ForTheSameMember()
    {
        var descriptor = new CompositionRequestDescriptor(
            CompositionRequestKind.ConstructorParameter, ordinal: 0, "FirstName", declaringType: typeof(Customer), Nullability.NotNullable);

        // Stage 4 (configuration rules) always runs before stage 5 (semantic providers) - the member
        // rule below must win over UseBogus()'s own convention guess for "FirstName", with no ordering
        // dependency between UseBogus() and .For<T>().Member(...).UseBogus(...).
        var composer = Composer.Create(builder => builder
            .WithSeed(4219)
            .UseBogus()
            .For<Customer>().Member(c => c.FirstName).UseBogus(f => $"member-rule-{f.Random.Guid()}"));

        var value = composer.CreateRow(typeof(MemberRuleExtensionsTests)).Resolve<string>(descriptor);

        value.Should().StartWith("member-rule-");
    }

    [Fact]
    public void UseBogus_IsDeterministic_ForTheSameSeedAndRequestPath()
    {
        var descriptor = new CompositionRequestDescriptor(
            CompositionRequestKind.ConstructorParameter, ordinal: 0, "FirstName", declaringType: typeof(Customer), Nullability.NotNullable);

        static string ComposeRoot(CompositionRequestDescriptor descriptor) =>
            Composer.Create(builder => builder
                    .WithSeed(4219)
                    .For<Customer>().Member(c => c.FirstName).UseBogus(f => f.Random.Guid().ToString()))
                .CreateRow(typeof(MemberRuleExtensionsTests))
                .Resolve<string>(descriptor);

        var first = ComposeRoot(descriptor);
        var second = ComposeRoot(descriptor);

        first.Should().Be(second);
    }

    [Fact]
    public void UseBogus_ThrowsForANullConfigureDelegate()
    {
        var act = () => Composer.Create(builder => builder
            .For<Customer>().Member(c => c.FirstName).UseBogus(null!));

        act.Should().Throw<ArgumentNullException>();
    }

    public sealed class Customer
    {
        public string FirstName { get; set; } = "";
    }
}
