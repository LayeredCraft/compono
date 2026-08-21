namespace Compono;

/// <summary>
/// The structural identity of one node in a <see cref="CompositionPath"/> - what a request was
/// <em>for</em> (a named constructor parameter, a collection index, a dictionary key/value role),
/// distinct from what <em>type</em> was requested there.
/// </summary>
/// <remarks>
/// Per <c>docs/adr/0012-composition-path-identity-and-deterministic-random-forking.md</c>, only
/// each variant's <c>Ordinal</c>/<c>Index</c> is stable identity fed into random-fork hashing -
/// <see cref="ConstructorParameter.Name"/>/<see cref="RequiredMember.Name"/> exist for diagnostic
/// display only.
/// </remarks>
internal abstract record PathSegment
{
    private PathSegment()
    {
    }

    /// <summary>A selected constructor's parameter, identified by its position in that constructor.</summary>
    internal sealed record ConstructorParameter(int Ordinal, string Name) : PathSegment;

    /// <summary>A required init-only member, identified by its generator-assigned declaration-order index.</summary>
    internal sealed record RequiredMember(int Ordinal, string Name) : PathSegment;

    /// <summary>An element of a sequence-shaped collection (array, <c>List&lt;T&gt;</c>, <c>HashSet&lt;T&gt;</c>).</summary>
    internal sealed record CollectionElement(int Index) : PathSegment;

    /// <summary>A dictionary entry's key, at the given entry position.</summary>
    internal sealed record DictionaryKey(int Index) : PathSegment;

    /// <summary>A dictionary entry's value, at the given entry position.</summary>
    internal sealed record DictionaryValue(int Index) : PathSegment;

    /// <summary>
    /// One descriptor-less <see cref="ICompositionContext.Resolve{TValue}()"/> call made inside a
    /// registration or configuration-rule factory, or a public
    /// <see cref="ICompositionValueProvider.TryProvide"/> invocation, identified by its call sequence
    /// within that one invocation - never by requested type. See
    /// <c>docs/adr/0019-registrations-and-service-provider-injection.md</c> and
    /// <c>docs/adr/0024-public-provider-extensibility-model.md</c>.
    /// </summary>
    internal sealed record ManualResolve(int Ordinal) : PathSegment;

    /// <summary>
    /// One of a test method's own parameters, identified by its position in the method's parameter
    /// list - a <see cref="CompositionRow"/>'s sibling top-level requests, as opposed to a
    /// constructor parameter or required member a generated plan is filling in. The seventh
    /// <see cref="PathSegment"/> kind. See
    /// <c>docs/adr/0021-row-composition-entry-point-for-test-framework-integrations.md</c>.
    /// </summary>
    internal sealed record TestParameter(int Ordinal, string Name) : PathSegment;

    /// <summary>
    /// One <see cref="CompositionContext.TryResolveConfigured"/> call - a runtime-<see cref="Type"/>
    /// request reaching only the scope/exact-registration/configuration-rule/provider stages, never
    /// a descriptor. Identified by its call sequence on the owning <see cref="CompositionContext"/>,
    /// same shape as <see cref="ManualResolve"/> - two sequential top-level
    /// <see cref="CompositionRow.TryResolveConfigured"/> calls on the same row ARE siblings under the
    /// row's pre-rooted path (PR #105 review, Codex: without this ordinal, every call forked from the
    /// identical parent random state via the identical fixed segment identity, so two different
    /// registrations/providers relying on randomness - <c>DeriveSeed()</c>, a nested composition, a
    /// Bogus-backed semantic provider - silently produced identical derived values instead of
    /// independent ones). The eighth <see cref="PathSegment"/> kind. See
    /// <c>docs/adr/0047-compono-dependencyinjection-configured-resolution-bridge.md</c>.
    /// </summary>
    internal sealed record ConfiguredResolution(int Ordinal) : PathSegment;
}
