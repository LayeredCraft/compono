using System.Diagnostics.Metrics;

namespace Compono.Metrics;

/// <summary>
/// Direct inspection and fluent verification over any <see cref="IMeterFactory"/> - no assertion framework
/// required. Every method here requires the <see cref="IMeterFactory"/> it's called on to actually be a
/// <see cref="CapturingMeterFactory"/> (produced by <see cref="CompositionBuilderExtensions.UseMetrics"/>, or
/// directly via <see cref="CapturingMeterFactory"/>'s public constructor) - calling any of these against a
/// substitute, a <c>Compono.TestDoubles</c>-generated double, <c>DefaultMeterFactory</c>,
/// or any other non-Compono <see cref="IMeterFactory"/> throws immediately, diagnostically, rather than
/// returning an empty/default result. See docs/adr/0072-compono-metrics-testing-support-package.md's
/// "Precedence and misuse" section.
/// </summary>
public static class MeterFactoryTestingExtensions
{
    /// <summary>
    /// Every measurement captured for instruments named <paramref name="instrumentName"/>, across every
    /// meter, merged in capture order (ADR-0072 selection rule 7). A new list is returned on each call;
    /// later captures, <see cref="ClearMeasurements"/>, and <see cref="CapturingMeterFactory.Dispose"/> never
    /// change a list already returned.
    /// </summary>
    /// <typeparam name="T">The measurement value type - one of the seven supported types.</typeparam>
    /// <param name="factory">The capturing factory.</param>
    /// <param name="instrumentName">The instrument name to match (ordinal, case-sensitive).</param>
    /// <exception cref="InvalidOperationException"><paramref name="factory"/> is not a
    /// <see cref="CapturingMeterFactory"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="instrumentName"/> is null or empty; or
    /// <typeparamref name="T"/> is not one of the seven supported measurement value types.</exception>
    public static IReadOnlyList<CapturedMeasurement<T>> GetMeasurements<T>(
        this IMeterFactory factory,
        string instrumentName)
        where T : struct =>
        RequireCapturing(factory).Select<T>(null, ValidateSelection<T>(instrumentName)).Measurements;

    /// <summary>
    /// Every measurement captured for instruments named <paramref name="instrumentName"/> in the meter named
    /// <paramref name="meterName"/>, merged in capture order. A new list is returned on each call.
    /// </summary>
    /// <typeparam name="T">The measurement value type - one of the seven supported types.</typeparam>
    /// <param name="factory">The capturing factory.</param>
    /// <param name="meterName">The meter name to match (ordinal, case-sensitive).</param>
    /// <param name="instrumentName">The instrument name to match (ordinal, case-sensitive).</param>
    /// <exception cref="InvalidOperationException"><paramref name="factory"/> is not a
    /// <see cref="CapturingMeterFactory"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="meterName"/> or <paramref name="instrumentName"/>
    /// is null or empty; or <typeparamref name="T"/> is not one of the seven supported measurement value
    /// types.</exception>
    public static IReadOnlyList<CapturedMeasurement<T>> GetMeasurements<T>(
        this IMeterFactory factory,
        string meterName,
        string instrumentName)
        where T : struct =>
        RequireCapturing(factory)
            .Select<T>(ValidateName(meterName), ValidateSelection<T>(instrumentName))
            .Measurements;

    /// <summary>
    /// The entry point for fluent verification, for example
    /// <c>meters.Verify&lt;long&gt;("shop.orders.placed").WithTag("region", "eu").Once()</c>. The selection is
    /// resolved here, when the builder is created, so a meter ambiguity, type conflict, or type mismatch
    /// throws before any filter is added.
    /// </summary>
    /// <typeparam name="T">The measurement value type - one of the seven supported types.</typeparam>
    /// <param name="factory">The capturing factory.</param>
    /// <param name="instrumentName">The instrument name to match (ordinal, case-sensitive).</param>
    /// <exception cref="InvalidOperationException"><paramref name="factory"/> is not a
    /// <see cref="CapturingMeterFactory"/>; the name matches instruments in more than one meter; the name
    /// matches instruments that record more than one value type; or the matching instruments record a type
    /// other than <typeparamref name="T"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="instrumentName"/> is null or empty; or
    /// <typeparamref name="T"/> is not one of the seven supported measurement value types.</exception>
    public static MeasurementVerificationBuilder<T> Verify<T>(
        this IMeterFactory factory,
        string instrumentName)
        where T : struct =>
        new(RequireCapturing(factory).Select<T>(null, ValidateSelection<T>(instrumentName)), instrumentName, null);

