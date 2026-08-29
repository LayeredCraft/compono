using Microsoft.CodeAnalysis;

namespace Compono.Generators.WellKnownTypes;

/// <summary>
/// <c>Microsoft.Extensions.Logging.ILogger</c>/<c>ILogger&lt;T&gt;</c>, resolved once per
/// <see cref="Compilation"/> - a separate, dedicated resolver rather than an addition to
/// <see cref="WellKnownTypeData"/>'s enum-and-name table, because that table's
/// <c>AssertEnumAndTableInSync</c> convention (an enum member's name reconstructs its metadata name
/// via underscore-to-dot replacement) has no way to represent a generic type's arity suffix
/// (<c>`1</c>). Deliberately dependency-free: this project carries no
/// <c>Microsoft.Extensions.Logging.Abstractions</c> package reference -
/// <see cref="Compilation.GetTypeByMetadataName"/> against the compilation being generated for
/// returns <see langword="null"/> cleanly for a consumer who never references that package, which
/// <see cref="TryCreate"/> surfaces as returning <see langword="null"/> itself. See
/// docs/adr/0055-compono-logging-testing-support-package.md Amendment 3's "Generator ownership".
/// </summary>
internal sealed class LoggingWellKnownTypes
{
    private static readonly BoundedCacheWithFactory<Compilation, LoggingWellKnownTypes?> Cache = new();

    private LoggingWellKnownTypes(INamedTypeSymbol iLogger, INamedTypeSymbol openGenericILoggerOfT)
    {
        ILogger = iLogger;
        OpenGenericILoggerOfT = openGenericILoggerOfT;
    }

    public INamedTypeSymbol ILogger { get; }

    public INamedTypeSymbol OpenGenericILoggerOfT { get; }

    /// <summary>
    /// Returns <see langword="null"/> if this compilation never references
    /// <c>Microsoft.Extensions.Logging.Abstractions</c> at all (neither <c>ILogger</c> nor
    /// <c>ILogger&lt;T&gt;</c> resolvable) - the case every real consumer of core <c>Compono</c> who
    /// never installed <c>Compono.Logging</c> hits, and the reason this must stay cheap and clean.
    /// </summary>
    public static LoggingWellKnownTypes? TryCreate(Compilation compilation) =>
        Cache.GetOrCreateValue(compilation, static c =>
        {
            var iLogger = c.GetTypeByMetadataName("Microsoft.Extensions.Logging.ILogger");
            var openGenericILoggerOfT = c.GetTypeByMetadataName("Microsoft.Extensions.Logging.ILogger`1");

            return iLogger is null || openGenericILoggerOfT is null
                ? null
                : new LoggingWellKnownTypes(iLogger, openGenericILoggerOfT);
        });

    /// <summary>
    /// The closed category type <c>T</c> if <paramref name="type"/> is a closed instantiation of
    /// <c>ILogger&lt;T&gt;</c>, else <see langword="null"/>. Never matches the open generic
    /// definition itself or bare, non-generic <see cref="ILogger"/> (which needs no generated
    /// activation - <c>LoggingProvider</c> constructs it directly).
    /// </summary>
    public ITypeSymbol? TryGetCategory(ITypeSymbol type) =>
        type is INamedTypeSymbol { IsGenericType: true } named
        && SymbolEqualityComparer.Default.Equals(named.ConstructedFrom, OpenGenericILoggerOfT)
            ? named.TypeArguments[0]
            : null;
}
