using Xunit;

// The assembly-level fixture this assembly declares, read by RequireFixtureIntegrationTests' own
// assembly-fixture case (ADR-0069). Kept in its own file because an assembly attribute cannot target
// a type declared in the same file's own file-scoped namespace below it without an awkward ordering,
// and because this file is the natural home for the assembly's (single, trivial, side-effect-free)
// assembly fixture. Registered with [assembly: AssemblyFixture(...)] - the third of the three concrete
// fixture declarations RequireFixture's own M3 guidance names.
[assembly: AssemblyFixture(typeof(Compono.XunitV3.Tests.RequireFixtureAssemblyFixture))]

namespace Compono.XunitV3.Tests;

public sealed class RequireFixtureAssemblyFixture
{
    public string Value => "assembly-fixture-value";
}
