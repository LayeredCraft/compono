using System.Runtime.CompilerServices;

namespace Compono.TUnit.Tests.Fixtures;

/// <summary>
/// Hand-fakes the <c>RowInvokerRegistry</c> registrations a real generated module initializer would
/// emit for every distinct dispatch-eligible parameter type <see cref="SampleTestMethods"/> declares -
/// this test project doesn't reference <c>Compono.Generators</c> as an analyzer (testing.md's hand-fake
/// convention), so nothing else populates these. <c>BindingPlan.Build</c> calls
/// <c>RowInvokers.Build</c> for every parameter of every signature-valid method it's given, so every
/// such type needs an entry here regardless of whether any test actually resolves a real value
/// through it. Mirrors <c>Compono.XunitV3.Tests.Fixtures.RowInvokerRegistryFakes</c>.
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
