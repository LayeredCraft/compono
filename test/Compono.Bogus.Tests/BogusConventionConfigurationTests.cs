namespace Compono.Bogus.Tests;

/// <summary>
/// ADR-0028's configurable member-name conventions - <see cref="BogusOptions.AddAlias"/>/
/// <see cref="BogusOptions.AddConvention"/>'s own eager validation, and their end-to-end effect once
/// merged by <c>UseBogus(Action{BogusOptions})</c> - PLAN-0006 Phase 3.
/// </summary>
public sealed class BogusConventionConfigurationTests
{
    [Fact]
    public void AddAlias_ResolvesToTheSameValue_ABuiltInConventionsOwnGeneratorWouldProduce_ForTheSameSeed()
    {
        var aliasDescriptor = new CompositionRequestDescriptor(
            CompositionRequestKind.ConstructorParameter, ordinal: 0, "GivenName", declaringType: null, Nullability.NotNullable);
        var builtInDescriptor = new CompositionRequestDescriptor(
            CompositionRequestKind.ConstructorParameter, ordinal: 0, "FirstName", declaringType: null, Nullability.NotNullable);

        var aliasValue = Composer.Create(builder => builder
                .WithSeed(4219)
                .UseBogus(options => options.AddAlias("GivenName", BogusConvention.FirstName)))
            .CreateRow(typeof(BogusConventionConfigurationTests))
            .Resolve<string>(aliasDescriptor);
        var builtInValue = Composer.Create(builder => builder.WithSeed(4219).UseBogus())
            .CreateRow(typeof(BogusConventionConfigurationTests))
            .Resolve<string>(builtInDescriptor);

        aliasValue.Should().Be(builtInValue);
    }

    [Fact]
    public void AddConvention_ProducesTheCustomCallbacksValue_SeededViaDeriveSeedLikeTheBuiltInPath()
    {
        var descriptor = new CompositionRequestDescriptor(
            CompositionRequestKind.ConstructorParameter, ordinal: 0, "Sku", declaringType: null, Nullability.NotNullable);

        static string ComposeRoot(CompositionRequestDescriptor descriptor) =>
            Composer.Create(builder => builder
                    .WithSeed(4219)
                    .UseBogus(options => options.AddConvention("Sku", f => $"SKU-{f.Random.Number(1000, 9999)}")))
                .CreateRow(typeof(BogusConventionConfigurationTests))
                .Resolve<string>(descriptor);

        var first = ComposeRoot(descriptor);
        var second = ComposeRoot(descriptor);

        first.Should().Be(second);
        first.Should().StartWith("SKU-");
    }

