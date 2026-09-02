using System.Runtime.CompilerServices;

namespace Compono.MSTest.Tests.Fixtures;

/// <summary>
/// Hand-fakes the <c>RowInvokerRegistry</c> registrations a real generated module initializer would
/// emit for every distinct dispatch-eligible parameter type <see cref="SampleTestMethods"/> declares
/// on a method that is itself never <c>[Compose]</c>-attributed (e.g. <see cref="SampleTestMethods.Simple"/>,
/// used only for <c>BindingPlan.Build</c> reflection tests) - matching
/// <c>Compono.XunitV3.Tests</c>'/<c>Compono.TUnit.Tests</c>' identical hand-fake convention.
/// Real <c>[Compose]</c>-attributed fixture methods (<see cref="SampleTestMethods.ComposesTwoStrings"/>
/// etc.) instead go through the real, generator-discovered registrations (this project's
/// <c>Compono.Generators</c> analyzer reference), so a duplicate fake registration for the same type
/// here is harmless - <c>RowInvokerRegistry.Register</c> is idempotent.
/// </summary>
internal static class RowInvokerRegistryFakes
{
    [ModuleInitializer]
    internal static void Register()
    {
        RowInvokerRegistry.Register(typeof(int),
            static (row, in descriptor) => row.Resolve<int>(descriptor),
            static (row, in descriptor) => row.ResolveShared<int>(descriptor),
            static (row, in descriptor, value) => row.ShareExplicit(descriptor, (int)value!));

        RowInvokerRegistry.Register(typeof(string),
            static (row, in descriptor) => row.Resolve<string>(descriptor),
            static (row, in descriptor) => row.ResolveShared<string>(descriptor),
            static (row, in descriptor, value) => row.ShareExplicit(descriptor, (string)value!));

        RowInvokerRegistry.Register(typeof(int?),
            static (row, in descriptor) => row.Resolve<int?>(descriptor),
            static (row, in descriptor) => row.ResolveShared<int?>(descriptor),
            static (row, in descriptor, value) => row.ShareExplicit(descriptor, (int?)value));
    }
}
