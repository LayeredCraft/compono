using System.Collections.Immutable;
using System.Linq;
using Compono.Generators.Models;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Compono.Generators.Tests;

// PLAN-0071 B5.2 #3: the five new AotComposeDataMethodInfo fields.
public sealed class AotComposeDataAmendment4ModelTests
{
    private static string Source(string body) => $"namespace TestNamespace\n{{\nusing Compono;\n{body}\n}}\n";

    private static (ImmutableArray<Diagnostic> Diagnostics, IReadOnlyList<AotComposeDataMethodInfo> Models) Discover(string body) =>
        ComposeDataAotTestHelper.Discover(Source(body));

    private static AotComposeDataMethodInfo Compose(string body) => ComposeDataAotTestHelper.SingleModel(Source(body));

    [Fact]
    public void TestClassIndex_NonGenericTestClass_EqualsFullyQualifiedTestClassName()
    {
        var model = Compose("""
            public sealed class SampleTests
            {
                public static System.Collections.Generic.IEnumerable<object> Cases => new object[] { "x" };

                [Compono.XunitV3.Aot.ComposeData("Cases")]
                public void TestMethod(string value) { }
            }
            """);

        model.TestClassIndex.Should().Be("global::TestNamespace.SampleTests");
        model.TestClassIndex.Should().Be(model.FullyQualifiedTestClassName);
        model.IsGenericTestClass.Should().BeFalse();
        model.IsTestClassAccessible.Should().BeTrue();
        model.LookupIsTestClass.Should().BeTrue();
    }

    [Fact]
    public void TestClassIndex_GenericTestClass_RendersOpenGenericIndex()
    {
        var (_, models) = Discover("""
            public class GenericBase<T>
            {
                public static System.Collections.Generic.IEnumerable<object> Cases => new object[] { "x" };

                [Compono.XunitV3.Aot.ComposeData("Cases", MemberType = typeof(Holder))]
                public void TestMethod(string value) { }
            }

            public sealed class Holder
            {
                public static System.Collections.Generic.IEnumerable<object> Cases => new object[] { "x" };
            }

            public sealed class SampleTests : GenericBase<int> { }
            """);

        var model = models.Single();
        model.TestClassIndex.Should().Be("global::TestNamespace.GenericBase<>");
        model.IsGenericTestClass.Should().BeTrue();
    }

    [Fact]
    public void LookupIsTestClass_FalseWhenMemberTypeNamedArgumentPresent()
    {
        var model = Compose("""
            public sealed class Holder
            {
                public static System.Collections.Generic.IEnumerable<object> Cases => new object[] { "x" };
            }

            public sealed class SampleTests
            {
                [Compono.XunitV3.Aot.ComposeData("Cases", MemberType = typeof(Holder))]
                public void TestMethod(string value) { }
            }
            """);

        model.LookupIsTestClass.Should().BeFalse();
    }

    [Fact]
    public void IsTestClassAccessible_FalseForPrivateNestedTestClass()
    {
        var (_, models) = Discover("""
            public sealed class Outer
            {
                private sealed class Hidden
                {
                    public static System.Collections.Generic.IEnumerable<object> Cases => new object[] { "x" };

                    [Compono.XunitV3.Aot.ComposeData("Cases")]
                    public void TestMethod(string value) { }
                }
            }
            """);

        var model = models.Single();
        model.IsTestClassAccessible.Should().BeFalse();
    }

    [Fact]
    public void AsyncRowElementIsValueType_FalseForNonAsyncMember()
    {
        var model = Compose("""
            public sealed class SampleTests
            {
                public static System.Collections.Generic.IEnumerable<object> Cases => new object[] { "x" };

                [Compono.XunitV3.Aot.ComposeData("Cases")]
                public void TestMethod(string value) { }
            }
            """);

        model.AsyncRowElementIsValueType.Should().BeFalse();
    }

    [Fact]
    public void AsyncRowElementIsValueType_TrueForSingleValueElement()
    {
        var (_, models) = Discover("""
            public sealed class Holder
            {
                public static System.Collections.Generic.IAsyncEnumerable<(int, int)> Cases =>
                    throw new System.NotImplementedException();
            }

            public sealed class SampleTests
            {
                [Compono.XunitV3.Aot.ComposeData("Cases", MemberType = typeof(Holder))]
                public void TestMethod(string value) { }
            }
            """);

        models.Single().AsyncRowElementIsValueType.Should().BeTrue();
    }
}
