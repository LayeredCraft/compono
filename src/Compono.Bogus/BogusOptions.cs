namespace Compono;

/// <summary>
/// Configuration for <see cref="BogusMemberNameProvider"/>, set via
/// <c>CompositionBuilderExtensions.UseBogus(Action{BogusOptions})</c>. See
/// <c>docs/adr/0027-compono-bogus-package-design.md</c>.
/// </summary>
public sealed class BogusOptions
{
    /// <summary>
    /// The Bogus locale used by the package-wide member-name convention provider
    /// (<see cref="BogusMemberNameProvider"/>) only. <c>UseBogus&lt;T&gt;()</c> is independent of this
    /// option and does not read it - it defaults to <c>"en"</c> on its own, or takes an explicit
    /// <c>locale</c> parameter. Defaults to Bogus's own default (<c>"en"</c>).
    /// </summary>
    public string Locale { get; set; } = "en";

    /// <summary>
    /// Whether the conservative member-name convention provider is active. Defaults to
    /// <see langword="true"/>.
    /// </summary>
    public bool EnableMemberNameConventions { get; set; } = true;
}
