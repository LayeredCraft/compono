namespace Compono.Metrics;

/// <summary>
/// The seven value types <see cref="System.Diagnostics.Metrics"/> instruments accept, and small helpers
/// for matching an instrument to its value type and for human-readable diagnostics. Internal - the set of
/// supported types is part of the public contract only through the diagnostics it produces (ADR-0072
/// selection rule 1).
/// </summary>
internal static class InstrumentValueTypes
{
    /// <summary>The seven supported measurement value types, in the order used in diagnostics.</summary>
    public static readonly Type[] Supported =
    [
        typeof(byte),
        typeof(short),
        typeof(int),
        typeof(long),
        typeof(float),
        typeof(double),
        typeof(decimal),
    ];

    /// <summary><see langword="true"/> when <paramref name="type"/> is one of the seven supported types.</summary>
    public static bool IsSupported(Type type) => Array.IndexOf(Supported, type) >= 0;

    /// <summary>A comma-separated list of the seven supported types' C# names, for diagnostics.</summary>
    public static string SupportedList => "byte, short, int, long, float, double, decimal";

    /// <summary>The C# keyword for a supported measurement type (for example <c>long</c>), falling back to
    /// the type's simple name for anything else.</summary>
    public static string DisplayName(Type type)
    {
        if (type == typeof(byte))
            return "byte";
        if (type == typeof(short))
            return "short";
        if (type == typeof(int))
            return "int";
        if (type == typeof(long))
            return "long";
        if (type == typeof(float))
            return "float";
        if (type == typeof(double))
            return "double";
        if (type == typeof(decimal))
            return "decimal";

        return type.Name;
    }
}
