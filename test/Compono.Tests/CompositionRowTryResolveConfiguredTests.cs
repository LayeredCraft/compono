namespace Compono.Tests;

/// <summary>
/// <see cref="CompositionRow.TryResolveConfigured"/> - the core primitive
/// <c>Compono.DependencyInjection</c> builds on. See
/// <c>docs/adr/0047-compono-dependencyinjection-configured-resolution-bridge.md</c>.
/// </summary>
public sealed class CompositionRowTryResolveConfiguredTests
{
    [Fact]
    public void TryResolveConfigured_ResolvesFromAnExactRegistration()
    {
        var registered = new RegisteredMarker("from-registration");
        var composer = Composer.Create(builder => builder.Register<RegisteredMarker>(() => registered));
        var row = composer.CreateRow(typeof(CompositionRowTryResolveConfiguredTests));

        var handled = row.TryResolveConfigured(typeof(RegisteredMarker), out var value);

        handled.Should().BeTrue();
        value.Should().BeSameAs(registered);
    }

    [Fact]
    public void TryResolveConfigured_ResolvesFromATestDoubleProvider()
    {
        var provider = new StubProvider(handles: true, value: new ProvidedMarker("from-provider"));
        var composer = Composer.Create(builder => builder.AddTestDoubleProvider(provider));
        var row = composer.CreateRow(typeof(CompositionRowTryResolveConfiguredTests));

        var handled = row.TryResolveConfigured(typeof(ProvidedMarker), out var value);

        handled.Should().BeTrue();
        value.Should().Be(new ProvidedMarker("from-provider"));
    }

    [Fact]
    public void TryResolveConfigured_ReadsAnAlreadyEstablishedSharedScopeValue()
    {
        var composer = Composer.Create();
        var row = composer.CreateRow(typeof(CompositionRowTryResolveConfiguredTests));
        var descriptor = new CompositionRequestDescriptor(
            CompositionRequestKind.TestParameter, ordinal: 0, name: "explicit", declaringType: typeof(CompositionRowTryResolveConfiguredTests), Nullability.NotNullable);
        var shared = new UnresolvableMarker("shared");
        row.ShareExplicit(descriptor, shared);

        var handled = row.TryResolveConfigured(typeof(UnresolvableMarker), out var value);

        handled.Should().BeTrue();
        value.Should().BeSameAs(shared);
    }

    [Fact]
    public void TryResolveConfigured_ReturnsFalse_ForATypeNoReachableStageCanHandle()
    {
        var composer = Composer.Create();
        var row = composer.CreateRow(typeof(CompositionRowTryResolveConfiguredTests));

        var handled = row.TryResolveConfigured(typeof(UnresolvableMarker), out var value);

        handled.Should().BeFalse();
        value.Should().BeNull();
    }

    [Fact]
    public void TryResolveConfigured_DoesNotConsultTheConfiguredServiceProvider()
    {
        var composer = Composer.Create(builder => builder.UseServiceProvider(new FakeServiceProvider(new ServiceProviderMarker("from-provider"))));
        var row = composer.CreateRow(typeof(CompositionRowTryResolveConfiguredTests));

        var handled = row.TryResolveConfigured(typeof(ServiceProviderMarker), out var value);

        handled.Should().BeFalse();
        value.Should().BeNull();
    }

    [Fact]
    public void TryResolveConfigured_DoesNotReachOrdinaryGeneratedPlanComposition()
    {
        PlanCache<ComposedMarker>.Instance = new FixedPlan(new ComposedMarker("composed"));

        try
        {
            var composer = Composer.Create();
            var row = composer.CreateRow(typeof(CompositionRowTryResolveConfiguredTests));
            var descriptor = new CompositionRequestDescriptor(
                CompositionRequestKind.TestParameter, ordinal: 0, name: "composed", declaringType: typeof(CompositionRowTryResolveConfiguredTests), Nullability.NotNullable);

            var handled = row.TryResolveConfigured(typeof(ComposedMarker), out var value);
            // The generated plan genuinely satisfies this type through the full pipeline (Resolve<T>) -
            // proving TryResolveConfigured's false above is a deliberate stage exclusion, not a
            // ComposedMarker composition gap of some other kind.
            var composedThroughFullPipeline = row.Resolve<ComposedMarker>(descriptor);

            handled.Should().BeFalse();
            value.Should().BeNull();
            composedThroughFullPipeline.Should().Be(new ComposedMarker("composed"));
        }
        finally
        {
            PlanCache<ComposedMarker>.Instance = null;
        }
    }

