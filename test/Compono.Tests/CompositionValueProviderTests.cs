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
            testDoubleProviders: [new Compono.Providers.PublicProviderAdapter(provider)],
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

    private sealed record Widget(string? Value = null);

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
}
