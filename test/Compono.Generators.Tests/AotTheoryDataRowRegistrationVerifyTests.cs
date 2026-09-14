using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

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

    // PR #140 Codex review round 2 findings.

    [Fact]
    public Task TwoTypeParameterAttribute_TConfigConstructorHasByRefParameter_ReportsCmp0041() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = XunitAotStandIns + """

                    namespace TestNamespace
                    {
                        public sealed class ByRefConfig
                        {
                            // A supplied literal argument isn't an assignable variable - `new
                            // ByRefConfig(1)` would fail with CS1620 if this constructor were ever
                            // selected as the "one usable public constructor". IParameterSymbol.Type
                            // strips the ref modifier, so a naive Type-only match would have accepted
                            // this and reported CMP0041 with count 0 was previously not enforced here.
                            public ByRefConfig(ref int value) { }
                        }

                        public sealed class ByRefConfigProfile : Compono.ICompositionProfile
                        {
                            public ByRefConfigProfile(ByRefConfig config) { }
                            public void Configure(Compono.CompositionBuilder builder) { }
                        }

                        public sealed class Cmp0041ByRefTests
                        {
                            [Compono.XunitV3.Aot.Compose<ByRefConfigProfile, ByRefConfig>(1)]
                            public void Test_config_constructor_takes_ref_parameter(string value)
                            {
                            }
                        }
                    }
                    """,
            },
            "CMP0041",
            TestContext.Current.CancellationToken);

    [Fact]
    public Task TwoTypeParameterAttribute_TProfileConstructorHasByRefParameter_ReportsCmp0042() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = XunitAotStandIns + """

                    namespace TestNamespace
                    {
                        public sealed class PlainConfig
                        {
                            public PlainConfig(int value) { }
                        }

                        public sealed class ByRefProfile : Compono.ICompositionProfile
                        {
                            // IParameterSymbol.Type strips the ref modifier, so this would otherwise
                            // match PlainConfig by type alone - the generated `new ByRefProfile(profileConfig)`
                            // call (no `in` keyword) would actually compile (the `in` modifier is
                            // call-site-optional), silently diverging from Compono.XunitV3's JIT-mode
                            // binder, which never matches this shape at all (it sees PlainConfig&).
                            public ByRefProfile(in PlainConfig config) { }
                            public void Configure(Compono.CompositionBuilder builder) { }
                        }

                        public sealed class Cmp0042ByRefTests
                        {
                            [Compono.XunitV3.Aot.Compose<ByRefProfile, PlainConfig>(1)]
                            public void Test_profile_constructor_takes_in_parameter(string value)
                            {
                            }
                        }
                    }
                    """,
            },
            "CMP0042",
            TestContext.Current.CancellationToken);

    [Fact]
    public void TwoTypeParameterAttribute_ErroneousArgumentExpression_DoesNotCrashGenerator()
    {
        // PR #140 Codex review round 2: an incomplete/erroneous compilation (e.g. a live IDE analysis
        // pass mid-edit) can hand TypedConstantMatcher a TypedConstant of Kind = Error whose Type is
        // still the parameter's own declared type - before the fix, ClassifyConversion would find a
        // trivial identity conversion and report Valid, reaching TypedConstantLiteralRenderer.Render's
        // unhandled default arm, which throws and crashes the whole generator (not just this method).
        // UndefinedIdentifier below is deliberately never declared, so the compiler itself reports
        // CS0103 for it - the property under test is that the generator driver doesn't also throw an
        // unhandled exception on top of that, not that this (deliberately invalid) source compiles.
        const string source = """
            namespace Xunit
            {
                public interface ITheoryDataRow { }
                public sealed class TheoryDataRow : ITheoryDataRow { public TheoryDataRow(object?[] data) { } }
            }

            namespace Xunit.Sdk
            {
                public sealed class DisposalTracker { }
            }

            namespace Xunit.v3
            {
                public abstract class DataAttribute : System.Attribute { }

                public static class RegisteredEngineConfig
                {
                    public static void RegisterTheoryDataRowFactory(
                        string testClassIndex, string methodName, bool disableDiscoveryEnumeration,
                        System.Func<Xunit.Sdk.DisposalTracker, System.Threading.Tasks.ValueTask<System.Collections.Generic.IReadOnlyCollection<Xunit.ITheoryDataRow>>> factory) { }
                }
            }

            namespace Compono.XunitV3.Aot
            {
                public sealed class ComposeAttribute : Xunit.v3.DataAttribute { }

                public sealed class ComposeAttribute<TProfile, TConfig> : Xunit.v3.DataAttribute
                    where TProfile : ICompositionProfile
                {
                    public ComposeAttribute(params object?[] configArguments) { }
                }
            }

            namespace TestNamespace;

            public sealed class ErrorConfig
            {
                public ErrorConfig(int value) { }
            }

            public sealed class ErrorProfile : Compono.ICompositionProfile
            {
                public ErrorProfile(ErrorConfig config) { }
                public void Configure(Compono.CompositionBuilder builder) { }
            }

            public sealed class ErrorArgumentTests
            {
                [Compono.XunitV3.Aot.Compose<ErrorProfile, ErrorConfig>(UndefinedIdentifier)]
                public void Test_supplies_undefined_identifier_as_argument(string value)
                {
                }
            }
            """;

        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp14);
        var tree = CSharpSyntaxTree.ParseText(source, parseOptions, "Program.cs", cancellationToken: TestContext.Current.CancellationToken);

        var compilationOptions = new CSharpCompilationOptions(
            OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable);
        List<MetadataReference> references =
        [
#if NET11_0_OR_GREATER
            .. Basic.Reference.Assemblies.Net110.References.All,
#elif NET10_0_OR_GREATER
            .. Basic.Reference.Assemblies.Net100.References.All,
#endif
            MetadataReference.CreateFromFile(typeof(Composer).Assembly.Location),
        ];
        var compilation = CSharpCompilation.Create("ErrorArgumentTestsAssembly", [tree], references, compilationOptions);

        var generator = new ComponoIncrementalGenerator().AsSourceGenerator();
        var driver = ((GeneratorDriver)CSharpGeneratorDriver.Create([generator]))
            .RunGenerators(compilation, TestContext.Current.CancellationToken);

        // The property under test: this call must not throw. GetRunResult() would surface an
        // unhandled generator exception as an aggregate failure if TypedConstantLiteralRenderer's
        // default arm were still reachable for Kind = Error.
        var act = () => driver.GetRunResult();

        act.Should().NotThrow("a malformed attribute argument must not crash the generator - it should be ignored/diagnosed, never an unhandled exception");
    }

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

    // PR #140 Codex review round 3 findings.

    [Fact]
    public Task TwoTypeParameterAttribute_TConfigHasAmbiguousConstructorsIncludingByRef_ReportsCmp0041WithRawCount() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = XunitAotStandIns + """

                    namespace TestNamespace
                    {
                        public sealed class AmbiguousConfig
                        {
                            // One ordinary constructor and one with a ref parameter - Compono.XunitV3's
                            // JIT-mode ConfigProfileBinder counts both via Type.GetConstructors() (a
                            // ref/out/in-parameter constructor is real, reflectable metadata there too)
                            // and rejects this TConfig as ambiguous ("has 2"), before it ever inspects
                            // either constructor's parameter shapes. Filtering the ref-parameter one out
                            // *before* counting would let this succeed here while JIT-mode rejects the
                            // identical TConfig outright - a real parity divergence.
                            public AmbiguousConfig(int value) { }
                            public AmbiguousConfig(ref int value) { }
                        }

                        public sealed class AmbiguousConfigProfile : Compono.ICompositionProfile
                        {
                            public AmbiguousConfigProfile(AmbiguousConfig config) { }
                            public void Configure(Compono.CompositionBuilder builder) { }
                        }

                        public sealed class Cmp0041AmbiguousTests
                        {
                            [Compono.XunitV3.Aot.Compose<AmbiguousConfigProfile, AmbiguousConfig>(1)]
                            public void Test_config_has_ordinary_and_byref_constructors(string value)
                            {
                            }
                        }
                    }
                    """,
            },
            "CMP0041",
            TestContext.Current.CancellationToken);

    [Fact]
    public Task TwoTypeParameterAttribute_TConfigIsStructWithOnlyImplicitConstructor_ReportsCmp0041() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = XunitAotStandIns + """

                    namespace TestNamespace
                    {
                        // No explicit constructor - Roslyn's own INamedTypeSymbol.Constructors still
                        // reports one (IsImplicitlyDeclared = true, confirmed by direct probe), but
                        // Compono.XunitV3's JIT-mode ConfigProfileBinder uses
                        // Type.GetConstructors(Public | Instance), which returns *zero* constructors
                        // for exactly this shape (also confirmed by direct probe) - counting the
                        // synthesized one here would let this TConfig succeed (emitting `new
                        // ImplicitCtorConfig()`) while JIT-mode rejects it with "has 0".
                        public struct ImplicitCtorConfig
                        {
                            public int Value;
                        }

                        public sealed class ImplicitCtorProfile : Compono.ICompositionProfile
                        {
                            public ImplicitCtorProfile(ImplicitCtorConfig config) { }
                            public void Configure(Compono.CompositionBuilder builder) { }
                        }

                        public sealed class Cmp0041ImplicitCtorTests
                        {
                            [Compono.XunitV3.Aot.Compose<ImplicitCtorProfile, ImplicitCtorConfig>]
                            public void Test_config_is_struct_with_only_implicit_constructor(string value)
                            {
                            }
                        }
                    }
                    """,
            },
            "CMP0041",
            TestContext.Current.CancellationToken);

    // PR #140 Codex review round 4 findings.

    [Fact]
    public Task TwoTypeParameterAttribute_TConfigClassImplicitCtor_Succeeds() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = XunitAotStandIns + """

                namespace TestNamespace
                {
                    // No explicit constructor - unlike a struct's synthesized parameterless
                    // constructor (not reflectable), a *class*'s own implicit default constructor is
                    // real, reflectable IL (Type.GetConstructors(Public|Instance) returns 1, confirmed
                    // by direct probe) - ConfigProfileBinder succeeds constructing this TConfig, and
                    // the round-3 fix must not regress that by excluding every IsImplicitlyDeclared
                    // constructor regardless of value-type-ness. Short names below - the generated
                    // hint-name path already gets long once the test-class/method/fact names combine.
                    public sealed class ImplicitCtorClassConfig
                    {
                    }

                    public sealed class ImplicitCtorClassProfile : Compono.ICompositionProfile
                    {
                        public ImplicitCtorClassProfile(ImplicitCtorClassConfig config) { }
                        public void Configure(Compono.CompositionBuilder builder) { }
                    }

                    public sealed class ImplicitCtorClassTests
                    {
                        [Compono.XunitV3.Aot.Compose<ImplicitCtorClassProfile, ImplicitCtorClassConfig>]
                        public void Test_uses_class(string value)
                        {
                        }
                    }
                }
                """,
        }, TestContext.Current.CancellationToken);

    [Fact]
    public void TwoTypeParameterAttribute_NestedErroneousArrayElement_DoesNotCrashGenerator()
    {
        // A malformed *array* argument (new int[] { UndefinedIdentifier }) reports a well-typed outer
        // Array TypedConstant whose *element* is Kind = Error - the round-2 fix only checked the outer
        // constant, so this shape still reached TypedConstantLiteralRenderer.Render's unhandled arm via
        // RenderArray's own recursive Render call. Same driver-based approach as
        // TwoTypeParameterAttribute_ErroneousArgumentExpression_DoesNotCrashGenerator - the property
        // under test is that the generator doesn't crash, not that this deliberately-invalid source
        // compiles.
        const string source = """
            namespace Xunit
            {
                public interface ITheoryDataRow { }
                public sealed class TheoryDataRow : ITheoryDataRow { public TheoryDataRow(object?[] data) { } }
            }

            namespace Xunit.Sdk
            {
                public sealed class DisposalTracker { }
            }

            namespace Xunit.v3
            {
                public abstract class DataAttribute : System.Attribute { }

                public static class RegisteredEngineConfig
                {
                    public static void RegisterTheoryDataRowFactory(
                        string testClassIndex, string methodName, bool disableDiscoveryEnumeration,
                        System.Func<Xunit.Sdk.DisposalTracker, System.Threading.Tasks.ValueTask<System.Collections.Generic.IReadOnlyCollection<Xunit.ITheoryDataRow>>> factory) { }
                }
            }

            namespace Compono.XunitV3.Aot
            {
                public sealed class ComposeAttribute : Xunit.v3.DataAttribute { }

                public sealed class ComposeAttribute<TProfile, TConfig> : Xunit.v3.DataAttribute
                    where TProfile : ICompositionProfile
                {
                    public ComposeAttribute(params object?[] configArguments) { }
                }
            }

            namespace TestNamespace;

            public sealed class ArrayErrorConfig
            {
                public ArrayErrorConfig(int[] values) { }
            }

            public sealed class ArrayErrorProfile : Compono.ICompositionProfile
            {
                public ArrayErrorProfile(ArrayErrorConfig config) { }
                public void Configure(Compono.CompositionBuilder builder) { }
            }

            public sealed class ArrayErrorArgumentTests
            {
                [Compono.XunitV3.Aot.Compose<ArrayErrorProfile, ArrayErrorConfig>(new int[] { UndefinedIdentifier })]
                public void Test_supplies_array_containing_undefined_identifier(string value)
                {
                }
            }
            """;

        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp14);
        var tree = CSharpSyntaxTree.ParseText(source, parseOptions, "Program.cs", cancellationToken: TestContext.Current.CancellationToken);

        var compilationOptions = new CSharpCompilationOptions(
            OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable);
        List<MetadataReference> references =
        [
#if NET11_0_OR_GREATER
            .. Basic.Reference.Assemblies.Net110.References.All,
#elif NET10_0_OR_GREATER
            .. Basic.Reference.Assemblies.Net100.References.All,
#endif
            MetadataReference.CreateFromFile(typeof(Composer).Assembly.Location),
        ];
        var compilation = CSharpCompilation.Create("ArrayErrorArgumentTestsAssembly", [tree], references, compilationOptions);

        var generator = new ComponoIncrementalGenerator().AsSourceGenerator();
        var driver = ((GeneratorDriver)CSharpGeneratorDriver.Create([generator]))
            .RunGenerators(compilation, TestContext.Current.CancellationToken);

        var act = () => driver.GetRunResult();

        act.Should().NotThrow("a malformed array element must not crash the generator - it should be ignored/diagnosed, never an unhandled exception");
    }

    [Fact]
    public Task TwoTypeParameterAttribute_InaccessibleTypeInsideArrayArgument_ReportsCmp0044() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = XunitAotStandIns + """

                    namespace TestNamespace
                    {
                        public sealed class Cmp0044ArrayTests
                        {
                            // Private to Cmp0044ArrayTests - legal at the [Compose<...>] use site. A
                            // PrivateKind[] argument bound to an `object`-typed config constructor
                            // parameter is real, legal attribute syntax (confirmed by direct probe: the
                            // array-creation-expression binds to the params object?[] element as one
                            // *boxed array* argument, not CS0182-illegal, since the declared parameter
                            // type it binds to - object - is what the array-creation-expression rule
                            // actually checks against, not the outer params array's own type). The
                            // round-1 CMP0044 fix only inspected the top-level TypedConstant's own Kind
                            // (Array here, not Enum), never recursing into the array's elements - so
                            // this slipped through, and the renderer would have emitted `new
                            // global::TestNamespace.Cmp0044ArrayTests.PrivateKind[] { ... }` in the
                            // generated top-level file, failing CS0122.
                            private enum PrivateKind
                            {
                                Default,
                                Special,
                            }

                            public sealed class ArrayConfig
                            {
                                public ArrayConfig(object value) { }
                            }

                            public sealed class ArrayConfigProfile : Compono.ICompositionProfile
                            {
                                public ArrayConfigProfile(ArrayConfig config) { }
                                public void Configure(Compono.CompositionBuilder builder) { }
                            }

                            [Compono.XunitV3.Aot.Compose<ArrayConfigProfile, ArrayConfig>(new PrivateKind[] { PrivateKind.Special })]
                            public void Test_config_argument_is_array_of_private_enum(string value)
                            {
                            }
                        }
                    }
                    """,
            },
            "CMP0044",
            TestContext.Current.CancellationToken);

    // PR #140 Codex review round 5 findings.

    [Fact]
    public Task TwoTypeParameterAttribute_ArgumentCastToSelectedConstructorParameterType() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = XunitAotStandIns + """

                namespace TestNamespace
                {
                    public sealed class OverloadConfig
                    {
                        // The public constructor is the one CMP0041 selects and validates - but
                        // without an explicit cast, `new OverloadConfig("value")` in the *generated*
                        // file (which lives in the same, consuming assembly, so the internal
                        // constructor is accessible there too) would resolve to the more-specific
                        // internal overload via ordinary C# overload resolution instead, silently
                        // diverging from what was selected. Compono.XunitV3.Binding.ConfigProfileBinder
                        // never has this problem - ConstructorInfo.Invoke invokes the exact constructor
                        // it resolved, with no overload resolution involved at all.
                        public OverloadConfig(object value) { }
                        internal OverloadConfig(string value) { }
                    }

                    public sealed class OverloadProfile : Compono.ICompositionProfile
                    {
                        public OverloadProfile(OverloadConfig config) { }
                        public void Configure(Compono.CompositionBuilder builder) { }
                    }

                    public sealed class OverloadTests
                    {
                        [Compono.XunitV3.Aot.Compose<OverloadProfile, OverloadConfig>("value")]
                        public void Test_config_has_more_specific_internal_overload(string value)
                        {
                        }
                    }
                }
                """,
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task TwoTypeParameterAttribute_NullArgumentForNullableParameter_DoesNotCrash() =>
        GeneratorTestHelpers.Verify(new CodeGenerationOptions
        {
            SourceCode = XunitAotStandIns + """

                namespace TestNamespace
                {
                    public sealed class NullableArgConfig
                    {
                        public NullableArgConfig(string? label) { }
                    }

                    public sealed class NullableArgProfile : Compono.ICompositionProfile
                    {
                        public NullableArgProfile(NullableArgConfig config) { }
                        public void Configure(Compono.CompositionBuilder builder) { }
                    }

                    public sealed class NullableArgTests
                    {
                        // A bare `null` binds to the params object?[] parameter in non-expanded form
                        // (the whole array is null, per NormalizeConstructorArguments' own handling) -
                        // the resulting TypedConstant reports Kind = Array with IsNull = true, and its
                        // own .Values throws NullReferenceException if accessed without an IsNull guard
                        // first (confirmed by a direct probe) - EmbeddedTypes' recursive array-element
                        // walk (added for CMP0044's own array-recursion fix) hit exactly this.
                        [Compono.XunitV3.Aot.Compose<NullableArgProfile, NullableArgConfig>(null)]
                        public void Test_supplies_null_for_nullable_parameter(string value)
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

    // PR #140 Codex review round 5/6 findings (missed by an incomplete `gh api` query at the time of
    // round 5's own fix commit, caught during round 6's re-check).

    [Fact]
    public Task TwoTypeParameterAttribute_EmptyArrayOfInaccessibleElementType_ReportsCmp0044() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = XunitAotStandIns + """

                    namespace TestNamespace
                    {
                        public sealed class Cmp0044EmptyArrayTests
                        {
                            // PR #140 Codex review round 5 (comment missed until round 6's re-check):
                            // EmbeddedTypes' Array-kind case only recursed into the array's *elements*
                            // (constant.Values), never checked the array's own *declared element type* -
                            // an EMPTY array has zero elements to recurse into, so this slipped through
                            // entirely even though the renderer still emits
                            // `new global::TestNamespace.Cmp0044EmptyArrayTests.PrivateKind[] { }` in the
                            // generated top-level file, failing CS0122.
                            private enum PrivateKind
                            {
                                Default,
                            }

                            public sealed class EmptyArrayConfig
                            {
                                public EmptyArrayConfig(object value) { }
                            }

                            public sealed class EmptyArrayConfigProfile : Compono.ICompositionProfile
                            {
                                public EmptyArrayConfigProfile(EmptyArrayConfig config) { }
                                public void Configure(Compono.CompositionBuilder builder) { }
                            }

                            [Compono.XunitV3.Aot.Compose<EmptyArrayConfigProfile, EmptyArrayConfig>(new PrivateKind[] { })]
                            public void Test_config_argument_is_empty_array_of_private_enum(string value)
                            {
                            }
                        }
                    }
                    """,
            },
            "CMP0044",
            TestContext.Current.CancellationToken);

    [Fact]
    public Task TwoTypeParameterAttribute_TConfigConstructorHasDynamicParameter_ReportsCmp0041() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = XunitAotStandIns + """

                    namespace TestNamespace
                    {
                        // PR #140 Codex review round 6: a `dynamic`-typed constructor parameter passes
                        // TypedConstantMatcher.Validate (ClassifyConversion treats string->dynamic as an
                        // implicit reference conversion, confirmed by direct probe) and would have
                        // generated a `(dynamic)"value"` cast in the top-level registration, invoking
                        // the C# runtime dynamic binder - not Native-AOT/trim-safe, violating ADR-0067's
                        // zero-reflection guarantee. Excluded from the usable-constructor set the same
                        // way a ref/out/in parameter already was, reported through the same CMP0041
                        // "0 usable constructors" diagnostic rather than a new one.
                        public sealed class DynamicParameterConfig
                        {
                            public DynamicParameterConfig(dynamic value) { }
                        }

                        public sealed class DynamicParameterConfigProfile : Compono.ICompositionProfile
                        {
                            public DynamicParameterConfigProfile(DynamicParameterConfig config) { }
                            public void Configure(Compono.CompositionBuilder builder) { }
                        }

                        public sealed class Cmp0041DynamicParameterTests
                        {
                            [Compono.XunitV3.Aot.Compose<DynamicParameterConfigProfile, DynamicParameterConfig>("value")]
                            public void Test_config_constructor_has_dynamic_parameter(string value)
                            {
                            }
                        }
                    }
                    """,
            },
            "CMP0041",
            TestContext.Current.CancellationToken);

    [Fact]
    public Task TwoTypeParameterAttribute_TConfigConstructorIsObsoleteAsError_ReportsCmp0047() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = XunitAotStandIns + """

                    namespace TestNamespace
                    {
                        // PR #140 Codex review round 6: a constructor marked [Obsolete("...", error:
                        // true)] passes every shape/accessibility/required-members check (it's otherwise
                        // a perfectly ordinary, JIT-reflectable constructor) but the generated
                        // registration calls it directly (`new ObsoleteConfig(...)`), which the compiler
                        // rejects with CS0619 for this attribute shape specifically (confirmed by direct
                        // compile probe).
                        public sealed class ObsoleteConfig
                        {
                            [System.Obsolete("do not use", error: true)]
                            public ObsoleteConfig(string value) { }
                        }

                        public sealed class ObsoleteConfigProfile : Compono.ICompositionProfile
                        {
                            public ObsoleteConfigProfile(ObsoleteConfig config) { }
                            public void Configure(Compono.CompositionBuilder builder) { }
                        }

                        public sealed class Cmp0047ObsoleteConstructorTests
                        {
                            [Compono.XunitV3.Aot.Compose<ObsoleteConfigProfile, ObsoleteConfig>("value")]
                            public void Test_config_constructor_is_obsolete_as_error(string value)
                            {
                            }
                        }
                    }
                    """,
            },
            "CMP0047",
            TestContext.Current.CancellationToken);

    // PR #140 Codex review round 7 findings.

    [Fact]
    public Task TwoTypeParameterAttribute_TProfileConstructorIsObsoleteAsError_ReportsCmp0047() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = XunitAotStandIns + """

                    namespace TestNamespace
                    {
                        // PR #140 Codex review round 7: the round-6 CMP0047 test only marked the TConfig
                        // constructor obsolete, never exercising the separate
                        // ProhibitedCallSiteAttribute(profileConstructor) check - this test covers that
                        // check independently, with only the TProfile constructor marked obsolete.
                        public sealed class ObsoleteProfileOnlyConfig
                        {
                            public ObsoleteProfileOnlyConfig(string value) { }
                        }

                        public sealed class ObsoleteProfileOnlyProfile : Compono.ICompositionProfile
                        {
                            [System.Obsolete("do not use", error: true)]
                            public ObsoleteProfileOnlyProfile(ObsoleteProfileOnlyConfig config) { }
                            public void Configure(Compono.CompositionBuilder builder) { }
                        }

                        public sealed class Cmp0047ObsoleteProfileConstructorTests
                        {
                            [Compono.XunitV3.Aot.Compose<ObsoleteProfileOnlyProfile, ObsoleteProfileOnlyConfig>("value")]
                            public void Test_profile_constructor_is_obsolete_as_error(string value)
                            {
                            }
                        }
                    }
                    """,
            },
            "CMP0047",
            TestContext.Current.CancellationToken);

    [Fact]
    public Task TwoTypeParameterAttribute_TConfigConstructorIsExperimental_ReportsCmp0047() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = XunitAotStandIns + """

                    namespace TestNamespace
                    {
                        // PR #140 Codex review round 7: [System.Diagnostics.CodeAnalysis.Experimental]
                        // is a second, independent standard attribute (distinct from
                        // [Obsolete(error: true)]) where any *use* of the marked constructor is always a
                        // compiler error at default severity - confirmed by direct compile probe. Caught
                        // by the same generalized ProhibitedCallSiteAttribute check, reported through the
                        // same CMP0047 diagnostic.
                        public sealed class ExperimentalConfig
                        {
                            [System.Diagnostics.CodeAnalysis.Experimental("EXP0047")]
                            public ExperimentalConfig(string value) { }
                        }

                        public sealed class ExperimentalConfigProfile : Compono.ICompositionProfile
                        {
                            public ExperimentalConfigProfile(ExperimentalConfig config) { }
                            public void Configure(Compono.CompositionBuilder builder) { }
                        }

                        public sealed class Cmp0047ExperimentalConstructorTests
                        {
                            [Compono.XunitV3.Aot.Compose<ExperimentalConfigProfile, ExperimentalConfig>("value")]
                            public void Test_config_constructor_is_experimental(string value)
                            {
                            }
                        }
                    }
                    """,
            },
            "CMP0047",
            TestContext.Current.CancellationToken);

    // PR #140 Codex review round 8 findings.

    [Fact]
    public Task TwoTypeParameterAttribute_TConfigConstructorRequiresDynamicCode_ReportsCmp0041() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = XunitAotStandIns + """

                    namespace TestNamespace
                    {
                        // PR #140 Codex review round 8: [RequiresDynamicCode]/[RequiresUnreferencedCode]
                        // compile and run fine under ordinary JIT execution (ConstructorInfo.Invoke
                        // doesn't care), but a PublishAot=true consumer of the generated direct
                        // `new T(...)` call gets a real IL3050/IL2026 warning (confirmed by direct
                        // probe) - directly contradicting this package's zero-reflection/AOT-safety
                        // guarantee. Excluded from the usable-constructor set the same way `dynamic` was
                        // in round 6, folded into the same CMP0041 diagnostic.
                        public sealed class RequiresDynamicCodeConfig
                        {
                            [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("uses reflection emit")]
                            public RequiresDynamicCodeConfig(string value) { }
                        }

                        public sealed class RequiresDynamicCodeConfigProfile : Compono.ICompositionProfile
                        {
                            public RequiresDynamicCodeConfigProfile(RequiresDynamicCodeConfig config) { }
                            public void Configure(Compono.CompositionBuilder builder) { }
                        }

                        public sealed class Cmp0041RequiresDynamicCodeTests
                        {
                            [Compono.XunitV3.Aot.Compose<RequiresDynamicCodeConfigProfile, RequiresDynamicCodeConfig>("value")]
                            public void Test_config_constructor_requires_dynamic_code(string value)
                            {
                            }
                        }
                    }
                    """,
            },
            "CMP0041",
            TestContext.Current.CancellationToken);

    [Fact]
    public Task TwoTypeParameterAttribute_TProfileConstructorRequiresUnreferencedCode_ReportsCmp0042() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = XunitAotStandIns + """

                    namespace TestNamespace
                    {
                        // PR #140 Codex review round 8: the same RequiresDynamicCode/
                        // RequiresUnreferencedCode exclusion applied to the TProfile constructor branch
                        // independently (round 7's own lesson: test both branches, not just TConfig's).
                        public sealed class RequiresUnreferencedCodeConfig
                        {
                            public RequiresUnreferencedCodeConfig(string value) { }
                        }

                        public sealed class RequiresUnreferencedCodeConfigProfile : Compono.ICompositionProfile
                        {
                            [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("uses reflection")]
                            public RequiresUnreferencedCodeConfigProfile(RequiresUnreferencedCodeConfig config) { }
                            public void Configure(Compono.CompositionBuilder builder) { }
                        }

                        public sealed class Cmp0042RequiresUnreferencedCodeTests
                        {
                            [Compono.XunitV3.Aot.Compose<RequiresUnreferencedCodeConfigProfile, RequiresUnreferencedCodeConfig>("value")]
                            public void Test_profile_constructor_requires_unreferenced_code(string value)
                            {
                            }
                        }
                    }
                    """,
            },
            "CMP0042",
            TestContext.Current.CancellationToken);

    [Fact]
    public Task TwoTypeParameterAttribute_TConfigConstructorSupersededByOverloadPriority_ReportsCmp0048() =>
        GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = XunitAotStandIns + """

                    namespace TestNamespace
                    {
                        // PR #140 Codex review round 8: round 5's fix for the overload-hijack finding
                        // (casting the rendered argument to the selected constructor's own declared
                        // parameter type) does not defend against [OverloadResolutionPriority] - a
                        // higher-priority accessible sibling constructor still wins even with an explicit
                        // cast, confirmed by direct probe, because priority pruning happens before
                        // applicability/conversion-quality comparison. No codegen shape can defeat this,
                        // so the only safe response is to reject it with a diagnostic.
                        public sealed class PriorityConfig
                        {
                            public PriorityConfig(object value) { }

                            [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
                            internal PriorityConfig(string value) { }
                        }

                        public sealed class PriorityConfigProfile : Compono.ICompositionProfile
                        {
                            public PriorityConfigProfile(PriorityConfig config) { }
                            public void Configure(Compono.CompositionBuilder builder) { }
                        }

                        public sealed class Cmp0048PriorityTests
                        {
                            [Compono.XunitV3.Aot.Compose<PriorityConfigProfile, PriorityConfig>("value")]
                            public void Test_config_constructor_superseded_by_priority(string value)
                            {
                            }
                        }
                    }
                    """,
            },
            "CMP0048",
            TestContext.Current.CancellationToken);

    [Fact]
    public Task TwoTypeParameterAttribute_TConfigConstructorIsCompilerFeatureRequired_ReportsCmp0047()
    {
        // PR #140 Codex review round 8: [CompilerFeatureRequired("...")] is blocked from direct source
        // use by CS8335 (a compiler-reserved attribute), so the only way to reproduce the real-world
        // shape - a constructor imported from *referenced metadata* that carries it (e.g. compiled by a
        // future/different compiler) - is to build that metadata directly via IL emission, the same way
        // the standalone probe that confirmed this finding did.
        var libraryReference = CompilerFeatureRequiredLibraryReference();

        return GeneratorTestHelpers.VerifyFailure(
            new CodeGenerationOptions
            {
                SourceCode = XunitAotStandIns + """

                    namespace TestNamespace
                    {
                        public sealed class Cmp0047CompilerFeatureRequiredTests
                        {
                            [Compono.XunitV3.Aot.Compose<CfrLib.CfrConfigProfile, CfrLib.CfrConfig>("value")]
                            public void Test_config_constructor_is_compiler_feature_required(string value)
                            {
                            }
                        }
                    }
                    """,
                ExtraReferences = [libraryReference],
            },
            "CMP0047",
            TestContext.Current.CancellationToken);
    }

    private static MetadataReference CompilerFeatureRequiredLibraryReference()
    {
        var asmName = new System.Reflection.AssemblyName("CfrLib_" + System.Guid.NewGuid().ToString("N"));
        var ab = new System.Reflection.Emit.PersistedAssemblyBuilder(asmName, typeof(object).Assembly);
        var mb = ab.DefineDynamicModule("CfrLib.dll");

        var configType = mb.DefineType("CfrLib.CfrConfig", System.Reflection.TypeAttributes.Public | System.Reflection.TypeAttributes.Class);
        var configCtor = configType.DefineConstructor(
            System.Reflection.MethodAttributes.Public,
            System.Reflection.CallingConventions.Standard,
            [typeof(string)]);
        configCtor.GetILGenerator().Emit(System.Reflection.Emit.OpCodes.Ret);

        var cfrCtor = typeof(System.Runtime.CompilerServices.CompilerFeatureRequiredAttribute).GetConstructor([typeof(string)])!;
        configCtor.SetCustomAttribute(new System.Reflection.Emit.CustomAttributeBuilder(cfrCtor, ["FutureFeature"]));

        configType.CreateType();

        var profileType = mb.DefineType(
            "CfrLib.CfrConfigProfile",
            System.Reflection.TypeAttributes.Public | System.Reflection.TypeAttributes.Class,
            typeof(object),
            [typeof(Compono.ICompositionProfile)]);
        var profileCtor = profileType.DefineConstructor(
            System.Reflection.MethodAttributes.Public,
            System.Reflection.CallingConventions.Standard,
            [configType]);
        var profileCtorIl = profileCtor.GetILGenerator();
        profileCtorIl.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
        profileCtorIl.Emit(System.Reflection.Emit.OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes)!);
        profileCtorIl.Emit(System.Reflection.Emit.OpCodes.Ret);

        var configureMethod = profileType.DefineMethod(
            "Configure",
            System.Reflection.MethodAttributes.Public | System.Reflection.MethodAttributes.Virtual,
            typeof(void),
            [typeof(Compono.CompositionBuilder)]);
        configureMethod.GetILGenerator().Emit(System.Reflection.Emit.OpCodes.Ret);

        profileType.CreateType();

        using var stream = new System.IO.MemoryStream();
        ab.Save(stream);
        stream.Position = 0;
        return MetadataReference.CreateFromStream(stream);
    }
}