    [Fact]
    public void TryResolveConfigured_PerformsAnIndependentResolution_OnEachCall()
    {
        // A provider that hands back a fresh instance every call - if TryResolveConfigured wrote its
        // result into CompositionScope (the way ResolveShared deliberately does), a second call would
        // observe the first call's cached instance instead of composing its own. It doesn't: this is
        // the "no new sharing/caching semantics" guarantee from ADR-0047, proven directly.
        var provider = new FreshInstanceProvider();
        var composer = Composer.Create(builder => builder.AddTestDoubleProvider(provider));
        var row = composer.CreateRow(typeof(CompositionRowTryResolveConfiguredTests));

        row.TryResolveConfigured(typeof(FreshMarker), out var first);
        row.TryResolveConfigured(typeof(FreshMarker), out var second);

        first.Should().NotBeSameAs(second);
    }

    [Fact]
    public void TryResolveConfigured_GivesSiblingRequests_IndependentRandomStreams()
    {
        // Regression for a Codex PR review finding (P1): two sequential top-level
        // TryResolveConfigured calls on the same row ARE siblings under the row's pre-rooted path,
        // exactly like TestParameter/ManualResolve - without a distinct fork identity per call, both
        // registrations below forked from the identical parent random state via the identical segment
        // identity and produced the identical derived value, silently breaking independence for
        // anything randomness-dependent (DeriveSeed(), nested composition, a Bogus-backed provider).
        var composer = Composer.Create(builder => builder
            .WithSeed(4219)
            .Register<SiblingMarkerA>(ctx => new SiblingMarkerA(ctx.DeriveSeed()))
            .Register<SiblingMarkerB>(ctx => new SiblingMarkerB(ctx.DeriveSeed())));
        var row = composer.CreateRow(typeof(CompositionRowTryResolveConfiguredTests));

        row.TryResolveConfigured(typeof(SiblingMarkerA), out var a);
        row.TryResolveConfigured(typeof(SiblingMarkerB), out var b);

        ((SiblingMarkerA)a!).Value.Should().NotBe(((SiblingMarkerB)b!).Value);
    }

    [Fact]
    public void TryResolveConfigured_DerivedValue_DependsOnCallOrder_NotOnWhichTypeWasRequested()
    {
        // Characterizes a real, deliberately-not-fixed gap surfaced in PR review (P1): a
        // ConfiguredResolution segment's fork identity is an ordinal assigned at CALL time (the Nth
        // distinct TryResolveConfigured call on this row), not anything intrinsic to the requested
        // Type - unlike every other value TryResolveConfigured can produce, which reaches Compono's
        // pipeline through a fixed, compile-time-known position. Two identically-seeded rows requesting
        // the SAME pair of types in a DIFFERENT order therefore derive a DIFFERENT value for the SAME
        // type - proven here directly (no actual concurrency needed: swapping call order alone
        // reproduces it). AsServiceProvider()'s adapter fully serializes concurrent GetService calls
        // (no data race, no corruption), but does not make ITS OWN scheduling deterministic - whichever
        // of two concurrent first-time requests happens to acquire the adapter's lock first gets
        // ordinal 0. See ADR-0047 Amendment 5 for why this is documented as a scoped limitation rather
        // than "fixed": every other PathSegment kind derives fork identity from a stable ordinal/index,
        // never from a Type's name (Fnv1a's own design forbids hashing formatted identifiers as fork
        // keys - see Fork_IsUnaffectedByName_ButDiffersByOrdinal) - so a Type-keyed alternative would be
        // a different architectural decision, not a same-scope bug fix.
        var composerAFirst = Composer.Create(builder => builder
            .WithSeed(4219)
            .Register<SiblingMarkerA>(ctx => new SiblingMarkerA(ctx.DeriveSeed()))
            .Register<SiblingMarkerB>(ctx => new SiblingMarkerB(ctx.DeriveSeed())));
        var rowAFirst = composerAFirst.CreateRow(typeof(CompositionRowTryResolveConfiguredTests));
        rowAFirst.TryResolveConfigured(typeof(SiblingMarkerA), out var aWhenAFirst);

        var composerBFirst = Composer.Create(builder => builder
            .WithSeed(4219)
            .Register<SiblingMarkerA>(ctx => new SiblingMarkerA(ctx.DeriveSeed()))
            .Register<SiblingMarkerB>(ctx => new SiblingMarkerB(ctx.DeriveSeed())));
        var rowBFirst = composerBFirst.CreateRow(typeof(CompositionRowTryResolveConfiguredTests));
        rowBFirst.TryResolveConfigured(typeof(SiblingMarkerB), out _);
        rowBFirst.TryResolveConfigured(typeof(SiblingMarkerA), out var aWhenBFirst);

        ((SiblingMarkerA)aWhenAFirst!).Value.Should().NotBe(((SiblingMarkerA)aWhenBFirst!).Value);
    }

