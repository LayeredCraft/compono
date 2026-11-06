#if COMPONO_XUNITV3_AOT
namespace Compono.XunitV3.Aot;
#else
namespace Compono.XunitV3;
#endif

using System.Collections;
using System.Globalization;
using System.Runtime.CompilerServices;
using Xunit;

#if COMPONO_XUNITV3_AOT
using System.ComponentModel;
#else
using System.Reflection;
#endif

/// <summary>
/// Shared runtime support for <c>[ComposeData]</c> theory rows, compiled into both
/// <c>Compono.XunitV3</c> and <c>Compono.XunitV3.Aot</c>. It normalizes row shapes, extracts and
/// validates <c>TConfig</c> values, validates leading positional row values, merges row/attribute
/// metadata into the output <see cref="TheoryDataRow"/>, and builds consistent row-failure
/// <see cref="CompositionException"/>s.
/// </summary>
/// <remarks>
/// This type never composes - composition stays in the typed JIT/generated call sites
/// (ADR-0070). <c>COMPONO_XUNITV3_AOT</c> selects the AOT namespace and makes the generator-facing
/// surface <see langword="public"/> (hidden from IntelliSense with
/// <c>EditorBrowsableState.Never</c>), per ADR-0058; on the JIT package the same members are
/// <see langword="internal"/>.
/// </remarks>
#if COMPONO_XUNITV3_AOT
[EditorBrowsable(EditorBrowsableState.Never)]
public
#else
internal
#endif
static class ComposeDataRows
{
    /// <summary>The trait name the effective row seed is written into.</summary>
#if COMPONO_XUNITV3_AOT
    [EditorBrowsable(EditorBrowsableState.Never)]
    public
#else
    internal
#endif
    const string SeedTraitName = "Compono.Seed";

#if !COMPONO_XUNITV3_AOT
    // The sole reflection in the ComposeData path (ADR-0070, RESEARCH-0038 §9.2 S1). xUnit 4.x
    // exposes DisableParallelization, but this package compiles against xUnit 3.2.2, which does
    // not, so both properties are located once per process and are null on 3.2.2, where the reads
    // and writes below become no-ops. Reading goes through the ITheoryDataRow interface property so
    // a custom row that does not derive from TheoryDataRowBase is handled too; writing goes through
    // the concrete TheoryDataRow output type. The names are string literals because the members do
    // not exist at compile time on 3.2.2 (a nameof would not compile).
    private static readonly PropertyInfo? SourceDisableParallelizationProperty =
        typeof(ITheoryDataRow).GetProperty("DisableParallelization");

    private static readonly PropertyInfo? TargetDisableParallelizationProperty =
        typeof(TheoryDataRow).GetProperty("DisableParallelization");
#endif

    /// <summary>Builds the D1 member-not-found failure (no seed line).</summary>
    /// <param name="member">The <c>Namespace.Type.Member</c> the attribute named.</param>
    /// <param name="methodDisplayName">The test method display name.</param>
    /// <returns>The failure to throw.</returns>
#if COMPONO_XUNITV3_AOT
    [EditorBrowsable(EditorBrowsableState.Never)]
    public
#else
    internal
#endif
    static CompositionException MemberNotFound(string member, string methodDisplayName) =>
        new($"ComposeData member '{member}' for '{methodDisplayName}' was not found. Expected a public static field, property, or parameterless method.");

    /// <summary>Builds the D2 not-public-static failure (no seed line).</summary>
    /// <param name="member">The <c>Namespace.Type.Member</c> the attribute named.</param>
    /// <param name="methodDisplayName">The test method display name.</param>
    /// <returns>The failure to throw.</returns>
#if COMPONO_XUNITV3_AOT
    [EditorBrowsable(EditorBrowsableState.Never)]
    public
#else
    internal
#endif
    static CompositionException MemberNotPublicStatic(string member, string methodDisplayName) =>
        new($"ComposeData member '{member}' for '{methodDisplayName}' must be public and static.");

    /// <summary>Builds the D3 requires-arguments failure (no seed line).</summary>
    /// <param name="member">The <c>Namespace.Type.Member</c> the attribute named.</param>
    /// <param name="methodDisplayName">The test method display name.</param>
    /// <returns>The failure to throw.</returns>
#if COMPONO_XUNITV3_AOT
    [EditorBrowsable(EditorBrowsableState.Never)]
    public
#else
    internal
#endif
    static CompositionException MemberRequiresArguments(string member, string methodDisplayName) =>
        new($"ComposeData member '{member}' for '{methodDisplayName}' requires arguments, which ComposeData does not support.");

