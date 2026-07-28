namespace Compono.Benchmarks;

/// <summary>
/// A flat, parameterless representative type - the only shape <see cref="Composer.Create{T}"/>
/// can fully execute in Milestone 1 today.
/// </summary>
/// <remarks>
/// A type with a constructor parameter (even one that's itself composable, like
/// <c>docs/mvp.md</c>'s <c>Customer(Address HomeAddress)</c> exit-criteria example) still throws
/// <see cref="NotSupportedException"/> at <c>Compose</c> time: every constructor argument in
/// generated code is resolved via <c>context.Resolve&lt;TParam&gt;()</c>
/// (<c>src/Compono.Generators/Templates/CompositionPlan.scriban</c>), and Milestone 1's
/// placeholder <see cref="ICompositionContext"/> (<c>src/Compono/Composer.cs</c>) always throws
/// there regardless of whether <c>TParam</c> has its own generated plan - dispatching a
/// <c>Resolve&lt;TParam&gt;()</c> call to the matching <c>PlanCache&lt;TParam&gt;</c> is the real
/// provider-resolution pipeline, which is Milestone 2 scope. So a parameterless type is the only
/// shape this benchmark can honestly measure end-to-end until then.
/// </remarks>
public sealed record Leaf;
