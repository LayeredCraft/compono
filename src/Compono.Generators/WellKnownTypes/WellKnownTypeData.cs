// ReSharper disable InconsistentNaming

namespace Compono.Generators.WellKnownTypes;

internal static class WellKnownTypeData
{
    public enum WellKnownType
    {
        Compono_ComposableAttribute,
        Compono_Composer,
        System_DateTime,
        System_DateTimeOffset,
        System_Guid,
        System_TimeSpan,
        System_DateOnly,
        System_TimeOnly,
    }

    public static readonly string[] WellKnownTypeNames =
    [
        "Compono.ComposableAttribute",
        "Compono.Composer",
        "System.DateTime",
        "System.DateTimeOffset",
        "System.Guid",
        "System.TimeSpan",
        "System.DateOnly",
        "System.TimeOnly",
    ];
}