    /// <summary>Builds the D4 unsupported-return-type failure (no seed line).</summary>
    /// <param name="member">The <c>Namespace.Type.Member</c> the attribute named.</param>
    /// <param name="methodDisplayName">The test method display name.</param>
    /// <param name="returnedType">The unsupported member return type.</param>
    /// <returns>The failure to throw.</returns>
#if COMPONO_XUNITV3_AOT
    [EditorBrowsable(EditorBrowsableState.Never)]
    public
#else
    internal
#endif
    static CompositionException UnsupportedReturnType(string member, string methodDisplayName, Type returnedType) =>
        new($"ComposeData member '{member}' for '{methodDisplayName}' returned unsupported type '{returnedType}'. Expected IEnumerable, Task/ValueTask of IEnumerable, or IAsyncEnumerable of ITheoryDataRow, object?[], or ITuple rows.");

    /// <summary>Builds the D5 null-result failure (no seed line).</summary>
    /// <param name="member">The <c>Namespace.Type.Member</c> the attribute named.</param>
    /// <param name="methodDisplayName">The test method display name.</param>
    /// <returns>The failure to throw.</returns>
#if COMPONO_XUNITV3_AOT
    [EditorBrowsable(EditorBrowsableState.Never)]
    public
#else
    internal
#endif
    static CompositionException NullResult(string member, string methodDisplayName) =>
        new($"ComposeData member '{member}' for '{methodDisplayName}' returned null.");

    /// <summary>
    /// Reports whether an awaited member result is a supported synchronous row collection: any
    /// <see cref="IEnumerable"/> other than <see cref="string"/>. Async
    /// (<c>IAsyncEnumerable&lt;T&gt;</c>) results are classified by the caller, which owns the
    /// enumeration loop.
    /// </summary>
    /// <param name="result">The awaited member result.</param>
    /// <returns><see langword="true"/> when the result is a supported collection.</returns>
#if COMPONO_XUNITV3_AOT
    [EditorBrowsable(EditorBrowsableState.Never)]
    public
#else
    internal
#endif
    static bool IsSupportedCollectionResult(object? result) => result is IEnumerable and not string;

    /// <summary>
    /// Normalizes one row into its values array and, when the row implements
    /// <see cref="ITheoryDataRow"/>, that source row (for metadata). Classification order is
    /// <see cref="ITheoryDataRow"/> (<see cref="ITheoryDataRow.GetData"/>), then <c>object?[]</c>
    /// (as-is), then <see cref="ITuple"/> (indexer, which flattens 8+ elements). A null or otherwise
    /// unsupported row throws an R0 wrapper with the pre-composition seed line.
    /// </summary>
    /// <param name="row">The raw row element.</param>
    /// <param name="context">
    /// The row context (index, member, method display name). The source row is not yet known when
    /// normalization runs, so this is normally created with a null source.
    /// </param>
    /// <param name="attributeSeed">The attribute-level seed, when set.</param>
    /// <returns>The normalized row.</returns>
    /// <exception cref="CompositionException">The row is null or an unsupported shape.</exception>
#if COMPONO_XUNITV3_AOT
    [EditorBrowsable(EditorBrowsableState.Never)]
    public
#else
    internal
#endif
    static RowData NormalizeRow(object? row, in RowContext context, int? attributeSeed)
    {
        var seedLine = PreRowSeedLine(attributeSeed);

        switch (row)
        {
            case null:
                throw RowFailure(context, "row is null", seedLine);
            case ITheoryDataRow theoryRow:
                return new RowData { Values = theoryRow.GetData(), Source = theoryRow };
            case object?[] array:
                return new RowData { Values = array, Source = null };
            case ITuple tuple:
                var values = new object?[tuple.Length];
                for (var i = 0; i < tuple.Length; i++)
                {
                    values[i] = tuple[i];
                }

                return new RowData { Values = values, Source = null };
            default:
                throw RowFailure(context, $"row has unsupported type '{row.GetType()}'; expected ITheoryDataRow, object?[], or ITuple", seedLine);
        }
    }

