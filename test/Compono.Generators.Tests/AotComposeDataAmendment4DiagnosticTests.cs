using System.Collections.Immutable;
using System.Linq;
using Compono.Generators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Compono.Generators.Tests;

// ADR-0070 Amendment 4 / PLAN-0071 B5.4: the AOT-only rejection diagnostics for [ComposeData].
public sealed class AotComposeDataAmendment4DiagnosticTests
{
    private static string Source(string body) => $"namespace TestNamespace\n{{\nusing Compono;\n{body}\n}}\n";

    private static (ImmutableArray<Diagnostic> Diagnostics, IReadOnlyList<AotComposeDataMethodInfo> Models) Discover(string body) =>
        ComposeDataAotTestHelper.Discover(Source(body));

    private static ImmutableArray<Diagnostic> Diagnostics(string body) => Discover(body).Diagnostics;

    private static string SingleId(string body) => Diagnostics(body).Single().Id;

    private static string SingleMessage(string body) => Diagnostics(body).Single().GetMessage();

    private static AotComposeDataMethodInfo Compose(string body) => ComposeDataAotTestHelper.SingleModel(Source(body));

    // Step 6a: CMP0054 - the selected member is declared on an open generic type.

    [Fact]
    public void OpenGenericMemberHolder_ViaDefaultLookup_ReportsCmp0054()
    {
        var message = SingleMessage("""
            public class GenericBase<T>
            {
                public static System.Collections.Generic.IEnumerable<object> Cases => new object[] { "x" };
            }

            public sealed class SampleTests<T> : GenericBase<T>
            {
                [Compono.XunitV3.Aot.ComposeData("Cases")]
                public void TestMethod(string value) { }
            }
            """);

        message.Should().Be(
            "ComposeData member 'TestNamespace.SampleTests<T>.Cases' for 'TestNamespace.SampleTests<T>.TestMethod' " +
            "is declared on open generic type 'TestNamespace.GenericBase<T>', which generated code cannot name; " +
            "set MemberType to a non-generic or closed generic type that declares the member");
    }