    [Fact]
    public void TryResolveConfigured_ReturnsTrueWithNullValue_ForARegistrationThatProducesNull()
    {
        // A bare runtime Type carries no compile-time nullable-reference annotation to validate
        // against, unlike Resolve<TValue>() - TryResolveConfigured always validates as nullable,
        // matching IServiceProvider.GetService(Type)'s own null-friendly BCL contract. See this
        // method's own XML doc on CompositionContext.
        var composer = Composer.Create(builder => builder.Register<NullableMarker>(() => null!));
        var row = composer.CreateRow(typeof(CompositionRowTryResolveConfiguredTests));

        var handled = row.TryResolveConfigured(typeof(NullableMarker), out var value);

        handled.Should().BeTrue();
        value.Should().BeNull();
    }

    [Fact]
    public void TryResolveConfigured_Throws_WhenAReachableRegistrationFactoryThrows()
    {
        var composer = Composer.Create(builder => builder.Register<RegisteredMarker>(() => throw new InvalidOperationException("boom")));
        var row = composer.CreateRow(typeof(CompositionRowTryResolveConfiguredTests));

        var act = () => row.TryResolveConfigured(typeof(RegisteredMarker), out _);

        act.Should().Throw<CompositionException>().WithMessage("*boom*");
    }

    [Fact]
    public void TryResolveConfigured_Throws_WhenAReachableProviderThrows()
    {
        var composer = Composer.Create(builder => builder.AddTestDoubleProvider(new ThrowingProvider()));
        var row = composer.CreateRow(typeof(CompositionRowTryResolveConfiguredTests));

        var act = () => row.TryResolveConfigured(typeof(ProvidedMarker), out _);

        act.Should().Throw<InvalidOperationException>().WithMessage("boom");
    }

    [Fact]
    public void TryResolveConfigured_Throws_WhenAReachableConfigurationRuleFactoryThrows()
    {
        // Regression for a Codex PR review finding: this repo's own docs claimed only an exact
        // registration factory's failure gets wrapped in a CompositionException, but a .For<T>().Use(...)
        // configuration-rule factory is invoked through the exact same CompositionContext.InvokeFactory
        // as an exact registration - TypeRuleProvider.TryCompose calls it directly - so its failure is
        // wrapped identically, not left raw like a stage 4-6 provider's own exception.
        var composer = Composer.Create(builder => builder.For<RegisteredMarker>().Use((ICompositionContext _) => throw new InvalidOperationException("rule-boom")));
        var row = composer.CreateRow(typeof(CompositionRowTryResolveConfiguredTests));

        var act = () => row.TryResolveConfigured(typeof(RegisteredMarker), out _);

        act.Should().Throw<CompositionException>().WithMessage("*rule-boom*");
    }

    [Fact]
    public void TryResolveConfigured_DoesNotLeakTraceEntries_AfterATopLevelExceptionPropagates()
    {
        // Regression for a Codex PR review finding: an exception thrown out of TryResolveConfigured
        // (a stage 4-6 provider's own raw exception, or a stage 3a factory's wrapped
        // CompositionException) used to leave every trace entry recorded since this call's own
        // checkpoint sitting in the row's trace buffer, with nothing left to ever rewind them - every
        // other path already rewinds on its way out, but the exception path skipped straight to
        // `finally`, which never touched the trace. Since BuildDiagnostic slices from index 0, a later,
        // unrelated failing call on the same row would pick up this orphaned batch too.
        var composer = Composer.Create(builder => builder
            .AddTestDoubleProvider(new ThrowingProvider())
            .Register<RegisteredMarker>(() => throw new InvalidOperationException("second-failure")));
        var row = composer.CreateRow(typeof(CompositionRowTryResolveConfiguredTests));

        var firstAttempt = () => row.TryResolveConfigured(typeof(ProvidedMarker), out _);
        firstAttempt.Should().Throw<InvalidOperationException>().WithMessage("boom");

        var secondAttempt = () => row.TryResolveConfigured(typeof(RegisteredMarker), out _);
        var diagnostic = secondAttempt.Should().Throw<CompositionException>().Which.Diagnostic;

        diagnostic!.Trace.Should().NotContain(attempt => attempt.Stage == PipelineStage.TestDoubleProvider);
    }

