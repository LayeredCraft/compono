using System.Linq;
using Xunit;

namespace Compono.Generators.Tests;

public sealed class AotComposeDataProfileTests
{
    private static string Source(string body) => $"namespace TestNamespace\n{{\nusing Compono;\n{body}\n}}\n";

    private static string DiagnosticId(string body) =>
        ComposeDataAotTestHelper.Discover(Source(body)).Diagnostics.Single().Id;

    private static void AssertNoDiagnostics(string body) =>
        ComposeDataAotTestHelper.SingleModel(Source(body)).Diagnostics.Should().BeEmpty();

    private static string DiagnosticMessage(string body) =>
        ComposeDataAotTestHelper.Discover(Source(body)).Diagnostics.Single().GetMessage();

    [Fact]
    public void OneTypeParameterForm_WithUsableProfile_ProducesNoDiagnostics()
    {
        AssertNoDiagnostics("""
            public sealed class SampleProfile : Compono.ICompositionProfile
            {
                public void Configure(Compono.CompositionBuilder builder) { }
            }

            public sealed class SampleTests
            {
                public static System.Collections.Generic.IEnumerable<object?[]> Cases { get; } = System.Array.Empty<object?[]>();

                [Compono.XunitV3.Aot.ComposeData<SampleProfile>("Cases")]
                public void TestMethod(string a) { }
            }
            """);
    }

    [Fact]
    public void OneTypeParameterForm_WithAotUnsafeParameterlessConstructor_ReportsCmp0049()
    {
        DiagnosticId("""
            public sealed class AotUnsafeProfile : Compono.ICompositionProfile
            {
                [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("unsafe")]
                public AotUnsafeProfile() { }

                public void Configure(Compono.CompositionBuilder builder) { }
            }

            public sealed class SampleTests
            {
                public static System.Collections.Generic.IEnumerable<object?[]> Cases { get; } = System.Array.Empty<object?[]>();

                [Compono.XunitV3.Aot.ComposeData<AotUnsafeProfile>("Cases")]
                public void TestMethod(string a) { }
            }
            """).Should().Be("CMP0049");
    }

    [Fact]
    public void TwoTypeParameterForm_WithUsableProfileAndConfig_ProducesNoDiagnostics()
    {
        AssertNoDiagnostics("""
            public sealed class SampleConfig { }

            public sealed class SampleProfile : Compono.ICompositionProfile
            {
                public SampleProfile(SampleConfig config) { }

                public void Configure(Compono.CompositionBuilder builder) { }
            }

            public sealed class SampleTests
            {
                public static System.Collections.Generic.IEnumerable<object?[]> Cases { get; } = System.Array.Empty<object?[]>();

                [Compono.XunitV3.Aot.ComposeData<SampleProfile, SampleConfig>("Cases")]
                public void TestMethod(string a) { }
            }
            """);
    }

    [Fact]
    public void TwoTypeParameterForm_ProfileWithoutMatchingConstructor_ReportsCmp0042()
    {
        DiagnosticId("""
            public sealed class SampleConfig { }

            public sealed class SampleProfile : Compono.ICompositionProfile
            {
                public SampleProfile(string unrelated) { }

                public void Configure(Compono.CompositionBuilder builder) { }
            }

            public sealed class SampleTests
            {
                public static System.Collections.Generic.IEnumerable<object?[]> Cases { get; } = System.Array.Empty<object?[]>();

                [Compono.XunitV3.Aot.ComposeData<SampleProfile, SampleConfig>("Cases")]
                public void TestMethod(string a) { }
            }
            """).Should().Be("CMP0042");
    }

    [Fact]
    public void TwoTypeParameterForm_InaccessibleProfile_ReportsCmp0044()
    {
        DiagnosticId("""
            public sealed class SampleConfig { }

            public sealed class SampleTests
            {
                private sealed class PrivateProfile : Compono.ICompositionProfile
                {
                    public PrivateProfile(SampleConfig config) { }

                    public void Configure(Compono.CompositionBuilder builder) { }
                }

                public static System.Collections.Generic.IEnumerable<object?[]> Cases { get; } = System.Array.Empty<object?[]>();

                [Compono.XunitV3.Aot.ComposeData<PrivateProfile, SampleConfig>("Cases")]
                public void TestMethod(string a) { }
            }
            """).Should().Be("CMP0044");
    }