    /// <summary>
    /// Extracts the per-row <typeparamref name="TConfig"/> value from element 0 of a
    /// <c>ComposeData&lt;TProfile, TConfig&gt;</c> row and returns the remaining row values.
    /// A missing, null, or wrong-typed element 0 throws an R0 wrapper with the pre-composition seed
    /// line.
    /// </summary>
    /// <typeparam name="TConfig">The profile configuration type.</typeparam>
    /// <param name="values">The normalized row values (element 0 is the configuration value).</param>
    /// <param name="context">The row context (index, source row, member, method display name).</param>
    /// <param name="attributeSeed">The attribute-level seed, when set.</param>
    /// <returns>The configuration value and the remaining row values.</returns>
    /// <exception cref="CompositionException">Element 0 is missing, null, or not a <typeparamref name="TConfig"/>.</exception>
#if COMPONO_XUNITV3_AOT
    [EditorBrowsable(EditorBrowsableState.Never)]
    public
#else
    internal
#endif
    static (TConfig Config, object?[] Values) ExtractConfig<TConfig>(object?[] values, in RowContext context, int? attributeSeed)
    {
        if (values.Length == 0 || values[0] is not TConfig config)
        {
            throw RowFailure(context, $"the first row element must be a non-null '{typeof(TConfig)}' profile configuration value", PreRowSeedLine(attributeSeed));
        }

        var remaining = new object?[values.Length - 1];
        Array.Copy(values, 1, remaining, 0, remaining.Length);
        return (config, remaining);
    }

    /// <summary>
    /// Binds one leading positional row value to a closed parameter type <typeparamref name="T"/>
    /// using ADR-0022's strict typing rules (no widening; a null is allowed only for a nullable value
    /// type or a nullable reference type; a non-null value must be an instance of the parameter's
    /// underlying type). A failure throws an R0 wrapper with the row's effective seed line.
    /// </summary>
    /// <typeparam name="T">The closed parameter type.</typeparam>
    /// <param name="values">The normalized row values.</param>
    /// <param name="index">The row value index (0-based, after any <c>TConfig</c> element).</param>
    /// <param name="parameterIsNullable">Whether the parameter is nullable.</param>
    /// <param name="parameterName">The parameter name, for failure messages.</param>
    /// <param name="context">The row context (index, source row, member, method display name).</param>
    /// <param name="rowSeed">The row's effective seed, for the failure seed line.</param>
    /// <returns>The bound value.</returns>
    /// <exception cref="CompositionException">The value is null for a non-nullable parameter, or its type is not assignable.</exception>
#if COMPONO_XUNITV3_AOT
    [EditorBrowsable(EditorBrowsableState.Never)]
    public
#else
    internal
#endif
    static T BindValue<T>(object?[] values, int index, bool parameterIsNullable, string parameterName, in RowContext context, int rowSeed)
    {
        var value = values[index];
        var seedLine = RowSeedLine(rowSeed);

        if (value is null)
        {
            // A nullable value type (Nullable<T>) is genuinely nullable, so a null is always valid
            // for it. A reference type defers to the parameter's declared nullability. A
            // non-nullable value type can never be null: never silently default it, even if the
            // caller passed parameterIsNullable: true by mistake.
            if (Nullable.GetUnderlyingType(typeof(T)) is not null)
            {
                return default!;
            }

            if (typeof(T).IsValueType || !parameterIsNullable)
            {
                throw RowFailure(context, $"Row value for parameter '{parameterName}' on '{context.MethodDisplayName}' is null, but the parameter is not nullable.", seedLine);
            }

            return default!;
        }

        var underlyingType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        if (!underlyingType.IsInstanceOfType(value))
        {
            throw RowFailure(context, $"Row value for parameter '{parameterName}' on '{context.MethodDisplayName}' has type '{value.GetType()}', which is not assignable to '{typeof(T)}'.", seedLine);
        }

        return (T)value;
    }

