namespace Compono.Generators.Tests;

/// <summary>
/// Real end-to-end execution of Milestone 3 Phase 3's compiled configuration rules and collection-size
/// configuration against a real generator-emitted plan - not a hand-written test double
/// (<c>Compono.Tests</c>' <c>ComposerConfigurationRuleTests</c>/<c>ComposerCollectionSizeTests</c>).
/// Proves the documented claim in <c>docs/adr/0020-composition-configuration-rules.md</c> that a member
/// rule always matches a positional record's constructor parameter, since the parameter name and the
/// compiler-synthesized property name are the identical string for that shape, and that
/// <c>WithCollectionSize</c> (global and member-scoped) actually reaches the generator-emitted
/// <c>context.ResolveCollectionSize()</c> call site, not just the hand-faked
/// <c>CollectionPlanCache&lt;T&gt;</c> entries the fast unit tests use (Codex review - the plan's own
/// checklist claimed this was verified end-to-end through a real generated collection plan, which
/// wasn't true until this file added it).
/// </summary>
public sealed class ConfigurationRuleExecutionTests
{
    [Fact]
    public void MemberRule_MatchesAPositionalRecordConstructorParameter_ThroughARealDispatchedPlan()
    {
        var result = GeneratorTestHelpers.CompileAndExecute(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public sealed record Customer(string FirstName, string LastName);

                    public static class EntryPoint
                    {
                        public static object Run()
                        {
                            var composer = Compono.Composer.Create(builder => builder
                                .For<Customer>().Member(x => x.FirstName).Use("Ada"));

                            return composer.Create<Customer>();
                        }
                    }
                    """,
            },
            "TestNamespace.EntryPoint",
            "Run",
            TestContext.Current.CancellationToken);

        var firstName = result!.GetType().GetProperty("FirstName")!.GetValue(result);

        firstName.Should().Be("Ada");
    }

    [Fact]
    public void GlobalWithCollectionSize_ChangesTheCollectionLength_ThroughARealGeneratedCollectionPlan()
    {
        var result = GeneratorTestHelpers.CompileAndExecute(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public static class EntryPoint
                    {
                        public static object Run()
                        {
                            var composer = Compono.Composer.Create(builder => builder.WithCollectionSize(7));
                            return composer.Create<System.Collections.Generic.List<int>>();
                        }
                    }
                    """,
            },
            "TestNamespace.EntryPoint",
            "Run",
            TestContext.Current.CancellationToken);

        var list = (System.Collections.Generic.List<int>)result!;

        list.Should().HaveCount(7);
    }

    [Fact]
    public void MemberScopedWithCollectionSize_OverridesTheGlobalDefault_ForThatMemberOnly_ThroughARealGeneratedPlan()
    {
        var result = GeneratorTestHelpers.CompileAndExecute(
            new CodeGenerationOptions
            {
                SourceCode = """
                    namespace TestNamespace;

                    public sealed record Wrapper(
                        System.Collections.Generic.List<int> ItemsA,
                        System.Collections.Generic.List<int> ItemsB);

                    public static class EntryPoint
                    {
                        public static object Run()
                        {
                            var composer = Compono.Composer.Create(builder => builder
                                .WithCollectionSize(3)
                                .For<Wrapper>().Member(x => x.ItemsA).WithCollectionSize(9));

                            return composer.Create<Wrapper>();
                        }
                    }
                    """,
            },
            "TestNamespace.EntryPoint",
            "Run",
            TestContext.Current.CancellationToken);

        var itemsA = (System.Collections.Generic.List<int>)result!.GetType().GetProperty("ItemsA")!.GetValue(result)!;
        var itemsB = (System.Collections.Generic.List<int>)result!.GetType().GetProperty("ItemsB")!.GetValue(result)!;

        itemsA.Should().HaveCount(9);
        itemsB.Should().HaveCount(3);
    }
}
