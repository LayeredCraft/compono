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

    int Calculate(int left, int right);
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

// ADR-0044 Amendment 20: a base interface's abstract declaration resolved by a more-derived
// interface's own concrete (default-interface-member) redeclaration via `new` - the
// IDefaultRequestHandler.CanHandle shape that motivated PLAN-0053. Exercised under Native AOT
// specifically: both the unconfigured fallback (must call through the owner-forwarding dispatch
// helper to run the real DIM body, not a fabricated computed default) and the configured path, plus
// the base-interface view sharing the same call-recording state as the derived view.
internal interface IDefaultHandlerBase
{
    bool CanHandle(string input);
}

internal interface IDefaultHandler : IDefaultHandlerBase
{
    new bool CanHandle(string input) => true;
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

            // ADR-0053: the generated strongly typed callback delegate and member-specific builder
            // survive trimming/AOT and receive the invocation's real arguments.
            accountRepository.Configure()
                .Calculate(Match.Any<int>(), Match.Any<int>())
                .ReturnsCallback((left, right) => left + right);
            var callbackResult = accountRepository.Calculate(20, 22);

            if (callbackResult != 42)
                throw new InvalidOperationException($"Expected invocation callback result 42, got {callbackResult}.");

            accountRepository.Verify().Calculate(Match.Any<int>(), Match.Any<int>()).Once();

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

            // ADR-0044 Amendment 20: unconfigured resolved-DIM fallback runs the real interface
            // default body (via the owner-forwarding dispatch helper), not a fabricated computed
            // default - and the base-interface view shares the same call-recording state as the
            // derived view (no independent recording on the forwarding-only base implementation).
            var defaultHandler = composer.Create<IDefaultHandler>();
            var unconfiguredDimResult = defaultHandler.CanHandle("anything");

            if (unconfiguredDimResult != true)
                throw new InvalidOperationException(
                    $"Expected unconfigured IDefaultHandler.CanHandle(...) to run the real DIM body " +
                    $"(true), got {unconfiguredDimResult}.");

            IDefaultHandlerBase baseHandlerView = defaultHandler;
            baseHandlerView.CanHandle("via base view");

            defaultHandler.Verify().CanHandle(Match.Any<string>()).Exactly(2);

            defaultHandler.Configure().CanHandle(Match.Any<string>()).Returns(false);
            var configuredDimResult = defaultHandler.CanHandle("anything");

            if (configuredDimResult != false)
                throw new InvalidOperationException(
                    $"Expected configured IDefaultHandler.CanHandle(...) to return the configured " +
                    $"value (false), got {configuredDimResult}.");

            // ADR-0054: sequential/call-count-based responses under Native AOT - a mixed
            // exception/value sequence on a Task<T>-returning member (the real evidenced shape),
            // exhaustion repeating the final outcome, call recording staying independent of
            // response consumption (RecordCall() fires even on the two throwing calls), and two
            // independently-configured ADR-0050 entries maintaining independent ordinals.
            //
            // A void member's sequence carries no value dimension (Compono.Unit is a marker, not a
            // real payload), but exception-only sequencing still applies and is meaningful - "throw
            // on the first call, succeed silently after" - proven here on the overloaded Send(string)
            // discriminator. A fresh double, not the `gateway` instance above (which already recorded
            // an unrelated Send("hello") call) - Send(string)'s Verify() is discriminator-only, an
            // unfiltered per-overload count regardless of the argument's actual value, so reusing
            // `gateway` here would inflate the count this test asserts on, unrelated to ADR-0054.
            var sequencedGateway = composer.Create<IGateway>();
            sequencedGateway.Configure().Send("sequenced").ReturnsSequence(SequenceOutcome.Throw(new InvalidOperationException("first call fails")), default(Unit));

            var firstSendThrew = false;
            try { sequencedGateway.Send("sequenced"); }
            catch (InvalidOperationException) { firstSendThrew = true; }

            sequencedGateway.Send("sequenced"); // second call: exhausted-but-one-element-left, succeeds silently
            sequencedGateway.Send("sequenced"); // third call: exhaustion repeats the final (non-throwing) outcome

