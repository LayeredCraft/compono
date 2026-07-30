namespace Compono.Tests;

/// <summary>
/// Milestone 3 Phase 4's combined end-to-end test: every prior phase's feature - direct
/// registrations, a profile-sourced registration, an <see cref="IServiceProvider"/> fallback,
/// type/member configuration rules, and both global and member-scoped collection-size overrides -
/// exercised together in one graph, matching <c>docs/plans/0003-milestone-3-profiles-and-configuration.md</c>'s
/// Execution Flow section. This is an integration-level confirmation that the phases compose
/// correctly, not a re-test of any individual phase's own conflict/diagnostic logic (each phase's
/// own test file already covers that in isolation).
/// </summary>
public sealed class ComposerEndToEndConfigurationTests
{
    [Fact]
    public void Create_ComposesAnOrder_UsingEveryConfigurationFeatureTogether()
    {
        PlanCache<Order>.Instance = new OrderPlan();
        CollectionPlanCache<List<string>>.Instance = new StringListPlan();

        try
        {
            var loggingServiceProvider = LoggingServiceProvider.Returning(typeof(ILogger), new ConsoleLogger());

            var composer = Composer.Create(builder => builder
                .WithSeed(4219)
                .WithCollectionSize(2)
                .UseServiceProvider(loggingServiceProvider)
                .AddProfile<ClockProfile>()
                .For<Order>().Member(x => x.Status).Use("Confirmed")
                .For<Order>().Member(x => x.Tags).WithCollectionSize(5));

            var order = composer.Create<Order>();

            // Stage 4 configuration rule (member value rule) wins for Status.
            order.Status.Should().Be("Confirmed");
            // Stage 3 exact registration, sourced from a profile's Configure, satisfies Clock.
            order.Clock.Should().BeOfType<FakeClock>();
            // Stage 3's IServiceProvider fallback sub-step satisfies Logger (no registration exists).
            order.Logger.Should().BeOfType<ConsoleLogger>();
            // Member-scoped WithCollectionSize overrides the global default for Tags only.
            order.Tags.Should().HaveCount(5);
            // Notes keeps the global WithCollectionSize default.
            order.Notes.Should().HaveCount(2);

            // Reuse: a second Create<T>() call on the same Composer repeats resolution against the
            // exact same frozen configuration, independently of the first call.
            var second = composer.Create<Order>();

            second.Status.Should().Be("Confirmed");
            second.Clock.Should().BeOfType<FakeClock>();
            second.Logger.Should().BeOfType<ConsoleLogger>();
            second.Tags.Should().HaveCount(5);
            second.Notes.Should().HaveCount(2);
        }
        finally
        {
            PlanCache<Order>.Instance = null;
            CollectionPlanCache<List<string>>.Instance = null;
        }
    }

    private static CompositionRequestDescriptor Descriptor(int ordinal, string name) =>
        new(CompositionRequestKind.ConstructorParameter, ordinal, name, typeof(Order), Nullability.NotNullable);

    private sealed record Order(string Status, IClock Clock, ILogger Logger, List<string> Tags, List<string> Notes);

    private interface IClock;

    private sealed class FakeClock : IClock;

    private interface ILogger;

    private sealed class ConsoleLogger : ILogger;

    private sealed class ClockProfile : ICompositionProfile
    {
        public void Configure(CompositionBuilder builder) => builder.Register<IClock>(() => new FakeClock());
    }

    private sealed class OrderPlan : ICompositionPlan<Order>
    {
        public Order Compose(ICompositionContext context) => new(
            context.Resolve<string>(Descriptor(0, "Status")),
            context.Resolve<IClock>(Descriptor(1, "Clock")),
            context.Resolve<ILogger>(Descriptor(2, "Logger")),
            context.Resolve<List<string>>(Descriptor(3, "Tags")),
            context.Resolve<List<string>>(Descriptor(4, "Notes")));
    }

    // Matches the shape a real generated collection plan produces - reads context.ResolveCollectionSize()
    // instead of a hardcoded literal, per ADR-0020. Shared by both Tags and Notes: which member is
    // currently resolving is read from the context's own in-flight request, not from this plan.
    private sealed class StringListPlan : ICompositionPlan<List<string>>
    {
        public List<string> Compose(ICompositionContext context)
        {
            var size = context.ResolveCollectionSize();
            var result = new List<string>(size);
            for (var i = 0; i < size; i++)
                result.Add($"item-{i}");
            return result;
        }
    }

    private sealed class LoggingServiceProvider : IServiceProvider, IDisposable
    {
        private readonly Type _type;
        private readonly object _value;

        private LoggingServiceProvider(Type type, object value)
        {
            _type = type;
            _value = value;
        }

        internal static LoggingServiceProvider Returning(Type type, object value) => new(type, value);

        public object? GetService(Type serviceType) => serviceType == _type ? _value : null;

        public void Dispose() =>
            throw new InvalidOperationException("Compono must never dispose a configured IServiceProvider.");
    }
}
