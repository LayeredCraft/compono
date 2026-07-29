using Compono;

namespace Compono.Providers;

/// <summary>
/// The real stage-7 built-in <see cref="ICompositionProvider"/> collection <see cref="Composer"/>
/// wires into every production <see cref="CompositionContext"/> - collections aren't in this list;
/// they dispatch via <see cref="CollectionPlanCache{T}"/> instead, per
/// <c>docs/adr/0010-composition-request-pipeline-and-diagnostics-tracing.md</c>'s third amendment.
/// </summary>
internal static class BuiltInProviders
{
    internal static readonly IReadOnlyList<ICompositionProvider> Default =
    [
        new PrimitiveValueProvider(),
        new EnumValueProvider(),
        new NullableValueProvider(),
    ];
}
