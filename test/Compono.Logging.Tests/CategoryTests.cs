using Microsoft.Extensions.Logging;

namespace Compono.Logging.Tests;

public sealed class CategoryTests
{
    [Fact]
    public void GenericLogger_CategoryMatchesLoggerFactory_ForPlainNamespacedType()
    {
        AssertCategoryMatchesFactory<PlainCategoryType>();
    }

    [Fact]
    public void GenericLogger_CategoryMatchesLoggerFactory_ForNestedType()
    {
        AssertCategoryMatchesFactory<OuterCategoryType.InnerCategoryType>();
    }

    [Fact]
    public void GenericLogger_CategoryMatchesLoggerFactory_ForNestedTypeInNestedType()
    {
        AssertCategoryMatchesFactory<OuterCategoryType.InnerCategoryType.DeepestCategoryType>();
    }

    [Fact]
    public void GenericLogger_CategoryMatchesLoggerFactory_ForClosedGenericType()
    {
        AssertCategoryMatchesFactory<GenericCategoryType<int>>();
    }

    [Fact]
    public void GenericLogger_CategoryMatchesLoggerFactory_ForNestedTypeInClosedGenericType()
    {
        AssertCategoryMatchesFactory<GenericOuterCategoryType<int>.NestedCategoryType>();
    }

    [Fact]
    public void GenericLogger_CategoryMatchesLoggerFactory_ForFrameworkGenericType()
    {
        AssertCategoryMatchesFactory<List<OuterCategoryType.InnerCategoryType>>();
    }

    [Fact]
    public void GenericLogger_CategoryMatchesLoggerFactory_ForGlobalNamespaceType()
    {
        AssertCategoryMatchesFactory<GlobalCategoryType>();
    }

    [Fact]
    public void GenericLogger_CategoryMatchesLoggerFactory_ForNestedTypeInGlobalNamespaceType()
    {
        AssertCategoryMatchesFactory<GlobalCategoryType.NestedCategoryType>();
    }

    [Fact]
    public void GenericLogger_CategoryMatchesLoggerFactory_ForMultiArgumentGenericType()
    {
        AssertCategoryMatchesFactory<Dictionary<string, int>>();
        AssertCategoryMatchesFactory<(int, string)>();
    }

    [Fact]
    public void GenericLogger_CategoryMatchesLoggerFactory_ForBuiltInTypes()
    {
        AssertCategoryMatchesFactory<int>();
        AssertCategoryMatchesFactory<string>();
        AssertCategoryMatchesFactory<object>();
        AssertCategoryMatchesFactory<bool>();
    }

    [Fact]
    public void GenericLogger_CategoryMatchesLoggerFactory_ForArrayType()
    {
        AssertCategoryMatchesFactory<int[]>();
    }

    [Fact]
    public void ComposedGenericLogger_CategoryMatchesLoggerFactory()
    {
        var expected = TestCategoryProbe.For<OrderService>();
        var composer = Composer.Create(builder => builder.UseLogging());

        var service = composer.Create<OrderService>();
        service.PlaceOrder(42);

        var entry = service.Logger.GetLastCapturedEntry()!.Value;
        entry.Category.Should().Be(expected);
    }

    [Fact]
    public void BareCapturingLogger_CategoryIsEmpty()
    {
        var logger = new CapturingLogger();

        logger.LogInformation("captured");

        logger.GetCapturedEntries()[0].Category.Should().BeEmpty();
    }

    [Fact]
    public void ComposedBareILogger_CategoryIsEmpty()
    {
        var composer = Composer.Create(builder => builder.UseLogging());

        var logger = composer.Create<ILogger>();
        logger.LogInformation("captured");

        logger.GetCapturedEntries()[0].Category.Should().BeEmpty();
    }

    [Fact]
    public void BareCapturingLogger_InCategoryEmptyString_MatchesItsEntries()
    {
        var logger = new CapturingLogger();
        logger.LogInformation("captured");

        logger.Verify().InCategory(string.Empty).Once();
    }

    [Fact]
    public void Equality_IncludesCategory_IntentionalBehavioralChangeFromAdr0071()
    {
        // ADR-0071 deliberately makes Category part of CapturedLogEntry's record value equality:
        // otherwise two entries that differ only in which logger produced them would compare equal.
        // This pins that decision so a future "fix" cannot quietly drop Category from equality.
        var timestamp = new DateTimeOffset(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);

        var first = new CapturedLogEntry(
            "Category.A", LogLevel.Information, default, null, "same", null, null, null, [], timestamp);
        var second = new CapturedLogEntry(
            "Category.B", LogLevel.Information, default, null, "same", null, null, null, [], timestamp);
        var third = new CapturedLogEntry(
            "Category.A", LogLevel.Information, default, null, "same", null, null, null, [], timestamp);

        first.Equals(second).Should().BeFalse();
        (first == second).Should().BeFalse();
        (first != second).Should().BeTrue();

        first.Equals(third).Should().BeTrue();
        (first == third).Should().BeTrue();
        first.GetHashCode().Should().Be(third.GetHashCode());
    }

    private static void AssertCategoryMatchesFactory<TCategory>()
    {
        var expected = TestCategoryProbe.For<TCategory>();

        var logger = new CapturingLogger<TCategory>();
        logger.LogInformation("captured");

        var entries = logger.GetCapturedEntries();
        entries.Should().ContainSingle();
        entries[0].Category.Should().Be(expected);
    }
}

public sealed class PlainCategoryType;

public sealed class OuterCategoryType
{
    public sealed class InnerCategoryType
    {
        public sealed class DeepestCategoryType;
    }
}

public sealed class GenericCategoryType<T>;

public sealed class GenericOuterCategoryType<T>
{
    public sealed class NestedCategoryType;
}
