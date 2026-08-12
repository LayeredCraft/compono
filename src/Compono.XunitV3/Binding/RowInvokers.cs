namespace Compono.XunitV3.Binding;

/// <summary>
/// Looks up the three non-generic dispatch delegates <see cref="Compono.RowInvokerRegistry"/> holds
/// for one parameter type - populated by a generated module initializer in this (or, for a
/// provider-resolved leaf type shared across assemblies, any) consuming assembly, per ADR-0041. No
/// reflection anywhere in this file: <see cref="Build"/> is an ordinary <see cref="Type"/>-keyed
/// dictionary lookup, not a <c>MethodInfo.MakeGenericMethod</c> call - the property that makes this
/// path Native AOT-safe (a <see cref="Type"/> object is an ordinary runtime value; only *dynamic
/// instantiation* of a generic method/type from one is unsafe, and this file never does that).
/// </summary>
internal static class RowInvokers
{
    public static (ResolveInvoker Resolve, ResolveSharedInvoker ResolveShared, ShareExplicitInvoker ShareExplicit) Build(Type parameterType)
    {
        if (!RowInvokerRegistry.TryGet(parameterType, out var resolve, out var resolveShared, out var shareExplicit))
        {
            // BindingPlan.ValidateSignature already rejects every shape that could ever cause this (a
            // ref struct/pointer-typed by-value parameter) before Build is reached, and every other
            // dispatch-eligible parameter type gets a real generator-emitted registration - so
            // reaching here means the consuming assembly's module initializers genuinely haven't run
            // yet, or (an inaccessible parameter type, CMP0013) the generator already refused to emit
            // one. Either way this is an environmental/build-configuration failure, not an expected
            // outcome a caller should branch on - an exception, not a silent fallback, is the right
            // signal per coding-standards.md's error-handling rules.
            throw new CompositionException(
                $"No row-binding dispatch is registered for '{parameterType}' - either its containing " +
                "assembly's generated module initializers haven't run yet, or the generator refused to " +
                "register it (see CMP0013 for an inaccessible parameter type).");
        }

        return (resolve, resolveShared, shareExplicit);
    }
}
