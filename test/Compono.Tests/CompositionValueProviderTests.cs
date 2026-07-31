namespace Compono.Tests;

/// <summary>
/// Milestone 5 Phase 0's public provider extension point -
/// <see cref="ICompositionValueProvider"/>/<see cref="CompositionBuilder.AddSemanticProvider"/>/
/// <see cref="CompositionBuilder.AddTestDoubleProvider"/> - exercised through the real
/// <see cref="Composer"/> path, in isolation from <c>Compono.NSubstitute</c> (which doesn't exist
/// yet), per <c>testing.md</c>'s "verifying a new public entry point" rule. See
/// <c>docs/adr/0024-public-provider-extensibility-model.md</c>.
/// </summary>
public sealed class CompositionValueProviderTests
{
    [Fact]
    public void AddSemanticProvider_ParticipatesInStage5_AndSatisfiesTheRequest()
    {
        var composer = Composer.Create(builder => builder.AddSemanticProvider(new StubProvider(handles: true, value: "from-semantic")));

        var result = composer.Create<Widget>();

        result.Value.Should().Be("from-semantic");
    }

    [Fact]
    public void AddTestDoubleProvider_ParticipatesInStage6_AndSatisfiesTheRequest()
    {
        var composer = Composer.Create(builder => builder.AddTestDoubleProvider(new StubProvider(handles: true, value: "from-test-double")));

        var result = composer.Create<Widget>();

        result.Value.Should().Be("from-test-double");
    }

    [Fact]
    public void SemanticProvider_TakesPrecedenceOverTestDoubleProvider_ForTheSameRequest()
    {
        // Stage 5 (semantic) runs before stage 6 (test-double) - docs/architecture.md's fixed
        // resolution order, unchanged by this milestone.
        var composer = Composer.Create(builder => builder
            .AddSemanticProvider(new StubProvider(handles: true, value: "from-semantic"))
            .AddTestDoubleProvider(new StubProvider(handles: true, value: "from-test-double")));

        var result = composer.Create<Widget>();

        result.Value.Should().Be("from-semantic");
    }

    [Fact]
    public void MultipleProvidersInTheSameStage_AreTriedInRegistrationOrder()
    {
        var callOrder = new List<string>();
        var composer = Composer.Create(builder => builder
            .AddTestDoubleProvider(new RecordingProvider(callOrder, "first", handles: false))
            .AddTestDoubleProvider(new RecordingProvider(callOrder, "second", handles: true))
            .AddTestDoubleProvider(new RecordingProvider(callOrder, "third", handles: true)));

        composer.Create<Widget>();

        // "third" never runs - "second" already won.
        callOrder.Should().Equal("first", "second");
    }

    [Fact]
    public void NotHandled_FallsThroughToTheNextPipelineStage()
    {
        // No PlanCache<Widget> is registered, so once every provider declines, this reaches stage 9's
        // ordinary "nothing could satisfy this" failure - proving NotHandled doesn't terminate the
        // pipeline early.
        var composer = Composer.Create(builder => builder.AddTestDoubleProvider(new StubProvider(handles: false, value: null)));

        var act = () => composer.Create<Widget>();

        act.Should().Throw<CompositionException>().WithMessage("*Widget*");
    }

    [Fact]
    public void ProviderAttempt_NamesTheRealProviderType_NotThePublicProviderAdapter()
    {
        var provider = new StubProvider(handles: false, value: null);
        var composer = Composer.Create(builder => builder.AddTestDoubleProvider(provider));

        var act = () => composer.Create<Widget>();

        var diagnostic = act.Should().Throw<CompositionException>().Which.Diagnostic;

        // The adapter that wraps a public ICompositionValueProvider must never leak its own type into
        // diagnostics - ProviderAttempt.Provider has to keep naming the real, logical provider
        // (ADR-0024's diagnostics-identity guarantee), exactly as it already does for an internal
        // stage-4/7 provider.
        diagnostic!.Trace.Should().Contain(attempt =>
            attempt.Stage == PipelineStage.TestDoubleProvider
            && attempt.Provider == typeof(StubProvider)
            && attempt.Outcome == CompositionAttemptOutcome.NotHandled);
        diagnostic.Trace.Should().NotContain(attempt => attempt.Provider != null && attempt.Provider!.Name.Contains("Adapter"));
    }

    [Fact]
    public void SharedRequest_SatisfiedByAPublicProvider_IsReusedByALaterOrdinaryRequest()
    {
        // Mirrors ADR-0011/ADR-0021's existing shared-scope mechanism - a public provider's successful
        // result for a shared request is stored and reused exactly like any other stage's, with zero
        // new code (ADR-0024's "shared substitute reuse: no new mechanism" claim, verified directly).
        var provider = new StubProvider(handles: true, value: "shared-value");
        var context = new CompositionContext(
            configurationRuleProviders: [],
            semanticProviders: [],
            testDoubleProviders: [new Compono.Providers.PublicProviderAdapter(provider, PipelineStage.TestDoubleProvider)],
            builtInProviders: []);

        var first = context.ResolveSharedForTesting<Widget>(ordinal: 0, name: "a");
        var second = context.ResolveRoot<Widget>();

        second.Should().BeSameAs(first);
    }

