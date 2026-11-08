using System;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Compono.Generators.Tests;

/// <summary>
/// Eligibility coverage for <c>AotComposeDataRegistrationEmitter</c>: an AOT <c>[ComposeData]</c>
/// registration must be emitted only for the approved S2 envelope (ADR-0070 / PLAN-0071 B5.11) -
/// non-profile, unseeded, non-shared, non-skipped, non-generic, synchronous member shapes. Every
/// other model must produce NO registration (and no new diagnostics, in particular no CMP0050).
/// </summary>
public sealed class AotComposeDataRegistrationEmissionTests
{
    private const string RegistrationHintSuffix = ".AotComposeDataRegistration.g.cs";

    private static string Source(string body) => ComposeDataAotStandIns.Source + """

        namespace TestNamespace
        {
            using System.Collections.Generic;

        """ + body + """
        }
        """;

    private static GeneratorDriverRunResult Run(string body) =>
        GeneratorTestHelpers.GenerateFromSource(
            new CodeGenerationOptions { SourceCode = Source(body) },
            TestContext.Current.CancellationToken).driver.GetRunResult();

    private static string[] RegistrationHintNames(GeneratorDriverRunResult result) =>
        result.Results
            .SelectMany(runResult => runResult.GeneratedSources)
            .Select(source => source.HintName)
            .Where(hintName => hintName.EndsWith(RegistrationHintSuffix, StringComparison.Ordinal))
            .ToArray();

    private static void AssertNoRegistration(string body)
    {
        var result = Run(body);
        result.Diagnostics.Should().BeEmpty();
        RegistrationHintNames(result).Should().BeEmpty();
    }

    [Fact]
    public void EligibleShape_EmitsRegistration()
    {
        var result = Run("""

            public sealed class EligibleTests
            {
                public static IEnumerable<object> Cases = new object[] { new object[] { "x" } };

                [Compono.XunitV3.Aot.ComposeData("Cases")]
                public void Eligible_rows_are_composed(string value)
                {
                }
            }
            """);

        RegistrationHintNames(result).Should().ContainSingle();
    }

    [Fact]
    public void OneTypeParameterProfileForm_DoesNotEmitRegistration() =>
        AssertNoRegistration("""

            public sealed class SampleProfile : Compono.ICompositionProfile
            {
                public void Configure(Compono.CompositionBuilder builder) { }
            }

            public sealed class ProfileTests
            {
                public static IEnumerable<object> Cases = new object[] { new object[] { "x" } };

                [Compono.XunitV3.Aot.ComposeData<SampleProfile>("Cases")]
                public void Profile_rows_are_not_emitted(string value) { }
            }
            """);

    [Fact]
    public void TwoTypeParameterProfileConfigForm_DoesNotEmitRegistration() =>
        AssertNoRegistration("""

            public sealed class SampleConfig { }

            public sealed class SampleProfile : Compono.ICompositionProfile
            {
                public SampleProfile(SampleConfig config) { }

                public void Configure(Compono.CompositionBuilder builder) { }
            }

            public sealed class ProfileConfigTests
            {
                public static IEnumerable<object> Cases = new object[] { new object[] { "x" } };

                [Compono.XunitV3.Aot.ComposeData<SampleProfile, SampleConfig>("Cases")]
                public void Profile_config_rows_are_not_emitted(string value) { }
            }
            """);

    [Fact]
    public void PositiveAttributeSeed_DoesNotEmitRegistration() =>
        AssertNoRegistration("""

            public sealed class SeededTests
            {
                public static IEnumerable<object> Cases = new object[] { new object[] { "x" } };

                [Compono.XunitV3.Aot.ComposeData("Cases", Seed = 42)]
                public void Seeded_rows_are_not_emitted(string value) { }
            }
            """);

    [Fact]
    public void NegativeAttributeSeed_DoesNotEmitRegistration() =>
        AssertNoRegistration("""

            public sealed class NegativeSeedTests
            {
                public static IEnumerable<object> Cases = new object[] { new object[] { "x" } };

                [Compono.XunitV3.Aot.ComposeData("Cases", Seed = -1)]
                public void Negative_seed_rows_are_not_emitted(string value) { }
            }
            """);

