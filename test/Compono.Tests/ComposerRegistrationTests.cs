namespace Compono.Tests;

/// <summary>
/// Exercises Milestone 3 Phase 1's public registration and service-injection surface -
/// <see cref="CompositionBuilder.Register{T}(Func{ICompositionContext, T})"/>,
/// <see cref="CompositionBuilder.Register{T}(Func{T})"/>, and
/// <see cref="CompositionBuilder.UseServiceProvider"/> - through the real
/// <see cref="Composer.Create(Action{CompositionBuilder})"/> path. See
/// <c>docs/adr/0019-registrations-and-service-provider-injection.md</c>.
/// </summary>
public sealed class ComposerRegistrationTests
{
    [Fact]
    public void Register_WithContextFactory_IsInvokedExactlyOnce_PerRequest()
    {
        var callCount = 0;
        var composer = Composer.Create(builder => builder.Register<Widget>(_ =>
        {
            callCount++;
            return new Widget("from-context-factory");
        }));

        var result = composer.Create<Widget>();

        result.Value.Should().Be("from-context-factory");
        callCount.Should().Be(1);
    }

    [Fact]
    public void Register_WithNoDependencyFactory_Works()
    {
        var composer = Composer.Create(builder => builder.Register<Widget>(() => new Widget("from-no-dependency-factory")));

        var result = composer.Create<Widget>();

        result.Value.Should().Be("from-no-dependency-factory");
    }

    [Fact]
    public void Register_FactoryCanResolveANestedDependency_ViaTheDescriptorLessOverload()
    {
        var composer = Composer.Create(builder => builder.Register<Widget>(ctx => new Widget(ctx.Resolve<string>())));

        var result = composer.Create<Widget>();

        result.Value.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Register_SameTypeTwiceDirectly_ThrowsWithOneDuplicateRegistrationErrorNamingBothSources()
    {
        var act = () => Composer.Create(builder => builder
            .Register<Widget>(() => new Widget("first"))
            .Register<Widget>(() => new Widget("second")));

        var exception = act.Should().Throw<CompositionConfigurationException>().Which;
        exception.Errors.Should().ContainSingle();
        var error = exception.Errors[0].Should().BeOfType<CompositionConfigurationError.DuplicateRegistration>().Which;
        error.RegisteredType.Should().Be(typeof(Widget));
        error.Sources.Should().HaveCount(2);
        error.Sources.Should().AllSatisfy(source => source.Should().Be(ConfigurationSource.Direct));
    }

    [Fact]
    public async Task SelfReferencingRegistration_FailsWithADiagnosableException_InsteadOfInvokingItselfForever()
    {
        var composer = Composer.Create(builder => builder.Register<Recursive>(ctx => ctx.Resolve<Recursive>()));

        // Bounded via Task.Run + a timeout race rather than a bare call - a regression in the
        // factory-reentrance guard would recurse to StackOverflowException (unrecoverable, would
        // crash the whole test process) or hang, neither of which a plain act.Should().Throw() would
        // survive to report; this makes that failure mode a normal, readable test failure instead.
        var task = Task.Run(() => composer.Create<Recursive>());
        var completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)) == task;

