using System.Linq;
using Compono.Generators.Diagnostics;
using Compono.Generators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Compono.Generators.Tests;

public sealed class AotComposeDataRemediationTests
{
    private static string Wrap(string body) => $"namespace TestNamespace\n{{\nusing Compono;\n{body}\n}}\n";

    private static AotComposeDataMethodInfo Compose(string body) => ComposeDataAotTestHelper.SingleModel(Wrap(body));

    private static Diagnostic Single(string body, string id) =>
        ComposeDataAotTestHelper.Discover(Wrap(body)).Diagnostics.Single(diagnostic => diagnostic.Id == id);

    // ---- B-1: unsupported parameter shapes ----

    [Fact]
    public void RefStructParameter_ReportsCmp0040()
    {
        var diagnostic = Single(
            """
            public sealed class SampleTests
            {
                [Compono.XunitV3.Aot.ComposeData("Cases")]
                public void TestMethod(System.Span<int> value) { }
            }
            """,
            "CMP0040");

        diagnostic.GetMessage().Should().Contain("cannot be composed");
    }

    [Fact]
    public void PointerParameter_ReportsCmp0040()
    {
        var diagnostic = Single(
            """
            public sealed class SampleTests
            {
                [Compono.XunitV3.Aot.ComposeData("Cases")]
                public unsafe void TestMethod(int* value) { }
            }
            """,
            "CMP0040");

        diagnostic.GetMessage().Should().Contain("cannot be composed");
    }

