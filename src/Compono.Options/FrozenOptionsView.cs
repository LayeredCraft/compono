using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Options;
using MSOptions = Microsoft.Extensions.Options.Options;

namespace Compono.Options;

// Backs both IOptions<T> and IOptionsSnapshot<T> - never public, never named by consumer code
// (docs/adr/0061-compono-options-testing-support.md's "Public object model" section). Captures a
// snapshot copy of TestOptionsSource<T>'s state at construction time and never touches the source
// again - the only difference between the two Microsoft interfaces this backs is how often
// UseOptions<T>'s wiring constructs a new instance (shared once for IOptions<T>, fresh per
// resolution for IOptionsSnapshot<T>), exactly matching real OptionsManager<T>'s own behavior.
internal sealed class FrozenOptionsView<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T>(TestOptionsSource<T> source) : IOptions<T>, IOptionsSnapshot<T>
    where T : class
{
    private readonly IReadOnlyDictionary<string, T> _values = source.CaptureSnapshot();

    public T Value => Get(MSOptions.DefaultName);

    public T Get(string? name)
    {
        var effectiveName = name ?? MSOptions.DefaultName;
        if (_values.TryGetValue(effectiveName, out var value))
        {
            return value;
        }

        throw new UnconfiguredNamedOptionException(typeof(T), effectiveName);
    }
}