    [Fact]
    public void TwoTypeParameterForm_RequiredMemberUnsatisfied_ReportsCmp0046()
    {
        DiagnosticId("""
            public sealed class SampleConfig { }

            public sealed class SampleProfile : Compono.ICompositionProfile
            {
                public required string Name { get; set; }

                public SampleProfile(SampleConfig config) { }

                public void Configure(Compono.CompositionBuilder builder) { }
            }

            public sealed class SampleTests
            {
                public static System.Collections.Generic.IEnumerable<object?[]> Cases { get; } = System.Array.Empty<object?[]>();

                [Compono.XunitV3.Aot.ComposeData<SampleProfile, SampleConfig>("Cases")]
                public void TestMethod(string a) { }
            }
            """).Should().Be("CMP0046");
    }

    [Fact]
    public void TwoTypeParameterForm_ProhibitedConstructor_ReportsCmp0047()
    {
        DiagnosticId("""
            public sealed class SampleConfig { }

            public sealed class SampleProfile : Compono.ICompositionProfile
            {
                [System.Obsolete("prohibited", true)]
                public SampleProfile(SampleConfig config) { }

                public void Configure(Compono.CompositionBuilder builder) { }
            }

            public sealed class SampleTests
            {
                public static System.Collections.Generic.IEnumerable<object?[]> Cases { get; } = System.Array.Empty<object?[]>();

                [Compono.XunitV3.Aot.ComposeData<SampleProfile, SampleConfig>("Cases")]
                public void TestMethod(string a) { }
            }
            """).Should().Be("CMP0047");
    }

    [Fact]
    public void TwoTypeParameterForm_HigherPrioritySibling_ReportsCmp0048()
    {
        DiagnosticId("""
            public sealed class SampleConfig { }

            public sealed class SampleProfile : Compono.ICompositionProfile
            {
                public SampleProfile(SampleConfig config) { }

                [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
                public SampleProfile(int other) { }

                public void Configure(Compono.CompositionBuilder builder) { }
            }

            public sealed class SampleTests
            {
                public static System.Collections.Generic.IEnumerable<object?[]> Cases { get; } = System.Array.Empty<object?[]>();

                [Compono.XunitV3.Aot.ComposeData<SampleProfile, SampleConfig>("Cases")]
                public void TestMethod(string a) { }
            }
            """).Should().Be("CMP0048");
    }

    [Fact]
    public void TwoTypeParameterForm_ConfigIsDateTimeOffset_ProducesNoDiagnostics()
    {
        AssertNoDiagnostics("""
            public sealed class SampleProfile : Compono.ICompositionProfile
            {
                public SampleProfile(System.DateTimeOffset config) { }

                public void Configure(Compono.CompositionBuilder builder) { }
            }

            public sealed class SampleTests
            {
                public static System.Collections.Generic.IEnumerable<object?[]> Cases { get; } = System.Array.Empty<object?[]>();

                [Compono.XunitV3.Aot.ComposeData<SampleProfile, System.DateTimeOffset>("Cases")]
                public void TestMethod(string a) { }
            }
            """);
    }

    [Fact]
    public void TwoTypeParameterForm_ConfigIsInterface_ProducesNoDiagnostics()
    {
        AssertNoDiagnostics("""
            public interface ISampleConfig { }

            public sealed class SampleProfile : Compono.ICompositionProfile
            {
                public SampleProfile(ISampleConfig config) { }

                public void Configure(Compono.CompositionBuilder builder) { }
            }

            public sealed class SampleTests
            {
                public static System.Collections.Generic.IEnumerable<object?[]> Cases { get; } = System.Array.Empty<object?[]>();

                [Compono.XunitV3.Aot.ComposeData<SampleProfile, ISampleConfig>("Cases")]
                public void TestMethod(string a) { }
            }
            """);
    }

    [Fact]
    public void TwoTypeParameterForm_ConfigHasTwoConstructors_ProducesNoDiagnostics()
    {
        AssertNoDiagnostics("""
            public sealed record SampleConfig(int Value)
            {
                public SampleConfig(string text) : this(0) { }
            }

            public sealed class SampleProfile : Compono.ICompositionProfile
            {
                public SampleProfile(SampleConfig config) { }

                public void Configure(Compono.CompositionBuilder builder) { }
            }

            public sealed class SampleTests
            {
                public static System.Collections.Generic.IEnumerable<object?[]> Cases { get; } = System.Array.Empty<object?[]>();

                [Compono.XunitV3.Aot.ComposeData<SampleProfile, SampleConfig>("Cases")]
                public void TestMethod(string a) { }
            }
            """);
    }

