using Xunit.v3;

namespace Compono.XunitV3.Aot;

/// <summary>
/// Composes an xUnit v3 theory row's parameters through Compono under xUnit v3's Native AOT pipeline,
/// with <typeparamref name="TProfile"/> applied to the underlying <see cref="Composer"/> - the AOT
/// counterpart to <c>Compono.XunitV3.ComposeAttribute&lt;TProfile&gt;</c>. Usage is identical:
/// <code>
/// [Theory]
/// [Compose&lt;MyProfile&gt;]
/// public void My_test(Widget widget) { ... }
/// </code>
/// </summary>
/// <typeparam name="TProfile">The profile to apply.</typeparam>
/// <remarks>
/// Marker-only, like the non-generic <see cref="ComposeAttribute"/> - discovered by
/// <c>Compono.Generators</c> at compile time (matched on this type's own arity-suffixed fully
/// qualified metadata name, <c>Compono.XunitV3.Aot.ComposeAttribute`1</c>, per
/// ADR-0067/PLAN-0067) and never invoked at runtime. The generated
/// <c>RegisteredEngineConfig.RegisterTheoryDataRowFactory(...)</c> registration constructs
/// <typeparamref name="TProfile"/> via a direct, compile-time-closed
/// <c>global::Compono.Composer.Create(b =&gt; b.AddProfile&lt;TProfile&gt;())</c> call - the identical
/// reflection-free construction <see cref="CompositionBuilder.AddProfile{TProfile}()"/> already uses in
/// both JIT and AOT modes today (a bare <c>new TProfile()</c> against a compile-time-closed generic
/// argument, never <c>Activator</c>/reflection), so this form needed no new AOT-safety work beyond the
/// attribute-discovery/codegen plumbing itself.
/// <para>
/// A profile type that doesn't implement <see cref="ICompositionProfile"/> or lacks a public
/// parameterless constructor is a compile error at the <c>[Compose&lt;TProfile&gt;]</c> use site (C#
/// enforces generic-attribute constraints there like any other generic type) - there is no compile-time
/// diagnostic or runtime check to design for that case, identical to
/// <c>Compono.XunitV3.ComposeAttribute&lt;TProfile&gt;</c>'s own remarks.
/// </para>
/// <para>
/// Derives directly from <see cref="DataAttribute"/>, <b>not</b> from the non-generic
/// <see cref="ComposeAttribute"/> - that type is deliberately <see langword="sealed"/> (see its own
/// remarks) since AOT discovery matches purely by metadata name, never by inheritance, so sharing a
/// base class here would buy nothing while opening an undiscovered-subclass hazard specific to this
/// marker-only attribute family (ADR-0067 Amendment 1).
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ComposeAttribute<TProfile> : DataAttribute
    where TProfile : ICompositionProfile, new();
