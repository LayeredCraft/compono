namespace Compono.Generators.Tests;

/// <summary>
/// Real end-to-end execution of Milestone 3 Phase 3's compiled configuration rules against a real
/// generator-emitted plan - not a hand-written test double
/// (<c>Compono.Tests</c>' <c>ComposerConfigurationRuleTests</c>). Proves the documented claim in
/// <c>docs/adr/0020-composition-configuration-rules.md</c> that a member rule always matches a
/// positional record's constructor parameter, since the parameter name and the compiler-synthesized
/// property name are the identical string for that shape.
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
}
