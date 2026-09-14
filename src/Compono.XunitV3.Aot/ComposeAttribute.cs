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
/// Phase 1 scope (ADR-0066's Decision Outcome): this non-generic form itself supports plain
/// parameters only - no inline values, no <c>[Shared]</c>.
/// </para>
/// <para>
/// Deliberately <see langword="sealed"/> - unlike <c>Compono.XunitV3.ComposeAttribute</c> (whose
/// generic siblings genuinely extend its shared runtime state: a cached <see cref="Composer"/>,
/// cached binding delegates, a real <c>GetData</c> override), this type carries no functional state
/// or behavior at all for a subtype to extend, and AOT discovery matches purely on each closed
/// attribute type's own fully qualified metadata name (never on assignability/inheritance - see
/// <c>AotComposeMethodDiscovery</c>'s remarks). Inheriting from this type would buy a consumer
/// nothing functionally while creating a real hazard specific to this marker-only attribute family:
/// a consumer-authored subclass would compile without error but never be discovered by
/// <c>Compono.Generators</c> (its own metadata name wouldn't match any registered discovery
/// provider), silently never running - exactly the failure mode ADR-0066's <c>CMP0040</c> exists to
/// prevent for every other unsupported shape. <see cref="ComposeAttribute{TProfile}"/> and
/// <see cref="ComposeAttribute{TProfile, TConfig}"/> (ADR-0067/PLAN-0067) are independent marker
/// siblings instead - same short type name and <c>[AttributeUsage]</c> convention, own direct
/// <see cref="DataAttribute"/> base, no inheritance relationship to this type (ADR-0067 Amendment 1).
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ComposeAttribute : DataAttribute;
