using System.Linq;
using Compono.Generators.Models;
using Xunit;

namespace Compono.Generators.Tests;

public sealed class AotComposeDataShapeTests
{
    private static string Source(string body) => $"namespace TestNamespace\n{{\nusing Compono;\n{body}\n}}\n";

    private static AotComposeDataMethodInfo Compose(string body) =>
        ComposeDataAotTestHelper.SingleModel(Source(body));

    private static string DiagnosticId(string body) =>
        ComposeDataAotTestHelper.Discover(Source(body)).Diagnostics.Single().Id;

    private static string DiagnosticMessage(string body) =>
        ComposeDataAotTestHelper.Discover(Source(body)).Diagnostics.Single().GetMessage();

    [Fact]
    public void TaskOfObject_IsValidAndUntyped()
    {
        var model = Compose("""
            public sealed class SampleTests
            {
                public static System.Threading.Tasks.Task<object> Cases { get; } = null!;

                [Compono.XunitV3.Aot.ComposeData("Cases")]
                public void TestMethod(string a, byte b, char c) { }
            }
            """);

        model.Diagnostics.Should().BeEmpty();
        model.ReturnShape.Should().Be(AotComposeDataReturnShape.TaskOf);
        model.RowsAreTyped.Should().BeFalse();
    }

    [Fact]
    public void ValueTaskOfTheoryData_IsValidAndTyped()
    {
        var model = Compose("""
            public sealed class SampleTests
            {
                public static System.Threading.Tasks.ValueTask<Xunit.TheoryData<int, string>> Cases { get; }

                [Compono.XunitV3.Aot.ComposeData("Cases")]
                public void TestMethod(int a, string b) { }
            }
            """);

        model.Diagnostics.Should().BeEmpty();
        model.ReturnShape.Should().Be(AotComposeDataReturnShape.ValueTaskOf);
        model.RowsAreTyped.Should().BeTrue();
        model.RowElementTypeNames.GetArray()!.Should().Equal("int", "string");
    }

    [Fact]
    public void AsyncEnumerableOfObjectArray_IsValid()
    {
        var model = Compose("""
            public sealed class SampleTests
            {
                public static System.Collections.Generic.IAsyncEnumerable<object[]> Cases() =>
                    throw new System.NotImplementedException();

                [Compono.XunitV3.Aot.ComposeData("Cases")]
                public void TestMethod(string a) { }
            }
            """);

        model.Diagnostics.Should().BeEmpty();
        model.ReturnShape.Should().Be(AotComposeDataReturnShape.AsyncEnumerable);
    }

    [Fact]
    public void ListOfStringArray_IsValidAndUntyped()
    {
        var model = Compose("""
            public sealed class SampleTests
            {
                public static System.Collections.Generic.List<string[]> Cases { get; } = new();

                [Compono.XunitV3.Aot.ComposeData("Cases")]
                public void TestMethod(string a) { }
            }
            """);

        model.Diagnostics.Should().BeEmpty();
        model.ReturnShape.Should().Be(AotComposeDataReturnShape.Sync);
        model.RowsAreTyped.Should().BeFalse();
    }

    [Fact]
    public void TheoryDataSubclass_IsValidAndTyped()
    {
        var model = Compose("""
            public sealed class MyCases : Xunit.TheoryData<int, string> { }

            public sealed class SampleTests
            {
                public static MyCases Cases { get; } = new();

                [Compono.XunitV3.Aot.ComposeData("Cases")]
                public void TestMethod(int a, string b) { }
            }
            """);

        model.Diagnostics.Should().BeEmpty();
        model.RowsAreTyped.Should().BeTrue();
        model.RowElementTypeNames.GetArray()!.Should().Equal("int", "string");
    }

    [Fact]
    public void EnumerableOfObject_IsValidAndUntyped()
    {
        var model = Compose("""
            public sealed class SampleTests
            {
                public static System.Collections.Generic.IEnumerable<object> Cases { get; } =
                    System.Array.Empty<object>();

                [Compono.XunitV3.Aot.ComposeData("Cases")]
                public void TestMethod(string a) { }
            }
            """);

        model.Diagnostics.Should().BeEmpty();
        model.RowsAreTyped.Should().BeFalse();
    }

    [Fact]
    public void StringReturnType_ReportsCmp0053()
    {
        DiagnosticId("""
            public sealed class SampleTests
            {
                public static string Cases => "";

                [Compono.XunitV3.Aot.ComposeData("Cases")]
                public void TestMethod(string a) { }
            }
            """).Should().Be("CMP0053");
    }

    [Fact]
    public void SingleTheoryDataRowReturnType_ReportsCmp0053()
    {
        DiagnosticId("""
            public sealed class SampleTests
            {
                public static Xunit.TheoryDataRow<int> Cases { get; } = null!;

                [Compono.XunitV3.Aot.ComposeData("Cases")]
                public void TestMethod(string a) { }
            }
            """).Should().Be("CMP0053");
    }

