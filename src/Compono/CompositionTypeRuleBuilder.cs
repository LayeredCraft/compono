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

    /// <summary>
    /// Selects <typeparamref name="T"/>'s parameterless constructor, for <c>Compono.Generators</c>'
    /// compile-time composition plan - the arity-0 counterpart to <see cref="UseConstructor{T1}"/>,
    /// needed because C# has no empty generic type-argument-list syntax to select a parameterless
    /// constructor through the generic overloads alone. See
    /// <see cref="UseConstructor{T1}"/>'s remarks - every rule there (compile-time-only marker,
    /// <c>Register&lt;T&gt;</c> distinction, compilation-wide scope, <c>CMP0033</c>/<c>CMP0034</c>)
    /// applies identically here.
    /// </summary>
    public void UseConstructor() { }

    /// <summary>
    /// Selects the constructor of <typeparamref name="T"/> whose parameter types are exactly
    /// <c>(T1)</c>, in order, for <c>Compono.Generators</c>' compile-time composition plan -
    /// <typeparamref name="T"/> is still composed by Compono exactly as an unambiguous type would
    /// be (every parameter resolved through the ordinary composition graph, recursively), the
    /// consumer only identifies which constructor to use. See
    /// <c>docs/adr/0052-compile-time-composition-discovery-boundary-for-registered-and-nested-resolved-types.md</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is a compile-time-only configuration signal, not a runtime factory.</b>
    /// <c>Compono.Generators</c> reads this call's own source (its containing type argument and
    /// this method's own type arguments) to select <typeparamref name="T"/>'s constructor for the
    /// single, compilation-wide generated composition plan - this method itself does nothing at
    /// runtime. Contrast with <see cref="Use(Func{ICompositionContext, T})"/>, which registers a
    /// real runtime factory the consumer fully controls (including calling arbitrary code, wrapping
    /// an existing instance, or supplying a value Compono could never compose on its own) - use
    /// <c>UseConstructor</c> when Compono should keep composing <typeparamref name="T"/> normally
    /// and you only need to say which entry point to use; use <see cref="Use(Func{ICompositionContext, T})"/>
    /// when you need to supply or construct <typeparamref name="T"/> yourself.
    /// </para>
    /// <para>
    /// <b>Scope is compilation-wide, not per-profile.</b> A generated composition plan is one plan
    /// per type, shared by every composition path that reaches it - not a per-profile variant. A
    /// selection made anywhere in the compilation applies everywhere <typeparamref name="T"/> is
    /// composed; a second, different selection for the same <typeparamref name="T"/> anywhere in
    /// the compilation is a compile-time conflict (<c>CMP0033</c>); an identical repeated selection
    /// is accepted (idempotent). Per-profile constructor selection is not supported.
    /// </para>
    /// <para>
    /// If no constructor of <typeparamref name="T"/> has exactly this parameter-type list (in this
    /// order), this is a compile-time diagnostic (<c>CMP0034</c>), not a silent fallback to another
    /// constructor. If <typeparamref name="T"/> already has exactly one accessible constructor, this
    /// call is unnecessary but harmless.
    /// </para>
    /// </remarks>
    /// <typeparam name="T1">The first (and only) parameter type of the constructor to select.</typeparam>
    public void UseConstructor<T1>() { }

    /// <inheritdoc cref="UseConstructor{T1}"/>
    /// <typeparam name="T1">The constructor's first parameter type, in order.</typeparam>
    /// <typeparam name="T2">The constructor's second parameter type, in order.</typeparam>
    public void UseConstructor<T1, T2>() { }

    /// <inheritdoc cref="UseConstructor{T1}"/>
    /// <typeparam name="T1">The constructor's first parameter type, in order.</typeparam>
    /// <typeparam name="T2">The constructor's second parameter type, in order.</typeparam>
    /// <typeparam name="T3">The constructor's third parameter type, in order.</typeparam>
    public void UseConstructor<T1, T2, T3>() { }

    /// <inheritdoc cref="UseConstructor{T1}"/>
    /// <typeparam name="T1">The constructor's first parameter type, in order.</typeparam>
    /// <typeparam name="T2">The constructor's second parameter type, in order.</typeparam>
    /// <typeparam name="T3">The constructor's third parameter type, in order.</typeparam>
    /// <typeparam name="T4">The constructor's fourth parameter type, in order.</typeparam>
    public void UseConstructor<T1, T2, T3, T4>() { }

    /// <inheritdoc cref="UseConstructor{T1}"/>
    /// <typeparam name="T1">The constructor's first parameter type, in order.</typeparam>
    /// <typeparam name="T2">The constructor's second parameter type, in order.</typeparam>
    /// <typeparam name="T3">The constructor's third parameter type, in order.</typeparam>
    /// <typeparam name="T4">The constructor's fourth parameter type, in order.</typeparam>
    /// <typeparam name="T5">The constructor's fifth parameter type, in order.</typeparam>
    public void UseConstructor<T1, T2, T3, T4, T5>() { }

    // Arity bound rationale (ADR-0052): surveyed real constructor arities across this repo's own
    // dogfooding consumers (alexa-vox-craft, trivia-platform, trivia-manager, cosmere-tracker,
    // lightsaber-skill) - 5-parameter constructors are already rare, nothing observed above that.
    // 6 is one full parameter of headroom past the largest real shape seen, not Func<>'s own
    // unrelated 16-parameter ceiling copied by default. A 7th-parameter constructor selection is a
    // genuine C# compile error (no matching UseConstructor overload) - immediately actionable at
    // the call site, never a silent fallback to a different constructor.
    /// <inheritdoc cref="UseConstructor{T1}"/>
    /// <typeparam name="T1">The constructor's first parameter type, in order.</typeparam>
    /// <typeparam name="T2">The constructor's second parameter type, in order.</typeparam>
    /// <typeparam name="T3">The constructor's third parameter type, in order.</typeparam>
    /// <typeparam name="T4">The constructor's fourth parameter type, in order.</typeparam>
    /// <typeparam name="T5">The constructor's fifth parameter type, in order.</typeparam>
    /// <typeparam name="T6">The constructor's sixth parameter type, in order.</typeparam>
    public void UseConstructor<T1, T2, T3, T4, T5, T6>() { }
}
