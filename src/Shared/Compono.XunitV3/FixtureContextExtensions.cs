#if COMPONO_XUNITV3_AOT
namespace Compono.XunitV3.Aot;
#else
namespace Compono.XunitV3;
#endif

using Xunit;

/// <summary>
/// Adds <see cref="ITestContext"/>-based fixture access for composition profiles. See
/// <c>docs/adr/0069-xunitv3-require-fixture-accessor.md</c>.
/// </summary>
/// <remarks>
/// Named <c>FixtureContextExtensions</c> rather than <c>TestContextExtensions</c> so it can't be
/// confused with xUnit's own <c>Xunit.TestContextExtensions</c> (which ships
/// <c>GetFixture&lt;TFixture&gt;()</c> in every supported xUnit version).
/// </remarks>
public static class FixtureContextExtensions
{
    extension(ITestContext context)
    {
        /// <summary>
        /// Returns the xUnit fixture of exact type <typeparamref name="TFixture"/> that is active for
        /// the currently executing test class, its test collection, or its test assembly.
        /// </summary>
        /// <typeparam name="TFixture">
        /// The fixture type to look up. Lookup matches the exact type only - a base type or an
        /// implemented interface never matches.
        /// </typeparam>
        /// <returns>The fixture instance xUnit already initialized.</returns>
        /// <remarks>
        /// Prefer reading the fixture from a registration factory (for example inside
        /// <c>builder.Register(() =&gt; ...)</c>) rather than once from a profile constructor or a
        /// <c>Configure</c> body. The factory runs per row at test execution time, so it always
        /// reads the fixture belonging to the test that is actually running. A profile instance can
        /// outlive a single row - under the JIT engine <c>Configure</c> runs once per attribute
        /// instance and any value it captured is reused for every row that instance composes - so a
        /// value captured in the constructor or <c>Configure</c> can be stale or leak state from
        /// another test. (<c>TestContext.Current</c> is ambient and available during construction
        /// and <c>Configure</c> too; this is about lifecycle correctness, not availability.)
        /// <para>
        /// This method never waits for fixture initialization. If the fixture has not finished
        /// initializing by the time the test executes, it throws immediately rather than blocking; a
        /// fixture must be declared concretely (<c>IClassFixture&lt;T&gt;</c>,
        /// <c>ICollectionFixture&lt;T&gt;</c>, or <c>[assembly: AssemblyFixture(typeof(T))]</c>) so
        /// xUnit initializes it before the test runs.
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        /// sealed class WebAppProfile : ICompositionProfile
        /// {
        ///     public void Configure(CompositionBuilder builder) =&gt;
        ///         builder.Register(() =&gt; TestContext.Current.RequireFixture&lt;WebApplicationFactory&lt;Program&gt;&gt;());
        /// }
        /// </code>
        /// </example>
        /// <exception cref="ArgumentNullException">The test context is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">
        /// No xUnit test class is currently active, the fixture has not finished initializing, the
        /// test class has no fixture of the exact requested type, or the looked-up value is not a
        /// <typeparamref name="TFixture"/>. Each case throws its own message describing the fix.
        /// </exception>
        public TFixture RequireFixture<TFixture>() where TFixture : class
        {
            ArgumentNullException.ThrowIfNull(context);

            var fixtureType = typeof(TFixture);
            var fixtureName = fixtureType.FullName ?? fixtureType.Name;

            var testClass = context.TestClass;
            if (testClass is null)
            {
                throw new InvalidOperationException(
                    $"RequireFixture<{fixtureName}>() requires a running xUnit test class, but none is active (pipeline stage: {context.PipelineStage}). Fixtures are only available while a test class is executing, for example inside a profile registration factory invoked by a [Compose]-attributed theory.");
            }

            // Deliberately the non-generic overload. xUnit also ships a generic GetFixture<TFixture>()
            // extension (Xunit.TestContextExtensions) in every supported version, but it is async and
            // awaits the ValueTask internally, so it can't express this accessor's never-wait contract.
            // The non-generic overload returns the ValueTask directly, letting us check IsCompleted
            // first; using it keeps semantics identical across the whole supported xUnit range.
            var pending = context.GetFixture(fixtureType);
            if (!pending.IsCompleted)
            {
                throw new InvalidOperationException(
                    $"Fixture '{fixtureName}' for test class '{testClass.TestClassName}' has not finished initializing. RequireFixture<{fixtureName}>() never waits for fixture initialization; declare the fixture concretely (IClassFixture<T>, ICollectionFixture<T>, or [assembly: AssemblyFixture(typeof(T))]) so xUnit initializes it before the test runs.");
            }

            // Already completed, so this never blocks. A faulted or canceled ValueTask rethrows the
            // original exception unchanged.
            var fixture = pending.GetAwaiter().GetResult();
            if (fixture is null)
            {
                throw new InvalidOperationException(
                    $"Test class '{testClass.TestClassName}' has no fixture of exact type '{fixtureName}'. Declare it with IClassFixture<{fixtureName}> on the test class, ICollectionFixture<{fixtureName}> on the test's collection definition, or [assembly: AssemblyFixture(typeof({fixtureName}))]. Fixture lookup matches the exact type only; base types and interfaces don't match.");
            }

            return fixture as TFixture
                ?? throw new InvalidOperationException(
                    $"Fixture lookup for '{fixtureName}' in test class '{testClass.TestClassName}' returned an instance of '{fixture.GetType().FullName ?? fixture.GetType().Name}', which is not a '{fixtureName}'.");
        }
    }
}
