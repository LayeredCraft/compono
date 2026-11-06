using System.Linq;
using Compono.Generators.Models;
using Xunit;

namespace Compono.Generators.Tests;

public sealed class AotComposeDataMemberResolutionTests
{
    private static string Wrap(string body) => $"namespace TestNamespace\n{{\nusing Compono;\n{body}\n}}\n";

    private static AotComposeDataMethodInfo Compose(string body) => ComposeDataAotTestHelper.SingleModel(Wrap(body));

    private static string DiagnosticId(string body) => ComposeDataAotTestHelper.Discover(Wrap(body)).Diagnostics.Single().Id;

    private static string DiagnosticMessage(string body) => ComposeDataAotTestHelper.Discover(Wrap(body)).Diagnostics.Single().GetMessage();

    private static string TestMethod => "[Compono.XunitV3.Aot.ComposeData(\"Cases\")]\n    public void TestMethod(string value) { }";

    [Fact]
    public void Row01_generic_method_only_reports_cmp0052_generic_method()
    {
        var message = DiagnosticMessage($$"""
        public class SampleTests
        {
            public static System.Collections.Generic.IEnumerable<object?[]> Cases<T>() => throw null!;
            {{TestMethod}}
        }
        """);

        message.Should().Contain("is a generic method");
    }

    [Fact]
    public void Row02_generic_and_parameterless_reports_cmp0051_ambiguous()
    {
        DiagnosticMessage($$"""
        public class SampleTests
        {
            public static System.Collections.Generic.IEnumerable<object?[]> Cases<T>() => throw null!;
            public static System.Collections.Generic.IEnumerable<object?[]> Cases() => throw null!;
            {{TestMethod}}
        }
        """).Should().Contain("is ambiguous");
    }

    [Fact]
    public void Row03_generic_and_int_reports_cmp0052_generic_method()
    {
        DiagnosticMessage($$"""
        public class SampleTests
        {
            public static System.Collections.Generic.IEnumerable<object?[]> Cases<T>() => throw null!;
            public static System.Collections.Generic.IEnumerable<object?[]> Cases(int value) => throw null!;
            {{TestMethod}}
        }
        """).Should().Contain("is a generic method");
    }

    [Fact]
    public void Row04_parameterless_and_int_is_valid()
    {
        var model = Compose($$"""
        public class SampleTests
        {
            public static System.Collections.Generic.IEnumerable<object?[]> Cases() => throw null!;
            public static System.Collections.Generic.IEnumerable<object?[]> Cases(int value) => throw null!;
            {{TestMethod}}
        }
        """);

        model.MemberKind.Should().Be(AotComposeDataMemberKind.Method);
        model.FullyQualifiedMemberContainingTypeName.Should().Be("global::TestNamespace.SampleTests");
    }

    [Fact]
    public void Row05_optional_only_reports_cmp0052_requires_arguments()
    {
        DiagnosticMessage($$"""
        public class SampleTests
        {
            public static System.Collections.Generic.IEnumerable<object?[]> Cases(int value = 0) => throw null!;
            {{TestMethod}}
        }
        """).Should().Contain("requires arguments");
    }

    [Fact]
    public void Row06_params_only_reports_cmp0052_requires_arguments()
    {
        DiagnosticMessage($$"""
        public class SampleTests
        {
            public static System.Collections.Generic.IEnumerable<object?[]> Cases(params int[] values) => throw null!;
            {{TestMethod}}
        }
        """).Should().Contain("requires arguments");
    }

    [Fact]
    public void Row07_derived_new_field_of_another_type_is_valid_derived_field()
    {
        var model = Compose($$"""
        public class Base
        {
            public static System.Collections.Generic.IEnumerable<object?[]> Cases = null!;
        }

        public class Derived : Base
        {
            public static new object?[] Cases = null!;
        }

        public class SampleTests : Derived
        {
            {{TestMethod}}
        }
        """);

        model.MemberKind.Should().Be(AotComposeDataMemberKind.Field);
        model.FullyQualifiedMemberContainingTypeName.Should().Be("global::TestNamespace.Derived");
    }

