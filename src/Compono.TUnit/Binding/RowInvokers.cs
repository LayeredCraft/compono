using System.Reflection;

namespace Compono.TUnit.Binding;

/// <summary>
/// Non-generic delegate shapes every cached invoker is adapted to, regardless of the closed
/// parameter type - what lets a row call an invoker with no reflection at all, only an ordinary
/// delegate invocation. Duplicated from <c>Compono.XunitV3.Binding.RowInvokers</c> per
/// <c>docs/adr/0040-compono-tunit-package-design.md</c>'s binding-logic decision.
/// </summary>
internal delegate object? ResolveInvoker(CompositionRow row, in CompositionRequestDescriptor descriptor);

/// <inheritdoc cref="ResolveInvoker"/>
internal delegate object? ResolveSharedInvoker(CompositionRow row, in CompositionRequestDescriptor descriptor);

/// <inheritdoc cref="ResolveInvoker"/>
internal delegate void ShareExplicitInvoker(CompositionRow row, in CompositionRequestDescriptor descriptor, object? value);

/// <summary>
/// Closes <see cref="CompositionRow"/>'s generic <c>Resolve</c>/<c>ResolveShared</c>/
/// <c>ShareExplicit</c> methods over one parameter type via <see cref="MethodInfo.MakeGenericMethod"/>
/// - exactly once per parameter, while <see cref="BindingPlan.Build"/> constructs the cached binding
/// plan - then wraps each in one of the non-generic delegate shapes above so the per-row binding
/// path never calls <c>MakeGenericMethod</c>/<c>MethodInfo.Invoke</c> at all.
/// </summary>
internal static class RowInvokers
{
    private static readonly MethodInfo ResolveMethod =
        typeof(RowInvokers).GetMethod(nameof(InvokeResolve), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo ResolveSharedMethod =
        typeof(RowInvokers).GetMethod(nameof(InvokeResolveShared), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo ShareExplicitMethod =
        typeof(RowInvokers).GetMethod(nameof(InvokeShareExplicit), BindingFlags.NonPublic | BindingFlags.Static)!;

    public static (ResolveInvoker Resolve, ResolveSharedInvoker ResolveShared, ShareExplicitInvoker ShareExplicit) Build(Type parameterType)
    {
        var resolve = (ResolveInvoker)Delegate.CreateDelegate(typeof(ResolveInvoker), ResolveMethod.MakeGenericMethod(parameterType));
        var resolveShared = (ResolveSharedInvoker)Delegate.CreateDelegate(typeof(ResolveSharedInvoker), ResolveSharedMethod.MakeGenericMethod(parameterType));
        var shareExplicit = (ShareExplicitInvoker)Delegate.CreateDelegate(typeof(ShareExplicitInvoker), ShareExplicitMethod.MakeGenericMethod(parameterType));

        return (resolve, resolveShared, shareExplicit);
    }

    // Each helper's own declared return type is object?, not T, so the T -> object boxing
    // conversion a value-typed T needs happens inside the closed method body itself (compiled per
    // closed T) - what makes an exact, non-covariant CreateDelegate binding possible.
    private static object? InvokeResolve<T>(CompositionRow row, in CompositionRequestDescriptor descriptor) =>
        row.Resolve<T>(descriptor);

    private static object? InvokeResolveShared<T>(CompositionRow row, in CompositionRequestDescriptor descriptor) =>
        row.ResolveShared<T>(descriptor);

    // Safe: BindingPlan's inline-value validation always runs before this is ever invoked, so
    // `value` is already known to be null-or-assignable for this exact closed T.
    private static void InvokeShareExplicit<T>(CompositionRow row, in CompositionRequestDescriptor descriptor, object? value) =>
        row.ShareExplicit(descriptor, (T)value!);
}
