// ReSharper disable InconsistentNaming

using Compono.Generators.Discovery;

namespace Compono.Generators.WellKnownTypes;

internal static class WellKnownTypeData
{
    public enum WellKnownType
    {
        Compono_ComposableAttribute,
        Compono_Composer,
        Compono_CompositionRow,
        System_DateTime,
        System_DateTimeOffset,
        System_Guid,
        System_TimeSpan,
        System_DateOnly,
        System_TimeOnly,
    }

    public static readonly string[] WellKnownTypeNames =
    [
        // Same identity ComposableAttributeDiscovery's own ForAttributeWithMetadataName registration
        // matches on - two independent discovery paths for the [Composable] attribute's metadata
        // name, kept in sync via this one constant instead of two copies of the literal
        // (PLAN-0061 Phase 1).
        ComposableAttributeDiscovery.AttributeMetadataName,
        "Compono.Composer",
        "Compono.CompositionRow",
        "System.DateTime",
        "System.DateTimeOffset",
        "System.Guid",
        "System.TimeSpan",
        "System.DateOnly",
        "System.TimeOnly",
    ];
}