    [Fact]
    public void Row08_derived_new_property_of_another_type_reports_cmp0051_ambiguous()
    {
        DiagnosticMessage($$"""
        public class Base
        {
            public static System.Collections.Generic.IEnumerable<object?[]> Cases => throw null!;
        }

        public class Derived : Base
        {
            public static new System.Collections.Generic.List<string> Cases => throw null!;
        }

        public class SampleTests : Derived
        {
            {{TestMethod}}
        }
        """).Should().Contain("is ambiguous");
    }

    [Fact]
    public void Row09_derived_new_property_of_same_type_is_valid_derived_property()
    {
        var model = Compose($$"""
        public class Base
        {
            public static System.Collections.Generic.IEnumerable<object?[]> Cases => throw null!;
        }

        public class Derived : Base
        {
            public static new System.Collections.Generic.IEnumerable<object?[]> Cases => throw null!;
        }

        public class SampleTests : Derived
        {
            {{TestMethod}}
        }
        """);

        model.MemberKind.Should().Be(AotComposeDataMemberKind.Property);
        model.FullyQualifiedMemberContainingTypeName.Should().Be("global::TestNamespace.Derived");
    }

    [Fact]
    public void Row10_base_field_with_derived_method_is_valid_base_field()
    {
        var model = Compose($$"""
        public class Base
        {
            public static System.Collections.Generic.IEnumerable<object?[]> Cases = null!;
        }

        public class Derived : Base
        {
            public static System.Collections.Generic.IEnumerable<object?[]> Cases() => throw null!;
        }

        public class SampleTests : Derived
        {
            {{TestMethod}}
        }
        """);

        model.MemberKind.Should().Be(AotComposeDataMemberKind.Field);
        model.FullyQualifiedMemberContainingTypeName.Should().Be("global::TestNamespace.Base");
    }

    [Fact]
    public void Row11_base_static_property_with_derived_instance_property_is_valid_base_property()
    {
        var model = Compose($$"""
        public class Base
        {
            public static System.Collections.Generic.IEnumerable<object?[]> Cases => throw null!;
        }

        public class Derived : Base
        {
            public System.Collections.Generic.IEnumerable<object?[]> Cases => throw null!;
        }

        public class SampleTests : Derived
        {
            {{TestMethod}}
        }
        """);

        model.MemberKind.Should().Be(AotComposeDataMemberKind.Property);
        model.FullyQualifiedMemberContainingTypeName.Should().Be("global::TestNamespace.Base");
    }

    [Fact]
    public void Row12_base_method_with_derived_new_method_is_valid_derived_method()
    {
        var model = Compose($$"""
        public class Base
        {
            public static System.Collections.Generic.IEnumerable<object?[]> Cases() => throw null!;
        }

        public class Derived : Base
        {
            public static new System.Collections.Generic.IEnumerable<object?[]> Cases() => throw null!;
        }

        public class SampleTests : Derived
        {
            {{TestMethod}}
        }
        """);

        model.MemberKind.Should().Be(AotComposeDataMemberKind.Method);
        model.FullyQualifiedMemberContainingTypeName.Should().Be("global::TestNamespace.Derived");
    }

    [Fact]
    public void Row13_public_static_property_with_private_getter_reports_cmp0052_not_public_static()
    {
        DiagnosticMessage($$"""
        public class SampleTests
        {
            public static System.Collections.Generic.IEnumerable<object?[]> Cases { private get => throw null!; set { } }
            {{TestMethod}}
        }
        """).Should().Contain("must be public and static");
    }

    [Fact]
    public void Row14_field_on_closed_generic_base_is_valid_with_generic_call_target()
    {
        var model = Compose($$"""
        public class GenericBase<T>
        {
            public static System.Collections.Generic.IEnumerable<object?[]> Cases = null!;
        }

        public class SampleTests : GenericBase<int>
        {
            {{TestMethod}}
        }
        """);

        model.MemberKind.Should().Be(AotComposeDataMemberKind.Field);
        model.FullyQualifiedMemberContainingTypeName.Should().Be("global::TestNamespace.GenericBase<int>");
    }

