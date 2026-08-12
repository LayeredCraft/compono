namespace Compono.TUnit.Binding;

/// <summary>
/// One test method parameter's cached binding metadata - computed once per attribute instance's
/// first row, never rebuilt or re-reflected on the per-row path. Holds no row-scoped state: no
/// <see cref="CompositionRow"/>, no composed value. Duplicated from
/// <c>Compono.XunitV3.Binding.ParameterBindingPlan</c> per
/// <c>docs/adr/0040-compono-tunit-package-design.md</c>'s binding-logic decision.
/// </summary>
internal sealed class ParameterBindingPlan
{
    /// <summary>The parameter's declared name.</summary>
    public required string Name { get; init; }

    /// <summary>The parameter's declared type.</summary>
    public required Type ParameterType { get; init; }

    /// <summary>Whether the parameter is annotated <c>[Shared]</c>.</summary>
    public required bool IsShared { get; init; }

    /// <summary>
    /// The compact request descriptor for this parameter - every field is fixed at reflection time
    /// (kind, ordinal, name, declaring type, nullability), so the whole value, not just a template
    /// of it, is safe to cache and reuse across every row.
    /// </summary>
    public required CompositionRequestDescriptor Descriptor { get; init; }

    /// <summary>The cached, closed-once <see cref="CompositionRow.Resolve{TValue}(in CompositionRequestDescriptor)"/> invoker for this parameter's type.</summary>
    public required ResolveInvoker ResolveInvoker { get; init; }

    /// <summary>The cached, closed-once <see cref="CompositionRow.ResolveShared{TValue}"/> invoker for this parameter's type.</summary>
    public required ResolveSharedInvoker ResolveSharedInvoker { get; init; }

    /// <summary>The cached, closed-once <see cref="CompositionRow.ShareExplicit{TValue}"/> invoker for this parameter's type.</summary>
    public required ShareExplicitInvoker ShareExplicitInvoker { get; init; }
}
