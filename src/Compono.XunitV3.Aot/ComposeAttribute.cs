using Xunit.v3;

namespace Compono.XunitV3.Aot;

/// <summary>
/// Marks a theory method's parameters for Compono composition under xUnit v3's Native AOT pipeline -
/// the AOT counterpart to <c>Compono.XunitV3.ComposeAttribute</c>. Usage is identical:
/// <code>
/// [Theory]
/// [Compose]
/// public void My_test(Widget widget, string leaf) { ... }
/// </code>
/// </summary>
/// <remarks>
/// Unlike <c>Compono.XunitV3.ComposeAttribute</c>, this attribute carries no runtime composition
/// logic of its own - it is a thin marker, discovered by <c>Compono.Generators</c> at compile time
/// (matched on this type's own fully qualified metadata name, per ADR-0066/PLAN-0066) and never
/// invoked at runtime. xUnit's Native AOT pipeline supplies theory data through a generator-emitted
/// <c>RegisteredEngineConfig.RegisterTheoryDataRowFactory(...)</c> registration instead of calling
/// <see cref="DataAttribute.GetData"/> - the same pattern xUnit's own official
/// <c>AotCsvDataSource</c>/<c>AotRetryFact</c>/<c>AotTraitExtensibility</c> samples use for custom
/// data-attribute extensibility under Native AOT (RESEARCH-0032 §2). Requires
/// <c>Compono.Generators</c> (shipped inside the <c>Compono</c> package this package depends on) to
/// actually produce that registration - referencing this package alone, without its generator
/// dependency, leaves a <c>[Compose]</c>-attributed method undiscovered by xUnit's AOT pipeline with
/// no compile error, since a marker attribute with no matching registration is, from the compiler's
/// perspective, indistinguishable from a marker attribute nobody generates anything for.
/// <para>
/// Phase 1 scope (ADR-0066's Decision Outcome): plain parameters only - no inline values, no
/// <c>[Shared]</c>, no profile variants (<c>[Compose&lt;TProfile&gt;]</c>/
/// <c>[Compose&lt;TProfile, TConfig&gt;]</c>). This attribute's parameterless-only constructor and
/// lack of generic siblings enforce that scope structurally: there is no supported syntax to attempt
/// any of those forms with this package's current public surface.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ComposeAttribute : DataAttribute;