    [Fact]
    public void Row15_member_on_private_nested_holder_reports_cmp0044()
    {
        DiagnosticId("""
        public class SampleTests
        {
            private static class PrivateHolder
            {
                public static System.Collections.Generic.IEnumerable<object?[]> Cases => throw null!;
            }

            [Compono.XunitV3.Aot.ComposeData("Cases", MemberType = typeof(PrivateHolder))]
            public void TestMethod(string value) { }
        }
        """).Should().Be("CMP0044");
    }

    [Fact]
    public void Row16_inherited_test_with_member_only_on_derived_reports_cmp0051_not_found()
    {
        DiagnosticMessage($$"""
        public class Base
        {
            {{TestMethod}}
        }

        public class Derived : Base
        {
            public static System.Collections.Generic.IEnumerable<object?[]> Cases => throw null!;
        }
        """).Should().Contain("was not found");
    }

    [Fact]
    public void Row17_member_on_the_test_class_is_valid()
    {
        var model = Compose($$"""
        public class SampleTests
        {
            public static System.Collections.Generic.IEnumerable<object?[]> Cases => throw null!;
            {{TestMethod}}
        }
        """);

        model.MemberKind.Should().Be(AotComposeDataMemberKind.Property);
        model.FullyQualifiedMemberContainingTypeName.Should().Be("global::TestNamespace.SampleTests");
    }

    [Fact]
    public void Row18_static_property_on_interface_member_type_is_valid()
    {
        var model = Compose("""
        public interface ICases
        {
            static System.Collections.Generic.IEnumerable<object?[]> Cases => throw null!;
        }

        public class SampleTests
        {
            [Compono.XunitV3.Aot.ComposeData("Cases", MemberType = typeof(ICases))]
            public void TestMethod(string value) { }
        }
        """);

        model.MemberKind.Should().Be(AotComposeDataMemberKind.Property);
        model.FullyQualifiedMemberContainingTypeName.Should().Be("global::TestNamespace.ICases");
    }

    [Fact]
    public void Row19_own_protected_static_reports_cmp0052()
    {
        DiagnosticMessage($$"""
        public class SampleTests
        {
            protected static System.Collections.Generic.IEnumerable<object?[]> Cases = null!;
            {{TestMethod}}
        }
        """).Should().Contain("must be public and static");
    }

    [Fact]
    public void Row20_own_internal_static_reports_cmp0052()
    {
        DiagnosticMessage($$"""
        public class SampleTests
        {
            internal static System.Collections.Generic.IEnumerable<object?[]> Cases = null!;
            {{TestMethod}}
        }
        """).Should().Contain("must be public and static");
    }

    [Fact]
    public void Row21_base_static_property_with_derived_instance_field_is_valid_base_property()
    {
        var model = Compose($$"""
        public class Base
        {
            public static System.Collections.Generic.IEnumerable<object?[]> Cases => throw null!;
        }

        public class Derived : Base
        {
            public System.Collections.Generic.IEnumerable<object?[]> Cases = null!;
        }

        public class SampleTests : Derived
        {
            {{TestMethod}}
        }
        """);

        model.MemberKind.Should().Be(AotComposeDataMemberKind.Property);
        model.FullyQualifiedMemberContainingTypeName.Should().Be("global::TestNamespace.Base");
    }

    [Fact]
    public void Row22_base_private_static_reports_cmp0051_not_found()
    {
        DiagnosticMessage($$"""
        public class Base
        {
            private static System.Collections.Generic.IEnumerable<object?[]> Cases = null!;
        }

        public class SampleTests : Base
        {
            {{TestMethod}}
        }
        """).Should().Contain("was not found");
    }

    [Fact]
    public void Row23_base_internal_static_reports_cmp0052()
    {
        DiagnosticMessage($$"""
        public class Base
        {
            internal static System.Collections.Generic.IEnumerable<object?[]> Cases = null!;
        }

        public class SampleTests : Base
        {
            {{TestMethod}}
        }
        """).Should().Contain("must be public and static");
    }

