namespace Compono.Generators.Models;

/// <summary>
/// A property member's write-accessor shape - not meaningful for <see cref="TestDoubleMemberKind.Method"/>.
/// <see langword="init"/> and <see langword="set"/> are non-interchangeable (ADR-0043 Amendment 9,
/// Finding U): emitting the wrong one fails to implement the interface.
/// </summary>
internal enum TestDoublePropertyAccessorKind
{
    /// <summary>Not a property (a method member).</summary>
    None,

    /// <summary>Get-only - configurable only via <c>Configure()</c>, no write accessor emitted.</summary>
    GetOnly,

    /// <summary>Real auto-property semantics through an ordinary <see langword="set"/> accessor.</summary>
    GetSet,

    /// <summary>Real auto-property semantics through an <see langword="init"/> accessor.</summary>
    GetInit,
}
