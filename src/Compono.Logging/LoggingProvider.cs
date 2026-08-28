using Microsoft.Extensions.Logging;

namespace Compono.Logging;

/// <summary>
/// A stage-6 test-double provider that composes an <see cref="ILogger"/> or closed
/// <see cref="ILogger{TCategoryName}"/> request as a <see cref="CapturingLogger"/>/
/// <see cref="CapturingLogger{T}"/>. Registered via
/// <see cref="CompositionBuilderExtensions.UseLogging"/>. See
/// docs/adr/0055-compono-logging-testing-support-package.md.
/// </summary>
internal sealed class LoggingProvider(LoggingOptions options) : ICompositionValueProvider
{
    private static readonly Type OpenLoggerOfT = typeof(ILogger<>);

    /// <inheritdoc />
    public CompositionProviderResult TryProvide(in CompositionProviderRequest request, ICompositionContext context)
    {
        var requestedType = request.RequestedType;

        if (requestedType == typeof(ILogger))
            return CompositionProviderResult.Handled(new CapturingLogger(options));

        if (!requestedType.IsGenericType || requestedType.GetGenericTypeDefinition() != OpenLoggerOfT)
            return CompositionProviderResult.NotHandled;

        if (LoggingFactoryRegistry.TryCreate(requestedType, options, out var value))
            return CompositionProviderResult.Handled(value);

        // Recognized as a closed ILogger<T> request but no generated activator exists for it -
        // never NotHandled here: falling through would let a later UseNSubstitute()/
        // UseGeneratedTestDoubles() registration silently claim the request and mask a real
        // Compono.Logging generator/discovery gap behind what looks like an ordinary substitute or
        // generated double. See docs/adr/0055-compono-logging-testing-support-package.md's
        // "Missing generated activation" section - a distinct condition, and a distinct message,
        // from LoggerTestingExtensions' "not a Compono.Logging logger at all" diagnostic.
        throw new InvalidOperationException(
            $"Compono.Logging recognized '{FriendlyTypeName(requestedType)}' as a " +
            "closed ILogger<T> request, but no generated activation was found for it. This usually " +
            "means the requested category type isn't reachable from a real Compono composition root " +
            "(Composer.Create<T>()/CreateMany<T>(), [Composable], or a [Compose]/[Compose<TProfile>] " +
            "theory-row parameter) for Compono.Logging's generator to discover - see " +
            "docs/adr/0055-compono-logging-testing-support-package.md's discovery model.");
    }

    // A small, local display helper - core Compono's own CompositionPath.FriendlyTypeName is
    // internal to that assembly (no InternalsVisibleTo grant to Compono.Logging), so this
    // deliberately doesn't reuse it. Good enough for a diagnostic message; not a general-purpose
    // type-name formatter.
    private static string FriendlyTypeName(Type type)
    {
        if (!type.IsGenericType)
            return type.Name;

        var genericArguments = string.Join(", ", type.GetGenericArguments().Select(FriendlyTypeName));
        var name = type.Name[..type.Name.IndexOf('`')];
        return $"{name}<{genericArguments}>";
    }
}
