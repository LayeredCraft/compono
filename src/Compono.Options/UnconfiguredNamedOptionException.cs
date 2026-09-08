namespace Compono.Options;

/// <summary>
/// Thrown when <see cref="TestOptionsSource{T}"/> (or the frozen view backing
/// <see cref="Microsoft.Extensions.Options.IOptions{TOptions}"/>/<see cref="Microsoft.Extensions.Options.IOptionsSnapshot{TOptions}"/>)
/// is asked for a named value that was never established via
/// <see cref="TestOptionsSource{T}.Change(string, T)"/>. This is an intentional divergence from real
/// <c>IOptionsFactory&lt;T&gt;</c>, which silently returns <c>new TOptions()</c> for an unmatched name -
/// see docs/adr/0061-compono-options-testing-support.md's "Unconfigured named option" decision, the
/// same explicit-configuration-over-silent-default tradeoff <c>Compono.TestDoubles</c> already made
/// under ADR-0045.
/// </summary>
public sealed class UnconfiguredNamedOptionException : Exception
{
    /// <summary>Creates an exception describing the settings type and the unconfigured name.</summary>
    /// <param name="optionsType">The settings type (<c>T</c> in <see cref="TestOptionsSource{T}"/>) that was requested.</param>
    /// <param name="name">The requested option name - <see cref="Microsoft.Extensions.Options.Options.DefaultName"/> for the default.</param>
    public UnconfiguredNamedOptionException(Type optionsType, string name)
        : base(BuildMessage(optionsType, name))
    {
    }

    private static string BuildMessage(Type optionsType, string name) =>
        name.Length == 0
            ? $"No default value configured for '{optionsType.Name}'. Call TestOptionsSource<{optionsType.Name}>.Change(value) before resolving it."
            : $"No value configured for '{optionsType.Name}' named \"{name}\". Call TestOptionsSource<{optionsType.Name}>.Change(\"{name}\", value) before resolving it.";
}