    [Fact]
    public void ThrownException_FromAPublicProvider_PropagatesUncaught()
    {
        // Per ADR-0024's Provider Failure Semantics: an ordinary pipeline stage never wraps a
        // provider's own thrown exception - a public provider is no exception to that, even though it
        // reaches the pipeline through PublicProviderAdapter rather than implementing
        // ICompositionProvider directly.
        var composer = Composer.Create(builder => builder.AddTestDoubleProvider(new ThrowingProvider()));

        var act = () => composer.Create<Widget>();

        act.Should().Throw<InvalidOperationException>().WithMessage("boom");
    }

    [Fact]
    public void Provider_CanComposeANestedValue_ViaTheDescriptorLessResolveOverload()
    {
        // Regression for PR #28 review (Codex, finding 1): a provider calling context.Resolve<T>()
        // (no descriptor) used to throw InvalidOperationException unconditionally, because
        // PublicProviderAdapter never pushed the manual-resolve frame that overload requires -
        // ICompositionValueProvider's own contract explicitly promises this works. NestedResolvingProvider
        // also proves the (provider, requested type) reentrance key doesn't over-block: the same
        // provider instance handles both Gadget and, while still "active" for Gadget, its nested
        // Widget request too - a different type, so it isn't treated as a false cycle.
        var composer = Composer.Create(builder => builder.AddTestDoubleProvider(new NestedResolvingProvider()));

        var result = composer.Create<Gadget>();

        result.Inner.Value.Should().Be("from-nested-resolve");
    }

    [Fact]
    public void Provider_RecursivelyResolvingItsOwnRequestedType_ThrowsADiagnosedException_NotStackOverflow()
    {
        // Regression for PR #28 review (Codex, finding 2): a provider that resolves its own requested
        // type again (via the descriptor-based overload, which needs no manual-resolve frame) used to
        // recurse until StackOverflowException instead of producing this diagnostic.
        var composer = Composer.Create(builder => builder.AddTestDoubleProvider(new SelfRecursingProvider()));

        var act = () => composer.Create<Loop>();

        act.Should().Throw<CompositionException>().WithMessage("*Recursive provider request*Loop*");
    }

    private sealed record Widget(string? Value = null);

    private sealed record Gadget(Widget Inner);

    private sealed record Loop;

    private sealed class StubProvider(bool handles, string? value) : ICompositionValueProvider
    {
        public CompositionProviderResult TryProvide(in CompositionProviderRequest request, ICompositionContext context) =>
            handles ? CompositionProviderResult.Handled(new Widget(value)) : CompositionProviderResult.NotHandled;
    }

    private sealed class RecordingProvider(List<string> callOrder, string name, bool handles) : ICompositionValueProvider
    {
        public CompositionProviderResult TryProvide(in CompositionProviderRequest request, ICompositionContext context)
        {
            callOrder.Add(name);
            return handles ? CompositionProviderResult.Handled(new Widget()) : CompositionProviderResult.NotHandled;
        }
    }

    private sealed class ThrowingProvider : ICompositionValueProvider
    {
        public CompositionProviderResult TryProvide(in CompositionProviderRequest request, ICompositionContext context) =>
            throw new InvalidOperationException("boom");
    }

    // Handles two different types through one instance - Gadget composes by nested-resolving Widget
    // (the descriptor-less overload), which this same provider also handles.
    private sealed class NestedResolvingProvider : ICompositionValueProvider
    {
        public CompositionProviderResult TryProvide(in CompositionProviderRequest request, ICompositionContext context)
        {
            if (request.RequestedType == typeof(Gadget))
            {
                var inner = context.Resolve<Widget>();
                return CompositionProviderResult.Handled(new Gadget(inner));
            }

            if (request.RequestedType == typeof(Widget))
                return CompositionProviderResult.Handled(new Widget("from-nested-resolve"));

            return CompositionProviderResult.NotHandled;
        }
    }

    private sealed class SelfRecursingProvider : ICompositionValueProvider
    {
        public CompositionProviderResult TryProvide(in CompositionProviderRequest request, ICompositionContext context)
        {
            if (request.RequestedType != typeof(Loop))
                return CompositionProviderResult.NotHandled;

            var descriptor = new CompositionRequestDescriptor(
                CompositionRequestKind.ConstructorParameter, ordinal: 0, name: "inner", declaringType: null, Nullability.NotNullable);

            // Never reached without throwing first - context.Resolve<Loop>(descriptor) re-enters this
            // same provider for the same type, which InvokeProvider's reentrance guard must catch.
            context.Resolve<Loop>(descriptor);
            return CompositionProviderResult.Handled(new Loop());
        }
    }
}
