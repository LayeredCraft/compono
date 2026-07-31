using NSubstitute.Core;

namespace Compono.NSubstitute.Tests;

/// <summary>
/// End-to-end composition through a real <see cref="Composer"/>/<see cref="CompositionRow"/>,
/// proving <c>UseNSubstitute()</c>'s <see cref="NSubstituteProvider"/> registration against the whole
/// pipeline rather than in isolation - PLAN-0005 Phase 2. The interface/delegate/abstract-class
/// positive and negative shapes are already covered by <see cref="NSubstituteProviderTests"/> and
/// <see cref="CompositionBuilderExtensionsTests"/>; this file covers the one scenario those don't:
/// this plan's own Goal-section shared-substitute-reuse shape, run against the real
/// <see cref="NSubstituteProvider"/> rather than a hand-written fake (Phase 0's
/// <c>CompositionValueProviderTests.SharedRequest_SatisfiedByAPublicProvider_IsReusedByALaterOrdinaryRequest</c>
/// already covers the general mechanism with a fake).
/// </summary>
public sealed class EndToEndCompositionTests
{
    [Fact]
    public void SharedSubstitute_EstablishedOnARow_IsReusedByALaterOrdinaryRequestForTheSameType()
    {
        var composer = Composer.Create(builder => builder.UseNSubstitute());
        var row = composer.CreateRow(typeof(EndToEndCompositionTests));

        // The [Shared] test-method-parameter request - what Compono.XunitV3's [Shared] attribute
        // itself compiles down to.
        var sharedDescriptor = new CompositionRequestDescriptor(
            CompositionRequestKind.TestParameter, ordinal: 0, name: "repository", declaringType: typeof(EndToEndCompositionTests), Nullability.NotNullable);
        var repositoryFromSharedRequest = row.ResolveShared<IRepository>(sharedDescriptor);

        // An ordinary, unmarked nested constructor-parameter request for the same type - what a SUT's
        // own generated plan compiles down to when it needs an IRepository - reuses the shared value
        // transparently (ADR-0021), with no [Shared] marker of its own.
        var nestedDescriptor = new CompositionRequestDescriptor(
            CompositionRequestKind.ConstructorParameter, ordinal: 0, name: "repository", declaringType: typeof(Handler), Nullability.NotNullable);
        var repositoryFromNestedRequest = row.Resolve<IRepository>(nestedDescriptor);

        repositoryFromNestedRequest.Should().BeSameAs(repositoryFromSharedRequest);
        repositoryFromSharedRequest.Should().BeAssignableTo<ICallRouterProvider>();
    }

    public interface IRepository;

    public sealed class Handler
    {
        public Handler(IRepository repository)
        {
            Repository = repository;
        }

        public IRepository Repository { get; }
    }
}
