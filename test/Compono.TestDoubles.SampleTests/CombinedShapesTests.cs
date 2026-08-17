using Compono.XunitV3;

namespace Compono.TestDoubles.SampleTests;

// PLAN-0044 Phase 3's own combined-shapes proof: an overloaded member, a generic method independent
// of its own type parameter, and Verify() call verification, all on the same interface, through the
// real packaged Compono -> Compono.Generators -> Compono.TestDoubles dependency chain. Phases 0-2
// each proved their own shape individually (OverloadedMemberTests.cs, GenericMemberTests.cs,
// VerificationTests.cs) - this is the first point all three are exercised together against a real
// external consumer, matching the AOT smoke test's own combined-shapes extension for this phase.
public interface INotifier
{
    void Notify(string message);

    void Notify(int code, string message);

    void Publish<TEvent>(TEvent @event);
}

public sealed class NotifierConsumer
{
    public NotifierConsumer(INotifier notifier) => Notifier = notifier;

    public INotifier Notifier { get; }
}

public sealed class CombinedShapesTests
{
    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public void Overloaded_member_and_generic_method_verify_independently_after_real_calls(
        [Shared] INotifier notifier,
        NotifierConsumer consumer)
    {
        notifier.Configure().Notify("hello");
        notifier.Configure().Notify(3, "retrying");
        notifier.Configure().Publish().Throws(new InvalidOperationException("boom"));

        consumer.Notifier.Notify("hello");
        consumer.Notifier.Notify(3, "retrying");
        consumer.Notifier.Notify(3, "retrying");

        var act = () => consumer.Notifier.Publish("payload");

        act.Should().Throw<InvalidOperationException>().WithMessage("boom");

        notifier.Verify().Notify("hello").Once();
        notifier.Verify().Notify(3, "retrying").Exactly(2);
        notifier.Verify().Publish().Once();
    }
}
