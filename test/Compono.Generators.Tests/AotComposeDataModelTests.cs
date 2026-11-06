using System.Linq;
using Compono.Generators.Models;
using Xunit;

namespace Compono.Generators.Tests;

public sealed class AotComposeDataModelTests
{
    private const string Method = """
        [Compono.XunitV3.Aot.ComposeData("Cases")]
        public void TestMethod(string value) { }
        """;

    private static string Source(string body) => $"namespace TestNamespace\n{{\nusing Compono;\n{body}\n}}\n";

    private static AotComposeDataMethodInfo Compose(string body) =>
        ComposeDataAotTestHelper.SingleModel(Source(body));

    private static AotComposeDataMethodInfo ComposeWithAttribute(string attributeArguments)
    {
        var source = Source($$"""
            public sealed class SampleTests
            {
                public static System.Collections.Generic.IEnumerable<object> Cases() => new object[] { "x" };

                [Compono.XunitV3.Aot.ComposeData({{attributeArguments}})]
                public void TestMethod(string value) { }
            }
            """);

        return ComposeDataAotTestHelper.SingleModel(source);
    }

    [Fact]
    public void Seed_Unset_IsNull()
    {
        var model = ComposeWithAttribute("\"Cases\"");

        model.AttributeMetadata.Seed.Should().BeNull();
    }

    [Fact]
    public void Seed_Zero_IsStoredAsZero()
    {
        var model = ComposeWithAttribute("\"Cases\", Seed = 0");

        model.AttributeMetadata.Seed.Should().Be(0);
    }

    [Fact]
    public void Seed_Negative_IsStoredWithoutDiagnostic()
    {
        var model = ComposeWithAttribute("\"Cases\", Seed = -1");

        model.AttributeMetadata.Seed.Should().Be(-1);
    }

    [Fact]
    public void Metadata_WhenAbsent_IsEmpty()
    {
        var model = ComposeWithAttribute("\"Cases\"");
        var metadata = model.AttributeMetadata;

        metadata.Label.Should().BeNull();
        metadata.Skip.Should().BeNull();
        metadata.TestDisplayName.Should().BeNull();
        metadata.Explicit.Should().BeNull();
        metadata.Timeout.Should().BeNull();
        metadata.DisableParallelization.Should().BeNull();
        metadata.Traits.Count.Should().Be(0);
    }

    [Fact]
    public void Metadata_WhenPresent_IsCaptured()
    {
        var model = ComposeWithAttribute(
            "\"Cases\", Label = \"the label\", TestDisplayName = \"display\", Explicit = true, Timeout = 42, DisableParallelization = true, Traits = new[] { \"a\", \"b\" }");

        var metadata = model.AttributeMetadata;
        metadata.Label.Should().Be("the label");
        metadata.TestDisplayName.Should().Be("display");
        metadata.Explicit.Should().BeTrue();
        metadata.Timeout.Should().Be(42);
        metadata.DisableParallelization.Should().BeTrue();
        metadata.Traits.GetArray().Should().Equal("a", "b");
    }

    [Fact]
    public void AttributeArity_NonGeneric_IsZeroWithoutProfile()
    {
        var model = ComposeWithAttribute("\"Cases\"");

        model.AttributeArity.Should().Be(0);
        model.FullyQualifiedProfileTypeName.Should().BeNull();
        model.FullyQualifiedConfigTypeName.Should().BeNull();
    }

    [Fact]
    public void AttributeArity_OneTypeParameter_CapturesProfile()
    {
        var source = Source($$"""
            public sealed class SampleProfile : Compono.ICompositionProfile
            {
                public void Configure(Compono.CompositionBuilder builder) { }
            }

            public sealed class SampleTests
            {
                public static System.Collections.Generic.IEnumerable<object> Cases() => new object[] { "x" };

                [Compono.XunitV3.Aot.ComposeData<SampleProfile>("Cases")]
                public void TestMethod(string value) { }
            }
            """);

        var model = ComposeDataAotTestHelper.SingleModel(source);

        model.AttributeArity.Should().Be(1);
        model.FullyQualifiedProfileTypeName.Should().Be("global::TestNamespace.SampleProfile");
        model.FullyQualifiedConfigTypeName.Should().BeNull();
    }

