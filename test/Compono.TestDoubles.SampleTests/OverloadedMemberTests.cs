using Compono.XunitV3;

namespace Compono.TestDoubles.SampleTests;

// PLAN-0044 Phase 0's own packaged-consumer smoke test - an overloaded interface, including a
// supported out-parameter overload alongside it (ADR-0044 Amendment 8 Finding 20), through the real
// packaged Compono -> Compono.Generators -> Compono.TestDoubles dependency chain. Every defect the
// design-review round found before this test existed (CS0122, CS0460, CS0111, CS0214, CS0177) was
// exactly the class of cross-assembly compile failure an in-process Verify() snapshot test cannot
// catch, since it only diffs generated source text against a golden file rather than actually
// compiling it into a genuinely separate consumer assembly.
public interface IGateway
{
    void Send(string message);

    void Send(int retryCount, string message);

    bool TryParse(string input, out int value);
}

public sealed class GatewayConsumer
{
    public GatewayConsumer(IGateway gateway) => Gateway = gateway;

    public IGateway Gateway { get; }
}

public sealed class OverloadedMemberTests
{
    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public void Overloaded_member_gets_its_own_per_overload_configuration_surface(
        [Shared] IGateway gateway,
        GatewayConsumer consumer)
    {
        gateway.Configure().Send("hello");
        gateway.Configure().Send(3, "retrying");

        consumer.Gateway.Send("hello");
        consumer.Gateway.Send(3, "retrying");
    }

    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public void Out_parameter_overload_falls_back_without_rejecting_its_sibling_overload(
        [Shared] IGateway gateway,
        GatewayConsumer consumer)
    {
        gateway.Configure().Send("hello");

        // TryParse(string, out int) has no Configure()/Verify() surface (an overload-set-internal
        // unsupported shape, ADR-0044 Amendment 5) - it still dispatches, deterministically, without
        // rejecting Send's own configuration surface above.
        var parsed = consumer.Gateway.TryParse("42", out var value);

        parsed.Should().BeFalse();
        value.Should().Be(0);
    }
}