    [Fact]
    public void OpenGenericMemberHolder_WithNonGenericMemberType_IsValid()
    {
        var model = Compose("""
            public class GenericBase<T>
            {
                public static System.Collections.Generic.IEnumerable<object> Cases => new object[] { "x" };
            }

            public sealed class Holder
            {
                public static System.Collections.Generic.IEnumerable<object> Cases => new object[] { "x" };
            }

            public sealed class SampleTests<T> : GenericBase<T>
            {
                [Compono.XunitV3.Aot.ComposeData("Cases", MemberType = typeof(Holder))]
                public void TestMethod(string value) { }
            }
            """);

        model.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void OpenGenericMemberHolder_MemberOnNonGenericRootBase_IsValid()
    {
        var model = Compose("""
            public class Root
            {
                public static System.Collections.Generic.IEnumerable<object> Cases => new object[] { "x" };
            }

            public class GenericBase<T> : Root { }

            public sealed class SampleTests<T> : GenericBase<T>
            {
                [Compono.XunitV3.Aot.ComposeData("Cases")]
                public void TestMethod(string value) { }
            }
            """);

        model.Diagnostics.Should().BeEmpty();
    }

    // Step 9a: CMP0054 - the active skip condition property is declared on an open generic type.

    [Fact]
    public void OpenGenericSkipHolder_ViaDefaultLookup_ReportsCmp0054()
    {
        var message = SingleMessage("""
            public class Root
            {
                public static System.Collections.Generic.IEnumerable<object> Cases => new object[] { "x" };
            }

            public class GenericBase<T> : Root
            {
                public static bool SkipIt => false;
            }

            public sealed class SampleTests<T> : GenericBase<T>
            {
                [Compono.XunitV3.Aot.ComposeData("Cases", Skip = "reason", SkipUnless = "SkipIt")]
                public void TestMethod(string value) { }
            }
            """);

        message.Should().Be(
            "[ComposeData] on 'TestNamespace.SampleTests<T>.TestMethod': SkipUnless 'SkipIt' is declared on open " +
            "generic type 'TestNamespace.GenericBase<T>', which generated code cannot name; set SkipType to a " +
            "non-generic or closed generic type that declares the property");
    }

    // An explicitly supplied unbound generic SkipType resolves its property through the OriginalDefinition, so an
    // existing property is found and rejected as an open-generic skip holder (CMP0054).
    [Fact]
    public void OpenGenericSkipHolder_WithUnboundGenericSkipType_ReportsCmp0054()
    {
        var message = SingleMessage("""
            public class Holder<T>
            {
                public static bool SkipIt => false;
            }

            public sealed class SampleTests
            {
                public static System.Collections.Generic.IEnumerable<object> Cases => new object[] { "x" };

                [Compono.XunitV3.Aot.ComposeData("Cases", Skip = "reason", SkipUnless = "SkipIt", SkipType = typeof(Holder<>))]
                public void TestMethod(string value) { }
            }
            """);

        message.Should().Be(
            "[ComposeData] on 'TestNamespace.SampleTests.TestMethod': SkipUnless 'SkipIt' is declared on open " +
            "generic type 'TestNamespace.Holder<T>', which generated code cannot name; set SkipType to a " +
            "non-generic or closed generic type that declares the property");
    }

    // An unbound generic SkipType whose OriginalDefinition lacks the named property still fails the existing
    // invalid-skip-property check (CMP0057).
    [Fact]
    public void OpenGenericSkipHolder_WithUnboundGenericSkipTypeAndMissingProperty_ReportsCmp0057()
    {
        var message = SingleMessage("""
            public class Holder<T>
            {
                public static bool Other => false;
            }

            public sealed class SampleTests
            {
                public static System.Collections.Generic.IEnumerable<object> Cases => new object[] { "x" };

                [Compono.XunitV3.Aot.ComposeData("Cases", Skip = "reason", SkipUnless = "SkipIt", SkipType = typeof(Holder<>))]
                public void TestMethod(string value) { }
            }
            """);

        message.Should().Be(
            "[ComposeData] on 'TestNamespace.SampleTests.TestMethod': SkipUnless 'SkipIt' must be a public static " +
            "bool property on 'TestNamespace.Holder<>' or one of its base types");
    }

    // Step 6b: CMP0052 - static abstract / static virtual interface member.

    [Fact]
    public void StaticAbstractInterfaceMember_ReportsCmp0052()
    {
        var message = SingleMessage("""
            public interface IFoo
            {
                static abstract System.Collections.Generic.IEnumerable<object> Cases { get; }
            }

            public sealed class SampleTests
            {
                [Compono.XunitV3.Aot.ComposeData("Cases", MemberType = typeof(IFoo))]
                public void TestMethod(string value) { }
            }
            """);

        message.Should().Be(
            "ComposeData member 'TestNamespace.IFoo.Cases' for 'TestNamespace.SampleTests.TestMethod' is a static " +
            "abstract or static virtual interface member, which generated code cannot call; move the data to a " +
            "static member of a class or struct");
    }

    [Fact]
    public void StaticVirtualInterfaceMember_ReportsCmp0052()
    {
        var message = SingleMessage("""
            public interface IFoo
            {
                static virtual System.Collections.Generic.IEnumerable<object> Cases => new object[] { "x" };
            }

            public sealed class SampleTests
            {
                [Compono.XunitV3.Aot.ComposeData("Cases", MemberType = typeof(IFoo))]
                public void TestMethod(string value) { }
            }
            """);

        message.Should().Be(
            "ComposeData member 'TestNamespace.IFoo.Cases' for 'TestNamespace.SampleTests.TestMethod' is a static " +
            "abstract or static virtual interface member, which generated code cannot call; move the data to a " +
            "static member of a class or struct");
    }

    // Step 6c: CMP0047 - prohibited attribute on the member or its getter.

    [Fact]
    public void ProhibitedMember_Field_ReportsCmp0047()
    {
        var message = SingleMessage("""
            public sealed class Holder
            {
                [System.Obsolete("gone", true)]
                public static System.Collections.Generic.IEnumerable<object> Cases = new object[] { "x" };
            }

            public sealed class SampleTests
            {
                [Compono.XunitV3.Aot.ComposeData("Cases", MemberType = typeof(Holder))]
                public void TestMethod(string value) { }
            }
            """);

        message.Should().Be(
            "ComposeData member 'TestNamespace.Holder.Cases' for 'TestNamespace.SampleTests.TestMethod' is marked " +
            "[Obsolete(error: true)], which makes any use of it a compiler error; the generated registration reads " +
            "the member directly, so it would fail to compile");
    }

    [Fact]
    public void ProhibitedMember_Property_ReportsCmp0047()
    {
        var message = SingleMessage("""
            public sealed class Holder
            {
                [System.Obsolete("gone", true)]
                public static System.Collections.Generic.IEnumerable<object> Cases => new object[] { "x" };
            }

            public sealed class SampleTests
            {
                [Compono.XunitV3.Aot.ComposeData("Cases", MemberType = typeof(Holder))]
                public void TestMethod(string value) { }
            }
            """);

        message.Should().Be(
            "ComposeData member 'TestNamespace.Holder.Cases' for 'TestNamespace.SampleTests.TestMethod' is marked " +
            "[Obsolete(error: true)], which makes any use of it a compiler error; the generated registration reads " +
            "the member directly, so it would fail to compile");
    }

    [Fact]
    public void ProhibitedMember_PropertyGetter_ReportsCmp0047()
    {
        var message = SingleMessage("""
            public sealed class Holder
            {
                public static System.Collections.Generic.IEnumerable<object> Cases
                {
                    [System.Obsolete("gone", true)]
                    get => new object[] { "x" };
                }
            }

            public sealed class SampleTests
            {
                [Compono.XunitV3.Aot.ComposeData("Cases", MemberType = typeof(Holder))]
                public void TestMethod(string value) { }
            }
            """);

        message.Should().Be(
            "ComposeData member 'TestNamespace.Holder.Cases' for 'TestNamespace.SampleTests.TestMethod' has a " +
            "getter marked [Obsolete(error: true)], which makes any use of it a compiler error; the generated " +
            "registration reads the member directly, so it would fail to compile");
    }

    [Fact]
    public void ProhibitedMember_Method_ReportsCmp0047()
    {
        var message = SingleMessage("""
            public sealed class Holder
            {
                [System.Obsolete("gone", true)]
                public static System.Collections.Generic.IEnumerable<object> Cases() => new object[] { "x" };
            }

            public sealed class SampleTests
            {
                [Compono.XunitV3.Aot.ComposeData("Cases", MemberType = typeof(Holder))]
                public void TestMethod(string value) { }
            }
            """);

        message.Should().Be(
            "ComposeData member 'TestNamespace.Holder.Cases' for 'TestNamespace.SampleTests.TestMethod' is marked " +
            "[Obsolete(error: true)], which makes any use of it a compiler error; the generated registration reads " +
            "the member directly, so it would fail to compile");
    }

    [Fact]
    public void ProhibitedMember_Experimental_ReportsCmp0047()
    {
        var message = SingleMessage("""
            public sealed class Holder
            {
                [System.Diagnostics.CodeAnalysis.Experimental("EXP1")]
                public static System.Collections.Generic.IEnumerable<object> Cases => new object[] { "x" };
            }

            public sealed class SampleTests
            {
                [Compono.XunitV3.Aot.ComposeData("Cases", MemberType = typeof(Holder))]
                public void TestMethod(string value) { }
            }
            """);

        message.Should().Be(
            "ComposeData member 'TestNamespace.Holder.Cases' for 'TestNamespace.SampleTests.TestMethod' is marked " +
            "[Experimental(\"EXP1\")], which makes any use of it a compiler error; the generated registration reads " +
            "the member directly, so it would fail to compile");
    }

    [Fact]
    public void ProhibitedSkipProperty_ReportsCmp0047()
    {
        var message = SingleMessage("""
            public class Root
            {
                public static System.Collections.Generic.IEnumerable<object> Cases => new object[] { "x" };
            }

            public sealed class SampleTests : Root
            {
                [System.Obsolete("gone", true)]
                public static bool SkipIt => false;

                [Compono.XunitV3.Aot.ComposeData("Cases", Skip = "reason", SkipUnless = "SkipIt")]
                public void TestMethod(string value) { }
            }
            """);

        message.Should().Be(
            "[ComposeData] on 'TestNamespace.SampleTests.TestMethod': SkipUnless 'SkipIt' on " +
            "'TestNamespace.SampleTests' is marked [Obsolete(error: true)], which makes any use of it a compiler " +
            "error; the generated registration reads the property directly, so it would fail to compile");
    }

    [Fact]
    public void ProhibitedSkipPropertyGetter_ReportsCmp0047()
    {
        var message = SingleMessage("""
            public class Root
            {
                public static System.Collections.Generic.IEnumerable<object> Cases => new object[] { "x" };
            }

            public sealed class SampleTests : Root
            {
                public static bool SkipIt
                {
                    [System.Obsolete("gone", true)]
                    get => false;
                }

                [Compono.XunitV3.Aot.ComposeData("Cases", Skip = "reason", SkipUnless = "SkipIt")]
                public void TestMethod(string value) { }
            }
            """);

        message.Should().Be(
            "[ComposeData] on 'TestNamespace.SampleTests.TestMethod': SkipUnless 'SkipIt' on " +
            "'TestNamespace.SampleTests' has a getter marked [Obsolete(error: true)], which makes any use of it a " +
            "compiler error; the generated registration reads the property directly, so it would fail to compile");
    }

    // Step 9 (SkipWhen) mirrors the SkipUnless checks.

    [Fact]
    public void OpenGenericSkipHolder_ViaDefaultLookup_ReportsCmp0054_ForSkipWhen()
    {
        var message = SingleMessage("""
            public class Root
            {
                public static System.Collections.Generic.IEnumerable<object> Cases => new object[] { "x" };
            }

            public class GenericBase<T> : Root
            {
                public static bool SkipIt => false;
            }

            public sealed class SampleTests<T> : GenericBase<T>
            {
                [Compono.XunitV3.Aot.ComposeData("Cases", Skip = "reason", SkipWhen = "SkipIt")]
                public void TestMethod(string value) { }
            }
            """);

        message.Should().Be(
            "[ComposeData] on 'TestNamespace.SampleTests<T>.TestMethod': SkipWhen 'SkipIt' is declared on open " +
            "generic type 'TestNamespace.GenericBase<T>', which generated code cannot name; set SkipType to a " +
            "non-generic or closed generic type that declares the property");
    }

    [Fact]
    public void ProhibitedSkipProperty_ReportsCmp0047_ForSkipWhen()
    {
        var message = SingleMessage("""
            public class Root
            {
                public static System.Collections.Generic.IEnumerable<object> Cases => new object[] { "x" };
            }

            public sealed class SampleTests : Root
            {
                [System.Obsolete("gone", true)]
                public static bool SkipIt => false;

                [Compono.XunitV3.Aot.ComposeData("Cases", Skip = "reason", SkipWhen = "SkipIt")]
                public void TestMethod(string value) { }
            }
            """);

        message.Should().Be(
            "[ComposeData] on 'TestNamespace.SampleTests.TestMethod': SkipWhen 'SkipIt' on " +
            "'TestNamespace.SampleTests' is marked [Obsolete(error: true)], which makes any use of it a compiler " +
            "error; the generated registration reads the property directly, so it would fail to compile");
    }

    [Fact]
    public void ProhibitedSkipPropertyGetter_ReportsCmp0047_ForSkipWhen()
    {
        var message = SingleMessage("""
            public class Root
            {
                public static System.Collections.Generic.IEnumerable<object> Cases => new object[] { "x" };
            }

            public sealed class SampleTests : Root
            {
                public static bool SkipIt
                {
                    [System.Obsolete("gone", true)]
                    get => false;
                }

                [Compono.XunitV3.Aot.ComposeData("Cases", Skip = "reason", SkipWhen = "SkipIt")]
                public void TestMethod(string value) { }
            }
            """);

        message.Should().Be(
            "[ComposeData] on 'TestNamespace.SampleTests.TestMethod': SkipWhen 'SkipIt' on " +
            "'TestNamespace.SampleTests' has a getter marked [Obsolete(error: true)], which makes any use of it a " +
            "compiler error; the generated registration reads the property directly, so it would fail to compile");
    }

    // Plain [Obsolete] (a warning, not an error) is NOT rejected.

    [Fact]
    public void PlainObsoleteMember_IsValid()
    {
        var model = Compose("""
            public sealed class Holder
            {
                [System.Obsolete("use something else")]
                public static System.Collections.Generic.IEnumerable<object> Cases => new object[] { "x" };
            }

            public sealed class SampleTests
            {
                [Compono.XunitV3.Aot.ComposeData("Cases", MemberType = typeof(Holder))]
                public void TestMethod(string value) { }
            }
            """);

        model.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void PlainObsoleteSkipProperty_IsValid()
    {
        var model = Compose("""
            public class Root
            {
                public static System.Collections.Generic.IEnumerable<object> Cases => new object[] { "x" };
            }

            public sealed class SampleTests : Root
            {
                [System.Obsolete("use something else")]
                public static bool SkipIt => false;

                [Compono.XunitV3.Aot.ComposeData("Cases", Skip = "reason", SkipUnless = "SkipIt")]
                public void TestMethod(string value) { }
            }
            """);

        model.Diagnostics.Should().BeEmpty();
    }

    // Step 7: the async-interface element table.

    [Fact]
    public void Async_OneReferenceElement_IsAsyncEnumerable()
    {
        var (diagnostics, models) = Discover("""
            public sealed class Holder
            {
                public static System.Collections.Generic.IAsyncEnumerable<Xunit.TheoryDataRow<string>> Cases =>
                    throw new System.NotImplementedException();
            }

            public sealed class SampleTests
            {
                [Compono.XunitV3.Aot.ComposeData("Cases", MemberType = typeof(Holder))]
                public void TestMethod(string value) { }
            }
            """);

        diagnostics.Should().BeEmpty();
        var model = models.Single();
        model.ReturnShape.Should().Be(AotComposeDataReturnShape.AsyncEnumerable);
        model.RowsAreTyped.Should().BeTrue();
        model.RowElementTypeNames.GetArray().Should().Equal("string");
        model.AsyncRowElementIsValueType.Should().BeFalse();
    }

    [Fact]
    public void Async_OneValueElement_IsAsyncEnumerableAndSetsAsyncRowElementIsValueType()
    {
        var (diagnostics, models) = Discover("""
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

        diagnostics.Should().BeEmpty();
        var model = models.Single();
        model.ReturnShape.Should().Be(AotComposeDataReturnShape.AsyncEnumerable);
        model.RowsAreTyped.Should().BeFalse();
        model.AsyncRowElementIsValueType.Should().BeTrue();
    }

    [Fact]
    public void Async_OneReferenceAndOneValueElement_IsSupportedAndPrefersReferenceElement()
    {
        var (diagnostics, models) = Discover("""
            public sealed class MixedSource
                : System.Collections.Generic.IAsyncEnumerable<Xunit.TheoryDataRow<string>>,
                  System.Collections.Generic.IAsyncEnumerable<(int, int)>
            {
                System.Collections.Generic.IAsyncEnumerator<Xunit.TheoryDataRow<string>>
                    System.Collections.Generic.IAsyncEnumerable<Xunit.TheoryDataRow<string>>.GetAsyncEnumerator(
                        System.Threading.CancellationToken cancellationToken) =>
                    throw new System.NotImplementedException();

                System.Collections.Generic.IAsyncEnumerator<(int, int)>
                    System.Collections.Generic.IAsyncEnumerable<(int, int)>.GetAsyncEnumerator(
                        System.Threading.CancellationToken cancellationToken) =>
                    throw new System.NotImplementedException();
            }

            public sealed class Holder
            {
                public static MixedSource Cases => new MixedSource();
            }

            public sealed class SampleTests
            {
                [Compono.XunitV3.Aot.ComposeData("Cases", MemberType = typeof(Holder))]
                public void TestMethod(string value) { }
            }
            """);

        diagnostics.Should().BeEmpty();
        var model = models.Single();
        model.ReturnShape.Should().Be(AotComposeDataReturnShape.AsyncEnumerable);
        model.RowsAreTyped.Should().BeTrue();
        model.RowElementTypeNames.GetArray().Should().Equal("string");
        model.AsyncRowElementIsValueType.Should().BeFalse();
    }

    [Fact]
    public void Async_TwoReferenceElements_ReportsCmp0053()
    {
        var id = SingleId("""
            public sealed class MultiRefSource
                : System.Collections.Generic.IAsyncEnumerable<Xunit.TheoryDataRow<string>>,
                  System.Collections.Generic.IAsyncEnumerable<Xunit.TheoryDataRow<int>>
            {
                System.Collections.Generic.IAsyncEnumerator<Xunit.TheoryDataRow<string>>
                    System.Collections.Generic.IAsyncEnumerable<Xunit.TheoryDataRow<string>>.GetAsyncEnumerator(
                        System.Threading.CancellationToken cancellationToken) =>
                    throw new System.NotImplementedException();

                System.Collections.Generic.IAsyncEnumerator<Xunit.TheoryDataRow<int>>
                    System.Collections.Generic.IAsyncEnumerable<Xunit.TheoryDataRow<int>>.GetAsyncEnumerator(
                        System.Threading.CancellationToken cancellationToken) =>
                    throw new System.NotImplementedException();
            }

            public sealed class Holder
            {
                public static MultiRefSource Cases => new MultiRefSource();
            }

            public sealed class SampleTests
            {
                [Compono.XunitV3.Aot.ComposeData("Cases", MemberType = typeof(Holder))]
                public void TestMethod(string value) { }
            }
            """);

        id.Should().Be("CMP0053");
    }

    [Fact]
    public void Async_TwoValueElements_ReportsCmp0053()
    {
        var id = SingleId("""
            public sealed class MultiValueSource
                : System.Collections.Generic.IAsyncEnumerable<(int, int)>,
                  System.Collections.Generic.IAsyncEnumerable<(long, long)>
            {
                System.Collections.Generic.IAsyncEnumerator<(int, int)>
                    System.Collections.Generic.IAsyncEnumerable<(int, int)>.GetAsyncEnumerator(
                        System.Threading.CancellationToken cancellationToken) =>
                    throw new System.NotImplementedException();

                System.Collections.Generic.IAsyncEnumerator<(long, long)>
                    System.Collections.Generic.IAsyncEnumerable<(long, long)>.GetAsyncEnumerator(
                        System.Threading.CancellationToken cancellationToken) =>
                    throw new System.NotImplementedException();
            }

            public sealed class Holder
            {
                public static MultiValueSource Cases => new MultiValueSource();
            }

            public sealed class SampleTests
            {
                [Compono.XunitV3.Aot.ComposeData("Cases", MemberType = typeof(Holder))]
                public void TestMethod(string value) { }
            }
            """);

        id.Should().Be("CMP0053");
    }

    // The approved mixed-interface case (one reference element + one value element) still feeds the B4.7 typed-row
    // validation: the reference element is TheoryDataRow<int>, whose value int cannot bind to the string parameter.
    [Fact]
    public void Async_MixedInterfaces_TypedRowElementMismatch_ReportsCmp0056()
    {
        var message = SingleMessage("""
            public sealed class MixedSource
                : System.Collections.Generic.IAsyncEnumerable<int>,
                  System.Collections.Generic.IAsyncEnumerable<Xunit.TheoryDataRow<int>>
            {
                System.Collections.Generic.IAsyncEnumerator<int>
                    System.Collections.Generic.IAsyncEnumerable<int>.GetAsyncEnumerator(
                        System.Threading.CancellationToken cancellationToken) =>
                    throw new System.NotImplementedException();

                System.Collections.Generic.IAsyncEnumerator<Xunit.TheoryDataRow<int>>
                    System.Collections.Generic.IAsyncEnumerable<Xunit.TheoryDataRow<int>>.GetAsyncEnumerator(
                        System.Threading.CancellationToken cancellationToken) =>
                    throw new System.NotImplementedException();
            }

            public sealed class Holder
            {
                public static MixedSource Cases => new MixedSource();
            }

            public sealed class SampleTests
            {
                [Compono.XunitV3.Aot.ComposeData("Cases", MemberType = typeof(Holder))]
                public void TestMethod(string value) { }
            }
            """);

        message.Should().Be(
            "ComposeData member 'TestNamespace.Holder.Cases' for 'TestNamespace.SampleTests.TestMethod': row value " +
            "0 type 'int' can never be assigned to parameter 'value' of type 'string'");
    }

    [Fact]
    public void Async_IAsyncEnumerableAndIEnumerable_IsStillAsyncEnumerable()
    {
        var (diagnostics, models) = Discover("""
            public sealed class MixedSource
                : System.Collections.Generic.IAsyncEnumerable<int>,
                  System.Collections.IEnumerable
            {
                System.Collections.Generic.IAsyncEnumerator<int>
                    System.Collections.Generic.IAsyncEnumerable<int>.GetAsyncEnumerator(
                        System.Threading.CancellationToken cancellationToken) =>
                    throw new System.NotImplementedException();

                System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
                    throw new System.NotImplementedException();
            }

            public sealed class Holder
            {
                public static MixedSource Cases => new MixedSource();
            }

            public sealed class SampleTests
            {
                [Compono.XunitV3.Aot.ComposeData("Cases", MemberType = typeof(Holder))]
                public void TestMethod(string value) { }
            }
            """);

        // The async shape wins over Sync, so the row element (int) is reached through the async path and
        // rejected as a row shape; a Sync classification would have found no generic IEnumerable element
        // and produced no diagnostic at all.
        models.Single().ReturnShape.Should().Be(AotComposeDataReturnShape.AsyncEnumerable);
        var diagnostic = diagnostics.Single();
        diagnostic.Id.Should().Be("CMP0053");
        diagnostic.GetMessage().Should().Contain("unsupported row element type 'int'");
    }

    // Adjacent-precedence boundaries.

    [Fact]
    public void Precedence_Step6_Over_Step6a()
    {
        var id = SingleId("""
            public sealed class Outer
            {
                private sealed class Hidden<T>
                {
                    public static System.Collections.Generic.IEnumerable<object> Cases => new object[] { "x" };

                    [Compono.XunitV3.Aot.ComposeData("Cases")]
                    public void TestMethod(string value) { }
                }
            }
            """);

        id.Should().Be("CMP0044");
    }

    [Fact]
    public void Precedence_Step6b_Over_Step6c()
    {
        var id = SingleId("""
            public interface IFoo
            {
                [System.Obsolete("gone", true)]
                static abstract System.Collections.Generic.IEnumerable<object> Cases { get; }
            }

            public sealed class SampleTests
            {
                [Compono.XunitV3.Aot.ComposeData("Cases", MemberType = typeof(IFoo))]
                public void TestMethod(string value) { }
            }
            """);

        id.Should().Be("CMP0052");
    }

    [Fact]
    public void Precedence_Step6c_ProhibitedProperty_Over_ProhibitedGetter()
    {
        var message = SingleMessage("""
            public sealed class Holder
            {
                [System.Obsolete("gone", true)]
                public static System.Collections.Generic.IEnumerable<object> Cases
                {
                    [System.Obsolete("gone2", true)]
                    get => new object[] { "x" };
                }
            }

            public sealed class SampleTests
            {
                [Compono.XunitV3.Aot.ComposeData("Cases", MemberType = typeof(Holder))]
                public void TestMethod(string value) { }
            }
            """);

        message.Should().Be(
            "ComposeData member 'TestNamespace.Holder.Cases' for 'TestNamespace.SampleTests.TestMethod' is marked " +
            "[Obsolete(error: true)], which makes any use of it a compiler error; the generated registration reads " +
            "the member directly, so it would fail to compile");
    }

    [Fact]
    public void Precedence_Step6a_Over_Step6c()
    {
        var id = SingleId("""
            public class GenericBase<T>
            {
                [System.Obsolete("gone", true)]
                public static System.Collections.Generic.IEnumerable<object> Cases => new object[] { "x" };
            }

            public sealed class SampleTests<T> : GenericBase<T>
            {
                [Compono.XunitV3.Aot.ComposeData("Cases")]
                public void TestMethod(string value) { }
            }
            """);

        id.Should().Be("CMP0054");
    }

    [Fact]
    public void Precedence_Step6c_Over_Step7()
    {
        var id = SingleId("""
            public sealed class Holder
            {
                [System.Obsolete("gone", true)]
                public static string Cases => "not a collection";
            }

            public sealed class SampleTests
            {
                [Compono.XunitV3.Aot.ComposeData("Cases", MemberType = typeof(Holder))]
                public void TestMethod(string value) { }
            }
            """);

        id.Should().Be("CMP0047");
    }

    [Fact]
    public void Precedence_Step8_Over_Step9()
    {
        var id = SingleId("""
            public sealed class Holder
            {
                public static Xunit.TheoryData<int> Cases => new Xunit.TheoryData<int>();
            }

            public sealed class SampleTests
            {
                [Compono.XunitV3.Aot.ComposeData("Cases", MemberType = typeof(Holder), Skip = "reason", SkipUnless = "SkipIt", SkipWhen = "SkipOther")]
                public void TestMethod(string value) { }
            }
            """);

        id.Should().Be("CMP0056");
    }

    [Fact]
    public void Precedence_Step9Conflicting_Over_InaccessibleSkipHolder()
    {
        var id = SingleId("""
            public sealed class Outer
            {
                private sealed class Hidden
                {
                    public static bool SkipIt => false;
                }

                public static System.Collections.Generic.IEnumerable<object> Cases => new object[] { "x" };

                [Compono.XunitV3.Aot.ComposeData("Cases", Skip = "reason", SkipUnless = "SkipIt", SkipWhen = "SkipIt", SkipType = typeof(Hidden))]
                public void TestMethod(string value) { }
            }
            """);

        id.Should().Be("CMP0057");
    }

    [Fact]
    public void Precedence_Step9Inaccessible_Over_OpenGenericSkipHolder()
    {
        var id = SingleId("""
            public class Root
            {
                public static System.Collections.Generic.IEnumerable<object> Cases => new object[] { "x" };
            }

            public class GenericBase<T> : Root
            {
                public static bool SkipIt => false;
            }

            public sealed class Outer
            {
                private sealed class SampleTests<T> : GenericBase<T>
                {
                    [Compono.XunitV3.Aot.ComposeData("Cases", Skip = "reason", SkipUnless = "SkipIt")]
                    public void TestMethod(string value) { }
                }
            }
            """);

        id.Should().Be("CMP0044");
    }

    [Fact]
    public void Precedence_Step9a_Over_Step9b()
    {
        var id = SingleId("""
            public class Root
            {
                public static System.Collections.Generic.IEnumerable<object> Cases => new object[] { "x" };
            }

            public class GenericBase<T> : Root
            {
                [System.Obsolete("gone", true)]
                public static bool SkipIt => false;
            }

            public sealed class SampleTests<T> : GenericBase<T>
            {
                [Compono.XunitV3.Aot.ComposeData("Cases", Skip = "reason", SkipUnless = "SkipIt")]
                public void TestMethod(string value) { }
            }
            """);

        id.Should().Be("CMP0054");
    }

    [Fact]
    public void Precedence_Step9b_ProhibitedSkipProperty_Over_ProhibitedGetter()
    {
        var message = SingleMessage("""
            public class Root
            {
                public static System.Collections.Generic.IEnumerable<object> Cases => new object[] { "x" };
            }

            public sealed class SampleTests : Root
            {
                [System.Obsolete("gone", true)]
                public static bool SkipIt
                {
                    [System.Obsolete("gone2", true)]
                    get => false;
                }

                [Compono.XunitV3.Aot.ComposeData("Cases", Skip = "reason", SkipUnless = "SkipIt")]
                public void TestMethod(string value) { }
            }
            """);

        message.Should().Be(
            "[ComposeData] on 'TestNamespace.SampleTests.TestMethod': SkipUnless 'SkipIt' on " +
            "'TestNamespace.SampleTests' is marked [Obsolete(error: true)], which makes any use of it a compiler " +
            "error; the generated registration reads the property directly, so it would fail to compile");
    }

    // Step 6c: a required [CompilerFeatureRequired] member is a prohibited call-site attribute; IsOptional = true is
    // not. The attribute cannot be applied in source (CS8335), so it is emitted into a referenced assembly.

    [Fact]
    public void ProhibitedMember_CompilerFeatureRequired_ReportsCmp0047()
    {
        var reference = CompilerFeatureRequiredLibraryReference(isOptional: false);
        var message = Discover(
            """
            public sealed class SampleTests
            {
                [Compono.XunitV3.Aot.ComposeData("Cases", MemberType = typeof(CfrLib.CfrData))]
                public void TestMethod(string value) { }
            }
            """,
            [reference]).Diagnostics.Single().GetMessage();

        message.Should().Be(
            "ComposeData member 'CfrLib.CfrData.Cases' for 'TestNamespace.SampleTests.TestMethod' is marked " +
            "[CompilerFeatureRequired(\"FutureFeature\")], which makes any use of it a compiler error; the generated " +
            "registration reads the member directly, so it would fail to compile");
    }

    [Fact]
    public void CompilerFeatureRequired_Optional_IsValid()
    {
        var reference = CompilerFeatureRequiredLibraryReference(isOptional: true);

        // Prove the emitted attribute really decodes as optional, so the "no diagnostic" assertion
        // below can't pass just because the named argument (or the whole attribute) failed to decode.
        var probe = CSharpCompilation.Create(
            "CfrOptionalDecodeProbe",
            [],
            [.. ComposeDataAotTestHelper.BaseReferences, reference]);
        var casesField = probe.GetTypeByMetadataName("CfrLib.CfrData")!.GetMembers("Cases").OfType<IFieldSymbol>().Single();
        var attribute = casesField.GetAttributes().Single();
        attribute.AttributeClass!.ToDisplayString().Should().Be("System.Runtime.CompilerServices.CompilerFeatureRequiredAttribute");
        attribute.ConstructorArguments.Should().ContainSingle().Which.Value.Should().Be("FutureFeature");
        attribute.NamedArguments.Should().ContainSingle();
        attribute.NamedArguments[0].Key.Should().Be("IsOptional");
        attribute.NamedArguments[0].Value.Value.Should().Be(true);

        var (diagnostics, models) = Discover(
            """
            public sealed class SampleTests
            {
                [Compono.XunitV3.Aot.ComposeData("Cases", MemberType = typeof(CfrLib.CfrData))]
                public void TestMethod(string value) { }
            }
            """,
            [reference]);

        diagnostics.Should().BeEmpty();
        var model = models.Single();
        model.Diagnostics.Count.Should().Be(0);
        model.FullyQualifiedMemberContainingTypeName.Should().Be("global::CfrLib.CfrData");
    }

    private static (ImmutableArray<Diagnostic> Diagnostics, IReadOnlyList<AotComposeDataMethodInfo> Models) Discover(
        string body,
        IReadOnlyList<MetadataReference> extraReferences) =>
        ComposeDataAotTestHelper.Discover(Source(body), extraReferences);

    private static MetadataReference CompilerFeatureRequiredLibraryReference(bool isOptional) =>
        CompilerFeatureRequiredReference.Create(isOptional, ComposeDataAotTestHelper.BaseReferences);
}
