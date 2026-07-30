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

            // Regression for a real gap (PR #13 review, second round): Outer and Inner were both
            // still "in flight" (their own GeneratedPlan dispatch had started but not concluded) when
            // Missing's failure propagated up - before the fix, neither ancestor's GeneratedPlan
            // attempt appeared in the trace at all, since Success was only ever recorded *after*
            // Compose returned (which never happened for either of them). Now each ancestor's
            // in-flight dispatch survives as a Pending entry instead of being silently absent.
            diagnostic.Trace.Count(attempt =>
                attempt.Stage == PipelineStage.GeneratedPlan && attempt.Outcome == CompositionAttemptOutcome.Pending)
                .Should().Be(2);
        }
        finally
        {
            PlanCache<Outer>.Instance = null;
            PlanCache<Inner>.Instance = null;
        }
    }

    [Fact]
    public void ResolveRoot_Throws_WithAFriendlyGenericRootName_NotTheRawClrForm()
    {
        var context = new CompositionContext();

        var act = () => context.ResolveRoot<List<Missing>>();

        var exception = act.Should().Throw<CompositionException>().Which;
        var diagnostic = exception.Diagnostic!;

        // Regression for a real gap (PR #13 review, second round): CompositionDiagnostic.ToString()'s
        // heading rendered RootType.Name directly, so a closed generic root type showed as the raw
        // CLR form ("List`1") even though Path already rendered the same type correctly via
        // FriendlyTypeName. The stage-9 failure Message had the identical bug.
        diagnostic.RootType.Should().Be(typeof(List<Missing>));
        diagnostic.Message.Should().Contain("List<Missing>").And.NotContain("List`1");
        diagnostic.ToString().Should().StartWith("Unable to compose List<Missing>.");
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
    public void ResolveRoot_DiagnosticTrace_DoesNotRecordSuccess_WhenACollectionPlanElementFails()
    {
        CollectionPlanCache<List<Missing>>.Instance = new FailingListPlan();

        try
        {
            var context = new CompositionContext();

            var act = () => context.ResolveRoot<List<Missing>>();

            var trace = act.Should().Throw<CompositionException>().Which.Diagnostic!.Trace;

            // Regression for a real bug (PR #13 review): the collection-dispatch branch used to
            // record "BuiltInProvider: Success" *before* calling collectionPlan.Compose(this) -
            // since Compose throws here (its one element can't be satisfied), the pre-fix trace
            // would have falsely claimed this collection request succeeded, even though it's the
            // root of the failing branch.
            trace.Should().NotContain(attempt => attempt.Outcome == CompositionAttemptOutcome.Success);
        }
        finally
        {
            CollectionPlanCache<List<Missing>>.Instance = null;
        }
    }

    [Fact]
    public void ResolveRoot_DiagnosticTrace_DoesNotRecordSuccess_WhenASharedProviderValueFailsValidation()
    {
        var provider = new NullProvider();
        var context = new CompositionContext(
            configurationRuleProviders: [provider],
            semanticProviders: [],
            testDoubleProviders: [],
            builtInProviders: []);

        var act = () => context.ResolveSharedForTesting<Widget>(ordinal: 0, name: "a");

        var trace = act.Should().Throw<CompositionException>().Which.Diagnostic!.Trace;

        // Same class of bug as the collection case above, at a different site: the profile-provider
        // branch used to record "ConfigurationRule: Success" before StoreSharedAndReturn's authoritative
        // null/type validation ran - a provider that hands back an invalid value for a shared
        // request would have left a false "Success" in the trace even though StoreSharedAndReturn
        // goes on to throw. The same fix applies to the semantic/test-double/built-in provider
        // branches and the generated-plan shared path - this test covers the pattern once, at the
        // stage that's cheapest to exercise via the injectable-provider test seam.
        trace.Should().NotContain(attempt => attempt.Outcome == CompositionAttemptOutcome.Success);

        // Regression for a real gap (PR #13 review, fourth round): the pre-fix shape recorded the
        // winning provider's Success *after* StoreSharedAndReturn returned, so a validation failure
        // that throws left no entry for the provider that actually produced the bad value - the
        // provider attempt was silently absent, not just missing its outcome. Now
        // StoreSharedAndReturn records the real outcome (Success or Failure) itself, before
        // Authoritative can throw, tagged with the provider that produced it.
        trace.Should().Contain(attempt =>
            attempt.Stage == PipelineStage.ConfigurationRule
            && attempt.Provider == typeof(NullProvider)
            && attempt.Outcome == CompositionAttemptOutcome.Failure);
    }

    [Fact]
    public void ResolveRoot_DiagnosticTrace_RecordsAPendingEntry_WhenAProviderRecursivelyResolvesAFailingNestedRequest()
    {
        var provider = new NestedResolvingProvider();
        var context = new CompositionContext(
            configurationRuleProviders: [provider],
            semanticProviders: [],
            testDoubleProviders: [],
            builtInProviders: []);

        var act = () => context.ResolveRoot<Wrapper>();

        var trace = act.Should().Throw<CompositionException>().Which.Diagnostic!.Trace;

        // Regression for a real gap (PR #13 review, sixth round): ICompositionProvider.TryCompose is
        // handed the live context specifically so a provider can recursively resolve part of its
        // value - if that nested Resolve<T> call throws, TryProviders never returns to record this
        // candidate at all, so the ancestor provider/stage that led to the failing child was silently
        // absent from the trace. Same class of bug as the GeneratedPlan/collection-plan Pending fixes
        // above, at a third dispatch site. Now a Pending marker is recorded immediately before
        // TryCompose runs, so a nested failure still surfaces this provider's own in-flight attempt.
        trace.Should().Contain(attempt =>
            attempt.Stage == PipelineStage.ConfigurationRule
            && attempt.Provider == typeof(NestedResolvingProvider)
            && attempt.Outcome == CompositionAttemptOutcome.Pending);
    }

    [Fact]
    public void ResolveRoot_DiagnosticTrace_RecordsADistinctEntryPerBuiltInProvider_NotOneIndistinguishableAggregateEntry()
    {
        // The real BuiltInProviders.Default - PrimitiveValueProvider, EnumValueProvider,
        // NullableValueProvider - all three actually registered in stage 7 today, not a
        // hypothetical future case. ADR-0015 deferred giving ProviderAttempt a provider-identity
        // field on the premise that no stage has more than one competing provider - already false
        // for stage 7 when ADR-0015 was written; ADR-0016 reverses that deferral.
        var context = new CompositionContext();

        var act = () => context.ResolveRoot<Missing>();

        var trace = act.Should().Throw<CompositionException>().Which.Diagnostic!.Trace;
        var builtInAttempts = trace.Where(attempt => attempt.Stage == PipelineStage.BuiltInProvider).ToList();

        // Regression for a real gap (PR #13 review, third round): TryProviders used to let its
        // caller record a single aggregate NotHandled for the whole stage regardless of how many
        // providers were actually tried, collapsing 3 real declines into 1 trace entry.
        builtInAttempts.Count(attempt => attempt.Outcome == CompositionAttemptOutcome.NotHandled)
            .Should().BeGreaterThanOrEqualTo(3);

        // Regression for the fourth-round finding specifically: even after the count was fixed, the
        // three entries were indistinguishable - (BuiltInProvider, NotHandled) three times over, with
        // no way to tell PrimitiveValueProvider's decline from EnumValueProvider's or
        // NullableValueProvider's. Provider now carries each one's concrete type.
        builtInAttempts
            .Where(attempt => attempt.Outcome == CompositionAttemptOutcome.NotHandled)
            .Select(attempt => attempt.Provider)
            .Should().Contain([
                typeof(Compono.Providers.PrimitiveValueProvider),
                typeof(Compono.Providers.EnumValueProvider),
                typeof(Compono.Providers.NullableValueProvider),
            ]);
    }

    [Fact]
    public void ResolveRoot_DiagnosticPath_RendersArraysOfGenericTypes_Recursively()
    {
        var context = new CompositionContext();

        var act = () => context.ResolveRoot<List<Missing>[]>();

        var diagnostic = act.Should().Throw<CompositionException>().Which.Diagnostic!;

        // Regression for a real gap (PR #13 review, third round): FriendlyTypeName checked
        // IsGenericType before IsArray, so an array of a generic type (List<Missing>[]) fell
        // straight through to the raw CLR form ("List`1[]") - arrays aren't generic types in the
        // reflection sense, so the generic-formatting branch never ran for them at all.
        diagnostic.Path.Should().Be("List<Missing>[]");
        diagnostic.Message.Should().Contain("List<Missing>[]").And.NotContain("`1");
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
        new(CompositionRequestKind.ConstructorParameter, ordinal, name, declaringType: null, Nullability.NotNullable);

    private sealed record Outer(Inner Value);

    private sealed record Inner(Missing Value);

    private sealed record Missing;

    private sealed record ThreeParams(string First, string Second, Missing Third);

    private sealed record HasGenericMember(List<Missing> Values);

    private sealed record Widget;

    private sealed record Wrapper;

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

    // Simulates a real generated List<Missing> collection plan whose one element can't be
    // satisfied - matching the shape a genuine CollectionPlanCache<T> entry has (a CollectionElement
    // descriptor per position), per docs/adr/0014-generator-emitted-collection-plans.md.
    private sealed class FailingListPlan : ICompositionPlan<List<Missing>>
    {
        public List<Missing> Compose(ICompositionContext context) =>
            [context.Resolve<Missing>(new CompositionRequestDescriptor(CompositionRequestKind.CollectionElement, 0, "", declaringType: null, Nullability.NotNullable))];
    }

    private sealed class NullProvider : ICompositionProvider
    {
        public CompositionResult TryCompose(in CompositionRequest request, ICompositionContext context) =>
            request.RequestedType == typeof(Widget)
                ? new CompositionResult.Success(null)
                : CompositionResult.NotHandled.Instance;
    }

    // Stands in for a Milestone 3/5/6 profile/semantic/test-double provider that composes part of
    // its own value by recursively calling back into the context - nothing today's built-in
    // providers do, but the ICompositionProvider contract explicitly allows it.
    private sealed class NestedResolvingProvider : ICompositionProvider
    {
        public CompositionResult TryCompose(in CompositionRequest request, ICompositionContext context)
        {
            if (request.RequestedType != typeof(Wrapper))
            {
                return CompositionResult.NotHandled.Instance;
            }

            // Missing has no registration, provider, or generated plan, so this throws - the point of
            // the test is to observe what the trace looks like while this call is still on the stack.
            context.Resolve<Missing>(Descriptor(0, "value"));
            return new CompositionResult.Success(new Wrapper());
        }
    }
}
