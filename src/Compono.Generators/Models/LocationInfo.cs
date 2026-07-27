using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Compono.Generators.Models;

/// <summary>
/// A serializable stand-in for <see cref="Location"/>, so a diagnostic can survive Roslyn's
/// incremental-generator pipeline caching - a live <see cref="Location"/>/<see cref="Diagnostic"/>
/// isn't safely cacheable. See <c>docs/adr/0005-generator-implementation-conventions.md</c>.
/// </summary>
internal sealed record LocationInfo(string FilePath, TextSpan TextSpan, LinePositionSpan LineSpan)
{
    public Location ToLocation() => Location.Create(FilePath, TextSpan, LineSpan);

    public static LocationInfo? From(Location location) =>
        location.SourceTree is null
            ? null
            : new LocationInfo(location.SourceTree.FilePath, location.SourceSpan, location.GetLineSpan().Span);

    public static LocationInfo? From(ISymbol symbol) =>
        symbol.Locations.FirstOrDefault() is { } location ? From(location) : null;

    public static LocationInfo? From(SyntaxNode syntaxNode) => From(syntaxNode.GetLocation());
}
