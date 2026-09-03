using System.Reflection;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

namespace Compono.NUnit.Tests.Fixtures;

// Wraps a real System.Reflection.MethodInfo into NUnit's own IMethodInfo, the shape
// ComposeAttribute.BuildFrom actually receives from a real NUnit test run - NUnit.Framework.Internal
// .MethodWrapper is a public type with a public (Type, MethodInfo) constructor (confirmed directly
// against the installed NUnit assembly, not assumed), used here so unit-level binding tests can call
// BuildFrom directly without spinning up a real NUnit discovery/execution pipeline.
internal static class MethodInfoWrapper
{
    public static IMethodInfo Wrap(MethodInfo method) =>
        new MethodWrapper(method.DeclaringType!, method);
}
