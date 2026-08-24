using Compono.XunitV3;

namespace Compono.TestDoubles.SampleTests;

// ADR-0049 / PLAN-0049: the real evidenced trivia-platform shape - a generic method whose return
// type depends on its own type parameter (Task<T?>), constrained `where T : class` so it has a real
// deterministic default (null) and composes with ADR-0048's argument-aware Match<TParam>/CallVerifier
// surface, scoped independently per closed T via a generator-emitted Dictionary<Type, object> bucket.
// GetRequiredDataAsync<T> mirrors the ADR-0049 design spike's second member (Task<T>, non-nullable) to
// exercise ADR-0045's *other* dispatch branch (configuration-required) through the same mechanism -
// IConversationalContextManager itself has no non-nullable generic-return member, only this sample
// interface's own completeness check does. TransitionAsync/CurrentState are ordinary, non-generic
// members proving a closed-instantiation-eligible member no longer poisons its siblings (the
// whole-interface-Failure() consequence ADR-0049's Context section named).
public interface IContextManager
{
    Task<T?> GetContextDataAsync<T>(string key) where T : class;

    Task<T> GetRequiredDataAsync<T>(string key) where T : class;

    Task TransitionAsync(string newState);

    string CurrentState { get; }
}

// ADR-0049 / PLAN-0049: an overloaded closed-instantiation-eligible member reuses ADR-0044
// Requirement 1's existing overload-discriminator machinery (real, un-wrapped parameter types,
// per-overload suffix) rather than ADR-0048's Match<TParam> surface - the same disposition every
// other overloaded member already has. Both overloads share the exact same closed T set, proving
// overload identity and closed-T identity are genuinely independent axes.
public interface IOverloadedContextManager
{
    Task<T?> GetDataAsync<T>(string id) where T : class;

    Task<T?> GetDataAsync<T>(string id, int version) where T : class;
}

public sealed record UserContext(string Sub);

public sealed record UpsellPayload(string ProductId);

public sealed class ClosedInstantiationTests
{
    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public async Task Two_closed_Ts_are_configured_and_verified_independently_on_the_same_instance(
        [Shared] IContextManager contextManager)
    {
        var user = new UserContext("sub-1");
        var payload = new UpsellPayload("prod-1");

        contextManager.Configure().GetContextDataAsync<UserContext>(Compono.Match.Any<string>()).Returns(Task.FromResult<UserContext?>(user));
        contextManager.Configure().GetContextDataAsync<UpsellPayload>(Compono.Match.Any<string>()).Returns(Task.FromResult<UpsellPayload?>(payload));

        var resolvedUser = await contextManager.GetContextDataAsync<UserContext>("user");
        var resolvedPayload = await contextManager.GetContextDataAsync<UpsellPayload>("upsell");

        resolvedUser.Should().BeSameAs(user);
        resolvedPayload.Should().BeSameAs(payload);

        contextManager.Verify().GetContextDataAsync<UserContext>(Compono.Match.Any<string>()).Once();
        contextManager.Verify().GetContextDataAsync<UpsellPayload>(Compono.Match.Any<string>()).Once();
    }

    // ADR-0045's deterministic-default branch, composed with the new bucket mechanism: an unconfigured
    // closed T on a nullable-return member returns the real default (null), not a throw - and never
    // leaks another closed T's configured value.
    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public async Task Unconfigured_closed_T_on_nullable_return_member_returns_null_not_configured_sibling_value(
        [Shared] IContextManager contextManager)
    {
        contextManager.Configure().GetContextDataAsync<UserContext>(Compono.Match.Any<string>())
            .Returns(Task.FromResult<UserContext?>(new UserContext("sub-1")));

        var unconfigured = await contextManager.GetContextDataAsync<UpsellPayload>("upsell");

        unconfigured.Should().BeNull();
    }

    // ADR-0045's configuration-required branch, on the same bucket mechanism: an unconfigured closed T
    // on a non-nullable-return member throws TestDoubleNotConfiguredException.
    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public async Task Unconfigured_closed_T_on_non_nullable_return_member_throws_TestDoubleNotConfiguredException(
        [Shared] IContextManager contextManager)
    {
        var act = async () => await contextManager.GetRequiredDataAsync<UserContext>("user");

        await act.Should().ThrowAsync<TestDoubleNotConfiguredException>();
    }

    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public async Task Configured_closed_T_on_non_nullable_return_member_returns_the_configured_value(
        [Shared] IContextManager contextManager)
    {
        var user = new UserContext("sub-1");
        contextManager.Configure().GetRequiredDataAsync<UserContext>(Compono.Match.Any<string>()).Returns(Task.FromResult(user));

        (await contextManager.GetRequiredDataAsync<UserContext>("user")).Should().BeSameAs(user);
    }

