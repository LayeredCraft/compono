using System.Globalization;
using System.Reflection;
using Compono.TUnit.Binding;
using global::TUnit.Core;
using global::TUnit.Core.Interfaces;

namespace Compono.TUnit;

/// <summary>
/// Composes a TUnit test method's parameters through Compono - the default (no explicit profile)
/// entry point. Every parameter not supplied inline is composed; a parameter targeted by a supplied
/// inline value takes that value instead, taking precedence over composition. See
/// <c>docs/adr/0040-compono-tunit-package-design.md</c> for the full binding algorithm, seed policy,
/// and diagnostics - adapted from <c>Compono.XunitV3.ComposeAttribute</c>, not a byte-for-byte port
/// (TUnit hands a data source <see cref="DataGeneratorMetadata"/>, not a <c>MethodInfo</c>).
/// </summary>
/// <remarks>
/// Deliberately unsealed - <c>ComposeAttribute&lt;TProfile&gt;</c> and
/// <c>ComposeAttribute&lt;TProfile, TConfig&gt;</c> (Phase 1) are the two designed extension points,
/// mirroring <c>Compono.XunitV3</c>'s own family exactly. Composition is deferred entirely into the
/// factory <see cref="GenerateDataSources"/> returns - never before TUnit actually invokes it,
/// matching TUnit's own "defer real work into the Func" convention
/// (<c>DependencyInjectionDataSourceAttribute</c> does the same).
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class ComposeAttribute : UntypedDataSourceGeneratorAttribute, ITestDiscoveryEventReceiver
{
    // The StateBag key this attribute stores a row's seed under, read back in OnTestDiscovered - see
    // ADR-0040's "Seed observability" section. Distinct from the reporter-visible AddProperty key
    // below; this one is an internal handoff, never surfaced to a consumer.
    private const string SeedStateBagKey = "Compono.TUnit.RowSeed";

    // The trait/property key every row's seed is reported under - see ADR-0040's "Seed observability"
    // section. Matches Compono.XunitV3's SeedTraitName in spirit (same purpose, different runner's
    // own reporting mechanism).
    internal const string SeedPropertyName = "Compono.Seed";

    private readonly object?[] _inlineValues;
    private readonly Lazy<Composer> _composer;
    private int? _seed;
    private BindingPlan? _bindingPlan;
    private object? _bindingPlanLock;

    /// <summary>
    /// Creates a <see cref="ComposeAttribute"/>.
    /// </summary>
    /// <param name="inlineValues">
    /// Values supplied positionally, left-to-right from the test method's first parameter - every
    /// parameter at an index beyond this array's length is composed instead. Matches
    /// <c>Compono.XunitV3.ComposeAttribute</c>'s own single-null/single-array constructor-binding
    /// edge cases exactly - see <see cref="NormalizeParamsArguments"/>.
    /// </param>
    public ComposeAttribute(params object?[] inlineValues)
    {
        _inlineValues = NormalizeParamsArguments(inlineValues);
        _composer = new Lazy<Composer>(BuildComposer);
    }

    /// <summary>
    /// An explicit root seed for this row - the same underlying contract as
    /// <see cref="CompositionBuilder.WithSeed"/>, but restricted to non-negative values (enforced in
    /// <see cref="GenerateDataSources"/>) so a seed reported via <see cref="OnTestDiscovered"/> is
    /// always pasteable back into this property unchanged. Unset: a fresh, non-negative seed is
    /// generated for every row.
    /// </summary>
    public int Seed
    {
        get => _seed ?? default;
        set => _seed = value;
    }

    /// <summary>
    /// The inline values supplied to this attribute's constructor, in positional order.
    /// </summary>
    internal IReadOnlyList<object?> InlineValues => _inlineValues;

    /// <summary>
    /// The value actually assigned to <see cref="Seed"/>, or <see langword="null"/> if it was never
    /// set - distinguishes "configured to 0" from "never configured."
    /// </summary>
    internal int? SeedAsNullable => _seed;

    /// <summary>
    /// Composes (or applies inline values to) one row's parameters for <paramref name="dataGeneratorMetadata"/>.
    /// See ADR-0040's "Package shape"/"Diagnostics, disposal, and seed observability" sections for the
    /// full step-by-step behavior this implements.
    /// </summary>
    /// <exception cref="CompositionException">
    /// This attribute's configured seed (or a profile-configured one) is negative; the test method's
    /// signature is unsupported (a generic method, a <c>ref</c>/<c>out</c>/<c>in</c>/<c>params</c>
    /// parameter, or more than one <c>[Shared]</c> parameter of the same type); too many inline
    /// values were supplied; a supplied inline value is <see langword="null"/> for a non-nullable
    /// parameter or has a type not assignable to its parameter; or composition itself fails for a
    /// parameter - a new <see cref="CompositionException"/> propagates whose message is the
    /// pipeline's original message with the row's seed appended.
    /// </exception>
    protected override IEnumerable<Func<object?[]?>> GenerateDataSources(DataGeneratorMetadata dataGeneratorMetadata)
    {
        ArgumentNullException.ThrowIfNull(dataGeneratorMetadata);

        yield return () => ComposeRow(dataGeneratorMetadata);
    }

    /// <inheritdoc />
    public ValueTask OnTestDiscovered(DiscoveredTestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.TestContext.StateBag.TryGetValue<int>(SeedStateBagKey, out var seed))
            context.AddProperty(SeedPropertyName, seed.ToString(CultureInfo.InvariantCulture));

        return default;
    }

    /// <summary>
    /// Applies this attribute's profile selection to <paramref name="builder"/> - a no-op for the
    /// non-generic <see cref="ComposeAttribute"/> (no profile), overridden by the generic forms.
    /// </summary>
    internal virtual void ApplyProfile(CompositionBuilder builder)
    {
    }

    // Shared with the two-type-parameter generic form's own profile-configuration-argument
    // constructor, which faces the identical params object?[] single-null/single-array binding
    // ambiguity this constructor's own remarks document.
    internal static object?[] NormalizeParamsArguments(object?[] arguments) => arguments switch
    {
        null => [null],
        not null when arguments.GetType() != typeof(object[]) => [arguments],
        _ => arguments,
    };

    // Internal test seam - lets Compono.TUnit.Tests assert the same BindingPlan instance (and the
    // same per-parameter invoker delegates on it) is returned across repeated calls with the same
    // testInformation, proving MakeGenericMethod ran exactly once per parameter.
    internal BindingPlan EnsureBindingPlan(MethodMetadata testInformation) =>
        LazyInitializer.EnsureInitialized(ref _bindingPlan, ref _bindingPlanLock, () => BindingPlan.Build(testInformation))!;

    // Internal test seam - lets Compono.TUnit.Tests assert the same Composer instance is reused
    // across repeated row-composition calls on one attribute instance.
    internal Composer GetComposer() => _composer.Value;

    private object?[]? ComposeRow(DataGeneratorMetadata dataGeneratorMetadata)
    {
        var testInformation = dataGeneratorMetadata.TestInformation
            ?? throw new CompositionException("Compono.TUnit requires TestInformation (a method-level [Compose] data source), but none was available.");

        var composer = _composer.Value;
        var bindingPlan = EnsureBindingPlan(testInformation);
        var declaringType = testInformation.Class.Type;

        var row = composer.CreateRow(declaringType);

        // Read from row.Seed, not SeedAsNullable alone - a profile's own CompositionBuilder.WithSeed
        // call reaches the composer's configuration independently of SeedAsNullable, which stays null
        // whenever this attribute's own Seed property is unset. row.Seed reflects whichever source
        // actually configured the row's seed - this attribute's property, a profile, or the
        // auto-generated fallback.
        if (row.Seed < 0)
        {
            throw new CompositionException(AppendSeed(
                $"Compono.TUnit requires a non-negative seed, but the configured seed was {row.Seed}.",
                row.Seed));
        }

        // Stored before any parameter composes, so a pre-composition signature failure still leaves
        // a seed discoverable for this row via OnTestDiscovered.
        dataGeneratorMetadata.TestBuilderContext.Current.StateBag[SeedStateBagKey] = row.Seed;

        if (bindingPlan.SignatureError is not null)
            throw new CompositionException(AppendSeed(bindingPlan.SignatureError, row.Seed));

        var parameters = bindingPlan.Parameters;
        var methodDisplayName = BindingPlan.MethodDisplayName(testInformation);

        if (_inlineValues.Length > parameters.Count)
        {
            throw new CompositionException(AppendSeed(
                $"Too many inline values supplied to '{methodDisplayName}': {_inlineValues.Length} supplied, but the method has {parameters.Count} parameter(s).",
                row.Seed));
        }

        // Every supplied inline value is validated before any parameter is bound, shared, or
        // composed - an inline mismatch always fails as its own category.
        for (var i = 0; i < _inlineValues.Length; i++)
        {
            var parameter = parameters[i];
            var value = _inlineValues[i];

            switch (PositionalArgumentBinder.Validate(parameter.ParameterType, parameter.Descriptor.Nullability, value))
            {
                case PositionalArgumentValidation.NullNotAllowed:
                    throw new CompositionException(AppendSeed(
                        $"Inline value for parameter '{parameter.Name}' on '{methodDisplayName}' is null, but the parameter is not nullable.",
                        row.Seed));

                case PositionalArgumentValidation.TypeMismatch:
                    throw new CompositionException(AppendSeed(
                        $"Inline value for parameter '{parameter.Name}' on '{methodDisplayName}' has type '{value!.GetType()}', which is not assignable to '{parameter.ParameterType}'.",
                        row.Seed));
            }
        }

        var values = new object?[parameters.Count];

        // [Shared] parameters compose (or share their inline value) first, in declaration order among
        // themselves, so a shared value is always in scope before any parameter that might
        // structurally depend on it composes.
        for (var i = 0; i < parameters.Count; i++)
        {
            var parameter = parameters[i];

            if (!parameter.IsShared)
                continue;

            if (i < _inlineValues.Length)
            {
                var value = _inlineValues[i];
                InvokeWithSeedOnFailure(() => parameter.ShareExplicitInvoker(row, parameter.Descriptor, value), row.Seed);
                values[i] = value;
            }
            else
            {
                values[i] = InvokeWithSeedOnFailure(() => parameter.ResolveSharedInvoker(row, parameter.Descriptor), row.Seed);
            }
        }

        // Every remaining (non-inline, non-shared) parameter composes next, in declaration order.
        for (var i = 0; i < parameters.Count; i++)
        {
            var parameter = parameters[i];

            if (parameter.IsShared)
                continue;

            values[i] = i < _inlineValues.Length
                ? _inlineValues[i]
                : InvokeWithSeedOnFailure(() => parameter.ResolveInvoker(row, parameter.Descriptor), row.Seed);
        }

        return values;
    }

    private Composer BuildComposer() => Composer.Create(builder =>
    {
        ApplyProfile(builder);

        if (SeedAsNullable is { } seed)
            builder.WithSeed(seed);
    });

    // The "\n\nSeed: {value}" convention every Compono.TUnit-authored pre-composition failure uses,
    // matching the same trailing text a propagated pipeline CompositionDiagnostic already renders -
    // so every failure category ends the same way, whether Compono.TUnit constructed the message or
    // the pipeline did. private protected, not private - the generic forms (Phase 1) reuse this exact
    // convention for their own pre-composer checks.
    private protected static string AppendSeed(string message, int seed) => $"{message}\n\nSeed: {seed}";

    // A genuine composition failure propagates un-wrapped from the pipeline otherwise, and
    // CompositionException.Message alone never carries the seed for that case - a real test runner's
    // failure display shows Message, not Diagnostic.ToString(). CompositionException.WithSeedInMessage
    // rewrites Message to include it, preserving Diagnostic (null or not) and the original exception
    // as InnerException unchanged.
    private static TResult InvokeWithSeedOnFailure<TResult>(Func<TResult> compose, int seed)
    {
        try
        {
            return compose();
        }
        catch (CompositionException exception)
        {
            throw CompositionException.WithSeedInMessage(exception, seed);
        }
    }

    private static void InvokeWithSeedOnFailure(Action compose, int seed)
    {
        try
        {
            compose();
        }
        catch (CompositionException exception)
        {
            throw CompositionException.WithSeedInMessage(exception, seed);
        }
    }
}