    [Fact]
    public void NonGenericTask_ReportsCmp0053()
    {
        DiagnosticId("""
            public sealed class SampleTests
            {
                public static System.Threading.Tasks.Task Cases() =>
                    System.Threading.Tasks.Task.CompletedTask;

                [Compono.XunitV3.Aot.ComposeData("Cases")]
                public void TestMethod(string a) { }
            }
            """).Should().Be("CMP0053");
    }

    [Fact]
    public void TaskOfInt_ReportsCmp0053()
    {
        DiagnosticId("""
            public sealed class SampleTests
            {
                public static System.Threading.Tasks.Task<int> Cases { get; } = null!;

                [Compono.XunitV3.Aot.ComposeData("Cases")]
                public void TestMethod(string a) { }
            }
            """).Should().Be("CMP0053");
    }

    [Fact]
    public void DeclaredObjectReturnType_ReportsCmp0053()
    {
        DiagnosticId("""
            public sealed class SampleTests
            {
                public static object Cases { get; } = new();

                [Compono.XunitV3.Aot.ComposeData("Cases")]
                public void TestMethod(string a) { }
            }
            """).Should().Be("CMP0053");
    }

    [Fact]
    public void EnumerableOfInt_ReportsCmp0053()
    {
        var message = DiagnosticMessage("""
            public sealed class SampleTests
            {
                public static System.Collections.Generic.IEnumerable<int> Cases { get; } =
                    System.Array.Empty<int>();

                [Compono.XunitV3.Aot.ComposeData("Cases")]
                public void TestMethod(int a) { }
            }
            """);

        message.Should().Contain("row element");
    }

    [Fact]
    public void EnumerableOfString_ReportsCmp0053()
    {
        DiagnosticId("""
            public sealed class SampleTests
            {
                public static System.Collections.Generic.IEnumerable<string> Cases { get; } =
                    System.Array.Empty<string>();

                [Compono.XunitV3.Aot.ComposeData("Cases")]
                public void TestMethod(string a) { }
            }
            """).Should().Be("CMP0053");
    }

    [Fact]
    public void OpenGenericMemberType_ReportsCmp0054()
    {
        DiagnosticId("""
            public static class Holder<T> { }

            public sealed class SampleTests
            {
                [Compono.XunitV3.Aot.ComposeData("Cases", MemberType = typeof(Holder<>))]
                public void TestMethod(string a) { }
            }
            """).Should().Be("CMP0054");
    }

    [Fact]
    public void NullableIntElement_ToIntParameter_IsValid()
    {
        var model = Compose("""
            public sealed class SampleTests
            {
                public static Xunit.TheoryData<int?, string> Cases { get; } = new();

                [Compono.XunitV3.Aot.ComposeData("Cases")]
                public void TestMethod(int a, string b) { }
            }
            """);

        model.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void IntElement_ToNullableIntParameter_IsValid()
    {
        var model = Compose("""
            public sealed class SampleTests
            {
                public static Xunit.TheoryData<int, string> Cases { get; } = new();

                [Compono.XunitV3.Aot.ComposeData("Cases")]
                public void TestMethod(int? a, string b) { }
            }
            """);

        model.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void NullableStringElement_ToStringParameter_IsValid()
    {
        var model = Compose("""
            public sealed class SampleTests
            {
                public static Xunit.TheoryData<string?, bool> Cases { get; } = new();

                [Compono.XunitV3.Aot.ComposeData("Cases")]
                public void TestMethod(string a, bool b) { }
            }
            """);

        model.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void SubclassElement_ToBaseParameter_IsValid()
    {
        var model = Compose("""
            public class Animal { }

            public sealed class Dog : Animal { }

            public sealed class SampleTests
            {
                public static Xunit.TheoryData<Dog, bool> Cases { get; } = new();

                [Compono.XunitV3.Aot.ComposeData("Cases")]
                public void TestMethod(Animal a, bool b) { }
            }
            """);

        model.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void StringElement_ToObjectParameter_IsValid()
    {
        var model = Compose("""
            public sealed class SampleTests
            {
                public static Xunit.TheoryData<string, bool> Cases { get; } = new();

                [Compono.XunitV3.Aot.ComposeData("Cases")]
                public void TestMethod(object a, bool b) { }
            }
            """);

        model.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void IntElement_ToLongParameter_ReportsCmp0056()
    {
        DiagnosticId("""
            public sealed class SampleTests
            {
                public static Xunit.TheoryData<int> Cases { get; } = new();

                [Compono.XunitV3.Aot.ComposeData("Cases")]
                public void TestMethod(long a) { }
            }
            """).Should().Be("CMP0056");
    }

    [Fact]
    public void TooManyRowValues_ReportsCmp0056()
    {
        var message = DiagnosticMessage("""
            public sealed class SampleTests
            {
                public static Xunit.TheoryData<int, string, int> Cases { get; } = new();

                [Compono.XunitV3.Aot.ComposeData("Cases")]
                public void TestMethod(int a, string b) { }
            }
            """);

        message.Should().Contain("supplies 3 row value(s)");
    }
}
