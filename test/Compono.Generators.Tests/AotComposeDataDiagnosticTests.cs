using System.Collections.Immutable;
using System.Linq;
using Compono.Generators.Models;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Compono.Generators.Tests;

public sealed class AotComposeDataDiagnosticTests
{
    private const string Preamble = """
        public sealed class SampleConfig { }

        public sealed class SampleProfile : ICompositionProfile
        {
            public void Configure(CompositionBuilder builder) { }
        }

        public sealed class ConfigProfile : ICompositionProfile
        {
            public ConfigProfile(SampleConfig config) { }
            public void Configure(CompositionBuilder builder) { }
        }

        """;

    private static string Source(string body) => $"namespace TestNamespace\n{{\nusing Compono;\n{Preamble}{body}\n}}\n";

    private static ImmutableArray<Diagnostic> Diagnostics(string body) =>
        ComposeDataAotTestHelper.Discover(Source(body)).Diagnostics;

    private static string SingleId(string body) => Diagnostics(body).Single().Id;

    private static string SingleMessage(string body) => Diagnostics(body).Single().GetMessage();

    private static AotComposeDataMethodInfo Compose(string body) =>
        ComposeDataAotTestHelper.SingleModel(Source(body));

    [Fact]
    public void GenericTestMethod_ReportsCmp0040()
    {
        var id = SingleId("""
            public sealed class SampleTests
            {
                [Compono.XunitV3.Aot.ComposeData("Cases")]
                public void TestMethod<T>(T value) { }
            }
            """);

        id.Should().Be("CMP0040");
    }

    [Fact]
    public void RefParameter_ReportsCmp0040()
    {
        var id = SingleId("""
            public sealed class SampleTests
            {
                [Compono.XunitV3.Aot.ComposeData("Cases")]
                public void TestMethod(ref string value) { }
            }
            """);

        id.Should().Be("CMP0040");
    }

    [Fact]
    public void ParamsParameter_ReportsCmp0040()
    {
        var id = SingleId("""
            public sealed class SampleTests
            {
                [Compono.XunitV3.Aot.ComposeData("Cases")]
                public void TestMethod(params string[] values) { }
            }
            """);

        id.Should().Be("CMP0040");
    }

    [Fact]
    public void DuplicateSharedStringParameters_ReportsCmp0040()
    {
        var id = SingleId("""
            public sealed class SampleTests
            {
                [Compono.XunitV3.Aot.ComposeData("Cases")]
                public void TestMethod(
                    [Compono.XunitV3.Aot.Shared] string a,
                    [Compono.XunitV3.Aot.Shared] string? b) { }
            }
            """);

        id.Should().Be("CMP0040");
    }

    [Fact]
    public void DuplicateSharedIntAndNullableInt_IsNotADuplicate()
    {
        var model = Compose("""
            public sealed class SampleTests
            {
                public static System.Collections.Generic.IEnumerable<object?[]> Cases => [];

                [Compono.XunitV3.Aot.ComposeData("Cases")]
                public void TestMethod(
                    [Compono.XunitV3.Aot.Shared] int a,
                    [Compono.XunitV3.Aot.Shared] int? b) { }
            }
            """);

        model.Diagnostics.Should().BeEmpty();
    }