    [Fact]
    public void TwoTypeParameterForm_UnrelatedRowElementZero_ReportsCmp0055()
    {
        DiagnosticId("""
            public sealed class SampleConfig { }

            public sealed class OtherConfig { }

            public sealed class SampleProfile : Compono.ICompositionProfile
            {
                public SampleProfile(SampleConfig config) { }

                public void Configure(Compono.CompositionBuilder builder) { }
            }

            public sealed class SampleTests
            {
                public static Xunit.TheoryData<OtherConfig, int> Cases { get; } = new();

                [Compono.XunitV3.Aot.ComposeData<SampleProfile, SampleConfig>("Cases")]
                public void TestMethod(int a) { }
            }
            """).Should().Be("CMP0055");
    }

    [Fact]
    public void TwoTypeParameterForm_StructConfigNullableRowElement_ProducesNoDiagnostics()
    {
        AssertNoDiagnostics("""
            public readonly struct SampleConfig { }

            public sealed class SampleProfile : Compono.ICompositionProfile
            {
                public SampleProfile(SampleConfig config) { }

                public void Configure(Compono.CompositionBuilder builder) { }
            }

            public sealed class SampleTests
            {
                public static Xunit.TheoryData<SampleConfig?, int> Cases { get; } = new();

                [Compono.XunitV3.Aot.ComposeData<SampleProfile, SampleConfig>("Cases")]
                public void TestMethod(int a) { }
            }
            """);
    }

    [Fact]
    public void OneTypeParameterForm_InaccessibleProfile_ReportsCmp0044WithTProfileRole()
    {
        var message = DiagnosticMessage("""
            public sealed class SampleTests
            {
                private sealed class PrivateProfile : Compono.ICompositionProfile
                {
                    public void Configure(Compono.CompositionBuilder builder) { }
                }

                public static System.Collections.Generic.IEnumerable<object?[]> Cases { get; } = System.Array.Empty<object?[]>();

                [Compono.XunitV3.Aot.ComposeData<PrivateProfile>("Cases")]
                public void TestMethod(string a) { }
            }
            """);

        message.Should().Contain("[ComposeData<...>]");
        message.Should().Contain("the TProfile type argument");
    }

    [Fact]
    public void TwoTypeParameterForm_InaccessibleConfig_ReportsCmp0044WithTConfigRole()
    {
        var message = DiagnosticMessage("""
            public sealed class SampleProfile : Compono.ICompositionProfile
            {
                public void Configure(Compono.CompositionBuilder builder) { }
            }

            public sealed class SampleTests
            {
                private sealed class PrivateConfig { }

                public static System.Collections.Generic.IEnumerable<object?[]> Cases { get; } = System.Array.Empty<object?[]>();

                [Compono.XunitV3.Aot.ComposeData<SampleProfile, PrivateConfig>("Cases")]
                public void TestMethod(string a) { }
            }
            """);

        message.Should().Contain("[ComposeData<...>]");
        message.Should().Contain("the TConfig type argument");
    }

    [Fact]
    public void TwoTypeParameterForm_ProfileWithoutMatchingConstructor_ReportsCmp0042WithComposeDataWording()
    {
        var message = DiagnosticMessage("""
            public sealed class SampleConfig { }

            public sealed class SampleProfile : Compono.ICompositionProfile
            {
                public void Configure(Compono.CompositionBuilder builder) { }
            }

            public sealed class SampleTests
            {
                public static System.Collections.Generic.IEnumerable<object?[]> Cases { get; } = System.Array.Empty<object?[]>();

                [Compono.XunitV3.Aot.ComposeData<SampleProfile, SampleConfig>("Cases")]
                public void TestMethod(string a) { }
            }
            """);

        message.Should().Contain("[ComposeData<");
        message.Should().Contain("usable public constructor");
    }