    [Fact]
    public void Row24_base_protected_static_reports_cmp0052()
    {
        DiagnosticMessage($$"""
        public class Base
        {
            protected static System.Collections.Generic.IEnumerable<object?[]> Cases = null!;
        }

        public class SampleTests : Base
        {
            {{TestMethod}}
        }
        """).Should().Contain("must be public and static");
    }

    [Fact]
    public void Row25_base_private_instance_reports_cmp0051_not_found()
    {
        DiagnosticMessage($$"""
        public class Base
        {
            private System.Collections.Generic.IEnumerable<object?[]> Cases => throw null!;
        }

        public class SampleTests : Base
        {
            {{TestMethod}}
        }
        """).Should().Contain("was not found");
    }

    [Fact]
    public void Row26_base_public_instance_reports_cmp0052()
    {
        DiagnosticMessage($$"""
        public class Base
        {
            public System.Collections.Generic.IEnumerable<object?[]> Cases => throw null!;
        }

        public class SampleTests : Base
        {
            {{TestMethod}}
        }
        """).Should().Contain("must be public and static");
    }

    [Fact]
    public void Row27_base_generic_with_derived_new_parameterless_is_valid_derived()
    {
        var model = Compose($$"""
        public class Base
        {
            public static System.Collections.Generic.IEnumerable<object?[]> Cases<T>() => throw null!;
        }

        public class Derived : Base
        {
            public static new System.Collections.Generic.IEnumerable<object?[]> Cases() => throw null!;
        }

        public class SampleTests : Derived
        {
            {{TestMethod}}
        }
        """);

        model.MemberKind.Should().Be(AotComposeDataMemberKind.Method);
        model.FullyQualifiedMemberContainingTypeName.Should().Be("global::TestNamespace.Derived");
    }

    [Fact]
    public void Row28_base_static_property_with_derived_static_field_is_valid_derived_field()
    {
        var model = Compose($$"""
        public class Base
        {
            public static System.Collections.Generic.IEnumerable<object?[]> Cases => throw null!;
        }

        public class Derived : Base
        {
            public static new System.Collections.Generic.IEnumerable<object?[]> Cases = null!;
        }

        public class SampleTests : Derived
        {
            {{TestMethod}}
        }
        """);

        model.MemberKind.Should().Be(AotComposeDataMemberKind.Field);
        model.FullyQualifiedMemberContainingTypeName.Should().Be("global::TestNamespace.Derived");
    }

    [Fact]
    public void Row29_base_int_only_reports_cmp0052_requires_arguments()
    {
        DiagnosticMessage($$"""
        public class Base
        {
            public static System.Collections.Generic.IEnumerable<object?[]> Cases(int value) => throw null!;
        }

        public class SampleTests : Base
        {
            {{TestMethod}}
        }
        """).Should().Contain("requires arguments");
    }

    [Fact]
    public void Row30_derived_ienumerable_of_dynamic_hides_base_ienumerable_of_object()
    {
        var model = Compose($$"""
        public class Base
        {
            public static System.Collections.Generic.IEnumerable<object> Cases => throw null!;
        }

        public class Derived : Base
        {
            public static new System.Collections.Generic.IEnumerable<dynamic> Cases => throw null!;
        }

        public class SampleTests : Derived
        {
            {{TestMethod}}
        }
        """);

        model.MemberKind.Should().Be(AotComposeDataMemberKind.Property);
        model.FullyQualifiedMemberContainingTypeName.Should().Be("global::TestNamespace.Derived");
    }

    [Fact]
    public void Row31_derived_list_of_tuple_with_different_element_names_hides_base_list()
    {
        var model = Compose($$"""
        public class Base
        {
            public static System.Collections.Generic.List<(int a, int b)> Cases => throw null!;
        }

        public class Derived : Base
        {
            public static new System.Collections.Generic.List<(int c, int d)> Cases => throw null!;
        }

        public class SampleTests : Derived
        {
            {{TestMethod}}
        }
        """);

        model.MemberKind.Should().Be(AotComposeDataMemberKind.Property);
        model.FullyQualifiedMemberContainingTypeName.Should().Be("global::TestNamespace.Derived");
    }
}
