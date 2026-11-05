using Compono.XunitV3.Aot;
using Xunit;

namespace Compono.XunitV3.Aot.Tests;

// Real xUnit v3 runner coverage for RequireFixture<TFixture>() under the AOT package
// (ADR-0069/PLAN-0070). This project opts out of the global Xunit/NSubstitute/AwesomeAssertions
// usings (see Compono.XunitV3.Aot.Tests.csproj), so it uses `using Xunit;` and plain Assert.
//
// Deliberately omits the assembly-fixture case: xUnit's own AOT source generator emits invalid code
// (CS1593) for [assembly: AssemblyFixture(...)] as of xUnit 4.0.1/4.0.2 (upstream xunit/xunit#3664,
// fixed in 4.0.3-pre.19). That is a defect in xUnit's generator, not Compono - class and collection
// fixtures are fully supported and covered below. Assembly-fixture coverage lives in the JIT project
// (Compono.XunitV3.Tests/RequireFixtureIntegrationTests.cs).

public sealed class RequireFixtureClassFixture : IAsyncLifetime
{
    public string Value { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
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
        Assert.Equal(_fixture.Value, composed.Value);
        Assert.Equal("class-fixture-value", composed.Value);
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
        Assert.Equal("collection-fixture-value", composed.Value);
    }
}

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
        Assert.Equal(_fixture.Id, composed.Value);
}

public sealed class RequireFixtureIsolationTests2 : IClassFixture<RequireFixtureIdFixture>
{
    private readonly RequireFixtureIdFixture _fixture;

    public RequireFixtureIsolationTests2(RequireFixtureIdFixture fixture) => _fixture = fixture;

    [Theory]
    [Compose<IdFixtureProfile>]
    public void ComposedId_MatchesThisClassesOwnFixture(FixtureId composed) =>
        Assert.Equal(_fixture.Id, composed.Value);
}

public sealed class RequireFixtureIsolationTests3 : IClassFixture<RequireFixtureIdFixture>
{
    private readonly RequireFixtureIdFixture _fixture;

    public RequireFixtureIsolationTests3(RequireFixtureIdFixture fixture) => _fixture = fixture;

    [Theory]
    [Compose<IdFixtureProfile>]
    public void ComposedId_MatchesThisClassesOwnFixture(FixtureId composed) =>
        Assert.Equal(_fixture.Id, composed.Value);
}

public sealed class RequireFixtureIsolationTests4 : IClassFixture<RequireFixtureIdFixture>
{
    private readonly RequireFixtureIdFixture _fixture;

    public RequireFixtureIsolationTests4(RequireFixtureIdFixture fixture) => _fixture = fixture;

    [Theory]
    [Compose<IdFixtureProfile>]
    public void ComposedId_MatchesThisClassesOwnFixture(FixtureId composed) =>
        Assert.Equal(_fixture.Id, composed.Value);
}

public sealed class RequireFixtureUndeclaredFixture
{
}

public sealed class RequireFixtureMissingFixtureTests
{
    [Fact]
    public void RequireFixture_ThrowsWithDeclarationGuidance_WhenNoFixtureOfTheExactTypeIsDeclared()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => TestContext.Current.RequireFixture<RequireFixtureUndeclaredFixture>());

        Assert.Contains("has no fixture of exact type", exception.Message);
        Assert.Contains(typeof(RequireFixtureUndeclaredFixture).FullName!, exception.Message);
        Assert.Contains("IClassFixture<", exception.Message);
        Assert.Contains("ICollectionFixture<", exception.Message);
        Assert.Contains("AssemblyFixture", exception.Message);
        Assert.Contains("exact type", exception.Message);
    }
}
