namespace Compono;

/// <summary>
/// Opts a type into generated composition when discovery can't find it on its own — a
/// plan-generation request equivalent to a <c>Composer.Create&lt;T&gt;()</c> call site, per
/// <c>docs/adr/0004-composition-plan-discovery-and-dispatch.md</c>'s hybrid discovery decision.
/// </summary>
/// <remarks>
/// Most types never need this: any type reachable from a <c>Create&lt;T&gt;()</c> call site in the
/// compilation (directly or through another type's constructor parameters) gets a generated plan
/// automatically. Apply <c>[Composable]</c> directly to a type this compilation owns; use the
/// assembly-level form (<c>[assembly: Composable(typeof(SomeType))]</c>) for a type in a referenced
/// assembly that can't be annotated. Both forms are equivalent requests and repeated requests for
/// the same type are deduplicated — prefer the type-level form whenever the type is owned locally.
/// </remarks>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Assembly,
    AllowMultiple = true, Inherited = false)]
public sealed class ComposableAttribute : Attribute
{
    /// <summary>
    /// Marks the annotated type as composable. Only valid on a type declaration — the assembly-level
    /// form requires <see cref="ComposableAttribute(System.Type)"/> to identify the target type.
    /// </summary>
    public ComposableAttribute()
    {
    }

    /// <summary>
    /// Requests a generated composition plan for <paramref name="type"/> — the form to use at
    /// assembly level, where there's no annotated type to infer the target from.
    /// </summary>
    /// <param name="type">The type to generate a composition plan for.</param>
    public ComposableAttribute(Type type)
    {
        Type = type;
    }

    /// <summary>
    /// The explicitly requested type, or <see langword="null"/> when the request targets the
    /// annotated type itself.
    /// </summary>
    public Type? Type { get; }
}