        completed.Should().BeTrue("a factory-reentrance regression would hang instead of throwing");
        var rethrow = async () => await task;
        await rethrow.Should().ThrowAsync<CompositionException>().WithMessage("*factory*");
    }

    [Fact]
    public void UseServiceProvider_NeverWins_WhenAnExactRegistrationExists()
    {
        var registered = new Widget("from-registration");
        var provider = StubServiceProvider.Returning(typeof(Widget), new Widget("from-provider"));
        var composer = Composer.Create(builder => builder
            .Register<Widget>(() => registered)
            .UseServiceProvider(provider));

        var result = composer.Create<Widget>();

        result.Should().BeSameAs(registered);
    }

    [Fact]
    public void UseServiceProvider_NullResult_FallsThroughToTheNextStage_InsteadOfBeingAuthoritative()
    {
        var provider = StubServiceProvider.Returning(typeof(string), value: null);
        var composer = Composer.Create(builder => builder.UseServiceProvider(provider));

        var result = composer.Create<string>();

        result.Should().NotBeNull();
    }

    [Fact]
    public void UseServiceProvider_MatchingResult_IsUsed()
    {
        var provider = StubServiceProvider.Returning(typeof(Widget), new Widget("from-provider"));
        var composer = Composer.Create(builder => builder.UseServiceProvider(provider));

        var result = composer.Create<Widget>();

        result.Value.Should().Be("from-provider");
    }

    [Fact]
    public void UseServiceProvider_Throws_PropagatesAsCompositionExceptionWithTheOriginalPreservedAsInnerException()
    {
        var original = new InvalidOperationException("container exploded");
        var provider = StubServiceProvider.Throwing(typeof(Widget), original);
        var composer = Composer.Create(builder => builder.UseServiceProvider(provider));

        var act = () => composer.Create<Widget>();

        var exception = act.Should().Throw<CompositionException>().Which;
        exception.InnerException.Should().BeSameAs(original);
    }

    [Fact]
    public void UseServiceProvider_WrongTypeResult_ThrowsAStructuredExceptionNamingBothTypes()
    {
        var provider = StubServiceProvider.Returning(typeof(Widget), "not-a-widget");
        var composer = Composer.Create(builder => builder.UseServiceProvider(provider));

        var act = () => composer.Create<Widget>();

        var exception = act.Should().Throw<CompositionException>().Which;
        exception.Message.Should().Contain("Widget");
        exception.Message.Should().Contain("String");
    }

    [Fact]
    public void UseServiceProvider_IsNeverDisposed_RegardlessOfHowResolutionEnds()
    {
        // StubServiceProvider throws from Dispose() - every scenario below succeeding at all (rather
        // than throwing the Dispose-guard exception) is itself the assertion that Compono never
        // touches a disposal-related member on the configured IServiceProvider, per
        // docs/adr/0019-registrations-and-service-provider-injection.md.
        var hit = StubServiceProvider.Returning(typeof(Widget), new Widget("from-provider"));
        var miss = StubServiceProvider.Returning(typeof(string), value: null);

        var composerHit = Composer.Create(builder => builder.UseServiceProvider(hit));
        var composerMiss = Composer.Create(builder => builder.UseServiceProvider(miss));

        var hitAct = () => composerHit.Create<Widget>();
        var missAct = () => composerMiss.Create<string>();

        hitAct.Should().NotThrow();
        missAct.Should().NotThrow();
    }

    [Fact]
    public void UseServiceProvider_CalledTwice_ThrowsWithOneDuplicateConfigurationOptionErrorNamingBothSources()
    {
        var first = StubServiceProvider.Returning(typeof(Widget), value: null);
        var second = StubServiceProvider.Returning(typeof(Widget), value: null);

        var act = () => Composer.Create(builder => builder.UseServiceProvider(first).UseServiceProvider(second));

        var exception = act.Should().Throw<CompositionConfigurationException>().Which;
        exception.Errors.Should().ContainSingle();
        var error = exception.Errors[0].Should().BeOfType<CompositionConfigurationError.DuplicateConfigurationOption>().Which;
        error.OptionName.Should().Be("UseServiceProvider");
        error.Sources.Should().HaveCount(2);
    }

    private sealed record Widget(string Value);

    private sealed record Recursive;

    private sealed class StubServiceProvider : IServiceProvider, IDisposable
    {
        private readonly Type _type;
        private readonly object? _value;
        private readonly Exception? _throwOnGet;

        private StubServiceProvider(Type type, object? value, Exception? throwOnGet)
        {
            _type = type;
            _value = value;
            _throwOnGet = throwOnGet;
        }

        internal static StubServiceProvider Returning(Type type, object? value) => new(type, value, throwOnGet: null);

        internal static StubServiceProvider Throwing(Type type, Exception exception) => new(type, value: null, exception);

        public object? GetService(Type serviceType)
        {
            if (serviceType != _type)
                return null;

            if (_throwOnGet is not null)
                throw _throwOnGet;

            return _value;
        }

        public void Dispose() =>
            throw new InvalidOperationException("Compono must never dispose a configured IServiceProvider.");
    }
}