    /// <summary>
    /// Builds the output <see cref="TheoryDataRow"/> for one composed row, applying the single
    /// Compono metadata-merge rule (ADR-0070): the skip group (<c>Skip</c>/<c>SkipType</c>/
    /// <c>SkipUnless</c>/<c>SkipWhen</c>) is grouped (row group when the row's <c>Skip</c> is set,
    /// else the attribute group when the attribute's <c>Skip</c> is set, else none); other metadata
    /// is row ?? attribute; traits are the union of the row's, the attribute's, and
    /// <see cref="SeedTraitName"/>.
    /// </summary>
    /// <param name="values">The composed row values, in parameter order.</param>
    /// <param name="context">The row context (index, source row, member, method display name).</param>
    /// <param name="attribute">The attribute-level metadata.</param>
    /// <param name="seed">The row's effective seed.</param>
    /// <returns>The output row.</returns>
#if COMPONO_XUNITV3_AOT
    [EditorBrowsable(EditorBrowsableState.Never)]
    public
#else
    internal
#endif
    static TheoryDataRow CreateDataRow(object?[] values, in RowContext context, in ComposeDataAttributeMetadata attribute, int seed)
    {
        var dataRow = new TheoryDataRow(values);
        var source = context.Source;

#if COMPONO_XUNITV3_AOT
        if (source is not null && source.Skip is { } rowSkip)
        {
            dataRow.Skip = rowSkip;
            dataRow.SkipUnless = source.SkipUnless;
            dataRow.SkipWhen = source.SkipWhen;
        }
        else if (attribute.Skip is { } attributeSkip)
        {
            dataRow.Skip = attributeSkip;
            dataRow.SkipUnless = attribute.SkipUnless;
            dataRow.SkipWhen = attribute.SkipWhen;
        }
#else
        if (source is not null && source.Skip is { } rowSkip)
        {
            dataRow.Skip = rowSkip;
            dataRow.SkipType = source.SkipType;
            dataRow.SkipUnless = source.SkipUnless;
            dataRow.SkipWhen = source.SkipWhen;
        }
        else if (attribute.Skip is { } attributeSkip)
        {
            dataRow.Skip = attributeSkip;
            dataRow.SkipType = attribute.SkipType;
            dataRow.SkipUnless = attribute.SkipUnless;
            dataRow.SkipWhen = attribute.SkipWhen;
        }
#endif

        dataRow.Label = source?.Label ?? attribute.Label;
        dataRow.TestDisplayName = source?.TestDisplayName ?? attribute.TestDisplayName;
        dataRow.Timeout = source?.Timeout ?? attribute.Timeout;
        dataRow.Explicit = source?.Explicit ?? attribute.Explicit;

#if COMPONO_XUNITV3_AOT
        dataRow.DisableParallelization = source?.DisableParallelization ?? attribute.DisableParallelization;
#else
        bool? rowDisableParallelization = null;
        if (source is not null && SourceDisableParallelizationProperty?.GetValue(source) is bool rowValue)
        {
            rowDisableParallelization = rowValue;
        }

        if ((rowDisableParallelization ?? attribute.DisableParallelization) is { } disableParallelization && TargetDisableParallelizationProperty is not null)
        {
            TargetDisableParallelizationProperty.SetValue(dataRow, disableParallelization);
        }
#endif

        var traits = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        MergeTraits(traits, source);
        AddAttributeTraits(traits, attribute.Traits);
        // Compono.Seed is reserved: a source row or attribute trait of that name must never override
        // the real composition seed, so drop any incoming value before writing the authoritative one.
        traits.Remove(SeedTraitName);
        AddTrait(traits, SeedTraitName, seed.ToString(CultureInfo.InvariantCulture));
        dataRow.Traits = traits;

        return dataRow;
    }

    /// <summary>The seed line used for R0 failures that occur before <c>CreateRow</c>.</summary>
    /// <param name="attributeSeed">The attribute-level seed, when set.</param>
    /// <returns>The seed line, including its leading blank line.</returns>
#if COMPONO_XUNITV3_AOT
    [EditorBrowsable(EditorBrowsableState.Never)]
    public
#else
    internal
#endif
    static string PreRowSeedLine(int? attributeSeed) =>
        attributeSeed is { } seed
            ? $"\n\nSeed: {seed}"
            : "\n\nSeed: not assigned (the failure occurred before the row's composition started)";

    /// <summary>The seed line used for R0 failures that occur from <c>CreateRow</c> on.</summary>
    /// <param name="rowSeed">The row's effective seed.</param>
    /// <returns>The seed line, including its leading blank line.</returns>
#if COMPONO_XUNITV3_AOT
    [EditorBrowsable(EditorBrowsableState.Never)]
    public
#else
    internal
#endif
    static string RowSeedLine(int rowSeed) => $"\n\nSeed: {rowSeed}";

