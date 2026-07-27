// ReSharper disable InconsistentNaming

namespace Compono.Generators.WellKnownTypes;

internal static class WellKnownTypeData
{
    public enum WellKnownType
    {
        Compono_Composer,
        System_DateTime,
        System_DateTimeOffset,
        System_Guid,
        System_TimeSpan,
    }

    public static readonly string[] WellKnownTypeNames =
    [
        "Compono.Composer",
        "System.DateTime",
        "System.DateTimeOffset",
        "System.Guid",
        "System.TimeSpan",
    ];
}