    /// <summary>
    /// The entry point for fluent verification scoped to one meter name. The selection is resolved here, when
    /// the builder is created.
    /// </summary>
    /// <typeparam name="T">The measurement value type - one of the seven supported types.</typeparam>
    /// <param name="factory">The capturing factory.</param>
    /// <param name="meterName">The meter name to match (ordinal, case-sensitive).</param>
    /// <param name="instrumentName">The instrument name to match (ordinal, case-sensitive).</param>
    /// <exception cref="InvalidOperationException"><paramref name="factory"/> is not a
    /// <see cref="CapturingMeterFactory"/>; the name matches instruments that record more than one value type;
    /// or the matching instruments record a type other than <typeparamref name="T"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="meterName"/> or <paramref name="instrumentName"/>
    /// is null or empty; or <typeparamref name="T"/> is not one of the seven supported measurement value
    /// types.</exception>
    public static MeasurementVerificationBuilder<T> Verify<T>(
        this IMeterFactory factory,
        string meterName,
        string instrumentName)
        where T : struct =>
        new(RequireCapturing(factory).Select<T>(ValidateName(meterName), ValidateSelection<T>(instrumentName)),
            instrumentName,
            meterName);

    /// <summary>
    /// Polls this factory's observable instruments: calls this factory's listener's
    /// <see cref="MeterListener.RecordObservableInstruments"/>. Callbacks run synchronously on the calling
    /// thread and only for this factory's observable instruments; each observation appends measurements, so
    /// calling it twice records twice. Reads never poll. A no-op before the first
    /// <see cref="CapturingMeterFactory.Create"/> or after disposal.
    /// </summary>
    /// <param name="factory">The capturing factory.</param>
    /// <exception cref="InvalidOperationException"><paramref name="factory"/> is not a
    /// <see cref="CapturingMeterFactory"/>.</exception>
    public static void RecordObservableInstruments(this IMeterFactory factory) =>
        RequireCapturing(factory).RecordObservableInstruments();

    /// <summary>Discards every measurement captured by <paramref name="factory"/> so far. Works before and
    /// after disposal.</summary>
    /// <param name="factory">The capturing factory.</param>
    /// <exception cref="InvalidOperationException"><paramref name="factory"/> is not a
    /// <see cref="CapturingMeterFactory"/>.</exception>
    public static void ClearMeasurements(this IMeterFactory factory) =>
        RequireCapturing(factory).ClearMeasurements();

    private static CapturingMeterFactory RequireCapturing(IMeterFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        if (factory is CapturingMeterFactory capturing)
            return capturing;

        throw new InvalidOperationException(
            $"'{factory.GetType()}' is not a Compono.Metrics capturing meter factory. This member is only usable " +
            "against an IMeterFactory produced by Compono.Metrics (UseMetrics(), or a directly constructed " +
            "CapturingMeterFactory). If this IMeterFactory was composed, confirm UseMetrics() is registered " +
            "and comes before UseNSubstitute() or any other catch-all test-double provider - Compono's stage-6 " +
            "test-double providers resolve in registration order, and whichever provider is registered first " +
            "wins for a given request. A Compono.TestDoubles generated double only competes when " +
            "ComponoMetricsOwnsMeterFactory has been set to false; by default Compono.Metrics owns " +
            "IMeterFactory and UseGeneratedTestDoubles() never doubles it. If this IMeterFactory came " +
            "from a host's IServiceProvider (for example AddMetrics()'s DefaultMeterFactory), register a " +
            "CapturingMeterFactory instance with services.AddSingleton<IMeterFactory>(meters) instead. The " +
            "container resolves a single IMeterFactory to its last registration, so add that call after anything " +
            "else that registers IMeterFactory (including AddMetrics() or a host's default) - the later " +
            "registration wins. Don't use TryAddSingleton to replace a factory: it would keep the existing " +
            "registration.");
    }

    private static string ValidateSelection<T>(string instrumentName)
        where T : struct
    {
        var name = ValidateName(instrumentName);

        if (!InstrumentValueTypes.IsSupported(typeof(T)))
        {
            throw new ArgumentException(
                $"Compono.Metrics supports only the seven System.Diagnostics.Metrics measurement value types " +
                $"({InstrumentValueTypes.SupportedList}), but the requested measurement type was " +
                $"'{InstrumentValueTypes.DisplayName(typeof(T))}'. No instrument can record that type, so this " +
                "selection can never match.",
                nameof(T));
        }

        return name;
    }

    private static string ValidateName(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        return name;
    }
}