    [Fact]
    public void AddAlias_CollidingWithABuiltInName_ThrowsArgumentException_ImmediatelyFromTheCall()
    {
        var options = new BogusOptions();

        var act = () => options.AddAlias("FirstName", BogusConvention.LastName);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddConvention_CollidingWithAnExistingAlias_ThrowsArgumentException_ImmediatelyFromTheCall()
    {
        var options = new BogusOptions();
        options.AddAlias("GivenName", BogusConvention.FirstName);

        var act = () => options.AddConvention("GivenName", f => f.Random.Guid().ToString());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddConvention_CollidingWithAnExistingCustomConvention_ThrowsArgumentException_ImmediatelyFromTheCall()
    {
        var options = new BogusOptions();
        options.AddConvention("Sku", f => f.Random.Guid().ToString());

        var act = () => options.AddConvention("Sku", f => f.Random.Guid().ToString());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddAlias_NullName_ThrowsArgumentNullException()
    {
        var act = () => new BogusOptions().AddAlias(null!, BogusConvention.FirstName);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddConvention_NullGenerate_ThrowsArgumentNullException()
    {
        var act = () => new BogusOptions().AddConvention("Sku", null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AddConvention_EmptyOrWhitespaceName_ThrowsArgumentException(string name)
    {
        var act = () => new BogusOptions().AddConvention(name, f => f.Random.Guid().ToString());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddAlias_UndefinedBogusConventionValue_ThrowsArgumentOutOfRangeException()
    {
        var act = () => new BogusOptions().AddAlias("GivenName", (BogusConvention)999);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void EnableMemberNameConventionsFalse_MeansAliasesAndCustomConventions_AreNeverRegistered()
    {
        var descriptor = new CompositionRequestDescriptor(
            CompositionRequestKind.ConstructorParameter, ordinal: 0, "Sku", declaringType: null, Nullability.NotNullable);

        var withoutProvider = Composer.Create(builder => builder.WithSeed(4219))
            .CreateRow(typeof(BogusConventionConfigurationTests))
            .Resolve<string>(descriptor);
        var withDisabledConventions = Composer.Create(builder => builder
                .WithSeed(4219)
                .UseBogus(options =>
                {
                    options.EnableMemberNameConventions = false;
                    options.AddConvention("Sku", f => $"SKU-{f.Random.Number(1000, 9999)}");
                }))
            .CreateRow(typeof(BogusConventionConfigurationTests))
            .Resolve<string>(descriptor);

        withDisabledConventions.Should().Be(withoutProvider);
    }

    [Fact]
    public void TwoSeparateUseBogusCalls_DefiningTheSameCustomName_ComposeViaFirstMatchWinsPipelineSemantics()
    {
        // The documented cross-call limitation (ADR-0028's Negative Consequences): no conflict
        // detection across separate UseBogus(...) calls - ordinary registration-order/first-match-wins
        // pipeline semantics apply instead, asserted directly rather than left implicit.
        var descriptor = new CompositionRequestDescriptor(
            CompositionRequestKind.ConstructorParameter, ordinal: 0, "Sku", declaringType: null, Nullability.NotNullable);

        var value = Composer.Create(builder => builder
                .WithSeed(4219)
                .UseBogus(options => options.AddConvention("Sku", _ => "first-call-value"))
                .UseBogus(options => options.AddConvention("Sku", _ => "second-call-value")))
            .CreateRow(typeof(BogusConventionConfigurationTests))
            .Resolve<string>(descriptor);

        value.Should().Be("first-call-value");
    }

    [Fact]
    public void ExactCaseSensitiveMatching_ADifferentlyCasedRequest_DoesNotMatchACustomConvention()
    {
        var descriptor = new CompositionRequestDescriptor(
            CompositionRequestKind.ConstructorParameter, ordinal: 0, "sku", declaringType: null, Nullability.NotNullable);

        var withoutProvider = Composer.Create(builder => builder.WithSeed(4219))
            .CreateRow(typeof(BogusConventionConfigurationTests))
            .Resolve<string>(descriptor);
        var withDifferentlyCasedConvention = Composer.Create(builder => builder
                .WithSeed(4219)
                .UseBogus(options => options.AddConvention("Sku", f => $"SKU-{f.Random.Number(1000, 9999)}")))
            .CreateRow(typeof(BogusConventionConfigurationTests))
            .Resolve<string>(descriptor);

        withDifferentlyCasedConvention.Should().Be(withoutProvider);
    }

    [Fact]
    public void ExactCaseSensitiveMatching_TwoAliasesDifferingOnlyByCase_AreTreatedAsDistinctNames()
    {
        var options = new BogusOptions();

        var act = () =>
        {
            options.AddAlias("givenname", BogusConvention.FirstName);
            options.AddAlias("GivenName", BogusConvention.FirstName);
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void ExactCaseSensitiveMatching_ANameDifferingOnlyByCaseFromABuiltIn_IsNotRejectedAsACollision_AndSurvivesTheMergeAsItsOwnDistinctEntry()
    {
        var descriptor = new CompositionRequestDescriptor(
            CompositionRequestKind.ConstructorParameter, ordinal: 0, "firstname", declaringType: null, Nullability.NotNullable);

        // Registered through the real UseBogus(...) call this time (not a throwaway BogusOptions), so
        // this proves the accepted lowercase entry actually survives CompositionBuilderExtensions'
        // built-in/custom merge, not just that AddConvention's own eager validation accepts it.
        var value = Composer.Create(builder => builder
                .WithSeed(4219)
                .UseBogus(options => options.AddConvention("firstname", _ => "custom-lowercase-firstname")))
            .CreateRow(typeof(BogusConventionConfigurationTests))
            .Resolve<string>(descriptor);

        // Not rejected as a collision with the built-in "FirstName" entry, and resolves to the custom
        // callback's own value - proving "firstname" and "FirstName" are merged as two distinct,
        // case-sensitive keys, not one colliding pair.
        value.Should().Be("custom-lowercase-firstname");
    }
}
