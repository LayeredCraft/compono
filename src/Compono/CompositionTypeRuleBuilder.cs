using System.Linq.Expressions;
using System.Reflection;

namespace Compono;

/// <summary>
/// A thin, type-scoped view over a <see cref="CompositionBuilder"/>'s shared accumulator state -
/// returned by <see cref="CompositionBuilder.For{T}"/>. Calling <see cref="Use(T)"/>/
/// <see cref="Use(Func{ICompositionContext, T})"/> directly registers a type rule; calling
/// <see cref="Member{TMember}"/> first returns a further-scoped
/// <see cref="CompositionMemberRuleBuilder{TParent, TMember}"/> whose own <c>Use</c>/
/// <c>WithCollectionSize</c> register a member rule instead. See
/// <c>docs/adr/0020-composition-configuration-rules.md</c>.
/// </summary>
/// <typeparam name="T">The type this rule builder is scoped to.</typeparam>
public sealed class CompositionTypeRuleBuilder<T>
{
    private readonly CompositionBuilder _builder;

    internal CompositionTypeRuleBuilder(CompositionBuilder builder)
    {
        _builder = builder;
    }

    /// <summary>
    /// Registers a type rule that always produces <paramref name="value"/> - matches any stage-4
    /// request for exactly <typeparamref name="T"/>, regardless of which member/position requested it.
    /// </summary>
    /// <param name="value">The value this rule always produces.</param>
    public CompositionBuilder Use(T value)
    {
        _builder.AddTypeRule(typeof(T), _ => value);
        return _builder;
    }

    /// <summary>
    /// Registers a type rule whose value is produced by <paramref name="factory"/> - matches any
    /// stage-4 request for exactly <typeparamref name="T"/>, regardless of which member/position
    /// requested it.
    /// </summary>
    /// <param name="factory">Produces the rule's value, given the resolving context.</param>
    public CompositionBuilder Use(Func<ICompositionContext, T> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _builder.AddTypeRule(typeof(T), context => factory(context));
        return _builder;
    }

    /// <summary>
    /// Scopes this rule to a single member of <typeparamref name="T"/> - parsed immediately, at the
    /// point this method is called, not deferred to <c>Build()</c>.
    /// </summary>
    /// <typeparam name="TMember">The member's type.</typeparam>
    /// <param name="member">A direct property or field access, e.g. <c>x => x.Email</c>.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="member"/> is not a direct property or field access.
    /// </exception>
    public CompositionMemberRuleBuilder<T, TMember> Member<TMember>(Expression<Func<T, TMember>> member)
    {
        ArgumentNullException.ThrowIfNull(member);

        if (member.Body is not MemberExpression { Expression: ParameterExpression, Member: PropertyInfo or FieldInfo } memberExpression)
        {
            throw new ArgumentException(
                $"'{member}' is not a direct property or field access (e.g. 'x => x.Property') - " +
                $"{nameof(Member)}(...) cannot parse this expression into a member rule.",
                nameof(member));
        }

        // DeclaringType is never null here - PropertyInfo/FieldInfo obtained via reflection on a real
        // member access always reports the type that declares it.
        return new CompositionMemberRuleBuilder<T, TMember>(_builder, memberExpression.Member.DeclaringType!, memberExpression.Member.Name);
    }
}
