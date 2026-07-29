using System.Reflection;

namespace Compono.Generators.Tests;

/// <summary>
/// Real end-to-end execution of a generated collection plan - not a snapshot/compile check
/// (<see cref="CollectionPlanVerifyTests"/>) and not the runtime pipeline exercised against a
/// hand-written test double (<c>UniqueValueResolverTests</c>,
/// <c>CollectionPlanCacheDispatchTests</c> in <c>Compono.Tests</c>). Closes the PR #11 review gap:
/// no existing test compiled, loaded, and actually ran a generated collection plan through
/// <see cref="Compono.CollectionPlanCache{T}"/> dispatch.
/// </summary>
public sealed class GeneratedCollectionPlanExecutionTests
{
    [Fact]
    public void HashSetRetryExhaustion_ThrowsCompositionException_ThroughARealDispatchedPlan()
    {
        // bool has exactly 2 possible values - a HashSet<bool> at the default collection size (3,
        // ADR-0013) can never succeed regardless of randomness, so this deterministically exercises
        // UniqueValueResolver.TryResolve's exhaustion path through the real generated HashSet<T> plan
        // (module initializer registration, template output, and CollectionPlanCache<T> dispatch all
        // together), not a hand-written stub.
        var act = () => GeneratorTestHelpers.CompileAndExecute(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public static class EntryPoint
                    {
                        public static object Run()
                        {
                            var composer = Compono.Composer.Create();
                            return composer.Create<System.Collections.Generic.HashSet<bool>>();
                        }
                    }
                    """,
            },
            "TestNamespace.EntryPoint",
            "Run",
            TestContext.Current.CancellationToken);

        act.Should().Throw<TargetInvocationException>()
            .WithInnerException<CompositionException>()
            .WithMessage("*unique*bool*");
    }

    [Fact]
    public void ListElements_EachGetsIndependentOutput_ThroughARealDispatchedPlan()
    {
        // Real end-to-end proof that the generated List<T> plan's per-index CollectionElement(i)
        // descriptors actually produce independent forked values when dispatched for real - not just
        // that the template emits the right shape (CollectionPlanVerifyTests) or that IRandomSource
        // forks independently in isolation (RandomSourceTests/CompositionRandomIntegrationTests in
        // Compono.Tests).
        //
        // Composer.CreateRootForTesting (not Composer.Create) - a fixed seed makes this reproducible
        // on failure and removes any dependence, however astronomically small, on GUID-space
        // collision odds. CreateRootForTesting and CompositionSeed are both internal, and this test's
        // source is compiled into its own separate in-memory assembly (GeneratorTestHelpers), so
        // reaching them needs *some* seam - granting Compono's InternalsVisibleTo to that assembly's
        // name was rejected (confirmed directly, then reverted): an unsigned InternalsVisibleTo
        // authenticates only a simple name, so any real consumer could name their own assembly the
        // same thing and gain the identical internal access to the shipped package (PR #11 review
        // caught this). Reflection instead - already how CompileAndExecute invokes every test's entry
        // point below, so this adds no new public/friend surface to the shipped package at all.
        var result = GeneratorTestHelpers.CompileAndExecute(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public static class EntryPoint
                    {
                        public static object Run()
                        {
                            // The generator only discovers a type from a `Composer.Create<T>()` call
                            // site (CreateInvocationDiscovery), not from CreateRootForTesting - this
                            // discarded call exists purely so the generator emits
                            // ICompositionPlan<List<Guid>>/registers it into CollectionPlanCache; the
                            // actual assertion runs the same generated, cache-dispatched plan through
                            // CreateRootForTesting's fixed seed instead, for reproducibility.
                            var composer = Compono.Composer.Create();
                            _ = composer.Create<System.Collections.Generic.List<System.Guid>>();

                            var composerType = typeof(Compono.Composer);
                            var seedType = composerType.Assembly.GetType("Compono.CompositionSeed")!;
                            var seed = System.Activator.CreateInstance(seedType, 4219UL)!;
                            var createRootForTesting = composerType
                                .GetMethod("CreateRootForTesting", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                                .MakeGenericMethod(typeof(System.Collections.Generic.List<System.Guid>));

                            return createRootForTesting.Invoke(null, new object?[] { seed })!;
                        }
                    }
                    """,
            },
            "TestNamespace.EntryPoint",
            "Run",
            TestContext.Current.CancellationToken);

        var guids = (System.Collections.Generic.List<Guid>)result!;

        guids.Should().HaveCount(3);
        guids.Should().OnlyHaveUniqueItems();
    }
}
