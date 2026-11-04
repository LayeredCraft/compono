namespace Compono.XunitV3.Tests;

// Real xUnit v3 runner coverage for RequireFixture<TFixture>() (ADR-0069/PLAN-0070). Every case here
// exercises the documented call site - reading a fixture from *inside* a profile registration factory
// (the lambda passed to CompositionBuilder.Register), not from Configure or a constructor - through a
// real [Theory, Compose<TProfile>] run, so what's proven is the actual xUnit pipeline wiring, not an
// in-process fake. The unit-level failure paths live in FixtureContextExtensionsTests.cs.
//
// Each composed parameter is a dedicated wrapper record registered explicitly by the profile, so the
// composed value can only have come from the fixture lookup (not from Compono's built-in providers).

public sealed class RequireFixtureClassFixture : IAsyncLifetime
{
    public string Value { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        // Deliberately completes asynchronously (after a yield), so the lookup in the registration
        // factory genuinely observes an already-initialized class fixture by the time composition runs.
        await Task.Yield();
        Value = "class-fixture-value";
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public sealed record ClassFixtureValue(string Value);

public sealed class ClassFixtureProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) =>
        builder.Register(() => new ClassFixtureValue(
            TestContext.Current.RequireFixture<RequireFixtureClassFixture>().Value));
}

public sealed class RequireFixtureClassFixtureTests : IClassFixture<RequireFixtureClassFixture>
{
    private readonly RequireFixtureClassFixture _fixture;

    public RequireFixtureClassFixtureTests(RequireFixtureClassFixture fixture) => _fixture = fixture;

    [Theory]
    [Compose<ClassFixtureProfile>]
    public void ClassFixture_IsReadableFromARegistrationFactory(ClassFixtureValue composed)
    {
        composed.Value.Should().Be(_fixture.Value);
        composed.Value.Should().Be("class-fixture-value");
    }
}

public sealed class RequireFixtureCollectionFixture
{
    public string Value => "collection-fixture-value";
}

[CollectionDefinition("RequireFixtureCollection")]
public sealed class RequireFixtureCollectionDefinition : ICollectionFixture<RequireFixtureCollectionFixture>
{
}

public sealed record CollectionFixtureValue(string Value);

public sealed class CollectionFixtureProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) =>
        builder.Register(() => new CollectionFixtureValue(
            TestContext.Current.RequireFixture<RequireFixtureCollectionFixture>().Value));
}

[Collection("RequireFixtureCollection")]
public sealed class RequireFixtureCollectionFixtureTests
{
    [Theory]
    [Compose<CollectionFixtureProfile>]
    public void CollectionFixture_IsReadableFromARegistrationFactory(CollectionFixtureValue composed)
    {
        composed.Value.Should().Be("collection-fixture-value");
    }
}

public sealed record AssemblyFixtureValue(string Value);

public sealed class AssemblyFixtureProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) =>
        builder.Register(() => new AssemblyFixtureValue(
            TestContext.Current.RequireFixture<RequireFixtureAssemblyFixture>().Value));
}

public sealed class RequireFixtureAssemblyFixtureTests
{
    [Theory]
    [Compose<AssemblyFixtureProfile>]
    public void AssemblyFixture_IsReadableFromARegistrationFactory(AssemblyFixtureValue composed)
    {
        composed.Value.Should().Be("assembly-fixture-value");
    }
}

// Parallel isolation: four classes each declare their own IClassFixture<RequireFixtureIdFixture>, and
// each fixture assigns a unique Id at construction (static Interlocked counter). Parallelization is
// left at xUnit's default (these are four separate implicit collections, so they run concurrently).
// Each test asserts the composed Id equals its *own* class's ctor-injected fixture Id - which can only
// hold if RequireFixture resolves against the running test's context rather than a process-global
// lookup.
public sealed class RequireFixtureIdFixture
{
    private static int _next;

    public RequireFixtureIdFixture() => Id = Interlocked.Increment(ref _next);

    public int Id { get; }
}

public sealed record FixtureId(int Value);

public sealed class IdFixtureProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) =>
        builder.Register(() => new FixtureId(
            TestContext.Current.RequireFixture<RequireFixtureIdFixture>().Id));
}

public sealed class RequireFixtureIsolationTests1 : IClassFixture<RequireFixtureIdFixture>
{
    private readonly RequireFixtureIdFixture _fixture;

    public RequireFixtureIsolationTests1(RequireFixtureIdFixture fixture) => _fixture = fixture;

    [Theory]
    [Compose<IdFixtureProfile>]
    public void ComposedId_MatchesThisClassesOwnFixture(FixtureId composed) =>
        composed.Value.Should().Be(_fixture.Id);
}

public sealed class RequireFixtureIsolationTests2 : IClassFixture<RequireFixtureIdFixture>
{
    private readonly RequireFixtureIdFixture _fixture;

    public RequireFixtureIsolationTests2(RequireFixtureIdFixture fixture) => _fixture = fixture;

    [Theory]
    [Compose<IdFixtureProfile>]
    public void ComposedId_MatchesThisClassesOwnFixture(FixtureId composed) =>
        composed.Value.Should().Be(_fixture.Id);
}

public sealed class RequireFixtureIsolationTests3 : IClassFixture<RequireFixtureIdFixture>
{
    private readonly RequireFixtureIdFixture _fixture;

    public RequireFixtureIsolationTests3(RequireFixtureIdFixture fixture) => _fixture = fixture;

    [Theory]
    [Compose<IdFixtureProfile>]
    public void ComposedId_MatchesThisClassesOwnFixture(FixtureId composed) =>
        composed.Value.Should().Be(_fixture.Id);
}

public sealed class RequireFixtureIsolationTests4 : IClassFixture<RequireFixtureIdFixture>
{
    private readonly RequireFixtureIdFixture _fixture;

    public RequireFixtureIsolationTests4(RequireFixtureIdFixture fixture) => _fixture = fixture;

    [Theory]
    [Compose<IdFixtureProfile>]
    public void ComposedId_MatchesThisClassesOwnFixture(FixtureId composed) =>
        composed.Value.Should().Be(_fixture.Id);
}

public sealed class RequireFixtureUndeclaredFixture
{
}

// The missing-fixture path is a plain [Fact] in a class that declares no fixtures at all - it calls
// RequireFixture directly (not through composition) and asserts the M3 declaration guidance, rather
// than adding a deliberately-failing composed theory that would pollute the suite's pass/fail signal.
public sealed class RequireFixtureMissingFixtureTests
{
    [Fact]
    public void RequireFixture_ThrowsWithDeclarationGuidance_WhenNoFixtureOfTheExactTypeIsDeclared()
    {
        var act = () => TestContext.Current.RequireFixture<RequireFixtureUndeclaredFixture>();

        var exception = act.Should().Throw<InvalidOperationException>().Which;

        exception.Message.Should().Contain("has no fixture of exact type");
        exception.Message.Should().Contain(typeof(RequireFixtureUndeclaredFixture).FullName!);
        exception.Message.Should().Contain("IClassFixture<");
        exception.Message.Should().Contain("ICollectionFixture<");
        exception.Message.Should().Contain("AssemblyFixture");
        exception.Message.Should().Contain("exact type");
    }
}
