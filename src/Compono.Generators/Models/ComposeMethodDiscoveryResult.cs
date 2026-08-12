using Compono.Generators.Types;

namespace Compono.Generators.Models;

/// <summary>
/// The result of walking one <c>[Compose]</c>-family-attributed method's parameters: the ordinary
/// plan-generation closure (<see cref="ClosureResult"/>, same as every other discovery path) plus,
/// separately, every dispatch-eligible parameter type needing a <c>RowInvokerRegistry</c> registration
/// (<see cref="RowInvokerTypes"/>, ADR-0041 Amendment 2). Kept as its own field rather than folded into
/// <see cref="TransitiveClosureResult.Types"/>, which stays exactly what it always was - a
/// plan-generation worklist, not a complete parameter-type inventory.
/// </summary>
internal sealed record ComposeMethodDiscoveryResult(
    TransitiveClosureResult ClosureResult,
    EquatableArray<RowInvokerTypeInfo> RowInvokerTypes)
{
    public EquatableArray<DiscoveredTypeInfo> Types => ClosureResult.Types;

    public EquatableArray<DiscoveredCollectionInfo> Collections => ClosureResult.Collections;
}
