using System.Globalization;
using System.Reflection;
using System.Threading;
using Compono.NUnit.Binding;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using NUnit.Framework.Internal.Builders;

namespace Compono.NUnit;

/// <summary>
/// Composes an NUnit test method's parameters through Compono - the default (no explicit profile)
/// entry point. Every parameter not supplied inline is composed; a parameter targeted by a supplied
/// inline value takes that value instead, taking precedence over composition. See
/// <c>docs/adr/0059-compono-nunit-package-design.md</c> for the full binding algorithm,
/// discovery/execution behavioral contract, seed policy, and diagnostics.
/// </summary>
/// <remarks>
/// Deliberately unsealed - <see cref="ComposeAttribute{TProfile}"/> and
/// <see cref="ComposeAttribute{TProfile, TConfig}"/> are the two designed extension points, matching
/// <c>Compono.XunitV3</c>/<c>Compono.TUnit</c>/<c>Compono.MSTest</c>'s own family shape. Derives from
/// <see cref="TestAttribute"/> (NUnit's own native test-identifying attribute) and implements
/// <see cref="ITestBuilder"/> directly - the smallest seam found capable of both making
/// <c>[Compose]</c>-decorated methods independently discoverable by NUnit (no <c>[TestFixture]</c>
/// required on the containing class) <em>and</em> owning one complete composed row per method
/// (ADR-0059 §4/§5/§7). <see cref="BuildFrom"/> is declared <see langword="new"/> - an explicit,
/// intentional hiding of <see cref="TestAttribute"/>'s own inherited <see cref="ISimpleTestBuilder"/>
/// implementation; spike-confirmed (ADR-0059 §4) to change no observable behavior (the
/// <see cref="ITestBuilder"/> interface map always resolves to this type's own <see cref="BuildFrom"/>
/// regardless of <see langword="new"/>), but required to build without <c>CS0108</c>.
/// <b>One <see cref="CompositionRow"/> per <see cref="BuildFrom"/> invocation.</b> NUnit may invoke
/// <see cref="BuildFrom"/> more than once across separately-invoked discovery and execution sessions
/// under classic VSTest (ADR-0059 §12) - consequently, composition (including any side-effecting
/// registration factory or <see cref="ICompositionValueProvider"/>) may also run more than once for
/// one eventual test case. Each invocation's row is independent: <c>[Shared]</c>/
/// <c>Share&lt;T&gt;()</c> are never split across calls.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class ComposeAttribute : TestAttribute, ITestBuilder
{
    private readonly object?[] _inlineValues;
    private readonly Lazy<Composer> _composer;

    private int? _seed;
    private BindingPlan? _bindingPlan;
    private object? _bindingPlanLock;

    // Test-observability hook only, not part of the row-binding algorithm itself - lets a real
    // consuming test project (packaged, running under a real MTP/VSTest test host) prove the actual
    // per-process BuildFrom call count under separately-invoked discovery/execution sessions
    // (ADR-0059 §12), rather than merely asserting it in an isolated unit test that calls BuildFrom
    // directly without going through NUnit's own discovery/execution pipeline at all.
    internal static int BuildFromCallCount;

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
    /// reported in a failure message or the constructed test's display name is always pasteable back
    /// into this property unchanged. Unset: a fresh, non-negative seed is generated on every
    /// <see cref="BuildFrom"/> call, matching <c>Compono.XunitV3</c>/<c>Compono.TUnit</c>/
    /// <c>Compono.MSTest</c>'s own <c>Seed</c> contract exactly.
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
    /// Composes (or applies inline values to) one test method's parameters into a single row and
    /// constructs the resulting <see cref="TestMethod"/> NUnit expects back. Called by NUnit once per
    /// discovered/executed test case - possibly more than once for the same eventual test case, per
    /// ADR-0059 §12. Yields exactly one <see cref="TestMethod"/>: this attribute owns the entire row,
    /// mirroring <c>Compono.XunitV3</c>/<c>Compono.TUnit</c>/<c>Compono.MSTest</c>'s "one Compose-family
    /// attribute per test method" design (enforced by <see cref="BindingPlan"/>'s own signature
    /// validation) - independently confirmed by spike (ADR-0059 §8) to coexist with NUnit's own
    /// <c>[TestCase]</c>/<c>[Values]</c>/<c>[Range]</c>/custom <see cref="IParameterDataSource"/> as
    /// independent rows, never merged.
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
    public new IEnumerable<TestMethod> BuildFrom(IMethodInfo method, Test? suite)
    {
        ArgumentNullException.ThrowIfNull(method);

        // Test-observability hook only (ADR-0059 §12's discovery/execution repeat-composition
        // contract).
        Interlocked.Increment(ref BuildFromCallCount);

        // NUnit's own metadata-wrapper type, not the real System.Reflection.MethodInfo the rest of
        // this package's binding machinery operates on (ADR-0059 §4/§5) - unwrap first, once.
        var reflectedMethod = method.MethodInfo;

        var composer = _composer.Value;
        var bindingPlan = EnsureBindingPlan(reflectedMethod);

        // A test method is always declared on a type - there is no "global" test method shape.
        var row = composer.CreateRow(reflectedMethod.DeclaringType!);

        if (row.Seed < 0)
        {
            throw new CompositionException(AppendSeed(
                $"Compono.NUnit requires a non-negative seed, but the configured seed was {row.Seed}.",
                row.Seed));
        }

        if (bindingPlan.SignatureError is not null)
            throw new CompositionException(AppendSeed(bindingPlan.SignatureError, row.Seed));

        var parameters = bindingPlan.Parameters;
        var methodDisplayName = BindingPlan.MethodDisplayName(reflectedMethod);

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

        // Unlike Compono.MSTest's ITestDataSource (a separate GetData/GetDisplayName call pair with
        // no shared row/context object between them), NUnit's ITestBuilder hands this attribute full
        // ownership of the constructed TestMethod in the same call the row is built in - the seed is
        // set directly here, with no cross-call correlation problem to solve.
        var testMethod = new NUnitTestCaseBuilder().BuildTestMethod(method, suite, new TestCaseParameters(values));
        testMethod.Name = string.Create(CultureInfo.InvariantCulture, $"{reflectedMethod.Name}(Compono, seed: {row.Seed})");

        return [testMethod];
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

    // The "\n\nSeed: {value}" convention every Compono.NUnit-authored pre-composition failure uses,
    // matching Compono.XunitV3/Compono.TUnit/Compono.MSTest's identical convention - private
    // protected, not private, since ComposeAttribute{TProfile,TConfig} reuses this exact convention
    // for its own pre-composer negative-seed check, which must run before ApplyProfile does any
    // config/profile binding work, i.e. before a CompositionRow (and this method's usual row.Seed
    // source) exists.
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
