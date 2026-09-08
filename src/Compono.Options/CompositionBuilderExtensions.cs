using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Options;

namespace Compono.Options;

/// <summary>
/// <see cref="CompositionBuilder"/> extension wiring a <see cref="TestOptionsSource{T}"/> into
/// composition - the one call a test or <see cref="ICompositionProfile"/> needs to coherently satisfy
/// <see cref="IOptions{TOptions}"/>, <see cref="IOptionsSnapshot{TOptions}"/>, and
/// <see cref="IOptionsMonitor{TOptions}"/> for a settings type from one source. See
/// docs/adr/0061-compono-options-testing-support.md's "Decision Outcome" for the full identity-model
/// rationale.
/// </summary>
public static class CompositionBuilderExtensions
{
    /// <summary>
    /// Registers <paramref name="source"/> so that, within one composition graph: <see cref="IOptionsMonitor{TOptions}"/>
    /// and <see cref="IOptions{TOptions}"/> both resolve to a single shared identity (the source itself
    /// for Monitor; one frozen view captured once for <see cref="IOptions{TOptions}"/>), while
    /// <see cref="IOptionsSnapshot{TOptions}"/> resolves to a fresh frozen view - capturing
    /// <paramref name="source"/>'s then-current state - on every resolution.
    /// </summary>
    /// <typeparam name="T">The settings type <paramref name="source"/> is the source of truth for.</typeparam>
    /// <param name="builder">The builder to configure.</param>
    /// <param name="source">The source a test constructed and configured directly.</param>
    public static CompositionBuilder UseOptions<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T>(this CompositionBuilder builder, TestOptionsSource<T> source)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(source);

        builder.Register<IOptionsMonitor<T>>(() => source).Share<IOptionsMonitor<T>>();
        builder.Register<IOptions<T>>(() => new FrozenOptionsView<T>(source)).Share<IOptions<T>>();
        builder.Register<IOptionsSnapshot<T>>(() => new FrozenOptionsView<T>(source));

        return builder;
    }
}
