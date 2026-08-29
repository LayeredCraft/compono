namespace Compono.XunitV3.Tests;

/// <summary>
/// Exercises <see cref="CompositionBuilder.Share{T}"/> against a real, compiled generated plan and a
/// real <c>[Compose&lt;TProfile&gt;]</c> binding path - not a synthetic/manual resolution path. See
/// <c>docs/adr/0056-composition-builder-share-graph-wide-sharing.md</c>. This project's
/// <c>Compono.Generators</c> analyzer reference is a real, permanent project dependency (confirmed to
/// introduce zero regressions to this project's other existing tests), not a temporary artifact.
/// </summary>
public sealed class ShareLeaf
{
    public string Origin { get; init; } = "generated";
}

public sealed class ShareConsumerA(ShareLeaf leaf)
{
    public ShareLeaf Leaf { get; } = leaf;
}

public sealed class ShareConsumerB(ShareLeaf leaf)
{
    public ShareLeaf Leaf { get; } = leaf;
}

// Two branches at the same nesting depth, neither a direct root constructor parameter - the shape
// ADR-0056's normative "two ordinary, unattributed production constructors" contract calls for: no
// [Shared], no test/theory parameter of the shared type, no Compono-specific annotation anywhere on
// these three types.
public sealed class ShareRoot(ShareConsumerA a, ShareConsumerB b)
{
    public ShareConsumerA A { get; } = a;
    public ShareConsumerB B { get; } = b;
}

// One direct root-level request alongside one nested (root -> consumer -> leaf) request - proves a
// real generated plan's own nested context.Resolve<T>(descriptor) call (ConsumerA's own generated
// plan, dispatching its own constructor parameter) observes the same instance a sibling, shallower
// request already established.
public sealed class ShareRootWithDirectAndNested(ShareLeaf directLeaf, ShareConsumerA a)
{
    public ShareLeaf DirectLeaf { get; } = directLeaf;
    public ShareConsumerA A { get; } = a;
}

public sealed class ShareProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) => builder.Share<ShareLeaf>();
}

public sealed class ShareLeafConsumer(ShareLeaf dependency)
{
    public ShareLeaf Dependency { get; } = dependency;
}

public sealed class CompositionBuilderShareTests
{
    // ---- A real generated plan's nested request participates in Share<T>() ----

    [Fact]
    public void Share_GeneratedPlanNestedRequest_ObservesShareConfiguredType()
    {
        var composer = Composer.Create(builder => builder.Share<ShareLeaf>());

        var root = composer.Create<ShareRootWithDirectAndNested>();

        // root.DirectLeaf: root's own generated plan's direct constructor-parameter request.
        // root.A.Leaf: ShareConsumerA's own, separately-generated plan's nested constructor-parameter
        // request, dispatched from inside root's plan's own context.Resolve<ShareConsumerA>(descriptor)
        // call. Reference identity here is proof, not inference, that the nested generated dispatch
        // participates in the exact same CompositionScope the shallower request populated.
        ReferenceEquals(root.DirectLeaf, root.A.Leaf).Should().BeTrue();
    }

    [Fact]
    public void NoShare_GeneratedPlanNestedAndDirectRequests_AreIndependent()
    {
        // Control case: proves the sharing above is attributable to Share<T>() and not some other
        // identity-preserving behavior already latent in generated-plan dispatch.
        var composer = Composer.Create();

        var root = composer.Create<ShareRootWithDirectAndNested>();

        ReferenceEquals(root.DirectLeaf, root.A.Leaf).Should().BeFalse();
    }

    // ---- Two ordinary, unattributed production-shaped constructors reached only as nested
    // dependencies receive identical shared identity - ADR-0056's strongest, least-precedented claim ----

    [Fact]
    public void Share_TwoOrdinaryUnattributedConsumers_ReceiveIdenticalSharedInstance()
    {
        var composer = Composer.Create(builder => builder.Share<ShareLeaf>());

        var root = composer.Create<ShareRoot>();

        ReferenceEquals(root.A.Leaf, root.B.Leaf).Should().BeTrue();
    }

    [Fact]
    public void NoShare_TwoOrdinaryUnattributedConsumers_ReceiveIndependentInstances()
    {
        // Control case: worth the extra proof that two ordinary constructors are independent by
        // default and shared only because Share<T>() was configured.
        var composer = Composer.Create();

        var root = composer.Create<ShareRoot>();

        ReferenceEquals(root.A.Leaf, root.B.Leaf).Should().BeFalse();
    }

    // ---- An ordinary, undecorated [Compose<TProfile>] theory parameter participates automatically,
    // in both declaration orders relative to its structural dependent - zero [Shared] anywhere ----

    [Theory]
    [Compose<ShareProfile>]
    public void Share_DependencyDeclaredBeforeSut_ZeroSharedAttributes(ShareLeaf dependency, ShareLeafConsumer sut)
    {
        ReferenceEquals(dependency, sut.Dependency).Should().BeTrue();
    }

    [Theory]
    [Compose<ShareProfile>]
    public void Share_SutDeclaredBeforeDependency_ZeroSharedAttributes(ShareLeafConsumer sut, ShareLeaf dependency)
    {
        ReferenceEquals(dependency, sut.Dependency).Should().BeTrue();
    }
}
