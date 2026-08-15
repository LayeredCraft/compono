using Compono.XunitV3;

namespace Compono.TestDoubles.SampleTests;

// PLAN-0044 Phase 1's own packaged-consumer smoke test - the ADR's actual motivating shape
// (ILogger<T>'s own Log<TState>/BeginScope<TState>) through the real packaged
// Compono -> Compono.Generators -> Compono.TestDoubles dependency chain, plus the combined
// overload+generic interaction (ADR-0044 Amendment 1). ILoggerLike here deliberately mirrors the
// real Microsoft.Extensions.Logging.ILogger shape rather than referencing that package directly -
// this project has no dependency on it, and the point is proving the generator's return-type-
// dependency symbol-graph walk against an ordinary metadata-defined interface reached only through
// a packaged consumer, the same class of cross-assembly concern OverloadedMemberTests.cs already
// covers for Phase 0 (a Verify() snapshot test only diffs generated source text against a golden
// file, it never actually compiles that text into a genuinely separate consumer assembly).
public interface ILoggerLike
{
    void Log<TState>(int logLevel, TState state, Exception? exception);

    IDisposable? BeginScope<TState>(TState state) where TState : notnull;
}

public sealed class LoggerLikeConsumer
{
    public LoggerLikeConsumer(ILoggerLike logger) => Logger = logger;

    public ILoggerLike Logger { get; }
}

public interface IWidget
{
    void Process<T>(T value);

    void Process<T>(IEnumerable<T> values);
}

public sealed class WidgetConsumer
{
    public WidgetConsumer(IWidget widget) => Widget = widget;

    public IWidget Widget { get; }
}

public sealed class GenericMemberTests
{
    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public void Generic_method_independent_of_its_own_type_parameter_gets_a_non_generic_configuration_surface(
        [Shared] ILoggerLike logger,
        LoggerLikeConsumer consumer)
    {
        var scope = new CancellationTokenSource();

        logger.Configure().Log().Throws(new InvalidOperationException("boom"));
        logger.Configure().BeginScope().Returns(scope);

        var act = () => consumer.Logger.Log(0, "state", null);

        act.Should().Throw<InvalidOperationException>().WithMessage("boom");
        consumer.Logger.BeginScope("state").Should().BeSameAs(scope);
    }

    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public void Overloaded_generic_method_gets_a_generic_configuration_surface_per_overload(
        [Shared] IWidget widget,
        WidgetConsumer consumer)
    {
        // The IEnumerable<T> overload needs an *explicit* type argument at both the Configure() call
        // and the real call site - ordinary C# overload-resolution betterness rules always prefer an
        // identity conversion (Process<T>(T value) with T inferred as the argument's own static
        // type, e.g. List<string>) over the implicit reference conversion IEnumerable<T> values
        // would otherwise need, so an implicit-type-argument call can never actually reach the
        // IEnumerable<T> overload with a single argument. This mirrors the ADR's own example
        // (Amendment 1) and is the exact property the discriminator extension is designed to
        // preserve: it can never disagree with which real overload a consumer is actually exercising.
        widget.Configure().Process(0).Throws(new InvalidOperationException("int overload"));
        widget.Configure().Process<string>(new List<string>()).Throws(new InvalidOperationException("enumerable overload"));

        var intAct = () => consumer.Widget.Process(42);
        var enumerableAct = () => consumer.Widget.Process<string>(new List<string>());

        intAct.Should().Throw<InvalidOperationException>().WithMessage("int overload");
        enumerableAct.Should().Throw<InvalidOperationException>().WithMessage("enumerable overload");
    }
}
