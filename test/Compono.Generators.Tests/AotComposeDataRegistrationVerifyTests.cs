namespace Compono.Generators.Tests;

/// <summary>
/// Snapshot coverage for <c>Compono.Generators.Emitters.AotComposeDataRegistrationEmitter</c> - the
/// <c>Compono.XunitV3.Aot</c>-specific <c>RegisteredEngineConfig.RegisterTheoryDataRowFactory(...)</c>
/// registration emitted for a <c>[ComposeData]</c> theory (ADR-0070 / PLAN-0071 B5.6).
/// </summary>
/// <remarks>
/// These tests deliberately use <see cref="GeneratorTestHelpers.VerifyGeneratedSource"/> rather than
/// <see cref="GeneratorTestHelpers.Verify"/>: the generated code binds to the real
/// <c>Compono.XunitV3.Aot.ComposeDataRows</c>/<c>RowContext</c>/<c>ComposeDataAttributeMetadata</c>
/// surface, which the stand-ins in <see cref="ComposeDataAotStandIns"/> intentionally do not model
/// (PLAN-0071 B5.10 - the compile proof for the generated registration lives in the real
/// <c>test/Compono.XunitV3.Aot.Tests</c> project against the real assemblies, never against a
/// stand-in). This asserts the generator produces the expected text with zero diagnostics (including
/// no CMP0050 emission-isolation failure) without recompiling the generated trees here.
/// </remarks>
public sealed class AotComposeDataRegistrationVerifyTests
{
    private static string Source(string body) => ComposeDataAotStandIns.Source + """

        namespace TestNamespace
        {
            using System.Collections.Generic;

        """ + body + """
        }
        """;

    [Fact]
    public Task SyncField_UntypedRows_GeneratesRegistration() =>
        GeneratorTestHelpers.VerifyGeneratedSource(
            new CodeGenerationOptions
            {
                SourceCode = Source("""

                    public sealed class ComposeDataFieldTests
                    {
                        public static IEnumerable<object> Cases = new object[] { new object[] { "x" } };

                        [Compono.XunitV3.Aot.ComposeData("Cases")]
                        public void Field_rows_are_composed(string value)
                        {
                        }
                    }
                    """),
            },
            TestContext.Current.CancellationToken);

    [Fact]
    public Task SyncProperty_TypedRows_GeneratesRegistration() =>
        GeneratorTestHelpers.VerifyGeneratedSource(
            new CodeGenerationOptions
            {
                SourceCode = Source("""

                    public sealed class ComposeDataPropertyTests
                    {
                        public static Xunit.TheoryData<int, string> Cases { get; } = new Xunit.TheoryData<int, string>();

                        [Compono.XunitV3.Aot.ComposeData("Cases")]
                        public void Property_rows_are_composed(int quantity, string label)
                        {
                        }
                    }
                    """),
            },
            TestContext.Current.CancellationToken);

    [Fact]
    public Task SyncParameterlessMethod_UntypedRows_GeneratesRegistration() =>
        GeneratorTestHelpers.VerifyGeneratedSource(
            new CodeGenerationOptions
            {
                SourceCode = Source("""

                    public sealed class ComposeDataMethodTests
                    {
                        public static IEnumerable<object> Cases() => new object[] { new object[] { "x" } };

                        [Compono.XunitV3.Aot.ComposeData("Cases")]
                        public void Parameterless_method_rows_are_composed(string value)
                        {
                        }
                    }
                    """),
            },
            TestContext.Current.CancellationToken);

    [Fact]
    public Task InheritedMember_ReadsThroughDeclaringType() =>
        GeneratorTestHelpers.VerifyGeneratedSource(
            new CodeGenerationOptions
            {
                SourceCode = Source("""

                    public abstract class ComposeDataBase
                    {
                        public static IEnumerable<object> Cases = new object[] { new object[] { "x" } };
                    }

                    public sealed class ComposeDataInheritedTests : ComposeDataBase
                    {
                        [Compono.XunitV3.Aot.ComposeData("Cases")]
                        public void Inherited_rows_are_composed(string value)
                        {
                        }
                    }
                    """),
            },
            TestContext.Current.CancellationToken);

