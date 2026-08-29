namespace Compono.Tests;

/// <summary>
/// Exercises <see cref="CompositionBuilder.Share{T}"/>'s graph-wide sharing contract - see
/// <c>docs/adr/0056-composition-builder-share-graph-wide-sharing.md</c>. Uses the same
/// Register-factory-driven nested-composition pattern as <see cref="CompositionManualResolveTests"/>
/// (rather than a real generated plan) so this project needs no active
/// <c>Compono.Generators</c> analyzer reference - the real generated-plan/<c>[Compose&lt;TProfile&gt;]</c>
/// proof lives in <c>test/Compono.XunitV3.Tests/CompositionBuilderShareTests.cs</c> instead. Every
/// assertion here is <c>ReferenceEquals</c> identity, never value equality, because identity is the
/// actual contract.
/// </summary>
public sealed class CompositionShareTests
{
    // ---- Sharing within one Create<T>() graph; not across independent Create<T>() calls ----

    [Fact]
    public void Share_SharesWithinOneCreateGraph_ButNotAcrossIndependentCreateCalls()
    {
        var composer = Composer.Create(builder =>
        {
            RegisterGraph(builder);
            builder.Share<ShareLeaf>();
        });

        var first = composer.Create<ShareRoot>();
        var second = composer.Create<ShareRoot>();

        ReferenceEquals(first.A.Leaf, first.B.Leaf).Should().BeTrue();
        ReferenceEquals(first.A.Leaf, second.A.Leaf).Should().BeFalse();
    }

    // ---- CreateMany<T>() boundary ----

    [Fact]
    public void Share_SharesWithinEachCreateManyItem_ButNeverAcrossItems()
    {
        var composer = Composer.Create(builder =>
        {
            RegisterGraph(builder);
            builder.Share<ShareLeaf>();
        });

        var roots = composer.CreateMany<ShareRoot>(3);

        foreach (var root in roots)
            ReferenceEquals(root.A.Leaf, root.B.Leaf).Should().BeTrue();

        ReferenceEquals(roots[0].A.Leaf, roots[1].A.Leaf).Should().BeFalse();
        ReferenceEquals(roots[1].A.Leaf, roots[2].A.Leaf).Should().BeFalse();
    }

    [Fact]
    public void NoShare_CreateManyItems_DoNotShareEvenWithinOneItem()
    {
        // Control case: the cross-item independence above only means something once within-item
        // sharing is confirmed to be real sharing caused by Share<T>(), not an accident of how
        // ShareConsumerA/B happen to be constructed.
        var composer = Composer.Create(RegisterGraph);

        var roots = composer.CreateMany<ShareRoot>(2);

        foreach (var root in roots)
            ReferenceEquals(root.A.Leaf, root.B.Leaf).Should().BeFalse();
    }

    // ---- CompositionRow boundary (hand-written Resolve, not [Compose] attribute binding) ----

    [Fact]
    public void Share_SharesAcrossAnEntireCompositionRow()
    {
        var composer = Composer.Create(builder =>
        {
            RegisterGraph(builder);
            builder.Share<ShareLeaf>();
        });
        var row = composer.CreateRow(typeof(CompositionShareTests));

        var a = row.Resolve<ShareConsumerA>(TestParameterDescriptor(0, "a"));
        var b = row.Resolve<ShareConsumerB>(TestParameterDescriptor(1, "b"));

        ReferenceEquals(a.Leaf, b.Leaf).Should().BeTrue();
    }

    [Fact]
    public void NoShare_CompositionRowSiblingRequests_DoNotShare()
    {
        // Control case: this is a graph-boundary claim (a hand-written, non-attribute path) with no
        // other precedent in this file proving row-wide sharing is actually caused by Share<T>().
        var composer = Composer.Create(RegisterGraph);
        var row = composer.CreateRow(typeof(CompositionShareTests));

        var a = row.Resolve<ShareConsumerA>(TestParameterDescriptor(0, "a"));
        var b = row.Resolve<ShareConsumerB>(TestParameterDescriptor(1, "b"));

        ReferenceEquals(a.Leaf, b.Leaf).Should().BeFalse();
    }

    // ---- Register<T>()/Share<T>() ordering ----

    [Fact]
    public void RegisterThenShare_ResolvesThroughRegistration_AndEstablishesSharedInstance()
    {
        var registered = new ShareLeaf("registered");
        var composer = Composer.Create(builder =>
        {
            RegisterConsumers(builder);
            builder.Register<ShareLeaf>(() => registered);
            builder.Share<ShareLeaf>();
        });

        var root = composer.Create<ShareRoot>();

        root.A.Leaf.Should().BeSameAs(registered);
        ReferenceEquals(root.A.Leaf, root.B.Leaf).Should().BeTrue();
    }

    [Fact]
    public void ShareThenRegister_ProducesIdenticalBehavior_ToRegisterThenShare()
    {
        var registered = new ShareLeaf("registered");
        var composer = Composer.Create(builder =>
        {
            RegisterConsumers(builder);
            builder.Share<ShareLeaf>();
            builder.Register<ShareLeaf>(() => registered);
        });

        var root = composer.Create<ShareRoot>();

        root.A.Leaf.Should().BeSameAs(registered);
        ReferenceEquals(root.A.Leaf, root.B.Leaf).Should().BeTrue();
    }

    // ---- Configured IServiceProvider fallback also participates (not just exact Register<T>()) ----

