using Compono.XunitV3;

namespace Compono.TestDoubles.SampleTests;

// ADR-0046's own central acceptance case, through the real packaged Compono -> Compono.Generators
// -> Compono.TestDoubles dependency chain: a static abstract member declared on a BASE interface,
// already resolved by a MORE-DERIVED interface in the closure providing a concrete implementation
// (C#'s own "most specific implementation" rule for static interface members), is not an
// unimplemented requirement at all - the leaf interface generates a fully normal double, every
// instance member unaffected. IAmazonS3-shaped: AWSSDK's IAmazonS3 re-implements its base
// IAmazonService.CreateDefaultClientConfig() concretely, even though IAmazonService itself only
// declares it abstract - this is that same general Roslyn/interface-inheritance shape, reduced to
// a minimal repro.
public interface IProfileFactory
{
    static abstract IProfileFactory CreateDefault();
}

public interface IProfileRepositoryWithStaticAbstractBase : IProfileFactory
{
    static IProfileFactory IProfileFactory.CreateDefault() =>
        throw new NotSupportedException("real production implementation, never invoked in a test");

    string GetName();
}

public sealed class StaticAbstractMemberTests
{
    // The double resolves and works exactly as if IProfileFactory.CreateDefault() didn't exist -
    // no CMP0021 whole-interface rejection, no throwing stub, no new exception type. Composer.Create
    // succeeding at all is itself part of the assertion: before ADR-0046's fix, this leaf's static
    // abstract member (inherited from IProfileFactory) was incorrectly whole-interface-rejected,
    // falling back to the ordinary runtime-provider path instead of resolving through the generated
    // double - Configure() would have thrown InvalidOperationException ("not the generated double
    // for this assembly") rather than reach TestDoubleNotConfiguredException.
    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public void Unconfigured_method_throws_TestDoubleNotConfiguredException(
        [Shared] IProfileRepositoryWithStaticAbstractBase repository)
    {
        var act = () => repository.GetName();

        act.Should().Throw<TestDoubleNotConfiguredException>();
    }

    [Theory]
    [Compose<GeneratedTestDoubleProfile>]
    public void Configured_method_returns_the_configured_value(
        [Shared] IProfileRepositoryWithStaticAbstractBase repository)
    {
        repository.Configure().GetName().Returns("Ada");

        repository.GetName().Should().Be("Ada");
    }
}
