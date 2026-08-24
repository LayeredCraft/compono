using Compono;

namespace Compono.TestDoubles.AotSmokeTest;

// IRepository extends IClock, not to re-litigate Amendment 11 Finding Z here (already proven by
// Compono.TestDoubles.SampleTests' own Verify()/real-runner coverage) but to exercise the exact same
// generated shape (closure-walked double, a Task<T>-returning member, a property) under Native AOT,
// not just under the ordinary JIT that runs Compono.TestDoubles.SampleTests' own dotnet test.
internal interface IClock
{
    DateTimeOffset UtcNow { get; }
}

internal interface IRepository : IClock
{
    Task<int> CountAsync();
}

// PLAN-0044 Phase 3: an overloaded member, exercising the per-overload Configure()/Verify()
// discriminator mechanism under Native AOT - not proven by IRepository above, which has no overloads.
internal interface IGateway
{
    void Send(string message);
    void Send(int retryCount, string message);
}

// PLAN-0044 Phase 3: a generic method whose return type doesn't depend on its own type parameter
// (the Requirement 2 shape, mirroring ILogger<T>.Log<TState>) - the Configure()/Verify() extension
// stays non-generic while the explicit interface implementation stays generic.
internal interface ILoggerLike
{
    void Log<TState>(int logLevel, TState state);
}

// PLAN-0045 Phase 2: the configuration-required dispatch shape (ADR-0045/PLAN-0045 Phase 0) under
// Native AOT specifically - a synchronous non-nullable-reference-returning method, a same-shaped
// property, and a Task<T>-returning method, each proven both unconfigured (throws
// TestDoubleNotConfiguredException) and configured (Returns(...) dispatches the literal
// generation-time-emitted throw branch correctly under trimming/AOT, not just under the ordinary JIT
// that runs Compono.TestDoubles.SampleTests' own dotnet test).
internal interface IProfileRepository
{
    string GetName();

    string Description { get; }

    Task<string> GetNameAsync();
}

// ADR-0046: a static abstract member declared on a base interface, already resolved by a more-
// derived interface's own concrete implementation (C#'s "most specific implementation" rule,
// IAmazonS3-shaped) - the generated double must be completely unaffected by it under Native AOT
// too, not just under the ordinary JIT that runs Compono.TestDoubles.SampleTests' own dotnet test.
internal interface IProfileFactory
{
    static abstract IProfileFactory CreateDefault();
}

internal interface IProfileRepositoryWithStaticAbstractBase : IProfileFactory
{
    static IProfileFactory IProfileFactory.CreateDefault() =>
        throw new NotSupportedException("real production implementation, never invoked here");

    string GetName();
}

// PLAN-0048: a non-overloaded, non-generic member with real parameters - the eligible shape
// ADR-0048's Match<T>/argument-aware Configure()/Verify() surface targets, exercised under Native
// AOT specifically (Compono.TestDoubles.SampleTests' MatchingTests.cs already proves this under the
// ordinary JIT). Mixes a literal, Match.Any, and Match.Is in one call - the real trivia-manager
// shape - and IGateway/ILoggerLike above (overloaded, generic-scoped-out) continue to prove the
// eligibility boundary itself survives AOT unchanged, not just the new surface in isolation.
internal interface IAccountRepository
{
    bool Withdraw(string accountId, decimal amount, bool overdraftAllowed);
}

// PLAN-0049: a generic method whose return type depends on its own type parameter - the exact
// closed-instantiation-eligible shape ADR-0049 adds (real trivia-platform GetContextDataAsync<T>
// shape), exercised through the real generator under Native AOT this time, not the hand-written
// spike ADR-0049's own design pass proved before this ADR was drafted. GetContextDataAsync<T> (Task<T?>,
// nullable) and GetRequiredDataAsync<T> (Task<T>, non-nullable) mirror that spike's own two-member
// shape, proving both ADR-0045 dispatch branches (deterministic-default, configuration-required)
// compose correctly with the new bucket mechanism through real generated code. Match.Any/Match.Is
// argument-aware Configure()/Verify() (ADR-0048 reuse) and two independent closed T's on the same
// double instance are both exercised below.
internal interface IReproContextManager
{
    Task<T?> GetContextDataAsync<T>(string key) where T : class;

