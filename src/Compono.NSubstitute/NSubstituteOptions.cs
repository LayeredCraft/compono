namespace Compono;

/// <summary>
/// Configuration for <see cref="NSubstituteProvider"/>, set via
/// <c>CompositionBuilderExtensions.UseNSubstitute(Action{NSubstituteOptions})</c>. See
/// <c>docs/adr/0025-compono-nsubstitute-package-design.md</c>.
/// </summary>
public sealed class NSubstituteOptions
{
    /// <summary>
    /// Whether an unsealed abstract class is substitutable, in addition to every interface and
    /// delegate type. Defaults to <see langword="true"/>.
    /// </summary>
    public bool SubstituteAbstractClasses { get; set; } = true;
}
