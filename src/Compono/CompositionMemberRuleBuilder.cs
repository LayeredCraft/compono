namespace Compono;

/// <summary>
/// A thin, member-scoped view over a <see cref="CompositionBuilder"/>'s shared accumulator state -
/// returned by <see cref="CompositionTypeRuleBuilder{T}.Member{TMember}"/>. Registers either a member
/// value rule (<see cref="Use(TMember)"/>/<see cref="Use(Func{ICompositionContext, TMember})"/>) or a
/// member-scoped collection-size override (<see cref="WithCollectionSize"/>) for the exact
/// (declaring type, member name) pair captured when <c>.Member(...)</c> was called. See
/// <c>docs/adr/0020-composition-configuration-rules.md</c>.
/// </summary>
/// <typeparam name="TParent">The declaring type this rule is scoped to.</typeparam>
/// <typeparam name="TMember">The member's type.</typeparam>
public sealed class CompositionMemberRuleBuilder<TParent, TMember>
{
    private readonly CompositionBuilder _builder;
    private readonly (Type DeclaringType, string MemberName) _key;

    internal CompositionMemberRuleBuilder(CompositionBuilder builder, Type declaringType, string memberName)
    {
        _builder = builder;
        _key = (declaringType, memberName);
    }

    /// <summary>
    /// Registers a member rule that always produces <paramref name="value"/> for this member.
    /// </summary>
    /// <param name="value">The value this rule always produces.</param>
    public CompositionBuilder Use(TMember value)
    {
        _builder.AddMemberRule(_key, typeof(TMember), _ => value);
        return _builder;
    }

    /// <summary>
    /// Registers a member rule whose value is produced by <paramref name="factory"/> for this member.
    /// </summary>
    /// <param name="factory">Produces the rule's value, given the resolving context.</param>
    public CompositionBuilder Use(Func<ICompositionContext, TMember> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _builder.AddMemberRule(_key, typeof(TMember), context => factory(context));
        return _builder;
    }

    /// <summary>
    /// Overrides the default collection size for this member only, following the same
    /// <see cref="CompositionBuilder.WithCollectionSize"/> precedence: this override wins over the
    /// global default and the built-in size of <c>3</c>.
    /// </summary>
    /// <param name="size">The collection size to build for this member.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> is negative.</exception>
    public CompositionBuilder WithCollectionSize(int size)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(size);
        _builder.AddMemberCollectionSize(_key, typeof(TMember), size);
        return _builder;
    }
}
