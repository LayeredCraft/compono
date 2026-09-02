using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Compono.MSTest.Binding;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Compono.MSTest;

/// <summary>
/// Composes an MSTest data-driven test method's parameters through Compono - the default (no
/// explicit profile) entry point. Every parameter not supplied inline is composed; a parameter
/// targeted by a supplied inline value takes that value instead, taking precedence over
/// composition. See <c>docs/adr/0057-compono-mstest-package-design.md</c> for the full binding
/// algorithm, discovery/execution behavioral contract, seed policy, and diagnostics.
/// </summary>
/// <remarks>
/// Deliberately unsealed - <see cref="ComposeAttribute{TProfile}"/> and
/// <see cref="ComposeAttribute{TProfile, TConfig}"/> are the two designed extension points, matching
/// <c>Compono.XunitV3</c>/<c>Compono.TUnit</c>'s own family shape. Implements
/// <see cref="ITestDataSource"/> directly on a plain <see cref="Attribute"/> - never derives from
/// <see cref="DataTestMethodAttribute"/> or any other MSTest attribute base type (ADR-0057 §3).
/// <b>One <see cref="CompositionRow"/> per <see cref="GetData"/> invocation.</b> MSTest may invoke
/// <see cref="GetData"/> more than once across separately-invoked discovery and execution sessions
/// (ADR-0057 §9) - consequently, composition (including any side-effecting registration factory or
/// <see cref="ICompositionValueProvider"/>) may also run more than once for one eventual test case.
/// Each invocation's row is independent: <c>[Shared]</c>/<c>Share&lt;T&gt;()</c> are never split
/// across calls.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class ComposeAttribute : Attribute, ITestDataSource
{
    private readonly object?[] _inlineValues;
    private readonly Lazy<Composer> _composer;

    // GetDisplayName(MethodInfo, object?[]?) is a separate call from GetData(MethodInfo) with no
    // shared row/context object MSTest threads between them - ITestDataSource has no Traits/property-
    // bag equivalent (RESEARCH-0017 §11/§15, an explicitly left-open implementation-plan detail).
    // Keying a seed by the exact `values` array instance GetData returned (not by-value equality,
    // not a single "last seed" field) is what lets GetDisplayName report the correct seed for
    // whichever specific row MSTest is currently asking about, even under overlapping/out-of-order
    // GetData/GetDisplayName calls across repeated discovery/execution invocations (ADR-0057 §9) -
    // a ConditionalWeakTable never prevents a stale entry's row array from being collected once
    // MSTest itself drops its own reference.
    private static readonly ConditionalWeakTable<object?[], object> SeedByRow = new();

    // Test-observability hooks only, not part of the row-binding algorithm itself - let a real
    // consuming test project (packaged, running under a real MTP/VSTest test host) prove the
    // GetData -> GetDisplayName row-array-identity assumption SeedByRow depends on actually holds
    // under both runners, rather than merely asserting it works in an isolated unit test that calls
    // GetData/GetDisplayName directly without going through MSTest's own discovery/execution
    // pipeline at all. Interlocked, since discovery/execution can run test methods concurrently.
    internal static int SeedByRowHitCount;
    internal static int SeedByRowMissCount;
    internal static int GetDataCallCount;

    private int? _seed;
    private BindingPlan? _bindingPlan;
    private object? _bindingPlanLock;

    /// <summary>
    /// Creates a <see cref="ComposeAttribute"/>.
    /// </summary>
    /// <param name="inlineValues">
    /// Values supplied positionally, left-to-right from the test method's first parameter - every
    /// parameter at an index beyond this array's length is composed instead. An explicit
    /// <see langword="null"/> entry is a supplied value, not "not supplied": presence is determined
    /// by array length alone. Matches <c>Compono.XunitV3.ComposeAttribute</c>'s own
    /// <c>params object?[]</c> single-null/single-array binding-ambiguity handling exactly (see
    /// <see cref="NormalizeParamsArguments"/>).
    /// </param>
    public ComposeAttribute(params object?[] inlineValues)
    {
        _inlineValues = NormalizeParamsArguments(inlineValues);
        _composer = new Lazy<Composer>(BuildComposer);
    }

    /// <summary>
    /// An explicit root seed for this row - the same underlying contract as
    /// <see cref="CompositionBuilder.WithSeed"/>, but restricted to non-negative values so a seed
    /// reported in a failure message or <see cref="GetDisplayName"/>'s output is always pasteable
    /// back into this property unchanged. Unset: a fresh, non-negative seed is generated on every
    /// <see cref="GetData"/> call, matching <c>Compono.XunitV3</c>/<c>Compono.TUnit</c>'s own
    /// <c>Seed</c> contract exactly.
    /// </summary>
    public int Seed
    {
        get => _seed ?? default;
        set => _seed = value;
    }

    /// <summary>
    /// The value actually assigned to <see cref="Seed"/>, or <see langword="null"/> if it was never
    /// set - distinguishes "configured to 0" from "never configured."
    /// </summary>
    internal int? SeedAsNullable => _seed;

    /// <summary>
    /// Composes (or applies inline values to) one test method's parameters into a single row. Called
    /// by MSTest once per discovered/executed test case - possibly more than once for the same
    /// eventual test case, per ADR-0057 §9. Returns exactly one <see cref="object"/>?[] row, wrapped
    /// in a single-element sequence: this attribute owns the entire row, mirroring
    /// <c>Compono.XunitV3</c>/<c>Compono.TUnit</c>'s "one Compose-family attribute per test method"
    /// design (enforced by <see cref="BindingPlan"/>'s own signature validation).
    /// </summary>
    /// <exception cref="CompositionException">
    /// This attribute's configured seed (or a profile-configured one) is negative; the test method's
    /// signature is unsupported (a generic method, a <c>ref</c>/<c>out</c>/<c>in</c>/<c>params</c>
    /// parameter, a <c>ref struct</c>/pointer-typed parameter, more than one Compose-family
    /// attribute, or more than one <c>[Shared]</c> parameter of the same type); too many inline
    /// values were supplied; a supplied inline value is <see langword="null"/> for a non-nullable
    /// parameter or has a type not assignable to its parameter; or composition itself fails for a
    /// parameter.
    /// </exception>
    public IEnumerable<object?[]> GetData(MethodInfo methodInfo)
    {
        ArgumentNullException.ThrowIfNull(methodInfo);

        // Test-observability hook only (ADR-0057 §9's discovery/execution repeat-composition
        // contract) - see RealRunnerRowIdentityTests for the real-runner verification this feeds.
        Interlocked.Increment(ref GetDataCallCount);

        var composer = _composer.Value;
        var bindingPlan = EnsureBindingPlan(methodInfo);

        // A test method is always declared on a type - there is no "global" test method shape.
        var row = composer.CreateRow(methodInfo.DeclaringType!);

        if (row.Seed < 0)
        {
            throw new CompositionException(AppendSeed(
                $"Compono.MSTest requires a non-negative seed, but the configured seed was {row.Seed}.",
                row.Seed));
        }

        if (bindingPlan.SignatureError is not null)
            throw new CompositionException(AppendSeed(bindingPlan.SignatureError, row.Seed));

        var parameters = bindingPlan.Parameters;
        var methodDisplayName = BindingPlan.MethodDisplayName(methodInfo);

        if (_inlineValues.Length > parameters.Count)
        {
            throw new CompositionException(AppendSeed(
                $"Too many inline values supplied to '{methodDisplayName}': {_inlineValues.Length} supplied, but the method has {parameters.Count} parameter(s).",
                row.Seed));
        }

        // Every supplied inline value is validated before any parameter is bound, shared, or
        // composed - an inline mismatch always fails as its own category, never surfacing
        // indirectly through a later ShareExplicit/Resolve call.
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

        // [Shared] parameters compose (or share their inline value) first, in declaration order
        // among themselves, regardless of where they sit among non-shared parameters.
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

        SeedByRow.AddOrUpdate(values, row.Seed);

        return [values];
    }

    /// <summary>
    /// Produces a stable, non-huge-object-dump display name of the form
    /// <c>{methodName} (Compono, seed: {seed})</c> - the primary and only supported seed-reporting
    /// path (ADR-0057 §15). Recomputes the row's binding plan/composer the same way <see cref="GetData"/>
    /// does rather than caching a value across calls, so the reported seed always matches whichever
    /// row this specific invocation's <paramref name="data"/> came from.
    /// </summary>
    public string? GetDisplayName(MethodInfo methodInfo, object?[]? data)
    {
        ArgumentNullException.ThrowIfNull(methodInfo);

        var methodName = methodInfo.Name;

        // data is the exact array instance GetData returned for this row (an implementation detail
        // of MSTest's own ITestDataSource contract, not something Compono.MSTest controls) - if
        // MSTest hands back a different/copied array for some runner path, this falls back to a
        // fresh seed rather than throwing, since a display name is diagnostic, never load-bearing.
        int seed;

        if (data is not null && SeedByRow.TryGetValue(data, out var boxedSeed))
        {
            seed = (int)boxedSeed;
            Interlocked.Increment(ref SeedByRowHitCount);
        }
        else
        {
            seed = SeedAsNullable ?? Random.Shared.Next(0, int.MaxValue);
            Interlocked.Increment(ref SeedByRowMissCount);
        }

        return string.Create(CultureInfo.InvariantCulture, $"{methodName} (Compono, seed: {seed})");
    }

    /// <summary>
    /// Applies this attribute's profile selection to <paramref name="builder"/> - a no-op for the
    /// non-generic <see cref="ComposeAttribute"/> (no profile), overridden by
    /// <see cref="ComposeAttribute{TProfile}"/>/<see cref="ComposeAttribute{TProfile, TConfig}"/> to
    /// add their own profile.
    /// </summary>
    internal virtual void ApplyProfile(CompositionBuilder builder)
    {
    }

    // Shared with ComposeAttribute{TProfile,TConfig}'s profile-configuration-argument constructor -
    // matching Compono.XunitV3.ComposeAttribute's identical normalization exactly.
    internal static object?[] NormalizeParamsArguments(object?[] arguments) => arguments switch
    {
        null => [null],
        not null when arguments.GetType() != typeof(object[]) => [arguments],
        _ => arguments,
    };

    internal BindingPlan EnsureBindingPlan(MethodInfo testMethod) =>
        LazyInitializer.EnsureInitialized(ref _bindingPlan, ref _bindingPlanLock, () => BindingPlan.Build(testMethod))!;

    internal Composer GetComposer() => _composer.Value;

    private Composer BuildComposer() => Composer.Create(builder =>
    {
        ApplyProfile(builder);

        if (SeedAsNullable is { } seed)
            builder.WithSeed(seed);
    });

    // The "\n\nSeed: {value}" convention every Compono.MSTest-authored pre-composition failure uses,
    // matching Compono.XunitV3/Compono.TUnit's identical convention - private protected, not
    // private, since ComposeAttribute{TProfile,TConfig} reuses this exact convention for its own
    // pre-composer negative-seed check, which must run before ApplyProfile does any config/profile
    // binding work, i.e. before a CompositionRow (and this method's usual row.Seed source) exists.
    private protected static string AppendSeed(string message, int seed) => $"{message}\n\nSeed: {seed}";

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