    Task<T> GetRequiredDataAsync<T>(string key) where T : class;
}

internal sealed record ReproUserContext(string Sub);

internal sealed record ReproUpsellPayload(string ProductId);

internal static class Program
{
    private static async Task<int> Main()
    {
        try
        {
            var placedAt = new DateTimeOffset(2026, 8, 13, 0, 0, 0, TimeSpan.Zero);

            var composer = Composer.Create(builder => builder.UseGeneratedTestDoubles());
            var repository = composer.Create<IRepository>();
            var gateway = composer.Create<IGateway>();
            var logger = composer.Create<ILoggerLike>();
            var profileRepository = composer.Create<IProfileRepository>();
            var profileRepositoryWithStaticAbstractBase = composer.Create<IProfileRepositoryWithStaticAbstractBase>();
            var accountRepository = composer.Create<IAccountRepository>();

            repository.Configure().CountAsync().Returns(Task.FromResult(7));
            repository.Configure().UtcNow().Returns(placedAt);

            var count = await repository.CountAsync();
            var utcNow = repository.UtcNow;

            if (count != 7)
                throw new InvalidOperationException($"Expected CountAsync() to return 7, got {count}.");

            if (utcNow != placedAt)
                throw new InvalidOperationException($"Expected UtcNow to return {placedAt}, got {utcNow}.");

            // Overloaded member: each overload dispatches through its own generated slot.
            gateway.Send("hello");
            gateway.Send(3, "retrying");

            gateway.Verify().Send("hello").Once();
            gateway.Verify().Send(3, "retrying").Once();

            // Generic method independent of its own type parameter: one non-generic Configure()/
            // Verify() slot covers every closed instantiation a real caller exercises.
            logger.Log(1, "first");
            logger.Log(2, 99);

            logger.Verify().Log().Exactly(2);

            // Call verification against the pre-existing IRepository shape too, proving
            // RecordCall()/CallVerifier survive Native AOT for a Task<T>-returning member and a
            // property, not just void methods.
            repository.Verify().CountAsync().Once();
            repository.Verify().UtcNow().Once();

            // Configuration-required members (ADR-0045/PLAN-0045 Phase 0): unconfigured-throws proven
            // first, then the same members configured and dispatching their real values, all under
            // Native AOT.
            var unconfiguredMethod = () => profileRepository.GetName();
            if (!ThrowsNotConfigured(unconfiguredMethod))
                throw new InvalidOperationException("Expected unconfigured GetName() to throw TestDoubleNotConfiguredException.");

            var unconfiguredProperty = () => profileRepository.Description;
            if (!ThrowsNotConfigured(unconfiguredProperty))
                throw new InvalidOperationException("Expected unconfigured Description to throw TestDoubleNotConfiguredException.");

            var unconfiguredAsyncMethod = async () => await profileRepository.GetNameAsync();
            if (!await ThrowsNotConfiguredAsync(unconfiguredAsyncMethod))
                throw new InvalidOperationException("Expected unconfigured GetNameAsync() to throw TestDoubleNotConfiguredException.");

            profileRepository.Configure().GetName().Returns("Ada");
            profileRepository.Configure().Description().Returns("a test double");
            profileRepository.Configure().GetNameAsync().Returns(Task.FromResult("Ada"));

            var name = profileRepository.GetName();
            var description = profileRepository.Description;
            var asyncName = await profileRepository.GetNameAsync();

            if (name != "Ada")
                throw new InvalidOperationException($"Expected GetName() to return 'Ada', got '{name}'.");

            if (description != "a test double")
                throw new InvalidOperationException($"Expected Description to return 'a test double', got '{description}'.");

            if (asyncName != "Ada")
                throw new InvalidOperationException($"Expected GetNameAsync() to return 'Ada', got '{asyncName}'.");

            // ADR-0046: a static abstract member resolved via a derived interface's own concrete
            // implementation doesn't reject the leaf interface, and every other instance member on
            // it works exactly as normal - proven under Native AOT here.
            profileRepositoryWithStaticAbstractBase.Configure().GetName().Returns("Ada");
            var staticAbstractBaseName = profileRepositoryWithStaticAbstractBase.GetName();

            if (staticAbstractBaseName != "Ada")
                throw new InvalidOperationException($"Expected GetName() to return 'Ada', got '{staticAbstractBaseName}'.");

            // PLAN-0048: argument-matched Configure() (mixed literal/Match.Any/Match.Is) and
            // argument-filtered Verify(), under Native AOT.
            //
            // ADR-0050: three Configure() calls on the same member append three entries, not
            // overwrite one slot - a broad Match.Any() default registered first, then two disjoint
            // literal-account entries registered after it. Dispatch's reverse scan (last-matching-
            // registration-wins) must find the correct one of the three for each account, and still
            // fall back to the broad default for any other account, all through the real generator
            // under Native AOT.
            accountRepository.Configure()
                .Withdraw(Match.Any<string>(), Match.Any<decimal>(), Match.Any<bool>())
                .Returns(false);
            accountRepository.Configure()
                .Withdraw("acct-1", Match.Any<decimal>(), Match.Is<bool>(allowed => allowed))
                .Returns(true);
            accountRepository.Configure()
                .Withdraw("acct-2", Match.Any<decimal>(), Match.Any<bool>())
                .Returns(true);

            var matchingCall = accountRepository.Withdraw("acct-1", 50m, overdraftAllowed: true);
            var secondEntryCall = accountRepository.Withdraw("acct-2", 50m, overdraftAllowed: false);
            var wrongOverdraftFlag = accountRepository.Withdraw("acct-1", 50m, overdraftAllowed: false);
            var fallsThroughToDefault = accountRepository.Withdraw("acct-3", 50m, overdraftAllowed: true);

            if (!matchingCall)
                throw new InvalidOperationException("Expected a matching Withdraw() call to return true.");

            if (!secondEntryCall)
                throw new InvalidOperationException("Expected the second, independently-registered entry (acct-2) to return its own configured value.");

            if (wrongOverdraftFlag)
                throw new InvalidOperationException("Expected a non-matching overdraft flag to fall through to the broad Match.Any() default entry, not the configured value.");

            if (fallsThroughToDefault)
                throw new InvalidOperationException("Expected acct-3 to fall through to the broad Match.Any() default entry registered first.");

            accountRepository.Verify()
                .Withdraw(Match.Is<string>(id => id == "acct-1"), Match.Any<decimal>(), Match.Any<bool>())
                .Exactly(2);
            accountRepository.Verify()
                .Withdraw(Match.Is<string>(id => id == "acct-2"), Match.Any<decimal>(), Match.Any<bool>())
                .Once();
            accountRepository.Verify().Withdraw().Exactly(4);

            // PLAN-0049: real generated (not hand-written) closed-instantiation-eligible members
            // under Native AOT - two closed T's on GetContextDataAsync<T> independently configured
            // and verified, the deterministic-default branch (unconfigured Task<T?> returns null,
            // never leaking the other closed T's configured value), and the configuration-required
            // branch (unconfigured Task<T> throws) on GetRequiredDataAsync<T>, all through the same
            // double instance.
            var contextManager = composer.Create<IReproContextManager>();
            var user = new ReproUserContext("sub-1");
            var payload = new ReproUpsellPayload("prod-1");

            contextManager.Configure().GetContextDataAsync<ReproUserContext>(Match.Any<string>())
                .Returns(Task.FromResult<ReproUserContext?>(user));
            contextManager.Configure().GetContextDataAsync<ReproUpsellPayload>(Match.Is<string>(key => key == "upsell"))
                .Returns(Task.FromResult<ReproUpsellPayload?>(payload));

            var resolvedUser = await contextManager.GetContextDataAsync<ReproUserContext>("user");
            var resolvedPayload = await contextManager.GetContextDataAsync<ReproUpsellPayload>("upsell");
            var resolvedPayloadWrongKey = await contextManager.GetContextDataAsync<ReproUpsellPayload>("not-upsell");

            if (!ReferenceEquals(resolvedUser, user))
                throw new InvalidOperationException("Expected GetContextDataAsync<ReproUserContext>('user') to return the configured user instance.");

            if (!ReferenceEquals(resolvedPayload, payload))
                throw new InvalidOperationException("Expected GetContextDataAsync<ReproUpsellPayload>('upsell') to return the configured payload instance.");

            if (resolvedPayloadWrongKey is not null)
                throw new InvalidOperationException("Expected a non-matching key to fall through to the deterministic default (null), not the configured value.");

            contextManager.Verify().GetContextDataAsync<ReproUserContext>(Match.Any<string>()).Once();
            contextManager.Verify().GetContextDataAsync<ReproUpsellPayload>(Match.Any<string>()).Exactly(2);

            var unconfiguredRequiredMethod = () => contextManager.GetRequiredDataAsync<ReproUserContext>("user");
            if (!await ThrowsNotConfiguredAsync(unconfiguredRequiredMethod))
                throw new InvalidOperationException("Expected unconfigured GetRequiredDataAsync<ReproUserContext>('user') to throw TestDoubleNotConfiguredException.");

            contextManager.Configure().GetRequiredDataAsync<ReproUserContext>(Match.Any<string>()).Returns(Task.FromResult(user));
            var requiredUser = await contextManager.GetRequiredDataAsync<ReproUserContext>("user");

            if (!ReferenceEquals(requiredUser, user))
                throw new InvalidOperationException("Expected GetRequiredDataAsync<ReproUserContext>('user') to return the configured user instance once configured.");

            Console.WriteLine(
                $"PASS: generated doubles (composer.Create<T>() + UseGeneratedTestDoubles(), full " +
                $"base-interface closure, overloaded member, generic method, call verification, " +
                $"configuration-required sync method/property/Task<T> method both unconfigured-throws " +
                $"and configured, a leaf interface whose base declares a static abstract member " +
                $"already resolved by the leaf itself, argument-matched Configure()/argument-filtered " +
                $"Verify() via Match<T>, and a closed-instantiation-eligible generic-return member " +
                $"(PLAN-0049) with two independently-configured closed T's and both ADR-0045 dispatch " +
                $"branches) survived Native AOT - CountAsync()={count}, " +
                $"UtcNow={utcNow}, GetName()={name}, Description={description}, " +
                $"GetNameAsync()={asyncName}, static-abstract-base GetName()={staticAbstractBaseName}, " +
                $"Withdraw matching={matchingCall}, GetContextDataAsync<ReproUserContext>()={resolvedUser}, " +
                $"GetRequiredDataAsync<ReproUserContext>()={requiredUser}.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAIL: {ex}");
            return 1;
        }
    }

    // No AwesomeAssertions/xUnit available here (this project deliberately removes those global
    // usings - see the .csproj) - a plain try/catch stands in for
    // act.Should().Throw<TestDoubleNotConfiguredException>().
    private static bool ThrowsNotConfigured<T>(Func<T> act)
    {
        try
        {
            act();
            return false;
        }
        catch (TestDoubleNotConfiguredException)
        {
            return true;
        }
    }

    private static async Task<bool> ThrowsNotConfiguredAsync(Func<Task> act)
    {
        try
        {
            await act();
            return false;
        }
        catch (TestDoubleNotConfiguredException)
        {
            return true;
        }
    }
}