    [Theory]
    [InlineData("[Compono.XunitV3.Aot.Compose]", "[Compono.XunitV3.Aot.Compose<SampleProfile>]")]
    [InlineData("[Compono.XunitV3.Aot.Compose]", "[Compono.XunitV3.Aot.Compose<ConfigProfile, SampleConfig>]")]
    [InlineData("[Compono.XunitV3.Aot.Compose]", "[Compono.XunitV3.Aot.ComposeData(\"Cases\")]")]
    [InlineData("[Compono.XunitV3.Aot.Compose]", "[Compono.XunitV3.Aot.ComposeData<SampleProfile>(\"Cases\")]")]
    [InlineData("[Compono.XunitV3.Aot.Compose]", "[Compono.XunitV3.Aot.ComposeData<ConfigProfile, SampleConfig>(\"Cases\")]")]
    [InlineData("[Compono.XunitV3.Aot.Compose<SampleProfile>]", "[Compono.XunitV3.Aot.Compose<ConfigProfile, SampleConfig>]")]
    [InlineData("[Compono.XunitV3.Aot.Compose<SampleProfile>]", "[Compono.XunitV3.Aot.ComposeData(\"Cases\")]")]
    [InlineData("[Compono.XunitV3.Aot.Compose<SampleProfile>]", "[Compono.XunitV3.Aot.ComposeData<SampleProfile>(\"Cases\")]")]
    [InlineData("[Compono.XunitV3.Aot.Compose<SampleProfile>]", "[Compono.XunitV3.Aot.ComposeData<ConfigProfile, SampleConfig>(\"Cases\")]")]
    [InlineData("[Compono.XunitV3.Aot.Compose<ConfigProfile, SampleConfig>]", "[Compono.XunitV3.Aot.ComposeData(\"Cases\")]")]
    [InlineData("[Compono.XunitV3.Aot.Compose<ConfigProfile, SampleConfig>]", "[Compono.XunitV3.Aot.ComposeData<SampleProfile>(\"Cases\")]")]
    [InlineData("[Compono.XunitV3.Aot.Compose<ConfigProfile, SampleConfig>]", "[Compono.XunitV3.Aot.ComposeData<ConfigProfile, SampleConfig>(\"Cases\")]")]
    [InlineData("[Compono.XunitV3.Aot.ComposeData(\"Cases\")]", "[Compono.XunitV3.Aot.ComposeData<SampleProfile>(\"Cases\")]")]
    [InlineData("[Compono.XunitV3.Aot.ComposeData(\"Cases\")]", "[Compono.XunitV3.Aot.ComposeData<ConfigProfile, SampleConfig>(\"Cases\")]")]
    [InlineData("[Compono.XunitV3.Aot.ComposeData<SampleProfile>(\"Cases\")]", "[Compono.XunitV3.Aot.ComposeData<ConfigProfile, SampleConfig>(\"Cases\")]")]
    public void StackedComposeFamilyAttributes_ReportCmp0045(string first, string second)
    {
        var diagnostics = Diagnostics($$"""
            public sealed class SampleTests
            {
                public static System.Collections.Generic.IEnumerable<object?[]> Cases => [];

                {{first}}
                {{second}}
                public void TestMethod(string value) { }
            }
            """);

        diagnostics.Select(d => d.Id).Should().Contain("CMP0045");
    }