    // Argument mismatch against a correctly-configured T falls through to that same T's own
    // default/configuration-required behavior - not the configured value, and not another T's state.
    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public async Task Argument_mismatch_against_a_configured_closed_T_falls_through_to_its_own_default(
        [Shared] IContextManager contextManager)
    {
        contextManager.Configure()
            .GetContextDataAsync<UserContext>(Compono.Match.Is<string>(key => key == "user"))
            .Returns(Task.FromResult<UserContext?>(new UserContext("sub-1")));

        var wrongKey = await contextManager.GetContextDataAsync<UserContext>("not-user");

        wrongKey.Should().BeNull();
    }

    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public async Task Argument_mismatch_against_a_configuration_required_closed_T_still_throws(
        [Shared] IContextManager contextManager)
    {
        contextManager.Configure()
            .GetRequiredDataAsync<UserContext>(Compono.Match.Is<string>(key => key == "user"))
            .Returns(Task.FromResult(new UserContext("sub-1")));

        var act = async () => await contextManager.GetRequiredDataAsync<UserContext>("not-user");

        await act.Should().ThrowAsync<TestDoubleNotConfiguredException>();
    }

    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public async Task Match_Any_and_literal_matchers_work_against_the_real_non_T_parameter(
        [Shared] IContextManager contextManager)
    {
        contextManager.Configure().GetContextDataAsync<UserContext>("user")
            .Returns(Task.FromResult<UserContext?>(new UserContext("sub-1")));

        var matched = await contextManager.GetContextDataAsync<UserContext>("user");
        var unmatched = await contextManager.GetContextDataAsync<UserContext>("other");

        matched.Should().NotBeNull();
        unmatched.Should().BeNull();
    }

    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public async Task Once_Never_and_Exactly_work_per_closed_T_independent_of_another_Ts_call_count(
        [Shared] IContextManager contextManager)
    {
        contextManager.Configure().GetContextDataAsync<UserContext>(Compono.Match.Any<string>())
            .Returns(Task.FromResult<UserContext?>(new UserContext("sub-1")));
        contextManager.Configure().GetContextDataAsync<UpsellPayload>(Compono.Match.Any<string>())
            .Returns(Task.FromResult<UpsellPayload?>(new UpsellPayload("prod-1")));

        await contextManager.GetContextDataAsync<UserContext>("user");
        await contextManager.GetContextDataAsync<UserContext>("user");

        contextManager.Verify().GetContextDataAsync<UserContext>(Compono.Match.Any<string>()).Exactly(2);
        contextManager.Verify().GetContextDataAsync<UpsellPayload>(Compono.Match.Any<string>()).Never();
    }

    // Regression: an interface containing both a closed-instantiation-eligible member and ordinary
    // members generates a real double for every member, not just the new shape - the
    // whole-interface-Failure() consequence ADR-0049's Context section named is actually gone.
    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public async Task Sibling_ordinary_members_are_unaffected_by_the_closed_instantiation_eligible_member(
        [Shared] IContextManager contextManager)
    {
        contextManager.Configure().CurrentState().Returns("idle");
        contextManager.Configure().GetContextDataAsync<UserContext>(Compono.Match.Any<string>())
            .Returns(Task.FromResult<UserContext?>(new UserContext("sub-1")));

        contextManager.CurrentState.Should().Be("idle");
        await contextManager.TransitionAsync("active");
        (await contextManager.GetContextDataAsync<UserContext>("user")).Should().NotBeNull();

        contextManager.Verify().TransitionAsync(Compono.Match.Any<string>()).Once();
    }
}

// Regression/composition: an overloaded closed-instantiation-eligible member - Configure<T>()/
// Verify<T>() on each overload only affects that overload's own bucket, proven with the *same* closed
// T used on both overloads to rule out any cross-overload bucket-key collision, not just
// different-T isolation. Proves ADR-0044's overload-discriminator mechanism and ADR-0049's
// bucket-by-closed-T mechanism compose correctly.
public sealed class OverloadedClosedInstantiationTests
{
    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public async Task Same_closed_T_on_both_overloads_keeps_fully_independent_configured_state(
        [Shared] IOverloadedContextManager contextManager)
    {
        var v1 = new UpsellPayload("v1");
        var v2 = new UpsellPayload("v2");

        contextManager.Configure().GetDataAsync<UpsellPayload>("id").Returns(Task.FromResult<UpsellPayload?>(v1));
        contextManager.Configure().GetDataAsync<UpsellPayload>("id", 2).Returns(Task.FromResult<UpsellPayload?>(v2));

        var resolvedV1 = await contextManager.GetDataAsync<UpsellPayload>("id");
        var resolvedV2 = await contextManager.GetDataAsync<UpsellPayload>("id", 2);

        resolvedV1.Should().BeSameAs(v1);
        resolvedV2.Should().BeSameAs(v2);

        contextManager.Verify().GetDataAsync<UpsellPayload>("id").Once();
        contextManager.Verify().GetDataAsync<UpsellPayload>("id", 2).Once();
    }

    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public async Task Different_closed_Ts_on_the_same_overload_stay_independent(
        [Shared] IOverloadedContextManager contextManager)
    {
        contextManager.Configure().GetDataAsync<UserContext>("id").Returns(Task.FromResult<UserContext?>(new UserContext("sub-1")));

        var resolvedUser = await contextManager.GetDataAsync<UserContext>("id");
        var resolvedPayload = await contextManager.GetDataAsync<UpsellPayload>("id");

        resolvedUser.Should().NotBeNull();
        resolvedPayload.Should().BeNull();
    }
}