    [Fact]
    public void Share_ParticipatesForAValueSatisfiedByTheServiceProviderFallback_NotAnExactRegistration()
    {
        var provided = new ShareLeaf("from-provider");
        var provider = StubServiceProvider.Returning(typeof(ShareLeaf), provided);
        var composer = Composer.Create(builder =>
        {
            RegisterConsumers(builder);
            builder.UseServiceProvider(provider);
            builder.Share<ShareLeaf>();
        });

        var root = composer.Create<ShareRoot>();

        root.A.Leaf.Should().BeSameAs(provided);
        ReferenceEquals(root.A.Leaf, root.B.Leaf).Should().BeTrue();
    }

    // ---- Duplicate Share<T>() calls are idempotent ----

    [Fact]
    public void Share_CalledTwiceDirectlyForTheSameType_IsIdempotent()
    {
        var composer = Composer.Create(builder =>
        {
            RegisterGraph(builder);
            builder.Share<ShareLeaf>();
            builder.Share<ShareLeaf>();
        });

        var root = composer.Create<ShareRoot>();

        ReferenceEquals(root.A.Leaf, root.B.Leaf).Should().BeTrue();
    }

    [Fact]
    public void Share_CalledOnceViaEachOfTwoProfilesForTheSameType_IsIdempotent()
    {
        var composer = Composer.Create(builder =>
        {
            RegisterGraph(builder);
            builder.AddProfile<ShareLeafProfileOne>();
            builder.AddProfile<ShareLeafProfileTwo>();
        });

        var root = composer.Create<ShareRoot>();

        ReferenceEquals(root.A.Leaf, root.B.Leaf).Should().BeTrue();
    }

    // ---- Nested/transitive dependencies at more than one level of depth participate ----

    [Fact]
    public void Share_ParticipatesAtMoreThanOneLevelOfNestingDepth()
    {
        var composer = Composer.Create(builder =>
        {
            builder.Register<ShareLeaf>(() => new ShareLeaf("generated"));
            builder.Register<ShareGrandchild>(ctx => new ShareGrandchild(ctx.Resolve<ShareLeaf>()));
            builder.Register<ShareDeepConsumer>(ctx => new ShareDeepConsumer(ctx.Resolve<ShareGrandchild>()));
            builder.Register<ShareConsumerA>(ctx => new ShareConsumerA(ctx.Resolve<ShareLeaf>()));
            builder.Register<ShareDepthRoot>(ctx =>
                new ShareDepthRoot(ctx.Resolve<ShareDeepConsumer>(), ctx.Resolve<ShareConsumerA>()));
            builder.Share<ShareLeaf>();
        });

        var root = composer.Create<ShareDepthRoot>();

        ReferenceEquals(root.Deep.Grandchild.Leaf, root.Shallow.Leaf).Should().BeTrue();
    }

    // ---- Existing [Shared]-driven CompositionRow behavior is unchanged when Share<T>() is never
    // configured - the control evidence for [Shared] non-regression; no separate paired case needed. ----

    [Fact]
    public void ExistingSharedAttributeMechanism_IsUnaffected_WhenShareIsNeverConfigured()
    {
        var composer = Composer.Create(builder => builder.Register<ShareLeaf>(() => new ShareLeaf("generated")));
        var row = composer.CreateRow(typeof(CompositionShareTests));
        var first = TestParameterDescriptor(0, "first");
        var second = TestParameterDescriptor(1, "second");

        var shared = row.ResolveShared<ShareLeaf>(first);
        var later = row.Resolve<ShareLeaf>(second);

        later.Should().BeSameAs(shared);

        var third = TestParameterDescriptor(2, "third");
        var act = () => row.ResolveShared<ShareLeaf>(third);
        act.Should().Throw<CompositionException>();
    }

    private static CompositionRequestDescriptor TestParameterDescriptor(int ordinal, string name) =>
        new(CompositionRequestKind.TestParameter, ordinal, name, declaringType: typeof(CompositionShareTests), Nullability.NotNullable);

    private static void RegisterConsumers(CompositionBuilder builder)
    {
        builder.Register<ShareConsumerA>(ctx => new ShareConsumerA(ctx.Resolve<ShareLeaf>()));
        builder.Register<ShareConsumerB>(ctx => new ShareConsumerB(ctx.Resolve<ShareLeaf>()));
        builder.Register<ShareRoot>(ctx => new ShareRoot(ctx.Resolve<ShareConsumerA>(), ctx.Resolve<ShareConsumerB>()));
    }

    private static void RegisterGraph(CompositionBuilder builder)
    {
        builder.Register<ShareLeaf>(() => new ShareLeaf("generated"));
        RegisterConsumers(builder);
    }

    private sealed record ShareLeaf(string Origin);

    private sealed record ShareConsumerA(ShareLeaf Leaf);

    private sealed record ShareConsumerB(ShareLeaf Leaf);

    private sealed record ShareRoot(ShareConsumerA A, ShareConsumerB B);

    private sealed record ShareGrandchild(ShareLeaf Leaf);

    private sealed record ShareDeepConsumer(ShareGrandchild Grandchild);

    private sealed record ShareDepthRoot(ShareDeepConsumer Deep, ShareConsumerA Shallow);

    private sealed class ShareLeafProfileOne : ICompositionProfile
    {
        public void Configure(CompositionBuilder builder) => builder.Share<ShareLeaf>();
    }

    private sealed class ShareLeafProfileTwo : ICompositionProfile
    {
        public void Configure(CompositionBuilder builder) => builder.Share<ShareLeaf>();
    }

    private sealed class StubServiceProvider : IServiceProvider
    {
        private readonly Type _type;
        private readonly object? _value;

        private StubServiceProvider(Type type, object? value)
        {
            _type = type;
            _value = value;
        }

        internal static StubServiceProvider Returning(Type type, object? value) => new(type, value);

        public object? GetService(Type serviceType) => serviceType == _type ? _value : null;
    }
}
