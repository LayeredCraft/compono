namespace Compono.XunitV3;

// Stands in for the real Compono.XunitV3.ComposeAttribute - Compono.Generators' ComposeMethodDiscovery
// matches purely on this type's fully qualified metadata name (see Compono.Generators.Tests' own
// CompositionPlanVerifyTests, which uses the identical trick), so this harness doesn't need a real
// reference to the Compono.XunitV3 package to trigger the same discovery/emission path PLAN-0041 needs
// proven under Native AOT.
internal sealed class ComposeAttribute : Attribute;