    [Fact]
    public void AttributeArity_TwoTypeParameters_CapturesProfileAndConfig()
    {
        var source = Source($$"""
            public sealed class SampleConfig { }

            public sealed class ConfigProfile : Compono.ICompositionProfile
            {
                public ConfigProfile(SampleConfig config) { }

                public void Configure(Compono.CompositionBuilder builder) { }
            }

            public sealed class SampleTests
            {
                public static System.Collections.Generic.IEnumerable<object> Cases() => new object[] { new SampleConfig(), "x" };

                [Compono.XunitV3.Aot.ComposeData<ConfigProfile, SampleConfig>("Cases")]
                public void TestMethod(string value) { }
            }
            """);

        var model = ComposeDataAotTestHelper.SingleModel(source);

        model.AttributeArity.Should().Be(2);
        model.FullyQualifiedProfileTypeName.Should().Be("global::TestNamespace.ConfigProfile");
        model.FullyQualifiedConfigTypeName.Should().Be("global::TestNamespace.SampleConfig");
    }

    [Fact]
    public void MethodMetadata_IsCaptured()
    {
        var model = Compose($$"""
            public sealed class SampleTests
            {
                public static Xunit.TheoryData<int, string> Cases() => new Xunit.TheoryData<int, string>();

                [Compono.XunitV3.Aot.ComposeData("Cases")]
                public void TestMethod(int value, string text) { }
            }
            """);

        model.FullyQualifiedTestClassName.Should().Be("global::TestNamespace.SampleTests");
        model.MethodName.Should().Be("TestMethod");
        model.FullyQualifiedLookupTypeName.Should().Be("global::TestNamespace.SampleTests");
        model.MemberName.Should().Be("Cases");
        model.MemberKind.Should().Be(AotComposeDataMemberKind.Method);
        model.FullyQualifiedMemberContainingTypeName.Should().Be("global::TestNamespace.SampleTests");
        model.ReturnShape.Should().Be(AotComposeDataReturnShape.Sync);
        model.RowsAreTyped.Should().BeTrue();
        model.RowElementTypeNames.GetArray().Should().Equal("int", "string");
    }

    [Fact]
    public void Parameters_CaptureNameTypeOrdinalNullabilityAndShared()
    {
        var source = Source("""
            public sealed class SampleTests
            {
                public static System.Collections.Generic.IEnumerable<object> Cases() => new object[] { "x" };

                [Compono.XunitV3.Aot.ComposeData("Cases")]
                public void TestMethod(string value, [Compono.XunitV3.Aot.Shared] System.IDisposable a, [Compono.XunitV3.Aot.Shared] System.IAsyncDisposable b) { }
            }
            """);

        var model = ComposeDataAotTestHelper.SingleModel(source);

        model.Parameters.Count.Should().Be(3);
        model.Parameters[0].Name.Should().Be("value");
        model.Parameters[0].FullyQualifiedTypeName.Should().Be("string");
        model.Parameters[0].Ordinal.Should().Be(0);
        model.Parameters[0].IsNullable.Should().BeFalse();
        model.Parameters[0].IsShared.Should().BeFalse();
        model.Parameters[1].IsShared.Should().BeTrue();
        model.Parameters[2].IsShared.Should().BeTrue();
    }

    [Fact]
    public void ReturnShape_AsyncEnumerable_IsCaptured()
    {
        var source = Source("""
            public sealed class SampleTests
            {
                public static async System.Collections.Generic.IAsyncEnumerable<object> Cases()
                {
                    await System.Threading.Tasks.Task.CompletedTask;
                    yield return "x";
                }

                [Compono.XunitV3.Aot.ComposeData("Cases")]
                public void TestMethod(string value) { }
            }
            """);

        var model = ComposeDataAotTestHelper.SingleModel(source);

        model.ReturnShape.Should().Be(AotComposeDataReturnShape.AsyncEnumerable);
    }

    [Fact]
    public void Rows_UntypedWhenNoElementTypeIsDiscovered()
    {
        var model = Compose($$"""
            public sealed class SampleTests
            {
                public static System.Collections.IEnumerable Cases() => new object[] { "x" };

                {{Method}}
            }
            """);

        model.RowsAreTyped.Should().BeFalse();
        model.RowElementTypeNames.Count.Should().Be(0);
    }
}