    [Fact]
    public Task MemberType_ReadsThroughLookupType() =>
        GeneratorTestHelpers.VerifyGeneratedSource(
            new CodeGenerationOptions
            {
                SourceCode = Source("""

                    public static class ComposeDataSource
                    {
                        public static IEnumerable<object> Cases = new object[] { new object[] { "x" } };
                    }

                    public sealed class ComposeDataMemberTypeTests
                    {
                        [Compono.XunitV3.Aot.ComposeData("Cases", MemberType = typeof(ComposeDataSource))]
                        public void MemberType_rows_are_composed(string value)
                        {
                        }
                    }
                    """),
            },
            TestContext.Current.CancellationToken);

    [Fact]
    public Task ConstantsOnlyMetadata_IsRendered() =>
        GeneratorTestHelpers.VerifyGeneratedSource(
            new CodeGenerationOptions
            {
                SourceCode = Source("""

                    public sealed class ComposeDataMetadataTests
                    {
                        public static IEnumerable<object> Cases = new object[] { new object[] { "x" } };

                        [Compono.XunitV3.Aot.ComposeData(
                            "Cases",
                            Label = "the label",
                            Skip = "the skip reason",
                            TestDisplayName = "the display name",
                            Explicit = true,
                            Timeout = 42,
                            DisableParallelization = true,
                            Traits = new[] { "category", "pricing" })]
                        public void Constant_metadata_is_rendered(string value)
                        {
                        }
                    }
                    """),
            },
            TestContext.Current.CancellationToken);

    [Fact]
    public Task MultipleParameters_BindsEveryOrdinal() =>
        GeneratorTestHelpers.VerifyGeneratedSource(
            new CodeGenerationOptions
            {
                SourceCode = Source("""

                    public sealed class ComposeDataOrderedTests
                    {
                        public static IEnumerable<object> Cases = new object[] { new object[] { "x", 1, true } };

                        [Compono.XunitV3.Aot.ComposeData("Cases")]
                        public void Ordered_parameters_are_bound_in_ordinal_order(string label, int quantity, bool enabled)
                        {
                        }
                    }
                    """),
            },
            TestContext.Current.CancellationToken);

    [Fact]
    public Task NullableParameter_FlagsNullableDescriptor() =>
        GeneratorTestHelpers.VerifyGeneratedSource(
            new CodeGenerationOptions
            {
                SourceCode = Source("""

                    public sealed class ComposeDataNullableTests
                    {
                        public static IEnumerable<object> Cases = new object[] { new object?[] { null, "x" } };

                        [Compono.XunitV3.Aot.ComposeData("Cases")]
                        public void Nullable_parameters_are_flagged(string? optional, string required)
                        {
                        }
                    }
                    """),
            },
            TestContext.Current.CancellationToken);

    [Fact]
    public Task KeywordMemberName_IsEscaped() =>
        GeneratorTestHelpers.VerifyGeneratedSource(
            new CodeGenerationOptions
            {
                SourceCode = Source("""

                    public sealed class ComposeDataKeywordTests
                    {
                        public static IEnumerable<object> @class = new object[] { new object[] { "x" } };

                        [Compono.XunitV3.Aot.ComposeData("class")]
                        public void Keyword_member_is_escaped(string value)
                        {
                        }
                    }
                    """),
            },
            TestContext.Current.CancellationToken);

    [Fact]
    public Task StructCollection_UntypedRows_GeneratesRegistration() =>
        GeneratorTestHelpers.VerifyGeneratedSource(
            new CodeGenerationOptions
            {
                SourceCode = Source("""

                    public sealed class ComposeDataStructCollectionTests
                    {
                        public static System.Collections.Immutable.ImmutableArray<object[]> Cases { get; } =
                            System.Collections.Immutable.ImmutableArray.Create(new object[] { "x" });

                        [Compono.XunitV3.Aot.ComposeData("Cases")]
                        public void Struct_collection_rows_are_composed(string value)
                        {
                        }
                    }
                    """),
            },
            TestContext.Current.CancellationToken);
}
