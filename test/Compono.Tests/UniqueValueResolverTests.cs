namespace Compono.Tests;

public sealed class UniqueValueResolverTests
{
    [Fact]
    public void TryResolve_ReturnsValueAndAddsIt_WhenFirstAttemptIsUnique()
    {
        var context = new StubContext(_ => "unique");
        var alreadyResolved = new HashSet<string>();

        var succeeded = UniqueValueResolver.TryResolve(
            context, CompositionRequestKind.CollectionElement, position: 0, Nullability.NotNullable, alreadyResolved, out var value);

        succeeded.Should().BeTrue();
        value.Should().Be("unique");
        alreadyResolved.Should().Contain("unique");
    }

    [Fact]
    public void TryResolve_RetriesWithDistinctForks_UntilAUniqueValueIsProduced()
    {
        var callCount = 0;
        var context = new StubContext(_ => callCount++ < 2 ? "duplicate" : "fresh");
        var alreadyResolved = new HashSet<string> { "duplicate" };

        var succeeded = UniqueValueResolver.TryResolve(
            context, CompositionRequestKind.CollectionElement, position: 0, Nullability.NotNullable, alreadyResolved, out var value);

        succeeded.Should().BeTrue();
        value.Should().Be("fresh");
    }

    [Fact]
    public void TryResolve_ReturnsFalse_WhenMaxAttemptsExhaustedWithoutAUniqueValue()
    {
        var context = new StubContext(_ => "always-duplicate");
        var alreadyResolved = new HashSet<string> { "always-duplicate" };

        var succeeded = UniqueValueResolver.TryResolve(
            context, CompositionRequestKind.CollectionElement, position: 0, Nullability.NotNullable, alreadyResolved, out _);

        succeeded.Should().BeFalse();
    }

    [Fact]
    public void TryResolve_UsesDistinctOrdinalPerAttempt_SoRetriesAreDeterministicNotRandom()
    {
        var seenOrdinals = new List<int>();
        var context = new StubContext(ordinal =>
        {
            seenOrdinals.Add(ordinal);
            return "always-duplicate";
        });
        var alreadyResolved = new HashSet<string> { "always-duplicate" };

        UniqueValueResolver.TryResolve(
            context, CompositionRequestKind.CollectionElement, position: 2, Nullability.NotNullable, alreadyResolved, out _);

        seenOrdinals.Should().HaveCount(UniqueValueResolver.MaxAttempts);
        seenOrdinals.Distinct().Should().HaveCount(UniqueValueResolver.MaxAttempts);
    }

    private sealed class StubContext(Func<int, string> valueForOrdinal) : ICompositionContext
    {
        public TValue Resolve<TValue>(in CompositionRequestDescriptor descriptor) =>
            (TValue)(object)valueForOrdinal(descriptor.Ordinal);

        public TValue Resolve<TValue>() => throw new NotSupportedException("Not exercised by these tests.");

        public int DeriveSeed() => throw new NotSupportedException("Not exercised by these tests.");

        public int ResolveCollectionSize() => throw new NotSupportedException("Not exercised by these tests.");
    }
}
