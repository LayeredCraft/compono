using System.Collections.Generic;
using Compono;
using Compono.XunitV3.Aot;
using Xunit;

namespace Compono.XunitV3.Aot.Tests;

// Native AOT counterpart of Compono.XunitV3.Tests/DocumentedRecipeTests.cs: the RESEARCH-0040 (#161
// 1.4 dogfood follow-up) recipes documented instead of new API - finding 2 (a ComposeData row's
// TConfig, registered by its profile, composes into a test parameter; row values bind to the leading
// parameters) and finding 6 (profile constructor arguments through a parameterless wrapper profile, or
// a single-row ComposeData). Run under the real xUnit Native AOT runner.

public sealed record RetentionWindow(int DaysBefore, int DaysAfter);

public sealed class RetentionPolicy
{
    private readonly RetentionWindow _window;

    public RetentionPolicy(RetentionWindow window) => _window = window;

    public bool Keeps(int ageInDays) => ageInDays >= -_window.DaysBefore && ageInDays <= _window.DaysAfter;
}

public sealed class RetentionProfile : ICompositionProfile
{
    private readonly RetentionWindow _window;

    public RetentionProfile(RetentionWindow window) => _window = window;

    public void Configure(CompositionBuilder builder) => builder.Register(() => _window);
}

public sealed class RecipeAuditLog
{
    public List<string> Entries { get; } = [];
}

public sealed class AuditInterceptor
{
    public AuditInterceptor(RecipeAuditLog log) => Log = log;

    public RecipeAuditLog Log { get; }
}

public sealed class RecipeRepository
{
    private readonly AuditInterceptor? _interceptor;

    public RecipeRepository(AuditInterceptor? interceptor) => _interceptor = interceptor;

    public void Save(string id) => _interceptor?.Log.Entries.Add($"saved {id}");
}

public sealed class RecipeDbProfile : ICompositionProfile
{
    private readonly AuditInterceptor? _interceptor;

    public RecipeDbProfile(AuditInterceptor? interceptor) => _interceptor = interceptor;

    public void Configure(CompositionBuilder builder) =>
        builder.Register(() => new RecipeRepository(_interceptor));
}

public sealed class AuditedRecipeDbProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder)
    {
        var log = new RecipeAuditLog();
        builder.Register(() => log);
        builder.AddProfile(new RecipeDbProfile(new AuditInterceptor(log)));
    }
}

public sealed record AuditConfig(AuditInterceptor Interceptor);

public sealed class ConfiguredRecipeDbProfile : ICompositionProfile
{
    private readonly AuditConfig _config;

    public ConfiguredRecipeDbProfile(AuditConfig config) => _config = config;

    public void Configure(CompositionBuilder builder)
    {
        builder.Register(() => _config.Interceptor.Log);
        builder.AddProfile(new RecipeDbProfile(_config.Interceptor));
    }
}

public sealed class DocumentedRecipeTests
{
    public static TheoryData<RetentionWindow, int, bool> RetentionCases => new()
    {
        { new RetentionWindow(7, 30), 30, true },
        { new RetentionWindow(7, 30), 31, false },
        { new RetentionWindow(1, 2), -2, false },
    };

    [Theory]
    [ComposeData<RetentionProfile, RetentionWindow>(nameof(RetentionCases))]
    public void RowConfig_IsReadableInTheBody_ThroughAComposedParameter(
        int ageInDays,
        bool expected,
        RetentionWindow window,
        RetentionPolicy sut)
    {
        Assert.Equal(expected, sut.Keeps(ageInDays));
        Assert.Equal(expected, ageInDays >= -window.DaysBefore && ageInDays <= window.DaysAfter);
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
        Assert.Equal("first", label);
        Assert.Equal(1, count);
        Assert.True(sut.Keeps(window.DaysAfter));
    }

    [Theory]
    [Compose<AuditedRecipeDbProfile>]
    public void WrapperProfile_SuppliesProfileConstructorArguments(RecipeRepository sut, RecipeAuditLog log)
    {
        sut.Save("42");

        Assert.Equal(["saved 42"], log.Entries);
    }

    public static TheoryData<AuditConfig> AuditedVariant =>
        new() { new AuditConfig(new AuditInterceptor(new RecipeAuditLog())) };

    [Theory]
    [ComposeData<ConfiguredRecipeDbProfile, AuditConfig>(nameof(AuditedVariant))]
    public void SingleRowComposeData_SuppliesNonConstantProfileArguments(RecipeRepository sut, RecipeAuditLog log)
    {
        sut.Save("7");

        Assert.Equal(["saved 7"], log.Entries);
    }
}
