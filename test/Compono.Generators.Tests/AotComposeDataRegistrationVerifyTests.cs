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

    [Fact]
    public Task OneTypeParameterProfile_AppliesProfile() =>
        GeneratorTestHelpers.VerifyGeneratedSource(
            new CodeGenerationOptions
            {
                SourceCode = Source("""

                    public sealed class SnapshotProfile : Compono.ICompositionProfile
                    {
                        public void Configure(Compono.CompositionBuilder builder) { }
                    }

                    public sealed class ComposeDataProfileTests
                    {
                        public static IEnumerable<object> Cases = new object[] { new object[] { "x" } };

                        [Compono.XunitV3.Aot.ComposeData<SnapshotProfile>("Cases")]
                        public void Profile_rows_are_composed(string value)
                        {
                        }
                    }
                    """),
            },
            TestContext.Current.CancellationToken);

    [Fact]
    public Task TwoTypeParameterProfileConfig_ConstructsConfigAndAppliesSeed() =>
        GeneratorTestHelpers.VerifyGeneratedSource(
            new CodeGenerationOptions
            {
                SourceCode = Source("""

                    public sealed class SnapshotConfig { }

                    public sealed class ConfiguredSnapshotProfile : Compono.ICompositionProfile
                    {
                        public ConfiguredSnapshotProfile(SnapshotConfig config) { }

                        public void Configure(Compono.CompositionBuilder builder) { }
                    }

                    public sealed class ComposeDataProfileConfigTests
                    {
                        public static IEnumerable<object> Cases = new object[] { new object[] { "x" } };

                        [Compono.XunitV3.Aot.ComposeData<ConfiguredSnapshotProfile, SnapshotConfig>("Cases", Seed = 7)]
                        public void Profile_config_rows_are_composed(string value)
                        {
                        }
                    }
                    """),
            },
            TestContext.Current.CancellationToken);

    [Fact]
    public Task AttributeSeed_IsAppliedAfterProfile() =>
        GeneratorTestHelpers.VerifyGeneratedSource(
            new CodeGenerationOptions
            {
                SourceCode = Source("""

                    public sealed class SeededSnapshotProfile : Compono.ICompositionProfile
                    {
                        public void Configure(Compono.CompositionBuilder builder) { }
                    }

                    public sealed class ComposeDataSeedTests
                    {
                        public static IEnumerable<object> Cases = new object[] { new object[] { "x" } };

                        [Compono.XunitV3.Aot.ComposeData<SeededSnapshotProfile>("Cases", Seed = 42)]
                        public void Seeded_rows_are_composed(string value)
                        {
                        }
                    }
                    """),
            },
            TestContext.Current.CancellationToken);

    [Fact]
    public Task NegativeAttributeSeed_ThrowsBeforeReading() =>
        GeneratorTestHelpers.VerifyGeneratedSource(
            new CodeGenerationOptions
            {
                SourceCode = Source("""

                    public sealed class NegativeSeedSnapshotTests
                    {
                        public static IEnumerable<object> Cases = new object[] { new object[] { "x" } };

                        [Compono.XunitV3.Aot.ComposeData("Cases", Seed = -1)]
                        public void Negative_seed_rows_are_not_composed(string value)
                        {
                        }
                    }
                    """),
            },
            TestContext.Current.CancellationToken);

    [Fact]
    public Task SharedParameter_ResolvesSharedAndBindsExplicitValue() =>
        GeneratorTestHelpers.VerifyGeneratedSource(
            new CodeGenerationOptions
            {
                SourceCode = Source("""

                    public sealed class SharedSnapshotTests
                    {
                        public static IEnumerable<object> Cases = new object[] { new object[] { "explicit" } };

                        [Compono.XunitV3.Aot.ComposeData("Cases")]
                        public void Shared_rows_are_composed(
                            [Compono.XunitV3.Aot.Shared] string shared,
                            string ordinary)
                        {
                        }
                    }
                    """),
            },
            TestContext.Current.CancellationToken);

    // PLAN-0071 B5.11 / S3 review: shared parameters are resolved before any ordinary parameter,
    // regardless of ordinal position. A snapshot is the direct proof of the emitted statement order:
    // the [Shared] ordinal 1 ResolveShared/ShareExplicit block must precede the ordinary ordinal 0
    // Resolve block. If the generator emitted ordinary ordinal order instead, the ordinary
    // parameter's Resolve would run first and compose a second instance instead of reusing the
    // shared one - the executable proof of that behavior lives in ComposeDataEmissionTests.
    [Fact]
    public Task SharedParameter_PrecededByOrdinaryParameter_ResolvesSharedFirst() =>
        GeneratorTestHelpers.VerifyGeneratedSource(
            new CodeGenerationOptions
            {
                SourceCode = Source("""

                    public sealed class SharedOrderingSnapshotTests
                    {
                        public static IEnumerable<object> Cases = new object[] { new object[] { "x" } };

                        [Compono.XunitV3.Aot.ComposeData("Cases")]
                        public void Shared_rows_are_composed(
                            string first,
                            [Compono.XunitV3.Aot.Shared] string shared)
                        {
                        }
                    }
                    """),
            },
            TestContext.Current.CancellationToken);

    // PLAN-0071 B5.11 / S3 review: Seed = 0 is a real configured seed, never treated as absent.
    // WithSeed(0) must be emitted (and, per ComposeDataRows.CreateDataRow, reported as "0") - the
    // runtime proof lives in ComposeDataEmissionTests' Zero_seed_is_applied target.
    [Fact]
    public Task AttributeSeedZero_IsApplied() =>
        GeneratorTestHelpers.VerifyGeneratedSource(
            new CodeGenerationOptions
            {
                SourceCode = Source("""

                    public sealed class ZeroSeedSnapshotTests
                    {
                        public static IEnumerable<object> Cases = new object[] { new object[] { "x" } };

                        [Compono.XunitV3.Aot.ComposeData("Cases", Seed = 0)]
                        public void Zero_seed_rows_are_composed(string value)
                        {
                        }
                    }
                    """),
            },
            TestContext.Current.CancellationToken);
}
