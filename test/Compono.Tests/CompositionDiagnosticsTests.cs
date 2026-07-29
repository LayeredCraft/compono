namespace Compono.Tests;

/// <summary>
/// Milestone 2 Phase 4's structured diagnostic (<see cref="CompositionException.Diagnostic"/>) and
/// the checkpoint/rewind trace buffer behind it (<c>docs/adr/0010-...</c>).
/// </summary>
public sealed class CompositionDiagnosticsTests
{
    [Fact]
    public void ResolveRoot_Throws_WithADiagnosticTree_MatchingTheNestedFailurePath()
    {
        PlanCache<Outer>.Instance = new OuterPlan();
        PlanCache<Inner>.Instance = new InnerPlan();

        try
        {
            var context = new CompositionContext();

            var act = () => context.ResolveRoot<Outer>();

            var exception = act.Should().Throw<CompositionException>().Which;
            var diagnostic = exception.Diagnostic;
            diagnostic.Should().NotBeNull();
            diagnostic!.RootType.Should().Be(typeof(Outer));
            diagnostic.FailedType.Should().Be(typeof(Missing));
            diagnostic.Path.Should().Be("Outer\n└── Inner value\n    └── Missing value");
            diagnostic.Message.Should().Contain("Missing");
            diagnostic.ToString().Should().Be(
                $"Unable to compose Outer.\n\n{diagnostic.Path}\n\n{diagnostic.Message}\n\nSeed: {diagnostic.Seed}");
        }
        finally
        {
            PlanCache<Outer>.Instance = null;
            PlanCache<Inner>.Instance = null;
        }
    }

    [Fact]
    public void ResolveRoot_DiagnosticTrace_RetainsOnlyTheFailingBranch_NotAlreadySucceededSiblings()
    {
        PlanCache<ThreeParams>.Instance = new ThreeParamsPlan();

        try
        {
            var context = new CompositionContext();

            var act = () => context.ResolveRoot<ThreeParams>();

            var exception = act.Should().Throw<CompositionException>().Which;
            var trace = exception.Diagnostic!.Trace;

            // The first two constructor parameters (both string) resolve successfully via the
            // built-in provider stage and rewind their own attempts away on the way out - if they
            // hadn't, their own "BuiltInProvider: Success" attempts would still be sitting in this
            // trace. Only the third (unsatisfiable) parameter's, and ThreeParams' own, declined
            // attempts should survive.
            trace.Should().NotBeEmpty();
            trace.Should().NotContain(attempt => attempt.Outcome == CompositionAttemptOutcome.Success);
        }
        finally
        {
            PlanCache<ThreeParams>.Instance = null;
        }
    }

    [Fact]
    public void ResolveRoot_DiagnosticPath_RendersClosedGenericTypes_InCSharpStyleNotRawClrForm()
    {
        PlanCache<HasGenericMember>.Instance = new HasGenericMemberPlan();

        try
        {
            var context = new CompositionContext();

            var act = () => context.ResolveRoot<HasGenericMember>();

            var diagnostic = act.Should().Throw<CompositionException>().Which.Diagnostic;

            diagnostic!.Path.Should().Be("HasGenericMember\n└── List<Missing> values");
        }
        finally
        {
            PlanCache<HasGenericMember>.Instance = null;
        }
    }

    private static CompositionRequestDescriptor Descriptor(int ordinal, string name) =>
        new(CompositionRequestKind.ConstructorParameter, ordinal, name, Nullability.NotNullable);

    private sealed record Outer(Inner Value);

    private sealed record Inner(Missing Value);

    private sealed record Missing;

    private sealed record ThreeParams(string First, string Second, Missing Third);

    private sealed record HasGenericMember(List<Missing> Values);

    private sealed class OuterPlan : ICompositionPlan<Outer>
    {
        public Outer Compose(ICompositionContext context) =>
            new(context.Resolve<Inner>(Descriptor(0, "value")));
    }

    private sealed class InnerPlan : ICompositionPlan<Inner>
    {
        public Inner Compose(ICompositionContext context) =>
            new(context.Resolve<Missing>(Descriptor(0, "value")));
    }

    private sealed class ThreeParamsPlan : ICompositionPlan<ThreeParams>
    {
        public ThreeParams Compose(ICompositionContext context) =>
            new(
                context.Resolve<string>(Descriptor(0, "first")),
                context.Resolve<string>(Descriptor(1, "second")),
                context.Resolve<Missing>(Descriptor(2, "third")));
    }

    // No CollectionPlanCache<List<Missing>> exists (nothing set one, since this project never runs
    // the real generator - Milestone 1's Phase 0 note) and List<Missing> has no PlanCache<T> either,
    // so this reaches stage 9's "no plan" failure directly - exercising FriendlyTypeName's rendering
    // without needing a real generated collection plan.
    private sealed class HasGenericMemberPlan : ICompositionPlan<HasGenericMember>
    {
        public HasGenericMember Compose(ICompositionContext context) =>
            new(context.Resolve<List<Missing>>(Descriptor(0, "values")));
    }
}
