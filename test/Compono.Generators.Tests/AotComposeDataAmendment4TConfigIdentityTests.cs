using System.Linq;
using Xunit;

namespace Compono.Generators.Tests;

// PLAN-0071 B5.2 #1: TConfig constructor matching uses CLR identity, not symbol equality.
public sealed class AotComposeDataAmendment4TConfigIdentityTests
{
    private static string Source(string body) => $"namespace TestNamespace\n{{\nusing Compono;\n{body}\n}}\n";

    private static string DiagnosticId(string body) =>
        ComposeDataAotTestHelper.Discover(Source(body)).Diagnostics.Single().Id;

    private static string DiagnosticMessage(string body) =>
        ComposeDataAotTestHelper.Discover(Source(body)).Diagnostics.Single().GetMessage();

    private static void AssertNoDiagnostics(string body) =>
        ComposeDataAotTestHelper.SingleModel(Source(body)).Diagnostics.Should().BeEmpty();

    [Fact]
    public void TupleConfig_WithMatchingTupleConstructor_ProducesNoDiagnostics() =>
        AssertNoDiagnostics("""
            public sealed class SampleProfile : Compono.ICompositionProfile
            {
                public SampleProfile((int A, int B) config) { }

                public void Configure(Compono.CompositionBuilder builder) { }
            }

            public sealed class SampleTests
            {
                public static System.Collections.Generic.IEnumerable<object?[]> Cases { get; } = System.Array.Empty<object?[]>();

                [Compono.XunitV3.Aot.ComposeData<SampleProfile, System.ValueTuple<int, int>>("Cases")]
                public void TestMethod(string a) { }
            }
            """);

    [Fact]
    public void DynamicConfig_WithObjectConfigConstructor_ProducesNoDiagnostics() =>
        AssertNoDiagnostics("""
            public sealed class SampleProfile : Compono.ICompositionProfile
            {
                public SampleProfile(dynamic config) { }

                public void Configure(Compono.CompositionBuilder builder) { }
            }

            public sealed class SampleTests
            {
                public static System.Collections.Generic.IEnumerable<object?[]> Cases { get; } = System.Array.Empty<object?[]>();

                [Compono.XunitV3.Aot.ComposeData<SampleProfile, object>("Cases")]
                public void TestMethod(string a) { }
            }
            """);

    [Fact]
    public void ListOfDynamicConfig_WithListOfObjectConfigConstructor_ProducesNoDiagnostics() =>
        AssertNoDiagnostics("""
            public sealed class SampleProfile : Compono.ICompositionProfile
            {
                public SampleProfile(System.Collections.Generic.List<dynamic> config) { }

                public void Configure(Compono.CompositionBuilder builder) { }
            }

            public sealed class SampleTests
            {
                public static System.Collections.Generic.IEnumerable<object?[]> Cases { get; } = System.Array.Empty<object?[]>();

                [Compono.XunitV3.Aot.ComposeData<SampleProfile, System.Collections.Generic.List<object>>("Cases")]
                public void TestMethod(string a) { }
            }
            """);

    [Fact]
    public void NullableIntConfig_WithIntConfigConstructor_ReportsCmp0042WithZeroUsableConstructors()
    {
        var message = DiagnosticMessage("""
            public sealed class SampleProfile : Compono.ICompositionProfile
            {
                public SampleProfile(int? config) { }

                public void Configure(Compono.CompositionBuilder builder) { }
            }

            public sealed class SampleTests
            {
                public static System.Collections.Generic.IEnumerable<object?[]> Cases { get; } = System.Array.Empty<object?[]>();

                [Compono.XunitV3.Aot.ComposeData<SampleProfile, int>("Cases")]
                public void TestMethod(string a) { }
            }
            """);

        message.Should().Contain("0 usable public constructor(s)");
    }
}
