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

    // PLAN-0071 B5.11 / S4: the asynchronous member shapes emit their own readers on the same
    // template. Task<T> reads the member, null-checks the pending task, awaits with ConfigureAwait,
    // then shares the sync append path; ValueTask<T> is identical minus the null check (ValueTask<>
    // can never be null).
    [Fact]
    public Task TaskOf_GeneratesAwaitingReader() =>
        GeneratorTestHelpers.VerifyGeneratedSource(
            new CodeGenerationOptions
            {
                SourceCode = Source("""

                    public sealed class ComposeDataTaskTests
                    {
                        public static System.Threading.Tasks.Task<IEnumerable<object>> Cases { get; } = null!;

                        [Compono.XunitV3.Aot.ComposeData("Cases")]
                        public void Task_rows_are_composed(string value)
                        {
                        }
                    }
                    """),
            },
            TestContext.Current.CancellationToken);

    [Fact]
    public Task ValueTaskOf_GeneratesAwaitingReader() =>
        GeneratorTestHelpers.VerifyGeneratedSource(
            new CodeGenerationOptions
            {
                SourceCode = Source("""

                    public sealed class ComposeDataValueTaskTests
                    {
                        public static System.Threading.Tasks.ValueTask<Xunit.TheoryData<int, string>> Cases { get; }

                        [Compono.XunitV3.Aot.ComposeData("Cases")]
                        public void Value_task_rows_are_composed(int quantity, string label)
                        {
                        }
                    }
                    """),
            },
            TestContext.Current.CancellationToken);

    // PLAN-0071 B5.11 / S4: a reference-type async row element is read through
    // IAsyncEnumerable<object?> with WithCancellation + ConfigureAwait, checking cancellation before
    // every row add, and wrapping every non-CompositionException/non-OperationCanceledException into
    // MemberReadFailure.
    [Fact]
    public Task AsyncEnumerableReferenceElement_GeneratesReferenceReader() =>
        GeneratorTestHelpers.VerifyGeneratedSource(
            new CodeGenerationOptions
            {
                SourceCode = Source("""

                    public sealed class ComposeDataAsyncReferenceTests
                    {
                        public static System.Collections.Generic.IAsyncEnumerable<object[]> Cases() =>
                            throw new System.NotImplementedException();

                        [Compono.XunitV3.Aot.ComposeData("Cases")]
                        public void Async_reference_rows_are_composed(string value)
                        {
                        }
                    }
                    """),
            },
            TestContext.Current.CancellationToken);

    // PLAN-0071 B5.11 / S4: a value-type async row element cannot be read by covariance, so the
    // declared member type is enumerated manually through a file-local generic reader. The reader
    // still performs the JIT reference-path runtime check first, and reproduces the reflection path's
    // GetAsyncEnumerator/MoveNextAsync/DisposeAsync failure precedence exactly.
    [Fact]
    public Task AsyncEnumerableValueElement_GeneratesValueReader() =>
        GeneratorTestHelpers.VerifyGeneratedSource(
            new CodeGenerationOptions
            {
                SourceCode = Source("""

                    public sealed class ComposeDataAsyncValueTests
                    {
                        public static System.Collections.Generic.IAsyncEnumerable<(int Quantity, string Value)> Cases() =>
                            throw new System.NotImplementedException();

                        [Compono.XunitV3.Aot.ComposeData("Cases")]
                        public void Async_value_rows_are_composed(int quantity, string label)
                        {
                        }
                    }
                    """),
            },
            TestContext.Current.CancellationToken);

    // PLAN-0071 B5.11 / S4: a declared member type carrying one reference-type and one value-type
    // IAsyncEnumerable<T> (the supported mixed case) is classified with
    // AsyncRowElementIsValueType == false, so it takes the reference reader - never the value reader.
    [Fact]
    public Task AsyncEnumerableMixedReferenceAndValue_UsesReferenceReader() =>
        GeneratorTestHelpers.VerifyGeneratedSource(
            new CodeGenerationOptions
            {
                SourceCode = Source("""

                    public sealed class MixedAsyncRows
                        : System.Collections.Generic.IAsyncEnumerable<int>,
                          System.Collections.Generic.IAsyncEnumerable<object[]>
                    {
                        public System.Collections.Generic.IAsyncEnumerator<int> GetAsyncEnumerator(
                            System.Threading.CancellationToken cancellationToken = default) =>
                            throw new System.NotImplementedException();

                        System.Collections.Generic.IAsyncEnumerator<object[]> System.Collections.Generic.IAsyncEnumerable<object[]>.GetAsyncEnumerator(
                            System.Threading.CancellationToken cancellationToken) =>
                            throw new System.NotImplementedException();
                    }

                    public sealed class ComposeDataAsyncMixedTests
                    {
                        public static MixedAsyncRows Cases() => throw new System.NotImplementedException();

                        [Compono.XunitV3.Aot.ComposeData("Cases")]
                        public void Mixed_async_rows_are_composed(string value)
                        {
                        }
                    }
                    """),
            },
            TestContext.Current.CancellationToken);

    // PLAN-0071 B5.11 / S4: the async reader composes with every S3 feature - profile, attribute
    // seed, and [Shared] parameter resolution all still run through the unchanged ComposeRow scaffold.
    [Fact]
    public Task AsyncEnumerable_WithProfileSeedAndShared_ComposesRows() =>
        GeneratorTestHelpers.VerifyGeneratedSource(
            new CodeGenerationOptions
            {
                SourceCode = Source("""

                    public sealed class AsyncSnapshotProfile : Compono.ICompositionProfile
                    {
                        public void Configure(Compono.CompositionBuilder builder) { }
                    }

                    public sealed class ComposeDataAsyncProfileTests
                    {
                        public static System.Collections.Generic.IAsyncEnumerable<object[]> Cases() =>
                            throw new System.NotImplementedException();

                        [Compono.XunitV3.Aot.ComposeData<AsyncSnapshotProfile>("Cases", Seed = 42)]
                        public void Async_profile_rows_are_composed(
                            [Compono.XunitV3.Aot.Shared] string shared,
                            string ordinary)
                        {
                        }
                    }
                    """),
            },
            TestContext.Current.CancellationToken);

    // PLAN-0071 B5.11 / S4 review: Task<object> is a supported TaskOf source whose awaited value is
    // an object, so the appended sync path reads it without any typed row element being named.
    [Fact]
    public Task TaskOfObject_GeneratesAwaitingReader() =>
        GeneratorTestHelpers.VerifyGeneratedSource(
            new CodeGenerationOptions
            {
                SourceCode = Source("""

                    public sealed class ComposeDataTaskObjectTests
                    {
                        public static System.Threading.Tasks.Task<object> Cases { get; } = null!;

                        [Compono.XunitV3.Aot.ComposeData("Cases")]
                        public void Task_object_rows_are_composed(string value)
                        {
                        }
                    }
                    """),
            },
            TestContext.Current.CancellationToken);

    // PLAN-0071 B5.11 / S5: an active attribute-level SkipUnless whose property is declared on the test
    // class emits a static predicate bound to that class - the holder is the property's own declaring
    // type, never a string name (Native AOT reads no reflection).
    [Fact]
    public Task SkipUnless_TestClassHolder_RendersPredicate() =>
        GeneratorTestHelpers.VerifyGeneratedSource(
            new CodeGenerationOptions
            {
                SourceCode = Source("""

                    public sealed class SkipUnlessTestClassTests
                    {
                        public static bool ShouldSkip => false;

                        public static IEnumerable<object> Cases = new object[] { new object[] { "x" } };

                        [Compono.XunitV3.Aot.ComposeData("Cases", Skip = "reason", SkipUnless = "ShouldSkip")]
                        public void Skip_unless_rows_are_composed(string value)
                        {
                        }
                    }
                    """),
            },
            TestContext.Current.CancellationToken);

    // PLAN-0071 B5.11 / S5: an inherited condition property binds to the base class that declares it,
    // not the test class the attribute names.
    [Fact]
    public Task SkipUnless_BaseClassHolder_RendersPredicate() =>
        GeneratorTestHelpers.VerifyGeneratedSource(
            new CodeGenerationOptions
            {
                SourceCode = Source("""

                    public class SkipConditionBase
                    {
                        public static bool ShouldSkip => false;
                    }

                    public sealed class SkipUnlessBaseClassTests : SkipConditionBase
                    {
                        public static IEnumerable<object> Cases = new object[] { new object[] { "x" } };

                        [Compono.XunitV3.Aot.ComposeData("Cases", Skip = "reason", SkipUnless = "ShouldSkip")]
                        public void Skip_unless_rows_are_composed(string value)
                        {
                        }
                    }
                    """),
            },
            TestContext.Current.CancellationToken);

    // PLAN-0071 B5.11 / S5: SkipWhen with an explicit SkipType binds to the SkipType's class.
    [Fact]
    public Task SkipWhen_SkipTypeHolder_RendersPredicate() =>
        GeneratorTestHelpers.VerifyGeneratedSource(
            new CodeGenerationOptions
            {
                SourceCode = Source("""

                    public static class SkipConditionHolder
                    {
                        public static bool ShouldSkip => false;
                    }

                    public sealed class SkipWhenSkipTypeTests
                    {
                        public static IEnumerable<object> Cases = new object[] { new object[] { "x" } };

                        [Compono.XunitV3.Aot.ComposeData(
                            "Cases",
                            Skip = "reason",
                            SkipWhen = "ShouldSkip",
                            SkipType = typeof(SkipConditionHolder))]
                        public void Skip_when_rows_are_composed(string value)
                        {
                        }
                    }
                    """),
            },
            TestContext.Current.CancellationToken);

    // PLAN-0071 B5.11 / S5: a keyword-named skip property is escaped at the predicate call site, just
    // like a keyword-named data member.
    [Fact]
    public Task SkipPropertyKeywordName_IsEscaped() =>
        GeneratorTestHelpers.VerifyGeneratedSource(
            new CodeGenerationOptions
            {
                SourceCode = Source("""

                    public static class KeywordSkipConditionHolder
                    {
                        public static bool @default => false;
                    }

                    public sealed class SkipPropertyKeywordTests
                    {
                        public static IEnumerable<object> Cases = new object[] { new object[] { "x" } };

                        [Compono.XunitV3.Aot.ComposeData(
                            "Cases",
                            Skip = "reason",
                            SkipUnless = "default",
                            SkipType = typeof(KeywordSkipConditionHolder))]
                        public void Keyword_skip_property_rows_are_composed(string value)
                        {
                        }
                    }
                    """),
            },
            TestContext.Current.CancellationToken);

    // PLAN-0071 B5.11 / S5: a plain [Obsolete] data member (not [Obsolete(error: true)], which is a
    // CMP0047 rejection) is read normally; the emitted file's CS0612/CS0618 pragma keeps the read
    // clean even when the consuming compilation raises those warnings to errors.
    [Fact]
    public Task PlainObsoleteMember_IsReadWithSuppressedWarning() =>
        GeneratorTestHelpers.VerifyGeneratedSource(
            new CodeGenerationOptions
            {
                SourceCode = Source("""

                    public sealed class ObsoleteMemberTests
                    {
                        [System.Obsolete("use something else")]
                        public static IEnumerable<object> Cases = new object[] { new object[] { "x" } };

                        [Compono.XunitV3.Aot.ComposeData("Cases")]
                        public void Obsolete_rows_are_composed(string value)
                        {
                        }
                    }
                    """),
            },
            TestContext.Current.CancellationToken);

    // PLAN-0071 B5.11 / S5 / ADR-0070 Amendment 4 §7: a plain (non-virtual) static interface property is
    // supported; the predicate reads it directly through the interface name.
    [Fact]
    public Task SkipUnless_PlainStaticInterfaceProperty_RendersPredicate() =>
        GeneratorTestHelpers.VerifyGeneratedSource(
            new CodeGenerationOptions
            {
                SourceCode = Source("""

                    public interface IStaticSkipConditions
                    {
                        static bool ShouldSkip => false;
                    }

                    public sealed class PlainStaticInterfaceTests
                    {
                        public static IEnumerable<object> Cases = new object[] { new object[] { "x" } };

                        [Compono.XunitV3.Aot.ComposeData(
                            "Cases",
                            Skip = "reason",
                            SkipUnless = "ShouldSkip",
                            SkipType = typeof(IStaticSkipConditions))]
                        public void Plain_static_interface_rows_are_composed(string value)
                        {
                        }
                    }
                    """),
            },
            TestContext.Current.CancellationToken);

    // PLAN-0071 B5.3 / S6: a [ComposeData] method declared on a generic test-class base registers
    // against the declaring type's OPEN-GENERIC index ("global::TestNamespace.ComposeDataGenericBase<>")
    // and resolves the CLOSED constructed base once, at execution time, via
    // ComposeDataRows.ResolveGenericTestClassType - generated code can never name the open generic type.
    [Fact]
    public Task GenericTestClass_MemberType_RendersResolverRegistration() =>
        GeneratorTestHelpers.VerifyGeneratedSource(
            new CodeGenerationOptions
            {
                SourceCode = Source("""

                    public static class ComposeDataGenericHolder
                    {
                        public static IEnumerable<object> Cases = new object[] { new object[] { "x" } };
                    }

                    public sealed class ComposeDataGenericBase<T>
                    {
                        [Compono.XunitV3.Aot.ComposeData("Cases", MemberType = typeof(ComposeDataGenericHolder))]
                        public void Generic_rows_are_composed(string value)
                        {
                        }
                    }
                    """),
            },
            TestContext.Current.CancellationToken);

    // PLAN-0071 B5.3 / S6: the member is inherited from a NON-generic root base while the declaring
    // test class is generic. The registration still resolves the closed constructed base at execution
    // time, but the member read binds to the non-generic root that actually declares it.
    [Fact]
    public Task GenericTestClass_InheritedMemberFromNonGenericRoot_RendersResolverRegistration() =>
        GeneratorTestHelpers.VerifyGeneratedSource(
            new CodeGenerationOptions
            {
                SourceCode = Source("""

                    public class ComposeDataRoot
                    {
                        public static IEnumerable<object> Cases = new object[] { new object[] { "x" } };
                    }

                    public class ComposeDataRootGenericBase<T> : ComposeDataRoot { }

                    public sealed class ComposeDataRootTests<T> : ComposeDataRootGenericBase<T>
                    {
                        [Compono.XunitV3.Aot.ComposeData("Cases")]
                        public void Root_member_rows_are_composed(string value)
                        {
                        }
                    }
                    """),
            },
            TestContext.Current.CancellationToken);

    // PLAN-0071 B6.0 / ADR-0070 Amendment 5: a [Theory] declaring SkipUnless makes the generator
    // record theory skip-condition kind Unless, emitted as TheorySkipCondition = 1.
    [Fact]
    public Task TheoryLevelSkipUnless_RendersTheorySkipCondition() =>
        GeneratorTestHelpers.VerifyGeneratedSource(
            new CodeGenerationOptions
            {
                SourceCode = Source("""

                    public sealed class TheorySkipUnlessTests
                    {
                        public static IEnumerable<object> Cases = new object[] { new object[] { "x" } };

                        [Xunit.Theory(Skip = "theory conditional", SkipUnless = "TheorySkipUnlessTests.ShouldSkip")]
                        [Compono.XunitV3.Aot.ComposeData("Cases")]
                        public void Theory_unless_rows_are_composed(string value)
                        {
                        }

                        public static bool ShouldSkip => true;
                    }
                    """),
            },
            TestContext.Current.CancellationToken);

    // PLAN-0071 B6.0 / ADR-0070 Amendment 5: a [Theory] declaring SkipWhen makes the generator record
    // theory skip-condition kind When, emitted as TheorySkipCondition = 2.
    [Fact]
    public Task TheoryLevelSkipWhen_RendersTheorySkipCondition() =>
        GeneratorTestHelpers.VerifyGeneratedSource(
            new CodeGenerationOptions
            {
                SourceCode = Source("""

                    public sealed class TheorySkipWhenTests
                    {
                        public static IEnumerable<object> Cases = new object[] { new object[] { "x" } };

                        [Xunit.Theory(Skip = "theory conditional", SkipWhen = "TheorySkipWhenTests.ShouldSkip")]
                        [Compono.XunitV3.Aot.ComposeData("Cases")]
                        public void Theory_when_rows_are_composed(string value)
                        {
                        }

                        public static bool ShouldSkip => true;
                    }
                    """),
            },
            TestContext.Current.CancellationToken);
}
