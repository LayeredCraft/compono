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
/// <param name="FullyQualifiedTypeName">This parameter's type, fully qualified.</param>
/// <param name="RefKindPrefix">
/// <c>""</c>, <c>"ref "</c>, <c>"out "</c>, or <c>"in "</c> - only a member with no
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
internal sealed record TestDoubleParameterInfo(
    string EscapedName, string FullyQualifiedTypeName, string RefKindPrefix = "", bool IsParams = false);
