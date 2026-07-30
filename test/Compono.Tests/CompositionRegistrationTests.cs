namespace Compono.Tests;

public sealed class CompositionRegistrationTests
{
    [Fact]
    public void ResolveRoot_ReturnsTheRegisteredValue_WhenARegistrationExists()
    {
        var registered = new Widget("from-registration");
        var context = ContextWithRegistration(typeof(Widget), registered);

        var result = context.ResolveRoot<Widget>();

        result.Should().BeSameAs(registered);
    }

    [Fact]
    public void ResolveRoot_NeverReachesRecursionDetection_WhenARegistrationTerminatesASelfReferencingType()
    {
        var terminal = new Node([]);
        var context = ContextWithRegistration(typeof(Node), terminal);

        // PlanCache<Node>.Instance is deliberately left unset - if stage 3's registration didn't win
        // first, this would throw CompositionException ("no registration, provider, or generated plan
        // could satisfy the request"), proving stage 8 (and its recursion check) is never reached.
        var result = context.ResolveRoot<Node>();

        result.Should().BeSameAs(terminal);
    }

    [Fact]
    public void ResolveRoot_Throws_WhenTheRegisteredValueIsNullForANonNullableRequest()
    {
        var context = ContextWithRegistration(typeof(Widget), value: null);

        var act = () => context.ResolveRoot<Widget>();

        act.Should().Throw<CompositionException>().WithMessage("*registration*");
    }

    [Fact]
    public void ResolveRoot_Throws_WhenTheRegisteredValuesRuntimeTypeIsIncompatible()
    {
        var context = ContextWithRegistration(typeof(Widget), "not-a-widget");

        var act = () => context.ResolveRoot<Widget>();

        act.Should().Throw<CompositionException>().WithMessage("*registration*");
    }

    private static CompositionContext ContextWithRegistration(Type type, object? value)
    {
        var registrations = new CompositionRegistrations(
            new Dictionary<Type, Func<ICompositionContext, object?>> { [type] = _ => value });
        return new CompositionContext(CompositionSeed.Generate(), registrations);
    }

    private sealed record Widget(string Value);

    private sealed record Node(List<Node> Children);
}