            if (!firstSendThrew)
                throw new InvalidOperationException("Expected the first sequenced Send(\"sequenced\") call to throw.");

            sequencedGateway.Verify().Send("sequenced").Exactly(3);

            var retryGateway = composer.Create<IRepository>();
            var attempt1 = new InvalidOperationException("attempt 1 fails");
            var attempt2 = new InvalidOperationException("attempt 2 fails");
            retryGateway.Configure().CountAsync().ReturnsSequence(SequenceOutcome.Throw(attempt1), SequenceOutcome.Throw(attempt2), Task.FromResult(42));

            var sequenceResults = new List<object>();
            for (var i = 0; i < 4; i++)
            {
                try { sequenceResults.Add(await retryGateway.CountAsync()); }
                catch (InvalidOperationException ex) { sequenceResults.Add(ex.Message); }
            }

            var expectedSequence = new object[] { "attempt 1 fails", "attempt 2 fails", 42, 42 };
            if (!sequenceResults.SequenceEqual(expectedSequence))
                throw new InvalidOperationException(
                    $"Expected sequential CountAsync() results [{string.Join(", ", expectedSequence)}], " +
                    $"got [{string.Join(", ", sequenceResults)}].");

            retryGateway.Verify().CountAsync().Exactly(4);

            // Independent ADR-0050 entries own independent sequence ordinals.
            accountRepository.Configure().Withdraw("acct-seq-1", Match.Any<decimal>(), Match.Any<bool>()).ReturnsSequence(false, true);
            accountRepository.Configure().Withdraw("acct-seq-2", Match.Any<decimal>(), Match.Any<bool>()).ReturnsSequence(true, false);

            var seq1First = accountRepository.Withdraw("acct-seq-1", 1m, true);
            var seq2First = accountRepository.Withdraw("acct-seq-2", 1m, true);
            var seq1Second = accountRepository.Withdraw("acct-seq-1", 1m, true);
            var seq2Second = accountRepository.Withdraw("acct-seq-2", 1m, true);

            if (seq1First || !seq2First || !seq1Second || seq2Second)
                throw new InvalidOperationException(
                    $"Expected independent per-entry sequence ordinals (false,true / true,false), got " +
                    $"({seq1First},{seq2First},{seq1Second},{seq2Second}).");

            // ADR-0044 Amendment 21: overload-safe argument matching under Native AOT - coexistence/
            // precedence (a broad discriminator-only Configure() registered first, a narrower
            // .Matching(...) override registered after it - the SUT-visible dispatch always goes
            // through the real IGateway.Send(string) overload, and both surfaces observe the same
            // calls) and sibling-overload independence (configuring Send(string)'s matching surface
            // never affects Send(int, string)'s own, independent entries/call-log/Verify() count).
            var matchingGateway = composer.Create<IGateway>();
            matchingGateway.Configure().Send("ignored").Throws(new InvalidOperationException("fallback"));
            matchingGateway.Configure().SendMatching(Match.Is<string>(m => m == "special")).Returns(default(Unit));

            var specialThrew = false;
            try { matchingGateway.Send("special"); } catch (InvalidOperationException) { specialThrew = true; }

            var otherThrew = false;
            try { matchingGateway.Send("other"); } catch (InvalidOperationException) { otherThrew = true; }

            if (specialThrew)
                throw new InvalidOperationException("Expected Send(\"special\") to match the narrower .Matching(...) entry and NOT throw.");

            if (!otherThrew)
                throw new InvalidOperationException("Expected Send(\"other\") to fall through to the broad discriminator entry and throw.");

            matchingGateway.Configure().SendMatching(Match.Any<int>(), Match.Any<string>()).Returns(default(Unit));
            matchingGateway.Send(1, "retry");

            matchingGateway.Verify().SendMatching(Match.Is<string>(m => m == "special")).Once();
            matchingGateway.Verify().Send("ignored").Exactly(2);
            matchingGateway.Verify().SendMatching(Match.Any<int>(), Match.Any<string>()).Once();

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
