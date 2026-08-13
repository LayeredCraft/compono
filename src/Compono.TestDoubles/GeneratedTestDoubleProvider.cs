namespace Compono;

/// <summary>
/// A stage-6 test-double provider that satisfies a request by looking up a factory already
/// registered into core <see cref="GeneratedTestDoubleRegistry"/> - populated by a
/// <c>Compono.Generators</c>-emitted <c>[ModuleInitializer]</c> per discovered interface, never by
/// this type. Registered via <c>CompositionBuilderExtensions.UseGeneratedTestDoubles()</c>. See
/// ADR-0043's "Runtime activation and precedence".
/// </summary>
public sealed class GeneratedTestDoubleProvider : ICompositionValueProvider
{
    /// <inheritdoc />
    public CompositionProviderResult TryProvide(in CompositionProviderRequest request, ICompositionContext context) =>
        GeneratedTestDoubleRegistry.TryCreate(request.RequestedType, out var value)
            ? CompositionProviderResult.Handled(value!)
            : CompositionProviderResult.NotHandled;
}
