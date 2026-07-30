namespace Compono.Tests;

public sealed class CompositionContextTests
{
    [Fact]
    public void ResolveRoot_ReturnsProviderSuccessValue_WhenProviderHandlesRequest()
    {
        var provider = new StubProvider(handles: true, value: "composed");
        var context = new CompositionContext(
            configurationRuleProviders: [],
            semanticProviders: [],
            testDoubleProviders: [],
            builtInProviders: [provider]);

        var result = context.ResolveRoot<string>();

        result.Should().Be("composed");
    }

    [Fact]
    public void Resolve_ReturnsProviderSuccessValue_ForDescriptorBasedRequest()
    {
        var provider = new StubProvider(handles: true, value: "composed");
        var context = new CompositionContext(
            configurationRuleProviders: [],
            semanticProviders: [],
            testDoubleProviders: [],
            builtInProviders: [provider]);
        var descriptor = new CompositionRequestDescriptor(
            CompositionRequestKind.ConstructorParameter, ordinal: 0, name: "value", declaringType: null, Nullability.NotNullable);

        var result = context.Resolve<string>(descriptor);

        result.Should().Be("composed");
    }

    [Fact]
    public void ResolveRoot_TriesStagesInDocumentedOrder_StoppingAtFirstSuccess()
    {
        var callOrder = new List<string>();
        var profileProvider = new RecordingProvider(callOrder, "profile", handles: false);
        var semanticProvider = new RecordingProvider(callOrder, "semantic", handles: false);
        var testDoubleProvider = new RecordingProvider(callOrder, "testDouble", handles: true);
        var builtInProvider = new RecordingProvider(callOrder, "builtIn", handles: true);

        var context = new CompositionContext(
            configurationRuleProviders: [profileProvider],
            semanticProviders: [semanticProvider],
            testDoubleProviders: [testDoubleProvider],
            builtInProviders: [builtInProvider]);

        context.ResolveRoot<string>();

        callOrder.Should().Equal("profile", "semantic", "testDouble");
    }

    [Fact]
    public void ResolveRoot_ReturnsComposedInstance_WhenGeneratedPlanIsRegistered()
    {
        PlanCache<PlanCacheProbe>.Instance = new FixedPlan(new PlanCacheProbe("from-plan"));

        try
        {
            var context = new CompositionContext();

            var result = context.ResolveRoot<PlanCacheProbe>();

            result.Value.Should().Be("from-plan");
        }
        finally
        {
            PlanCache<PlanCacheProbe>.Instance = null;
        }
    }

    [Fact]
    public void ResolveRoot_Throws_WhenNothingCanSatisfyTheRequest()
    {
        var context = new CompositionContext();

        var act = () => context.ResolveRoot<UnresolvableType>();

        act.Should().Throw<CompositionException>()
            .WithMessage("*UnresolvableType*");
    }

    [Fact]
    public void ResolveRoot_TerminalFailureMessage_NamesConfigurationRule_NotTheStaleProfileRuleTerm()
    {
        // Codex review: the stage-9 terminal message still said "profile rule" after PipelineStage's
        // ProfileRule -> ConfigurationRule rename - a stale, misleading term for anyone whose direct
        // .For<T>() rule wasn't what actually failed to satisfy the request.
        var context = new CompositionContext();

        var act = () => context.ResolveRoot<UnresolvableType>();

        act.Should().Throw<CompositionException>()
            .WithMessage("*configuration rule*")
            .Which.Message.Should().NotContain("profile rule");
    }

    private sealed record PlanCacheProbe(string Value);

    private sealed class UnresolvableType
    {
    }

    private sealed class FixedPlan(PlanCacheProbe value) : ICompositionPlan<PlanCacheProbe>
    {
        public PlanCacheProbe Compose(ICompositionContext context) => value;
    }

    private sealed class StubProvider(bool handles, object? value) : ICompositionProvider
    {
        public CompositionResult TryCompose(in CompositionRequest request, ICompositionContext context) =>
            handles ? new CompositionResult.Success(value) : CompositionResult.NotHandled.Instance;
    }

    private sealed class RecordingProvider(List<string> callOrder, string name, bool handles) : ICompositionProvider
    {
        public CompositionResult TryCompose(in CompositionRequest request, ICompositionContext context)
        {
            callOrder.Add(name);
            return handles ? new CompositionResult.Success("value") : CompositionResult.NotHandled.Instance;
        }
    }
}
