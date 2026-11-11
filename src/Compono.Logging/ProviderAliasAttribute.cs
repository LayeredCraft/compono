#if !NET10_0_OR_GREATER
// net8.0/net9.0 only. Compono.Logging pins Microsoft.Extensions.Logging.Abstractions to the 8.x/9.x
// line on these targets, and ProviderAliasAttribute only moved into Abstractions in 10.x (before
// that it lived in the Microsoft.Extensions.Logging implementation package, which this package
// deliberately doesn't reference). On net10.0/net11.0 the real attribute from Abstractions is used
// instead, so this file compiles to nothing there.
//
// This internal copy works because Microsoft.Extensions.Logging resolves a provider's alias by the
// attribute's *full type name* ("Microsoft.Extensions.Logging.ProviderAliasAttribute") through
// CustomAttributeData, never by type identity - so it must keep exactly this namespace and name.
// Being internal, it is invisible to consumers and cannot collide with the public attribute in
// their compilation. If an Abstractions 10.x+ reference ever reaches these targets, the compiler
// reports the duplicate (CS0436), which Compono.Logging.csproj promotes to an error - a hard build
// failure rather than a silent change. See docs/adr/0071-compono-logging-host-wide-log-capture.md
// Amendment 2.
namespace Microsoft.Extensions.Logging;

/// <summary>
/// Internal net8.0/net9.0 stand-in for <c>Microsoft.Extensions.Logging.ProviderAliasAttribute</c>, so
/// <see cref="Compono.Logging.ComponoLogCollectorProvider"/>'s <c>Compono</c> alias works on every
/// target framework without an implementation-package dependency (ADR-0071 Amendment 2).
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
internal sealed class ProviderAliasAttribute : Attribute
{
    public ProviderAliasAttribute(string alias)
    {
        Alias = alias;
    }

    public string Alias { get; }
}
#endif
