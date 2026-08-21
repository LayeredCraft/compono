using Compono.XunitV3;

namespace Compono.Samples.AspNetApi.Tests;

// PLAN-0048's real-project regression proof: this file's own namespace nests under Compono (the
// exact shape that broke when the new matcher type was still named Compono.Arg - see ADR-0048's
// Decision Outcome and ADR-0044 Amendment 18), and uses ordinary unqualified NSubstitute.Arg calls
// (via the project's own global `using NSubstitute;`) side by side with Compono.TestDoubles'
// Match.Any/Match.Is - no `using Compono;` written anywhere in this file, no alias, no qualification
// for either. If this compiles and passes, the collision that motivated the Arg -> Match rename is
// gone for real, not just in isolation.
public interface INotificationSender
{
    bool Send(string recipient, string message, bool urgent);
}

public sealed class CoexistenceTests
{
    [Theory]
    [Compose<ApiTestProfile>]
    public async Task NSubstituteArgAndComponoMatch_CoexistInTheSameFile_WithNoAliasingRequired(
        [Shared] IOrderRepository repository, OrderService service, PlaceOrder command, Order savedOrder)
    {
        // Real NSubstitute usage - unqualified Arg.Any resolves to NSubstitute.Arg, not Compono.Arg
        // (which no longer exists), even though this file's namespace nests under Compono.
        repository.SaveAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>()).Returns(savedOrder);

        var result = await service.PlaceAsync(command, CancellationToken.None);

        result.Should().Be(savedOrder);
        await repository.Received(1).SaveAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());

        // Compono.TestDoubles usage - unqualified Match.Any/Match.Is in the same file, same method,
        // no conflict with the NSubstitute Arg calls above.
        var sender = Composer.Create(builder => builder.UseGeneratedTestDoubles()).Create<INotificationSender>();

        sender.Configure()
            .Send("ops@example.com", Match.Any<string>(), Match.Is<bool>(urgent => urgent))
            .Returns(true);

        var sent = sender.Send("ops@example.com", "order placed", urgent: true);
        var notUrgent = sender.Send("ops@example.com", "order placed", urgent: false);

        sent.Should().BeTrue();
        notUrgent.Should().BeFalse();
        // Both calls above share the same recipient - filtering by recipient alone matches both.
        sender.Verify().Send(Match.Is<string>(r => r == "ops@example.com"), Match.Any<string>(), Match.Any<bool>()).Exactly(2);
        sender.Verify().Send(Match.Any<string>(), Match.Any<string>(), Match.Is<bool>(urgent => urgent)).Once();
    }
}