    /// <summary>
    /// Builds an R0 row-failure <see cref="CompositionException"/> from a <see cref="RowContext"/>.
    /// When <paramref name="inner"/> is a <see cref="CompositionException"/>, its
    /// <see cref="CompositionException.Diagnostic"/> is carried through unchanged and it is preserved
    /// as <see cref="Exception.InnerException"/>; any other inner exception is preserved as
    /// <see cref="Exception.InnerException"/> with no diagnostic.
    /// </summary>
    /// <param name="context">The row context (index, source row label, member, method display name).</param>
    /// <param name="cause">The cause message.</param>
    /// <param name="seedLine">The seed line to append (see <see cref="PreRowSeedLine"/> and <see cref="RowSeedLine"/>).</param>
    /// <param name="inner">The cause exception, when any.</param>
    /// <returns>The failure to throw.</returns>
#if COMPONO_XUNITV3_AOT
    [EditorBrowsable(EditorBrowsableState.Never)]
    public
#else
    internal
#endif
    static CompositionException RowFailure(in RowContext context, string cause, string seedLine, Exception? inner = null)
    {
        var labelPart = context.Label is null ? string.Empty : $" [{context.Label}]";
        var message = $"ComposeData row {context.Index}{labelPart} from '{context.Member}' for '{context.MethodDisplayName}' failed: {cause}{seedLine}";

        return inner is null
            ? new CompositionException(message)
            : new CompositionException(message, (inner as CompositionException)?.Diagnostic, inner);
    }

    /// <summary>
    /// Builds the R0 failure for a row that supplies more leading positional values than the test
    /// method has parameters. The message is byte-identical on both engines.
    /// </summary>
    /// <param name="context">The row context (index, source row label, member, method display name).</param>
    /// <param name="supplied">The number of row values supplied.</param>
    /// <param name="parameterCount">The number of test method parameters.</param>
    /// <param name="rowSeed">The row's effective seed, for the failure seed line.</param>
    /// <returns>The failure to throw.</returns>
#if COMPONO_XUNITV3_AOT
    [EditorBrowsable(EditorBrowsableState.Never)]
    public
#else
    internal
#endif
    static CompositionException TooManyValues(in RowContext context, int supplied, int parameterCount, int rowSeed) =>
        RowFailure(context, $"too many values: {supplied} supplied, but the method has {parameterCount} parameter(s)", RowSeedLine(rowSeed));

    private static void MergeTraits(Dictionary<string, HashSet<string>> target, ITheoryDataRow? source)
    {
        if (source?.Traits is not { } sourceTraits)
        {
            return;
        }

        foreach (var pair in sourceTraits)
        {
            AddTrait(target, pair.Key, pair.Value);
        }
    }

    private static void AddAttributeTraits(Dictionary<string, HashSet<string>> target, string[]? traits)
    {
        if (traits is null)
        {
            return;
        }

        for (var i = 0; i + 1 < traits.Length; i += 2)
        {
            AddTrait(target, traits[i], traits[i + 1]);
        }
    }

    private static void AddTrait(Dictionary<string, HashSet<string>> target, string name, string value)
    {
        if (!target.TryGetValue(name, out var values))
        {
            values = new HashSet<string>(StringComparer.Ordinal);
            target[name] = values;
        }

        values.Add(value);
    }

    private static void AddTrait(Dictionary<string, HashSet<string>> target, string name, IEnumerable<string> values)
    {
        if (!target.TryGetValue(name, out var set))
        {
            set = new HashSet<string>(StringComparer.Ordinal);
            target[name] = set;
        }

        foreach (var value in values)
        {
            set.Add(value);
        }
    }
}

/// <summary>One normalized <c>[ComposeData]</c> row: its values and, when it was an <see cref="ITheoryDataRow"/>, the source row.</summary>
#if COMPONO_XUNITV3_AOT
[EditorBrowsable(EditorBrowsableState.Never)]
public
#else
internal
#endif
readonly struct RowData
{
    /// <summary>The row's values, in parameter order.</summary>
#if COMPONO_XUNITV3_AOT
    [EditorBrowsable(EditorBrowsableState.Never)]
#endif
    public required object?[] Values { get; init; }

    /// <summary>The source row, when the row implemented <see cref="ITheoryDataRow"/>; otherwise <see langword="null"/>.</summary>
#if COMPONO_XUNITV3_AOT
    [EditorBrowsable(EditorBrowsableState.Never)]
#endif
    public required ITheoryDataRow? Source { get; init; }
}

