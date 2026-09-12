namespace Compono.Generators.Tests;

/// <summary>
/// Snapshot coverage for <c>Compono.Generators.Emitters.AotTheoryDataRowRegistrationEmitter</c> -
/// the <c>Compono.XunitV3.Aot</c>-specific <c>RegisteredEngineConfig.RegisterTheoryDataRowFactory(...)</c>
/// registration, per ADR-0066/PLAN-0066. <c>Xunit</c>/<c>Xunit.Sdk</c>/<c>Xunit.v3</c> types are
/// hand-written stand-ins matching the real <c>xunit.v3.extensibility.core.aot</c>/
/// <c>xunit.v3.core.aot</c> API shapes and namespaces exactly (confirmed directly by loading the real
/// assembly and enumerating its exported types during implementation - <c>Xunit.ITheoryDataRow</c>/
/// <c>Xunit.TheoryDataRow</c> live in the plain <c>Xunit</c> namespace, not <c>Xunit.Sdk</c> or
/// <c>Xunit.v3</c> as the reflection-mode package's equivalents do) - the same "stand in for the real
/// package, matched by metadata name alone" pattern
/// <see cref="CompositionPlanVerifyTests.MSTestComposeAttributedMethodParameter_GeneratesCompositionPlan"/>
/// already establishes for the other four <c>[Compose]</c>-family attribute integrations, so this
/// project never needs a real xUnit package reference.
/// </summary>
public sealed class AotTheoryDataRowRegistrationVerifyTests
{
    private const string XunitAotStandIns = """
        namespace Xunit
        {
            public interface ITheoryDataRow
            {
            }

            public sealed class TheoryDataRow : ITheoryDataRow
            {
                public TheoryDataRow(object?[] data) { }
            }
        }

        namespace Xunit.Sdk
        {
            public sealed class DisposalTracker
            {
            }
        }

        namespace Xunit.v3
        {
            public abstract class DataAttribute : System.Attribute
            {
            }

            public static class RegisteredEngineConfig
            {
                public static void RegisterTheoryDataRowFactory(
                    string testClassIndex,
                    string methodName,
                    bool disableDiscoveryEnumeration,
                    System.Func<Xunit.Sdk.DisposalTracker, System.Threading.Tasks.ValueTask<System.Collections.Generic.IReadOnlyCollection<Xunit.ITheoryDataRow>>> factory)
                {
                }
            }
        }

        namespace Compono.XunitV3.Aot
        {
            // Stands in for the real Compono.XunitV3.Aot.ComposeAttribute (a separate package/
            // assembly, not referenced from this generator test project) - AotComposeMethodDiscovery
            // matches on the fully qualified metadata name alone, so a same-named type here triggers
            // it identically to the real one.
            public sealed class ComposeAttribute : Xunit.v3.DataAttribute
            {
            }
        }
        """;

    [Fact]
    public Task SingleComposedParameter_GeneratesAotTheoryDataRowRegistration() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = XunitAotStandIns + """

                namespace TestNamespace
                {
                    public sealed class Widget
                    {
                        public Widget(string name) { Name = name; }
                        public string Name { get; }
                    }

                    public sealed class WidgetTests
                    {
                        [Compono.XunitV3.Aot.Compose]
                        public void Widget_is_composed(Widget widget)
                        {
                        }
                    }
                }
                """,
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task MultipleParameters_PreservesDeclarationOrder() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = XunitAotStandIns + """

                namespace TestNamespace
                {
                    public sealed class Widget
                    {
                        public Widget(string name) { Name = name; }
                        public string Name { get; }
                    }

                    public sealed class OrderedParameterTests
                    {
                        [Compono.XunitV3.Aot.Compose]
                        public void Ordered_parameters_are_composed_in_declaration_order(Widget widget, string leaf, int quantity)
                        {
                        }
                    }
                }
                """,
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task MultipleComposeAotMethods_DoNotInterfere() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = XunitAotStandIns + """

                namespace TestNamespace
                {
                    public sealed class MultipleMethodsTests
                    {
                        [Compono.XunitV3.Aot.Compose]
                        public void First_method_is_composed(string value)
                        {
                        }

                        [Compono.XunitV3.Aot.Compose]
                        public void Second_method_is_composed(int value)
                        {
                        }
                    }
                }
                """,
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task GenericTestMethod_ReportsCmp0040() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = XunitAotStandIns + """

                    namespace TestNamespace
                    {
                        public sealed class GenericMethodTests
                        {
                            [Compono.XunitV3.Aot.Compose]
                            public void Generic_test_method_is_unsupported<T>(string value)
                            {
                            }
                        }
                    }
                    """,
            },
            "CMP0040",
            TestContext.Current.CancellationToken);

    [Fact]
    public Task RefParameter_ReportsCmp0040() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = XunitAotStandIns + """

                    namespace TestNamespace
                    {
                        public sealed class RefParameterTests
                        {
                            [Compono.XunitV3.Aot.Compose]
                            public void Ref_parameter_is_unsupported(ref int value)
                            {
                            }
                        }
                    }
                    """,
            },
            "CMP0040",
            TestContext.Current.CancellationToken);
}
