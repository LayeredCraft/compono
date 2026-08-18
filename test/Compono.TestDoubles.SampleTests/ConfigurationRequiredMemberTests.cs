using Compono.XunitV3;

namespace Compono.TestDoubles.SampleTests;

// ADR-0045's own central acceptance case, through the real packaged Compono -> Compono.Generators
// -> Compono.TestDoubles dependency chain: a member with no deterministic default return no longer
// rejects the whole interface - it generates as configuration-required instead, throwing
// TestDoubleNotConfiguredException if invoked before Configure().Member(...).Returns(...)/
// .Throws(...) is called. Covers every shape ADR-0045 scopes: a synchronous method, a property
// (IOptions<T>.Value/ILambdaContext.AwsRequestId-shaped), Task<T>/ValueTask<T> async members (the
// ADR's own "no separate implementation needed" hypothesis, proved here rather than assumed), and a
// fluent self-returning member (IResponseBuilder-shaped, this ADR's central "no special-casing"
// decision - RESEARCH-0004's own motivating example).
public interface IProfileRepository
{
    string GetName();

    string Description { get; }

    Task<string> GetNameAsync();

    ValueTask<string> GetDescriptionAsync();

    IProfileRepository Self();

    int ViewCount { get; }

    bool IsActive { get; }

    string? Nickname { get; }

    IReadOnlyList<string> Tags();

    Task<int> GetScoreAsync();
}

public sealed class ConfigurationRequiredMemberTests
{
    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public void Unconfigured_method_throws_TestDoubleNotConfiguredException(
        [Shared] IProfileRepository repository)
    {
        var act = () => repository.GetName();

        act.Should().Throw<TestDoubleNotConfiguredException>();
    }

    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public void Configured_method_returns_the_configured_value(
        [Shared] IProfileRepository repository)
    {
        repository.Configure().GetName().Returns("Ada");

        repository.GetName().Should().Be("Ada");
    }

    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public void Method_configured_to_throw_throws_the_configured_exception(
        [Shared] IProfileRepository repository)
    {
        repository.Configure().GetName().Throws(new InvalidOperationException("boom"));

        var act = () => repository.GetName();

        act.Should().Throw<InvalidOperationException>().WithMessage("boom");
    }

    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public void Unconfigured_property_throws_TestDoubleNotConfiguredException(
        [Shared] IProfileRepository repository)
    {
        var act = () => repository.Description;

        act.Should().Throw<TestDoubleNotConfiguredException>();
    }

    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public void Configured_property_returns_the_configured_value(
        [Shared] IProfileRepository repository)
    {
        repository.Configure().Description().Returns("a test double");

        repository.Description.Should().Be("a test double");
    }

    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public void Property_configured_to_throw_throws_the_configured_exception(
        [Shared] IProfileRepository repository)
    {
        repository.Configure().Description().Throws(new InvalidOperationException("boom"));

        var act = () => repository.Description;

        act.Should().Throw<InvalidOperationException>().WithMessage("boom");
    }

    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public async Task Unconfigured_Task_of_T_method_throws_TestDoubleNotConfiguredException(
        [Shared] IProfileRepository repository)
    {
        var act = async () => await repository.GetNameAsync();

        await act.Should().ThrowAsync<TestDoubleNotConfiguredException>();
    }

    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public async Task Configured_Task_of_T_method_returns_the_configured_value(
        [Shared] IProfileRepository repository)
    {
        repository.Configure().GetNameAsync().Returns(Task.FromResult("Ada"));

        (await repository.GetNameAsync()).Should().Be("Ada");
    }

    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public async Task Task_of_T_method_configured_to_throw_throws_the_configured_exception(
        [Shared] IProfileRepository repository)
    {
        repository.Configure().GetNameAsync().Throws(new InvalidOperationException("boom"));

        var act = async () => await repository.GetNameAsync();

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");
    }

    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public async Task Unconfigured_ValueTask_of_T_method_throws_TestDoubleNotConfiguredException(
        [Shared] IProfileRepository repository)
    {
        var act = async () => await repository.GetDescriptionAsync();

        await act.Should().ThrowAsync<TestDoubleNotConfiguredException>();
    }

    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public async Task Configured_ValueTask_of_T_method_returns_the_configured_value(
        [Shared] IProfileRepository repository)
    {
        repository.Configure().GetDescriptionAsync().Returns(new ValueTask<string>("a test double"));

        (await repository.GetDescriptionAsync()).Should().Be("a test double");
    }

    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public async Task ValueTask_of_T_method_configured_to_throw_throws_the_configured_exception(
        [Shared] IProfileRepository repository)
    {
        repository.Configure().GetDescriptionAsync().Throws(new InvalidOperationException("boom"));

        var act = async () => await repository.GetDescriptionAsync();

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");
    }

    // ADR-0045's central "no special-casing" decision: a fluent self-returning member is treated
    // as an ordinary non-nullable-reference-return member, configuration-required like any other -
    // Returns(self) works for a chained-call test, same as any other reference-typed value.
    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public void Unconfigured_fluent_self_return_throws_TestDoubleNotConfiguredException(
        [Shared] IProfileRepository repository)
    {
        var act = () => repository.Self();

        act.Should().Throw<TestDoubleNotConfiguredException>();
    }

    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public void Configured_fluent_self_return_returns_the_configured_self_instance(
        [Shared] IProfileRepository repository)
    {
        repository.Configure().Self().Returns(repository);

        repository.Self().Should().BeSameAs(repository);
        repository.Self().Self().Should().BeSameAs(repository);
    }

    // PLAN-0045 Phase 1's own regression case: every already-shipped deterministic-default shape
    // (bool, int, nullable reference, a known collection shape, Task<T> over a value type) on the
    // same interface as configuration-required members is completely unaffected - each returns its
    // own computed default unconfigured, with no interaction between the dispatch paths.
    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public async Task Deterministic_default_members_are_unaffected_by_sibling_configuration_required_members(
        [Shared] IProfileRepository repository)
    {
        repository.ViewCount.Should().Be(0);
        repository.IsActive.Should().BeFalse();
        repository.Nickname.Should().BeNull();
        repository.Tags().Should().BeEmpty();
        (await repository.GetScoreAsync()).Should().Be(0);

        var act = () => repository.GetName();

        act.Should().Throw<TestDoubleNotConfiguredException>();
    }
}