    [Fact]
    public void SupportedParameterShapes_ProduceValidModel()
    {
        var model = Compose(
            """
            public sealed class SampleTests
            {
                public static System.Collections.Generic.IEnumerable<object?[]> Cases => [];

                [Compono.XunitV3.Aot.ComposeData("Cases")]
                public void TestMethod(string value, System.IDisposable disposable) { }
            }
            """);

        model.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void InvalidParameterShape_ModelCarriesDiagnostic()
    {
        var model = ComposeDataAotTestHelper
            .Discover(
                Wrap(
                    """
                    public sealed class SampleTests
                    {
                        [Compono.XunitV3.Aot.ComposeData("Cases")]
                        public void TestMethod(System.Span<int> value) { }
                    }
                    """))
            .Models
            .Single();

        model.Diagnostics.Select(diagnostic => diagnostic.Descriptor.Id).Should().Contain("CMP0040");
    }

    // ---- B-2: diagnostic equality and incremental correctness ----

    [Fact]
    public void DiagnosticInfo_SameIdDifferentDescriptor_IsNotEqual()
    {
        var notFound = new DiagnosticInfo(DiagnosticDescriptors.ComposeDataMemberNotFound, null, "M", "T");
        var ambiguous = new DiagnosticInfo(DiagnosticDescriptors.ComposeDataMemberAmbiguous, null, "M", "T");

        notFound.Should().NotBe(ambiguous);
        notFound.GetHashCode().Should().NotBe(ambiguous.GetHashCode());

        var notFoundAgain = new DiagnosticInfo(DiagnosticDescriptors.ComposeDataMemberNotFound, null, "M", "T");
        notFound.Should().Be(notFoundAgain);
    }

    [Fact]
    public void IncrementalRun_WhenMemberBecomesAmbiguous_ReportsFreshMessage()
    {
        const string original =
            """
            namespace TestNamespace
            {
                public sealed class SampleTests
                {
                    [Compono.XunitV3.Aot.ComposeData("Cases")]
                    public void TestMethod() { }
                }
            }
            """;
        const string edited =
            """
            namespace TestNamespace
            {
                public sealed class SampleTests
                {
                    public static System.Collections.Generic.IEnumerable<object?[]> Cases() => [];
                    public static System.Collections.Generic.IEnumerable<object?[]> Cases<T>() => [];

                    [Compono.XunitV3.Aot.ComposeData("Cases")]
                    public void TestMethod() { }
                }
            }
            """;

        var cancellationToken = TestContext.Current.CancellationToken;
        var (driver, compilation) = ComposeDataAotTestHelper.CreateTrackingDriver(original);
        var firstRun = driver.RunGenerators(compilation, cancellationToken);
        var firstMessage = firstRun.GetRunResult().Diagnostics.Single(diagnostic => diagnostic.Id == "CMP0051").GetMessage();
        firstMessage.Should().Contain("was not found");

        var originalTree = compilation.SyntaxTrees.Single();
        var editedCompilation = compilation.ReplaceSyntaxTree(
            originalTree,
            originalTree.WithChangedText(SourceText.From(ComposeDataAotStandIns.Source + edited)));
        var secondRun = firstRun.RunGenerators(editedCompilation, cancellationToken);
        var secondMessage = secondRun.GetRunResult().Diagnostics.Single(diagnostic => diagnostic.Id == "CMP0051").GetMessage();
        secondMessage.Should().Contain("is ambiguous");
        secondMessage.Should().NotContain("was not found");

        var freshMessage = ComposeDataAotTestHelper.Discover(edited).Diagnostics.Single(diagnostic => diagnostic.Id == "CMP0051").GetMessage();
        secondMessage.Should().Be(freshMessage);
    }

    // ---- B-3: skip-condition property validation ----

    [Fact]
    public void SkipCondition_PrivateGetter_ReportsCmp0057()
    {
        var diagnostic = Single(
            """
            public sealed class SampleTests
            {
                public static bool ShouldSkip { private get; set; }
                public static System.Collections.Generic.IEnumerable<object?[]> Cases => [];

                [Compono.XunitV3.Aot.ComposeData("Cases", Skip = "reason", SkipUnless = "ShouldSkip")]
                public void TestMethod() { }
            }
            """,
            "CMP0057");

        diagnostic.GetMessage().Should().Contain("SkipUnless");
        diagnostic.GetMessage().Should().Contain("ShouldSkip");
    }

    [Fact]
    public void SkipCondition_SetterOnly_ReportsCmp0057()
    {
        var diagnostic = Single(
            """
            public sealed class SampleTests
            {
                public static bool ShouldSkip { set { } }
                public static System.Collections.Generic.IEnumerable<object?[]> Cases => [];

                [Compono.XunitV3.Aot.ComposeData("Cases", Skip = "reason", SkipUnless = "ShouldSkip")]
                public void TestMethod() { }
            }
            """,
            "CMP0057");

        diagnostic.GetMessage().Should().Contain("ShouldSkip");
    }

    [Fact]
    public void SkipCondition_DerivedInstanceHidingBaseStatic_ReportsCmp0057()
    {
        var diagnostic = Single(
            """
            public class Base
            {
                public static bool ShouldSkip => false;
            }

            public class SampleTests : Base
            {
                public new bool ShouldSkip => false;
                public static System.Collections.Generic.IEnumerable<object?[]> Cases => [];

                [Compono.XunitV3.Aot.ComposeData("Cases", Skip = "reason", SkipUnless = "ShouldSkip")]
                public void TestMethod() { }
            }
            """,
            "CMP0057");

        diagnostic.GetMessage().Should().Contain("ShouldSkip");
    }

    [Fact]
    public void SkipCondition_InheritedPublicStaticBool_IsRecordedWithBaseHolder()
    {
        var model = Compose(
            """
            public class Base
            {
                public static bool ShouldSkip => false;
            }

            public class SampleTests : Base
            {
                public static System.Collections.Generic.IEnumerable<object?[]> Cases => [];

                [Compono.XunitV3.Aot.ComposeData("Cases", Skip = "reason", SkipUnless = "ShouldSkip")]
                public void TestMethod() { }
            }
            """);

        model.Diagnostics.Should().BeEmpty();
        model.SkipUnless.Should().NotBeNull();
        model.SkipUnless!.PropertyName.Should().Be("ShouldSkip");
        model.SkipUnless!.FullyQualifiedHolderTypeName.Should().Be("global::TestNamespace.Base");
    }

    // ---- B-4: reflection-compatible property hiding ----

    [Theory]
    [InlineData("private")]
    [InlineData("internal")]
    [InlineData("protected")]
    public void NonPublicDerivedStaticProperty_SameType_HidesPublicBase_ReportsCmp0052(string accessibility)
    {
        var diagnostic = Single(
            $$"""
            public class Base
            {
                public static System.Collections.Generic.IEnumerable<object?[]> Cases => [];
            }

            public class SampleTests : Base
            {
                {{accessibility}} static System.Collections.Generic.IEnumerable<object?[]> Cases => [];

                [Compono.XunitV3.Aot.ComposeData("Cases")]
                public void TestMethod() { }
            }
            """,
            "CMP0052");

        diagnostic.GetMessage().Should().Contain("must be public and static");
    }

    [Fact]
    public void NonPublicDerivedStaticProperty_DifferentType_LeavesBaseSelectable()
    {
        var model = Compose(
            """
            public class Base
            {
                public static System.Collections.Generic.IEnumerable<object?[]> Cases => [];
            }

            public class SampleTests : Base
            {
                private static string[] Cases => [];

                [Compono.XunitV3.Aot.ComposeData("Cases")]
                public void TestMethod() { }
            }
            """);

        model.Diagnostics.Should().BeEmpty();
        model.MemberKind.Should().Be(AotComposeDataMemberKind.Property);
        model.FullyQualifiedMemberContainingTypeName.Should().Be("global::TestNamespace.Base");
    }

    // ---- C-1: inherited private property hiding ----

    [Fact]
    public void InheritedPrivateStaticProperty_DoesNotHidePublicBaseProperty_SelectsBaseProperty()
    {
        var model = Compose(
            """
            public class G
            {
                public static System.Collections.Generic.IEnumerable<object?[]> Cases => [];
            }

            public class M : G
            {
                private static new System.Collections.Generic.IEnumerable<object?[]> Cases => [];
            }

            public sealed class L : M
            {
                [Compono.XunitV3.Aot.ComposeData("Cases")]
                public void TestMethod() { }
            }
            """);

        model.MemberKind.Should().Be(AotComposeDataMemberKind.Property);
        model.FullyQualifiedMemberContainingTypeName.Should().Be("global::TestNamespace.G");
    }

    [Fact]
    public void PrivateStaticProperty_OnLookupType_ReportsNotPublicStatic()
    {
        var diagnostic = Single(
            """
            public class G
            {
                public static System.Collections.Generic.IEnumerable<object?[]> Cases => [];
            }

            public sealed class M : G
            {
                private static new System.Collections.Generic.IEnumerable<object?[]> Cases => [];

                [Compono.XunitV3.Aot.ComposeData("Cases")]
                public void TestMethod() { }
            }
            """,
            "CMP0052");

        diagnostic.GetMessage().Should().Contain("must be public and static");
    }

    // ---- C-2: [Shared] CLR type identity ----

    private static string DuplicateSharedMessage(string firstType, string secondType)
    {
        var source = Wrap(
            "public sealed class Box<T> { }\n\n" +
            "public sealed class SampleTests\n{\n" +
            "    [Compono.XunitV3.Aot.ComposeData(\"Cases\")]\n" +
            $"    public void TestMethod([Compono.XunitV3.Aot.Shared] {firstType} first, [Compono.XunitV3.Aot.Shared] {secondType} second) {{ }}\n" +
            "}\n");

        return ComposeDataAotTestHelper.Discover(source)
            .Diagnostics.Single(diagnostic => diagnostic.Id == "CMP0040")
            .GetMessage();
    }

    [Fact]
    public void DuplicateShared_DynamicParameter_ReportsNonComposableCmp0040()
    {
        var diagnostics = ComposeDataAotTestHelper.AssertDiagnostic(
            Wrap(
                """
                public sealed class SampleTests
                {
                    [Compono.XunitV3.Aot.ComposeData("Cases")]
                    public void TestMethod([Compono.XunitV3.Aot.Shared] dynamic first, [Compono.XunitV3.Aot.Shared] object second) { }
                }
                """),
            "CMP0040");

        diagnostics.Single(diagnostic => diagnostic.Id == "CMP0040").GetMessage().Should().Contain("cannot be composed");
    }

    [Theory]
    [InlineData("Box<(int a, int b)>", "Box<(int c, int d)>")]
    [InlineData("Box<dynamic>", "Box<object>")]
    [InlineData("Box<dynamic[]>", "Box<object[]>")]
    [InlineData("Box<Box<(int a, dynamic b)>>", "Box<Box<(int, object?)>>")]
    [InlineData("Box<string?>", "Box<string>")]
    [InlineData("(int x, int y)", "(int p, int q)")]
    public void DuplicateShared_ClrIdentity_ReportsDuplicateShared(string firstType, string secondType)
    {
        DuplicateSharedMessage(firstType, secondType)
            .Should().Contain("More than one [Shared] parameter");
    }

    [Fact]
    public void SharedDistinctClrTypes_BoxIntAndBoxNullableInt_ProduceNoDuplicateDiagnostic()
    {
        var diagnostics = ComposeDataAotTestHelper.Discover(
            Wrap(
                "public sealed class Box<T> { }\n\n" +
                "public sealed class SampleTests\n{\n" +
                "    public static System.Collections.Generic.IEnumerable<object?[]> Cases => [];\n" +
                "    [Compono.XunitV3.Aot.ComposeData(\"Cases\")]\n" +
                "    public void TestMethod([Compono.XunitV3.Aot.Shared] Box<int> first, [Compono.XunitV3.Aot.Shared] Box<int?> second) { }\n" +
                "}\n"))
            .Diagnostics;

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void SharedDistinctTypes_NoDuplicateDiagnostic()
    {
        var model = Compose(
            """
            public sealed class SampleTests
            {
                public static System.Collections.Generic.IEnumerable<object?[]> Cases => [];

                [Compono.XunitV3.Aot.ComposeData("Cases")]
                public void TestMethod([Compono.XunitV3.Aot.Shared] string first, [Compono.XunitV3.Aot.Shared] int second) { }
            }
            """);

        model.Diagnostics.Should().BeEmpty();
    }

    // ---- Inv2: mixed-family CMP0045 wording ----

    [Fact]
    public void ComposeAndComposeData_MixedStack_ReportsCmp0045NamingAllSixForms()
    {
        var diagnostics = ComposeDataAotTestHelper.AssertDiagnostic(
            Wrap(
                """
                public sealed class SampleTests
                {
                    [Compono.XunitV3.Aot.Compose]
                    [Compono.XunitV3.Aot.ComposeData("Cases")]
                    public void TestMethod(string value) { }
                }
                """),
            "CMP0045");

        diagnostics
            .Select(diagnostic => diagnostic.GetMessage())
            .Should()
            .Contain(message => message.Contains("[ComposeData<TProfile, TConfig>]"));
    }

    // ---- Inv3: diagnostic formatting ----

    [Fact]
    public void DuplicateSharedMessage_HasNoDoublePeriod()
    {
        var diagnostic = Single(
            """
            public sealed class SampleTests
            {
                [Compono.XunitV3.Aot.ComposeData("Cases")]
                public void TestMethod([Compono.XunitV3.Aot.Shared] string first, [Compono.XunitV3.Aot.Shared] string second) { }
            }
            """,
            "CMP0040");

        diagnostic.GetMessage().Should().NotContain("..");
        diagnostic.GetMessage().Should().Contain("is allowed. ComposeData supports only");
    }

    // ---- Strengthened acceptance coverage ----

    [Fact]
    public void Cmp0045_MixedStack_SuppressingModelAsValid()
    {
        var (diagnostics, models) = ComposeDataAotTestHelper.Discover(
            Wrap(
                """
                public sealed class SampleTests
                {
                    [Compono.XunitV3.Aot.Compose]
                    [Compono.XunitV3.Aot.ComposeData("Cases")]
                    public void TestMethod(string value) { }
                }
                """));

        diagnostics.Select(diagnostic => diagnostic.Id).Should().Contain("CMP0045");
        models.Should().NotBeEmpty();
        models.Should().OnlyContain(model => model.Diagnostics.Any(diagnostic => diagnostic.Descriptor.Id == "CMP0045"));
    }

    [Fact]
    public void Cmp0051_NotFoundAndAmbiguous_MessagesDiffer()
    {
        var notFound = Single(
            """
            public sealed class SampleTests
            {
                [Compono.XunitV3.Aot.ComposeData("Cases")]
                public void TestMethod() { }
            }
            """,
            "CMP0051");
        notFound.GetMessage().Should().Contain("was not found");

        var ambiguous = Single(
            """
            public sealed class SampleTests
            {
                public static System.Collections.Generic.IEnumerable<object?[]> Cases() => [];
                public static System.Collections.Generic.IEnumerable<object?[]> Cases<T>() => [];

                [Compono.XunitV3.Aot.ComposeData("Cases")]
                public void TestMethod() { }
            }
            """,
            "CMP0051");
        ambiguous.GetMessage().Should().Contain("is ambiguous");
    }

    [Fact]
    public void ComposeDataWithProfile_PrivateProfile_ReportsCmp0044()
    {
        var diagnostic = Single(
            """
            public sealed class SampleTests
            {
                private sealed class HiddenProfile : Compono.ICompositionProfile
                {
                    public void Configure(Compono.CompositionBuilder builder) { }
                }

                [Compono.XunitV3.Aot.ComposeData<HiddenProfile>("Cases")]
                public void TestMethod(string value) { }
            }
            """,
            "CMP0044");

        diagnostic.GetMessage().Should().Contain("TProfile");
    }
}