    [Fact]
    public void SharedParameter_DoesNotEmitRegistration() =>
        AssertNoRegistration("""

            public sealed class SharedParameterTests
            {
                public static IEnumerable<object> Cases = new object[] { new object[] { "x" } };

                [Compono.XunitV3.Aot.ComposeData("Cases")]
                public void Shared_rows_are_not_emitted([Compono.XunitV3.Aot.Shared] string value) { }
            }
            """);

    [Fact]
    public void SkipUnless_DoesNotEmitRegistration() =>
        AssertNoRegistration("""

            public class SkipHolder
            {
                public static bool ShouldSkip => false;
            }

            public sealed class SkipUnlessTests : SkipHolder
            {
                public static IEnumerable<object> Cases = new object[] { new object[] { "x" } };

                [Compono.XunitV3.Aot.ComposeData("Cases", Skip = "reason", SkipUnless = "ShouldSkip")]
                public void Skip_unless_rows_are_not_emitted(string value) { }
            }
            """);

    [Fact]
    public void SkipWhen_DoesNotEmitRegistration() =>
        AssertNoRegistration("""

            public class SkipHolder
            {
                public static bool ShouldSkip => false;
            }

            public sealed class SkipWhenTests : SkipHolder
            {
                public static IEnumerable<object> Cases = new object[] { new object[] { "x" } };

                [Compono.XunitV3.Aot.ComposeData("Cases", Skip = "reason", SkipWhen = "ShouldSkip")]
                public void Skip_when_rows_are_not_emitted(string value) { }
            }
            """);

    [Fact]
    public void GenericTestClass_DoesNotEmitRegistration() =>
        AssertNoRegistration("""

            public static class GenericMemberHolder
            {
                public static IEnumerable<object> Cases = new object[] { new object[] { "x" } };
            }

            public sealed class GenericTestClass<T>
            {
                [Compono.XunitV3.Aot.ComposeData("Cases", MemberType = typeof(GenericMemberHolder))]
                public void Generic_rows_are_not_emitted(string value) { }
            }
            """);

    [Fact]
    public void TaskOfReturnShape_DoesNotEmitRegistration() =>
        AssertNoRegistration("""

            public sealed class TaskShapeTests
            {
                public static System.Threading.Tasks.Task<object> Cases { get; } = null!;

                [Compono.XunitV3.Aot.ComposeData("Cases")]
                public void Task_rows_are_not_emitted(string value) { }
            }
            """);

    [Fact]
    public void ValueTaskOfReturnShape_DoesNotEmitRegistration() =>
        AssertNoRegistration("""

            public sealed class ValueTaskShapeTests
            {
                public static System.Threading.Tasks.ValueTask<Xunit.TheoryData<int, string>> Cases { get; }

                [Compono.XunitV3.Aot.ComposeData("Cases")]
                public void Value_task_rows_are_not_emitted(int quantity, string value) { }
            }
            """);

    [Fact]
    public void AsyncEnumerableReturnShape_DoesNotEmitRegistration() =>
        AssertNoRegistration("""

            public sealed class AsyncEnumerableShapeTests
            {
                public static System.Collections.Generic.IAsyncEnumerable<object[]> Cases() =>
                    throw new System.NotImplementedException();

                [Compono.XunitV3.Aot.ComposeData("Cases")]
                public void Async_rows_are_not_emitted(string value) { }
            }
            """);

    [Fact]
    public void ModelWithDiagnostic_DoesNotEmitRegistration()
    {
        var result = Run("""

            public sealed class MissingMemberTests
            {
                [Compono.XunitV3.Aot.ComposeData("Missing")]
                public void Missing_member_is_not_emitted(string value) { }
            }
            """);

        RegistrationHintNames(result).Should().BeEmpty();
        result.Diagnostics.Should().Contain(diagnostic => diagnostic.Id == "CMP0051");
        result.Diagnostics.Should().NotContain(diagnostic => diagnostic.Id == "CMP0050");
    }

    [Fact]
    public void InaccessibleTestClass_DoesNotEmitRegistration() =>
        AssertNoRegistration("""

            public static class PublicMemberHolder
            {
                public static IEnumerable<object> Cases = new object[] { new object[] { "x" } };
            }

            public sealed class Enclosing
            {
                private sealed class Hidden
                {
                    [Compono.XunitV3.Aot.ComposeData("Cases", MemberType = typeof(PublicMemberHolder))]
                    public void Hidden_rows_are_not_emitted(string value) { }
                }
            }
            """);
}
