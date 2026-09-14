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
            // Stands in for the real Compono.XunitV3.Aot.ComposeAttribute family (a separate
            // package/assembly, not referenced from this generator test project) -
            // AotComposeMethodDiscovery matches on the fully qualified metadata name alone, so
            // same-named types here trigger it identically to the real ones. Deliberately no local
            // ICompositionProfile stand-in: this generator test project already has a real
            // ProjectReference to Compono core, and the generated code for a profile form calls the
            // real Compono.CompositionBuilder.AddProfile<TProfile>()/AddProfile(ICompositionProfile)
            // directly - unlike CompositionPlanVerifyTests's Compono.XunitV3 stand-ins (which only
            // ever exercise TransformMethod's own type-discovery concern, never actual generated
            // AddProfile plumbing), so TProfile below is left unconstrained-by-a-local-fake and
            // instead resolves ICompositionProfile via ordinary namespace-nesting lookup to the real
            // Compono.ICompositionProfile - exactly matching how the real production
            // Compono.XunitV3.Aot.ComposeAttribute{TProfile}.cs file itself resolves it.
            //
            // All three forms derive directly from Xunit.v3.DataAttribute, mirroring the real
            // production shape exactly (ADR-0067 Amendment 1) - no shared base class between them,
            // so AotComposeMethodDiscovery.CountAotComposeAttributes' metadata-name-based cross-check
            // (CMP0045) is genuinely necessary here, not a stand-in-only artifact.
            public sealed class ComposeAttribute : Xunit.v3.DataAttribute
            {
            }

            public sealed class ComposeAttribute<TProfile> : Xunit.v3.DataAttribute
                where TProfile : ICompositionProfile, new()
            {
            }

            public sealed class ComposeAttribute<TProfile, TConfig> : Xunit.v3.DataAttribute
                where TProfile : ICompositionProfile
            {
                public ComposeAttribute(params object?[] configArguments) { }
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

    [Fact]
    public Task GenericAttribute_AppliesProfileWithAddProfile() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = XunitAotStandIns + """

                namespace TestNamespace
                {
                    public sealed class ProfileTests
                    {
                        public sealed class TestProfile : Compono.ICompositionProfile
                        {
                            public void Configure(Compono.CompositionBuilder builder) { }
                        }

                        [Compono.XunitV3.Aot.Compose<TestProfile>]
                        public void Test_uses_profile(string value)
                        {
                        }
                    }
                }
                """,
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task TwoTypeParameterAttribute_ConstructsConfigAndProfileDirectly() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = XunitAotStandIns + """

                namespace TestNamespace
                {
                    public enum ProfileKind
                    {
                        Default,
                        Special,
                    }

                    public sealed class ProfileConfig
                    {
                        public ProfileConfig(ProfileKind kind, string label, bool enabled) { }
                    }

                    public sealed class ConfiguredProfile : Compono.ICompositionProfile
                    {
                        public ConfiguredProfile(ProfileConfig config) { }
                        public void Configure(Compono.CompositionBuilder builder) { }
                    }

                    public sealed class TwoTypeParameterTests
                    {
                        [Compono.XunitV3.Aot.Compose<ConfiguredProfile, ProfileConfig>(ProfileKind.Special, "widget", true)]
                        public void Test_uses_configured_profile(string value)
                        {
                        }
                    }
                }
                """,
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task TwoTypeParameterAttribute_TConfigWithoutSinglePublicConstructor_ReportsCmp0041() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = XunitAotStandIns + """

                    namespace TestNamespace
                    {
                        public sealed class BadConfig
                        {
                            public BadConfig(int a) { }
                            public BadConfig(int a, int b) { }
                        }

                        public sealed class SomeProfile : Compono.ICompositionProfile
                        {
                            public SomeProfile(BadConfig config) { }
                            public void Configure(Compono.CompositionBuilder builder) { }
                        }

                        public sealed class Cmp0041Tests
                        {
                            [Compono.XunitV3.Aot.Compose<SomeProfile, BadConfig>(1)]
                            public void Test_has_ambiguous_config_constructor(string value)
                            {
                            }
                        }
                    }
                    """,
            },
            "CMP0041",
            TestContext.Current.CancellationToken);

    [Fact]
    public Task TwoTypeParameterAttribute_TProfileWithoutMatchingConstructor_ReportsCmp0042() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = XunitAotStandIns + """

                    namespace TestNamespace
                    {
                        public sealed class SomeConfig
                        {
                            public SomeConfig(int a) { }
                        }

                        public sealed class BadProfile : Compono.ICompositionProfile
                        {
                            public BadProfile() { }
                            public void Configure(Compono.CompositionBuilder builder) { }
                        }

                        public sealed class Cmp0042Tests
                        {
                            [Compono.XunitV3.Aot.Compose<BadProfile, SomeConfig>(1)]
                            public void Test_profile_has_no_config_constructor(string value)
                            {
                            }
                        }
                    }
                    """,
            },
            "CMP0042",
            TestContext.Current.CancellationToken);

    [Fact]
    public Task TwoTypeParameterAttribute_ArgumentCountMismatch_ReportsCmp0043() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = XunitAotStandIns + """

                    namespace TestNamespace
                    {
                        public sealed class TwoArgConfig
                        {
                            public TwoArgConfig(int a, int b) { }
                        }

                        public sealed class MismatchProfile : Compono.ICompositionProfile
                        {
                            public MismatchProfile(TwoArgConfig config) { }
                            public void Configure(Compono.CompositionBuilder builder) { }
                        }

                        public sealed class Cmp0043CountTests
                        {
                            [Compono.XunitV3.Aot.Compose<MismatchProfile, TwoArgConfig>(1)]
                            public void Test_supplies_too_few_arguments(string value)
                            {
                            }
                        }
                    }
                    """,
            },
            "CMP0043",
            TestContext.Current.CancellationToken);

    [Fact]
    public Task TwoTypeParameterAttribute_ArgumentTypeMismatch_ReportsCmp0043() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = XunitAotStandIns + """

                    namespace TestNamespace
                    {
                        public sealed class NumericConfig
                        {
                            public NumericConfig(double value) { }
                        }

                        public sealed class NumericProfile : Compono.ICompositionProfile
                        {
                            public NumericProfile(NumericConfig config) { }
                            public void Configure(Compono.CompositionBuilder builder) { }
                        }

                        public sealed class Cmp0043TypeTests
                        {
                            // An int constant is not implicitly IsInstanceOfType a double parameter -
                            // Compono.XunitV3.Binding.PositionalArgumentBinder's exact runtime rule,
                            // mirrored here at compile time (no numeric widening).
                            [Compono.XunitV3.Aot.Compose<NumericProfile, NumericConfig>(1)]
                            public void Test_supplies_int_for_double_parameter(string value)
                            {
                            }
                        }
                    }
                    """,
            },
            "CMP0043",
            TestContext.Current.CancellationToken);

    // PR #140 Codex review findings - CMP0044/CMP0045/CMP0046 and the two real-value rendering fixes
    // (non-named TConfig no longer crashes the generator; NaN/Infinity/negative-zero render as valid
    // C#), added after the initial pass.

    [Fact]
    public Task TwoTypeParameterAttribute_InaccessibleTConfig_ReportsCmp0044() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = XunitAotStandIns + """

                    namespace TestNamespace
                    {
                        public sealed class Cmp0044Tests
                        {
                            // Private to Cmp0044Tests - legal at the [Compose<...>] use site (nested
                            // inside the same class), but not accessible from the generated top-level
                            // registration file, which would fail with CS0122 if this weren't caught
                            // here first.
                            private sealed class PrivateConfig
                            {
                                public PrivateConfig(int value) { }
                            }

                            public sealed class PrivateConfigProfile : Compono.ICompositionProfile
                            {
                                public PrivateConfigProfile(PrivateConfig config) { }
                                public void Configure(Compono.CompositionBuilder builder) { }
                            }

                            [Compono.XunitV3.Aot.Compose<PrivateConfigProfile, PrivateConfig>(1)]
                            public void Test_config_type_is_private(string value)
                            {
                            }
                        }
                    }
                    """,
            },
            "CMP0044",
            TestContext.Current.CancellationToken);

    [Fact]
    public Task StackedComposeAndGenericAttribute_ReportsCmp0045() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = XunitAotStandIns + """

                    namespace TestNamespace
                    {
                        public sealed class StackedProfile : Compono.ICompositionProfile
                        {
                            public void Configure(Compono.CompositionBuilder builder) { }
                        }

                        public sealed class Cmp0045Tests
                        {
                            // [Compose] and [Compose<TProfile>] are unrelated attribute types (ADR-0067
                            // Amendment 1) - nothing in the C# compiler itself rejects stacking both on
                            // one method, so this has to be caught here instead of crashing the
                            // generator with a duplicate AddSource hint name.
                            [Compono.XunitV3.Aot.Compose]
                            [Compono.XunitV3.Aot.Compose<StackedProfile>]
                            public void Test_has_two_compose_family_attributes(string value)
                            {
                            }
                        }
                    }
                    """,
            },
            "CMP0045",
            TestContext.Current.CancellationToken);

    [Fact]
    public Task TwoTypeParameterAttribute_NonNamedTConfig_ReportsCmp0041WithoutCrashing() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = XunitAotStandIns + """

                    namespace TestNamespace
                    {
                        public sealed class ArrayConfigProfile : Compono.ICompositionProfile
                        {
                            public void Configure(Compono.CompositionBuilder builder) { }
                        }

                        public sealed class Cmp0041NonNamedTests
                        {
                            // TConfig carries no constraint at all, so a non-named type (an array,
                            // here) is legal C# generic-attribute syntax - an earlier pass cast
                            // TypeArguments[1] straight to INamedTypeSymbol, which throws
                            // InvalidCastException (crashing the whole generator) for exactly this
                            // shape instead of reporting CMP0041.
                            [Compono.XunitV3.Aot.Compose<ArrayConfigProfile, string[]>(new[] { "a", "b" })]
                            public void Test_supplies_array_type_as_config(string value)
                            {
                            }
                        }
                    }
                    """,
            },
            "CMP0041",
            TestContext.Current.CancellationToken);

    [Fact]
    public Task TwoTypeParameterAttribute_RequiredMemberUnsatisfied_ReportsCmp0046() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = XunitAotStandIns + """

                    namespace TestNamespace
                    {
                        public sealed class RequiredMemberConfig
                        {
                            public RequiredMemberConfig(int value) { Value = value; }

                            public int Value { get; }

                            // Not satisfied by the constructor above (no [SetsRequiredMembers]) - a
                            // direct `new RequiredMemberConfig(1)` call fails with CS9035 in the
                            // generated registration unless this is caught here first.
                            public required string Name { get; init; }
                        }

                        public sealed class RequiredMemberProfile : Compono.ICompositionProfile
                        {
                            public RequiredMemberProfile(RequiredMemberConfig config) { }
                            public void Configure(Compono.CompositionBuilder builder) { }
                        }

                        public sealed class Cmp0046Tests
                        {
                            [Compono.XunitV3.Aot.Compose<RequiredMemberProfile, RequiredMemberConfig>(1)]
                            public void Test_config_has_unsatisfied_required_member(string value)
                            {
                            }
                        }
                    }
                    """,
            },
            "CMP0046",
            TestContext.Current.CancellationToken);

    [Fact]
    public Task TwoTypeParameterAttribute_SpecialFloatingPointValues_RenderAsValidCSharp() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = XunitAotStandIns + """

                namespace TestNamespace
                {
                    public sealed class FloatingPointConfig
                    {
                        public FloatingPointConfig(double notANumber, double positiveInfinity, double negativeInfinity, double negativeZero, float floatNotANumber)
                        {
                        }
                    }

                    public sealed class FloatingPointProfile : Compono.ICompositionProfile
                    {
                        public FloatingPointProfile(FloatingPointConfig config) { }
                        public void Configure(Compono.CompositionBuilder builder) { }
                    }

                    public sealed class FloatingPointTests
                    {
                        // double.NaN/PositiveInfinity/NegativeInfinity and float.NaN are real, legal
                        // compile-time-constant attribute arguments - the initial TypedConstantLiteralRenderer
                        // pass rendered these as bare "NaN"/"Infinity" identifiers via Convert.ToString,
                        // which isn't valid C# syntax; -0.0 lost its sign the same way. This is a
                        // compiles-and-runs (Verify, not VerifyFailure) proof, not just a snapshot of
                        // the rendered text - GeneratorTestHelpers.Verify already asserts the generated
                        // code has zero compiler errors.
                        [Compono.XunitV3.Aot.Compose<FloatingPointProfile, FloatingPointConfig>(double.NaN, double.PositiveInfinity, double.NegativeInfinity, -0.0, float.NaN)]
                        public void Test_supplies_special_floating_point_values(string value)
                        {
                        }
                    }
                }
                """,
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task RefStructParameterType_ReportsCmp0040() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = XunitAotStandIns + """

                    namespace TestNamespace
                    {
                        public sealed class RefStructParameterTypeTests
                        {
                            [Compono.XunitV3.Aot.Compose]
                            public void Span_parameter_type_is_unsupported(System.Span<int> value)
                            {
                            }
                        }
                    }
                    """,
            },
            "CMP0040",
            TestContext.Current.CancellationToken);
}
