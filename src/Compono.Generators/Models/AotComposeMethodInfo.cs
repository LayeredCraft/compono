using Compono.Generators.Diagnostics;
using Compono.Generators.Types;

namespace Compono.Generators.Models;

/// <summary>
/// One <c>Compono.XunitV3.Aot.ComposeAttribute</c>-attributed test method - per ADR-0066/PLAN-0066,
/// this drives a generated <c>RegisteredEngineConfig.RegisterTheoryDataRowFactory(...)</c>
/// registration, one per method (not deduped by parameter type the way <see cref="RowInvokerTypeInfo"/>
/// is - each method needs its own factory closure, in its own declared parameter order).
/// </summary>
internal sealed record AotComposeMethodInfo(
    string FullyQualifiedTestClassName,
    string MethodName,
    EquatableArray<AotComposeParameterInfo> Parameters,
    EquatableArray<DiagnosticInfo> Diagnostics,
    AotProfileInfo? Profile = null);

/// <summary>
/// One parameter of an <see cref="AotComposeMethodInfo"/> method, in declaration order - everything
/// the generated factory closure needs to emit a <c>row.Resolve&lt;T&gt;(descriptor)</c> call with a
/// compile-time-known <c>T</c>.
/// </summary>
internal sealed record AotComposeParameterInfo(
    string Name,
    string FullyQualifiedTypeName,
    int Ordinal,
    bool IsNullable);

/// <summary>
/// The profile-selection shape of a <c>[Compose&lt;TProfile&gt;]</c>/<c>[Compose&lt;TProfile,
/// TConfig&gt;]</c>-attributed method (ADR-0067/PLAN-0067) - everything the generated factory closure
/// needs to construct and apply the profile with no reflection. <see cref="FullyQualifiedConfigTypeName"/>/
/// <see cref="ConfigArguments"/> are <see langword="null"/>/empty for the one-type-parameter
/// (<c>[Compose&lt;TProfile&gt;]</c>) form, which applies <c>TProfile</c> via
/// <c>CompositionBuilder.AddProfile&lt;TProfile&gt;()</c> directly - no config type exists in that
/// form.
/// </summary>
internal sealed record AotProfileInfo(
    string FullyQualifiedProfileTypeName,
    string? FullyQualifiedConfigTypeName,
    EquatableArray<AotProfileConfigArgumentInfo> ConfigArguments);

/// <summary>
/// One already-rendered profile configuration argument (<c>[Compose&lt;TProfile, TConfig&gt;]</c>'s
/// own constructor arguments) - the C#-source-literal text
/// <see cref="Emitters.TypedConstantLiteralRenderer"/> produced at discovery time, in
/// <c>TConfig</c>'s constructor's own declared parameter order.
/// </summary>
/// <param name="RenderedLiteral">
/// The argument's own literal C# source text (<c>"value"</c>, <c>(global::Ns.SomeEnum)1</c>, ...).
/// </param>
/// <param name="FullyQualifiedParameterTypeName">
/// The selected constructor parameter's own declared type - the generated call wraps
/// <see cref="RenderedLiteral"/> in an explicit cast to this type (PR #140 Codex review round 5): the
/// generated registration lives in the *consumer's own assembly*, so an internal-but-accessible
/// sibling constructor overload with a more specific parameter type could otherwise win ordinary C#
/// overload resolution instead of the one <c>Compono.Generators</c> actually selected and validated
/// (e.g. a public <c>TConfig(object)</c> plus an internal <c>TConfig(string)</c>, with a string
/// literal argument - <c>Compono.XunitV3.Binding.ConfigProfileBinder</c> never has this problem,
/// since <c>ConstructorInfo.Invoke</c> invokes the exact constructor it resolved, with no overload
/// resolution involved at all).
/// </param>
internal sealed record AotProfileConfigArgumentInfo(string RenderedLiteral, string FullyQualifiedParameterTypeName);