    [Fact]
    public void InlineDataAndMemberDataAlongsideComposeData_NoDiagnostic()
    {
        var model = Compose("""
            public sealed class SampleTests
            {
                public static System.Collections.Generic.IEnumerable<object?[]> Cases => [];

                [Xunit.InlineData(1)]
                [Xunit.MemberData("Cases")]
                [Compono.XunitV3.Aot.ComposeData("Cases")]
                public void TestMethod(string value) { }
            }
            """);

        model.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void ActiveSkipCondition_OnTestClass_IsRecorded()
    {
        var model = Compose("""
            public sealed class SampleTests
            {
                public static System.Collections.Generic.IEnumerable<object?[]> Cases => [];
                public static bool ShouldSkip => true;

                [Compono.XunitV3.Aot.ComposeData("Cases", Skip = "reason", SkipUnless = "ShouldSkip")]
                public void TestMethod(string value) { }
            }
            """);

        model.SkipUnless.Should().NotBeNull();
        model.SkipUnless!.PropertyName.Should().Be("ShouldSkip");
        model.SkipWhen.Should().BeNull();
    }

    [Fact]
    public void ActiveSkipCondition_OnBaseClass_IsRecorded()
    {
        var model = Compose("""
            public class SkipBase
            {
                public static bool ShouldSkip => true;
            }

            public sealed class SampleTests : SkipBase
            {
                public static System.Collections.Generic.IEnumerable<object?[]> Cases => [];

                [Compono.XunitV3.Aot.ComposeData("Cases", Skip = "reason", SkipWhen = "ShouldSkip")]
                public void TestMethod(string value) { }
            }
            """);

        model.SkipWhen.Should().NotBeNull();
        model.SkipWhen!.PropertyName.Should().Be("ShouldSkip");
    }

    [Fact]
    public void ActiveSkipCondition_WithSkipType_IsRecorded()
    {
        var model = Compose("""
            public static class SkipHolder
            {
                public static bool ShouldSkip => true;
            }

            public sealed class SampleTests
            {
                public static System.Collections.Generic.IEnumerable<object?[]> Cases => [];

                [Compono.XunitV3.Aot.ComposeData("Cases", Skip = "reason", SkipUnless = "ShouldSkip", SkipType = typeof(SkipHolder))]
                public void TestMethod(string value) { }
            }
            """);

        model.SkipUnless.Should().NotBeNull();
        model.SkipUnless!.FullyQualifiedHolderTypeName.Should().Be("global::TestNamespace.SkipHolder");
    }

    [Fact]
    public void InactiveSkipConditions_AreNotRecorded()
    {
        var model = Compose("""
            public sealed class SampleTests
            {
                public static System.Collections.Generic.IEnumerable<object?[]> Cases => [];

                [Compono.XunitV3.Aot.ComposeData("Cases", SkipUnless = "Missing", SkipWhen = "AlsoMissing")]
                public void TestMethod(string value) { }
            }
            """);

        model.SkipUnless.Should().BeNull();
        model.SkipWhen.Should().BeNull();
    }

    [Fact]
    public void ActiveSkipCondition_MissingProperty_ReportsCmp0057()
    {
        var id = SingleId("""
            public sealed class SampleTests
            {
                public static System.Collections.Generic.IEnumerable<object?[]> Cases => [];

                [Compono.XunitV3.Aot.ComposeData("Cases", Skip = "reason", SkipUnless = "Missing")]
                public void TestMethod(string value) { }
            }
            """);

        id.Should().Be("CMP0057");
    }

    [Theory]
    [InlineData("Skip = \"r\", SkipUnless = \"A\", SkipWhen = \"B\"")]
    [InlineData("Skip = \"r\", SkipUnless = \"InstanceProp\"")]
    [InlineData("Skip = \"r\", SkipUnless = \"NonPublicProp\"")]
    [InlineData("Skip = \"r\", SkipUnless = \"NonBoolProp\"")]
    public void ActiveSkipCondition_InvalidShape_ReportsCmp0057(string skipArguments)
    {
        var id = SingleId($$"""
            public sealed class SampleTests
            {
                public static System.Collections.Generic.IEnumerable<object?[]> Cases => [];
                public bool InstanceProp => true;
                private static bool NonPublicProp => true;
                public static int NonBoolProp => 1;

                [Compono.XunitV3.Aot.ComposeData("Cases", {{skipArguments}})]
                public void TestMethod(string value) { }
            }
            """);

        id.Should().Be("CMP0057");
    }

    [Fact]
    public void MemberTypeHeldInPrivateNestedType_ReportsCmp0044()
    {
        var id = SingleId("""
            public sealed class SampleTests
            {
                private static class PrivateHolder
                {
                    public static System.Collections.Generic.IEnumerable<object?[]> Cases => [];
                }

                [Compono.XunitV3.Aot.ComposeData("Cases", MemberType = typeof(PrivateHolder))]
                public void TestMethod(string value) { }
            }
            """);

        id.Should().Be("CMP0044");
    }

    [Fact]
    public void MemberTypeReachableOnlyThroughPrivateNestedType_ReportsCmp0044()
    {
        var id = SingleId("""
            public sealed class SampleTests
            {
                private static class PrivateOuter
                {
                    public static class Inner
                    {
                        public static System.Collections.Generic.IEnumerable<object?[]> Cases => [];
                    }
                }

                [Compono.XunitV3.Aot.ComposeData("Cases", MemberType = typeof(PrivateOuter.Inner))]
                public void TestMethod(string value) { }
            }
            """);

        id.Should().Be("CMP0044");
    }

    [Fact]
    public void ActiveSkipCondition_PrivateSkipTypeHolder_ReportsCmp0044()
    {
        var id = SingleId("""
            public sealed class SampleTests
            {
                public static System.Collections.Generic.IEnumerable<object?[]> Cases => [];

                private static class PrivateSkipHolder
                {
                    public static bool ShouldSkip => true;
                }

                [Compono.XunitV3.Aot.ComposeData("Cases", Skip = "reason", SkipUnless = "ShouldSkip", SkipType = typeof(PrivateSkipHolder))]
                public void TestMethod(string value) { }
            }
            """);

        id.Should().Be("CMP0044");
    }

    [Fact]
    public void InternalMemberTypeAndSkipTypeHolders_AreValid()
    {
        var model = Compose("""
            internal static class InternalHolder
            {
                public static System.Collections.Generic.IEnumerable<object?[]> Cases => [];
                public static bool ShouldSkip => true;
            }

            public sealed class SampleTests
            {
                [Compono.XunitV3.Aot.ComposeData("Cases", MemberType = typeof(InternalHolder), Skip = "reason", SkipUnless = "ShouldSkip", SkipType = typeof(InternalHolder))]
                public void TestMethod(string value) { }
            }
            """);

        model.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Precedence_MultipleComposeFamilyAttributesBeatsEverything()
    {
        var diagnostics = Diagnostics("""
            public sealed class SampleTests
            {
                [Compono.XunitV3.Aot.Compose]
                [Compono.XunitV3.Aot.ComposeData("Cases")]
                public void TestMethod<T>(T value) { }
            }
            """);

        diagnostics.Select(d => d.Id).Should().Contain("CMP0045");
        diagnostics.Select(d => d.Id).Should().OnlyContain(id => id == "CMP0045");
    }

    [Fact]
    public void Precedence_SignatureBeatsMemberResolution()
    {
        var id = SingleId("""
            public sealed class SampleTests
            {
                [Compono.XunitV3.Aot.ComposeData("Missing")]
                public void TestMethod<T>(T value) { }
            }
            """);

        id.Should().Be("CMP0040");
    }

    [Fact]
    public void Precedence_OpenGenericMemberTypeBeatsMemberResolution()
    {
        var id = SingleId("""
            public sealed class Holder<T> { }

            public sealed class SampleTests
            {
                [Compono.XunitV3.Aot.ComposeData("Missing", MemberType = typeof(Holder<>))]
                public void TestMethod(string value) { }
            }
            """);

        id.Should().Be("CMP0054");
    }

    [Fact]
    public void Precedence_ComposeDataSignatureBeatsMemberResolution()
    {
        SingleMessage("""
            public sealed class SampleTests
            {
                [Compono.XunitV3.Aot.ComposeData("Missing")]
                public void TestMethod<T>(T value) { }
            }
            """).Should().Contain("cannot be registered");
    }

    [Fact]
    public void Precedence_MemberResolutionBeatsInaccessibleMemberTypeHolder()
    {
        SingleMessage("""
            public sealed class SampleTests
            {
                private sealed class PrivateHolder { }

                [Compono.XunitV3.Aot.ComposeData("Missing", MemberType = typeof(PrivateHolder))]
                public void TestMethod(string value) { }
            }
            """).Should().Contain("was not found");
    }

    [Fact]
    public void Precedence_ConfigValueMismatchBeatsTooManyRowValues()
    {
        SingleMessage("""
            public sealed class OtherConfig { }

            public sealed class SampleTests
            {
                public static Xunit.TheoryData<OtherConfig, int, int> Cases { get; } = new();

                [Compono.XunitV3.Aot.ComposeData<ConfigProfile, SampleConfig>("Cases")]
                public void TestMethod(int value) { }
            }
            """).Should().Contain("row element 0");
    }

    [Fact]
    public void Precedence_TooManyRowValuesBeatsRowValueTypeMismatch()
    {
        SingleMessage("""
            public sealed class SampleTests
            {
                public static Xunit.TheoryData<SampleConfig, string, int> Cases { get; } = new();

                [Compono.XunitV3.Aot.ComposeData<ConfigProfile, SampleConfig>("Cases")]
                public void TestMethod(System.Guid value) { }
            }
            """).Should().Contain("supplies 2 row value(s)");
    }

    [Fact]
    public void Precedence_ConflictingSkipConditionsBeatsInaccessibleSkipTypeHolder()
    {
        SingleMessage("""
            public sealed class SampleTests
            {
                private sealed class PrivateSkipHolder
                {
                    public static bool ShouldSkip => true;
                }

                public static System.Collections.Generic.IEnumerable<object?[]> Cases => [];

                [Compono.XunitV3.Aot.ComposeData("Cases", Skip = "reason", SkipUnless = "ShouldSkip", SkipWhen = "ShouldSkip", SkipType = typeof(PrivateSkipHolder))]
                public void TestMethod(string value) { }
            }
            """).Should().Contain("sets both SkipUnless and SkipWhen");
    }

    [Fact]
    public void Precedence_SignatureBeatsProfileValidation()
    {
        SingleMessage("""
            public sealed class SampleTests
            {
                private sealed class PrivateProfile : Compono.ICompositionProfile
                {
                    public void Configure(Compono.CompositionBuilder builder) { }
                }

                public static System.Collections.Generic.IEnumerable<object?[]> Cases => [];

                [Compono.XunitV3.Aot.ComposeData<PrivateProfile>("Cases")]
                public void TestMethod<T>(T value) { }
            }
            """).Should().Contain("cannot be registered");
    }

    [Fact]
    public void Precedence_ProfileValidationBeatsOpenGenericMemberType()
    {
        SingleMessage("""
            public sealed class SampleTests
            {
                public sealed class Holder<T> { }

                public static System.Collections.Generic.IEnumerable<object?[]> Cases => [];

                [Compono.XunitV3.Aot.ComposeData<SampleProfile, SampleConfig>("Cases", MemberType = typeof(Holder<>))]
                public void TestMethod(string value) { }
            }
            """).Should().Contain("usable public constructor");
    }

    [Fact]
    public void Precedence_MemberTypeHolderAccessibilityBeatsUnsupportedReturnType()
    {
        SingleMessage("""
            public sealed class SampleTests
            {
                private sealed class PrivateHolder
                {
                    public static string Cases => "x";
                }

                [Compono.XunitV3.Aot.ComposeData("Cases", MemberType = typeof(PrivateHolder))]
                public void TestMethod(string value) { }
            }
            """).Should().Contain("the MemberType type argument");
    }

    [Fact]
    public void Precedence_UnsupportedReturnTypeBeatsConfigValueMismatch()
    {
        SingleMessage("""
            public sealed class SampleTests
            {
                public static string Cases => "x";

                [Compono.XunitV3.Aot.ComposeData<ConfigProfile, SampleConfig>("Cases")]
                public void TestMethod(string value) { }
            }
            """).Should().Contain("unsupported return type");
    }

    [Fact]
    public void Precedence_TooManyRowValuesBeatsConflictingSkipConditions()
    {
        SingleMessage("""
            public sealed class SampleTests
            {
                public static Xunit.TheoryData<SampleConfig, int, int> Cases { get; } = new();

                [Compono.XunitV3.Aot.ComposeData<ConfigProfile, SampleConfig>("Cases", Skip = "reason", SkipUnless = "A", SkipWhen = "B")]
                public void TestMethod(int value) { }
            }
            """).Should().Contain("supplies 2 row value(s)");
    }
}
