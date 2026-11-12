namespace Compono.XunitV3.Tests;

// Real-runner proof for the recipes RESEARCH-0040 (#161 1.4 dogfood follow-up) documents instead of
// adding API: finding 2 (read a ComposeData row's TConfig in the test body by registering it from the
// profile; row values bind to the leading parameters) and finding 6 (supply profile constructor
// arguments through a parameterless wrapper profile, or a single-row ComposeData). The same recipes
// are proven under the xUnit Native AOT runner in Compono.XunitV3.Aot.Tests/DocumentedRecipeTests.cs.

public sealed record RetentionWindow(int DaysBefore, int DaysAfter);

public sealed class RetentionPolicy(RetentionWindow window)
{
    public bool Keeps(int ageInDays) => ageInDays >= -window.DaysBefore && ageInDays <= window.DaysAfter;
}

// Finding 2: the profile already receives the row's config - registering it makes it composable.
public sealed class RetentionProfile(RetentionWindow window) : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) => builder.Register(() => window);
}

public sealed class RecipeAuditLog
{
    public List<string> Entries { get; } = [];
}

public sealed class AuditInterceptor(RecipeAuditLog log)
{
    public RecipeAuditLog Log { get; } = log;
}

public sealed class RecipeRepository(AuditInterceptor? interceptor)
{
    public void Save(string id) => interceptor?.Log.Entries.Add($"saved {id}");
}

// Finding 6: a profile that takes runtime constructor arguments...
public sealed class RecipeDbProfile(AuditInterceptor? interceptor) : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) =>
        builder.Register(() => new RecipeRepository(interceptor));
}

// ...named in [Compose<TProfile>] through a parameterless wrapper per variant.
public sealed class AuditedRecipeDbProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder)
    {
        var log = new RecipeAuditLog();
        builder.Register(() => log);
        builder.AddProfile(new RecipeDbProfile(new AuditInterceptor(log)));
    }
}

public sealed class DocumentedRecipeTests
{
    public static TheoryData<RetentionWindow, int, bool> RetentionCases => new()
    {
        { new RetentionWindow(DaysBefore: 7, DaysAfter: 30), 30, true },
        { new RetentionWindow(DaysBefore: 7, DaysAfter: 30), 31, false },
        { new RetentionWindow(DaysBefore: 1, DaysAfter: 2), -2, false },
    };

    [Theory]
    [ComposeData<RetentionProfile, RetentionWindow>(nameof(RetentionCases))]
    public void RowConfig_IsReadableInTheBody_ThroughAComposedParameter(
        int ageInDays,
        bool expected,
        RetentionWindow window,
        RetentionPolicy sut)
    {
        sut.Keeps(ageInDays).Should().Be(expected);
        window.DaysAfter.Should().BeOneOf(30, 2);
        expected.Should().Be(ageInDays >= -window.DaysBefore && ageInDays <= window.DaysAfter);
    }

    // Element 0 is the row's config; "first" and 1 bind to the two leading parameters.
    public static readonly object?[][] ConfiguredOrderedRows = [[new RetentionWindow(3, 4), "first", 1]];

    [Theory]
    [ComposeData<RetentionProfile, RetentionWindow>(nameof(ConfiguredOrderedRows))]
    public void RowValues_BindToTheLeadingParameters_BeforeSharedAndComposedOnes(
        string label,
        int count,
        [Shared] RetentionWindow window,
        RetentionPolicy sut)
    {
        label.Should().Be("first");
        count.Should().Be(1);
        sut.Keeps(window.DaysAfter).Should().BeTrue();
    }

    [Theory, Compose<AuditedRecipeDbProfile>]
    public void WrapperProfile_SuppliesProfileConstructorArguments(RecipeRepository sut, RecipeAuditLog log)
    {
        sut.Save("42");

        log.Entries.Should().Equal("saved 42");
    }

    public sealed record AuditConfig(AuditInterceptor Interceptor);

    public sealed class ConfiguredRecipeDbProfile(AuditConfig config) : ICompositionProfile
    {
        public void Configure(CompositionBuilder builder)
        {
            builder.Register(() => config.Interceptor.Log);
            builder.AddProfile(new RecipeDbProfile(config.Interceptor));
        }
    }

    public static TheoryData<AuditConfig> AuditedVariant =>
        new() { new AuditConfig(new AuditInterceptor(new RecipeAuditLog())) };

    [Theory]
    [ComposeData<ConfiguredRecipeDbProfile, AuditConfig>(nameof(AuditedVariant))]
    public void SingleRowComposeData_SuppliesNonConstantProfileArguments(RecipeRepository sut, RecipeAuditLog log)
    {
        sut.Save("7");

        log.Entries.Should().Equal("saved 7");
    }
}
