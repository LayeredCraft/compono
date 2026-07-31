namespace Compono;

/// <summary>
/// What an <see cref="ICompositionValueProvider"/> reports for one <see cref="CompositionProviderRequest"/>.
/// </summary>
/// <remarks>
/// Deliberately only two cases, mirroring the engine's own internal provider result contract: a
/// public provider can report that it doesn't apply, or that it produced a value - never a
/// stronger "failure." An unhandled exception a provider's own
/// <see cref="ICompositionValueProvider.TryProvide"/> implementation throws propagates uncaught,
/// exactly like an internal pipeline-stage provider's exception does today. See
/// <c>docs/adr/0024-public-provider-extensibility-model.md</c>.
/// </remarks>
public readonly struct CompositionProviderResult
{
    private CompositionProviderResult(bool isHandled, object? value)
    {
        IsHandled = isHandled;
        Value = value;
    }

    /// <summary>The provider does not handle this request.</summary>
    public static CompositionProviderResult NotHandled => default;

    /// <summary>The provider produced <paramref name="value"/> for this request.</summary>
    public static CompositionProviderResult Handled(object? value) => new(isHandled: true, value);

    // Internal - a public provider constructs a result only through the two static members above,
    // never by inspecting or round-tripping one it didn't just receive.
    internal bool IsHandled { get; }

    internal object? Value { get; }
}