/// <summary>Identifies one <c>[ComposeData]</c> row for failure messages and metadata merging.</summary>
/// <param name="index">The zero-based row index.</param>
/// <param name="source">The source row, when the row implemented <see cref="ITheoryDataRow"/>.</param>
/// <param name="member">The <c>Namespace.Type.Member</c> the attribute named.</param>
/// <param name="methodDisplayName">The test method display name.</param>
#if COMPONO_XUNITV3_AOT
[EditorBrowsable(EditorBrowsableState.Never)]
public
#else
internal
#endif
readonly struct RowContext(int index, ITheoryDataRow? source, string member, string methodDisplayName)
{
    /// <summary>The zero-based row index.</summary>
#if COMPONO_XUNITV3_AOT
    [EditorBrowsable(EditorBrowsableState.Never)]
#endif
    public int Index { get; } = index;

    /// <summary>The source row, when the row implemented <see cref="ITheoryDataRow"/>; otherwise <see langword="null"/>.</summary>
#if COMPONO_XUNITV3_AOT
    [EditorBrowsable(EditorBrowsableState.Never)]
#endif
    public ITheoryDataRow? Source { get; } = source;

    /// <summary>The <c>Namespace.Type.Member</c> the attribute named.</summary>
#if COMPONO_XUNITV3_AOT
    [EditorBrowsable(EditorBrowsableState.Never)]
#endif
    public string Member { get; } = member;

    /// <summary>The test method display name.</summary>
#if COMPONO_XUNITV3_AOT
    [EditorBrowsable(EditorBrowsableState.Never)]
#endif
    public string MethodDisplayName { get; } = methodDisplayName;

    /// <summary>The source row's label, when present.</summary>
#if COMPONO_XUNITV3_AOT
    [EditorBrowsable(EditorBrowsableState.Never)]
#endif
    public string? Label => Source?.Label;
}

/// <summary>
/// The attribute-level metadata a <c>[ComposeData]</c> method contributes to the merge rule. Skip
/// conditions are engine-specific: the JIT package uses xUnit's <see cref="string"/> condition names
/// and <see cref="Type"/> skip type, while AOT uses compiled <see cref="Func{TResult}"/> predicates.
/// </summary>
#if COMPONO_XUNITV3_AOT
[EditorBrowsable(EditorBrowsableState.Never)]
public
#else
internal
#endif
readonly struct ComposeDataAttributeMetadata
{
    /// <summary>The attribute-level explicit flag, when set.</summary>
#if COMPONO_XUNITV3_AOT
    [EditorBrowsable(EditorBrowsableState.Never)]
#endif
    public bool? Explicit { get; init; }

    /// <summary>The attribute-level label, when set.</summary>
#if COMPONO_XUNITV3_AOT
    [EditorBrowsable(EditorBrowsableState.Never)]
#endif
    public string? Label { get; init; }

    /// <summary>The attribute-level skip reason, when set.</summary>
#if COMPONO_XUNITV3_AOT
    [EditorBrowsable(EditorBrowsableState.Never)]
#endif
    public string? Skip { get; init; }

    /// <summary>The attribute-level test display name, when set.</summary>
#if COMPONO_XUNITV3_AOT
    [EditorBrowsable(EditorBrowsableState.Never)]
#endif
    public string? TestDisplayName { get; init; }

    /// <summary>The attribute-level timeout, when set.</summary>
#if COMPONO_XUNITV3_AOT
    [EditorBrowsable(EditorBrowsableState.Never)]
#endif
    public int? Timeout { get; init; }

    /// <summary>The attribute-level trait name/value pairs, when set.</summary>
#if COMPONO_XUNITV3_AOT
    [EditorBrowsable(EditorBrowsableState.Never)]
#endif
    public string[]? Traits { get; init; }

    /// <summary>The attribute-level disable-parallelization flag, when set.</summary>
#if COMPONO_XUNITV3_AOT
    [EditorBrowsable(EditorBrowsableState.Never)]
#endif
    public bool? DisableParallelization { get; init; }

#if COMPONO_XUNITV3_AOT
    /// <summary>The compiled attribute-level skip-unless predicate, when set.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public Func<bool>? SkipUnless { get; init; }

    /// <summary>The compiled attribute-level skip-when predicate, when set.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public Func<bool>? SkipWhen { get; init; }
#else
    /// <summary>The attribute-level skip-unless property name, when set.</summary>
    public string? SkipUnless { get; init; }

    /// <summary>The attribute-level skip-when property name, when set.</summary>
    public string? SkipWhen { get; init; }

    /// <summary>The attribute-level skip-condition declaring type, when set.</summary>
    public Type? SkipType { get; init; }
#endif
}
