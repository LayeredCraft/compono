using Compono.Generators.Diagnostics;
using Compono.Generators.Types;

namespace Compono.Generators.Models;

/// <summary>
/// A type discovered at a <c>Composer.Create&lt;T&gt;()</c> call site
/// (<c>docs/adr/0004-composition-plan-discovery-and-dispatch.md</c>), with its selected constructor's
/// parameters (or a diagnostic explaining why one couldn't be selected).
/// </summary>
internal sealed record DiscoveredTypeInfo(
    string Namespace,
    string TypeName,
    string FullyQualifiedName,
    EquatableArray<ConstructorParameterInfo> Parameters,
    EquatableArray<RequiredMemberInfo> RequiredMembers,
    EquatableArray<DiagnosticInfo> Diagnostics)
{
    public string PlanClassName => $"{TypeName}CompositionPlan";
}