    [Fact]
    public void CrossRowCycle_IsDetectedAsARecursiveFactory_NotAStackOverflow()
    {
        // Regression for a Codex PR review finding: ADR-0047's Recursion section (and this file's own
        // AsServiceProvider XML doc) originally claimed a cycle crossing two rows goes undetected and
        // overflows the stack, on the assumption each hop gets a fresh CompositionContext. It doesn't -
        // a CompositionRow's context is created once and reused for every call made on that row
        // (including calls arriving indirectly through another row's UseServiceProvider), so the
        // existing same-context reentrance guard (the one that already stops a registration factory
        // from recursively invoking itself) trips before the two rows' calls can recurse indefinitely.
        CompositionRow? rowB = null;
        var descriptorB = new CompositionRequestDescriptor(
            CompositionRequestKind.TestParameter, ordinal: 0, name: "b", declaringType: typeof(CompositionRowTryResolveConfiguredTests), Nullability.NotNullable);

        var composerA = Composer.Create(builder => builder.Register<CycleMarker>(() => rowB!.Resolve<CycleMarker>(descriptorB)));
        var rowA = composerA.CreateRow(typeof(CompositionRowTryResolveConfiguredTests));

        var composerB = Composer.Create(builder => builder.UseServiceProvider(new TryResolveConfiguredServiceProvider(rowA)));
        rowB = composerB.CreateRow(typeof(CompositionRowTryResolveConfiguredTests));

        var act = () => rowB.Resolve<CycleMarker>(descriptorB);

        act.Should().Throw<CompositionException>().WithMessage("*Recursive registration or configuration-rule factory detected*");
    }

    private sealed record CycleMarker;

    // Stands in for Compono.DependencyInjection's AsServiceProvider() (this test project doesn't
    // reference that package) - it forwards to the same TryResolveConfigured primitive, so it exercises
    // the identical stage-2/3a/4-6 path a real cross-row UseServiceProvider wiring would.
    private sealed class TryResolveConfiguredServiceProvider(CompositionRow row) : IServiceProvider
    {
        public object? GetService(Type serviceType) => row.TryResolveConfigured(serviceType, out var value) ? value : null;
    }

    private sealed record RegisteredMarker(string Value);

    private sealed record ProvidedMarker(string Value);

    private sealed record ServiceProviderMarker(string Value);

    private sealed record ComposedMarker(string Value);

    private sealed record UnresolvableMarker(string Value);

    private sealed record NullableMarker;

    private sealed record FreshMarker;

    private sealed record SiblingMarkerA(int Value);

    private sealed record SiblingMarkerB(int Value);

    private sealed class FakeServiceProvider(ServiceProviderMarker value) : IServiceProvider
    {
        public object? GetService(Type serviceType) => serviceType == typeof(ServiceProviderMarker) ? value : null;
    }

    private sealed class FixedPlan(ComposedMarker value) : ICompositionPlan<ComposedMarker>
    {
        public ComposedMarker Compose(ICompositionContext context) => value;
    }

    private sealed class StubProvider(bool handles, object? value) : ICompositionValueProvider
    {
        public CompositionProviderResult TryProvide(in CompositionProviderRequest request, ICompositionContext context) =>
            handles ? CompositionProviderResult.Handled(value) : CompositionProviderResult.NotHandled;
    }

    private sealed class FreshInstanceProvider : ICompositionValueProvider
    {
        public CompositionProviderResult TryProvide(in CompositionProviderRequest request, ICompositionContext context) =>
            request.RequestedType == typeof(FreshMarker) ? CompositionProviderResult.Handled(new FreshMarker()) : CompositionProviderResult.NotHandled;
    }

    private sealed class ThrowingProvider : ICompositionValueProvider
    {
        public CompositionProviderResult TryProvide(in CompositionProviderRequest request, ICompositionContext context) =>
            throw new InvalidOperationException("boom");
    }
}