    [Fact]
    public void TwoTypeParameterForm_TypedRowSuppliesMoreValuesThanRemainingParameters_ReportsCmp0056()
    {
        var message = DiagnosticMessage("""
            public sealed class SampleConfig { }

            public sealed class SampleProfile : Compono.ICompositionProfile
            {
                public SampleProfile(SampleConfig config) { }

                public void Configure(Compono.CompositionBuilder builder) { }
            }

            public sealed class SampleTests
            {
                public static Xunit.TheoryData<SampleConfig, int, int> Cases { get; } = new();

                [Compono.XunitV3.Aot.ComposeData<SampleProfile, SampleConfig>("Cases")]
                public void TestMethod(int value) { }
            }
            """);

        message.Should().Contain("supplies 2 row value(s)");
        message.Should().Contain("1 parameter(s)");
    }

    [Fact]
    public void TwoTypeParameterForm_RequiredMemberUnsatisfied_UsesComposeDataWording()
    {
        var message = DiagnosticMessage("""
            public sealed class SampleConfig { }

            public sealed class SampleProfile : Compono.ICompositionProfile
            {
                public required string Name { get; set; }

                public SampleProfile(SampleConfig config) { }

                public void Configure(Compono.CompositionBuilder builder) { }
            }

            public sealed class SampleTests
            {
                public static System.Collections.Generic.IEnumerable<object?[]> Cases { get; } = System.Array.Empty<object?[]>();

                [Compono.XunitV3.Aot.ComposeData<SampleProfile, SampleConfig>("Cases")]
                public void TestMethod(string a) { }
            }
            """);

        message.Should().Contain("does not satisfy required member 'Name'");
        message.Should().Contain("[ComposeData<...>]");
    }

    [Fact]
    public void TwoTypeParameterForm_ProhibitedConstructor_UsesComposeDataWording()
    {
        var message = DiagnosticMessage("""
            public sealed class SampleConfig { }

            public sealed class SampleProfile : Compono.ICompositionProfile
            {
                [System.Obsolete("prohibited", true)]
                public SampleProfile(SampleConfig config) { }

                public void Configure(Compono.CompositionBuilder builder) { }
            }

            public sealed class SampleTests
            {
                public static System.Collections.Generic.IEnumerable<object?[]> Cases { get; } = System.Array.Empty<object?[]>();

                [Compono.XunitV3.Aot.ComposeData<SampleProfile, SampleConfig>("Cases")]
                public void TestMethod(string a) { }
            }
            """);

        message.Should().Contain("is marked [Obsolete(error: true)]");
        message.Should().Contain("used by [ComposeData<...>]");
    }

    [Fact]
    public void TwoTypeParameterForm_HigherPrioritySibling_UsesComposeDataWording()
    {
        var message = DiagnosticMessage("""
            public sealed class SampleConfig { }

            public sealed class SampleProfile : Compono.ICompositionProfile
            {
                public SampleProfile(SampleConfig config) { }

                [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
                public SampleProfile(int other) { }

                public void Configure(Compono.CompositionBuilder builder) { }
            }

            public sealed class SampleTests
            {
                public static System.Collections.Generic.IEnumerable<object?[]> Cases { get; } = System.Array.Empty<object?[]>();

                [Compono.XunitV3.Aot.ComposeData<SampleProfile, SampleConfig>("Cases")]
                public void TestMethod(string a) { }
            }
            """);

        message.Should().Contain("could be silently superseded");
        message.Should().Contain("[ComposeData<...>]");
    }

    [Fact]
    public void TwoTypeParameterForm_AotUnsafeParameterlessConstructor_UsesComposeDataWording()
    {
        var message = DiagnosticMessage("""
            public sealed class AotUnsafeProfile : Compono.ICompositionProfile
            {
                [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("unsafe")]
                public AotUnsafeProfile() { }

                public void Configure(Compono.CompositionBuilder builder) { }
            }

            public sealed class SampleTests
            {
                public static System.Collections.Generic.IEnumerable<object?[]> Cases { get; } = System.Array.Empty<object?[]>();

                [Compono.XunitV3.Aot.ComposeData<AotUnsafeProfile>("Cases")]
                public void TestMethod(string a) { }
            }
            """);

        message.Should().Contain("public parameterless constructor is marked");
        message.Should().Contain("[RequiresDynamicCode]/[RequiresUnreferencedCode]/[RequiresAssemblyFiles]");
        message.Should().Contain("used by [ComposeData<...>]");
    }
}
