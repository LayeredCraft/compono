namespace Compono.Generators.Models;

/// <summary>
/// One parameter of a generated test double's method member.
/// </summary>
/// <param name="EscapedName">
/// <c>@</c>-escaped per <c>RequiredMemberCollector.EscapeIdentifier</c>'s existing convention -
/// used everywhere this name is emitted as identifier syntax: the explicit interface implementation's
/// own parameter name (ADR-0043 Amendment 10, Finding X) and, for an overloaded member (ADR-0044
/// Amendment 1), the configuration extension's own parameter name too.
/// </param>
/// <param name="OriginalName">
/// The real, unescaped symbol name - never used where this name stands alone as an identifier token
/// (that's <see cref="EscapedName"/>'s job), only where it's spliced as a fragment inside a larger
/// synthetic identifier (ADR-0048's per-parameter matcher field/local names,
/// <c>{fieldName}_m_{OriginalName}</c>) - gluing an <c>@</c>-escaped name into the middle of another
/// identifier produces invalid syntax (<c>@</c> is only legal as an identifier's first character),
/// while a bare reserved word glued into a larger identifier needs no escaping at all (Codex review,
/// PR #106).
/// </param>
/// <param name="FullyQualifiedTypeName">This parameter's type, fully qualified.</param>
/// <param name="RefKindPrefix">
/// <c>""</c>, <c>"ref "</c>, <c>"out "</c>, <c>"in "</c>, or <c>"ref readonly "</c>, optionally
/// preceded by <c>"scoped "</c> and/or <c>"[global::System.Diagnostics.CodeAnalysis.UnscopedRef] "</c>
/// (see <c>TestDoubleAnalyzer.RefKindPrefixFor</c>) - only a member with no
/// <c>Configure()</c>/<c>Verify()</c> surface (<see cref="TestDoubleMemberInfo.HasConfigurationSurface"/>
/// <see langword="false"/>) can have a non-empty value here (ADR-0044 Amendment 5: a
/// <see langword="ref"/>/<see langword="out"/>/<see langword="in"/> parameter is an
/// overload-set-internal-unsupported shape, never a configurable one).
/// </param>
/// <param name="IsParams">
/// Whether the real overload declares this as its trailing <see langword="params"/> parameter - an
/// overloaded member's configuration extension (ADR-0044 Amendment 1) mirrors this so a call site
/// applicable to zero arguments against the real overload (e.g. <c>Speak(params ISsml[] parts)</c>
/// called as <c>Speak()</c>) stays applicable against the generated extension too.
/// </param>
/// <param name="DefaultValueExpression">
/// A C# literal expression for this parameter's optional default value (e.g. <c>"0"</c>, <c>"null"</c>,
/// <c>"default"</c>) - empty when the real parameter has no default value. Mirrored onto an
/// overloaded member's configuration extension for the same reason as <paramref name="IsParams"/>:
/// <c>M(int value = 0)</c> is callable as <c>M()</c> on the real interface, and the generated
/// extension needs to stay reachable the same way (Codex review, PR #88).
/// </param>
/// <param name="CallSiteRefKindPrefix">
/// The by-ref modifier to restate in an ARGUMENT list forwarding to this parameter -
/// <c>""</c>, <c>"ref "</c>, or <c>"out "</c> only, never <see cref="RefKindPrefix"/>'s full
/// declaration-site text. C# call-site rules genuinely differ from declaration-site rules here:
/// <see langword="ref"/>/<see langword="out"/> must be restated at the call site, but
/// <see langword="in"/>, <see langword="ref readonly"/>, <see langword="scoped"/>, and
/// <c>[UnscopedRef]</c> are all declaration-only concepts that accept a plain by-value argument
/// expression with no modifier at all - reusing <see cref="RefKindPrefix"/> at a call site for
/// one of those (e.g. emitting the literal text <c>"ref readonly "</c> before an argument) is a
/// syntax error (code-review finding). Every forwarding call site (an <c>IsForwarding</c>
/// member's own body, a DIM fallback dispatch, a DIM sibling's forwarding declaration) must use
/// this field, never <see cref="RefKindPrefix"/>, for its argument list.
/// </param>
internal sealed record TestDoubleParameterInfo(
    string EscapedName, string OriginalName, string FullyQualifiedTypeName, string RefKindPrefix = "", bool IsParams = false,
    string DefaultValueExpression = "", string CallSiteRefKindPrefix = "");
