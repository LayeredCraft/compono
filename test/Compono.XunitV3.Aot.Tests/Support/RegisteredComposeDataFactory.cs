using Xunit;
using Xunit.Sdk;
using Xunit.v3;

namespace Compono.XunitV3.Aot.Tests.Support;

/// <summary>
/// Drives a generated <c>[ComposeData]</c> registration the way the xUnit Native AOT runtime would,
/// so tests can assert the exact theory rows a generated factory produces (values, seed trait, merged
/// metadata) and the exact failure text its composition throws. Must be called from inside a running
/// test, because the generated factory reads <see cref="TestContext.Current"/> for its cancellation
/// token.
/// </summary>
internal static class RegisteredComposeDataFactory
{
    public static async ValueTask<IReadOnlyCollection<ITheoryDataRow>> InvokeAsync(
        string testClassIndex,
        string methodName,
        DisposalTracker? disposalTracker = null)
    {
        var testMethod = new StubCodeGenTestMethod(testClassIndex, methodName);

        var factories = RegisteredEngineConfig.GetTheoryDataRowFactories(testMethod);
        var (factory, disableDiscoveryEnumeration) = Assert.Single(factories);

        Assert.True(
            disableDiscoveryEnumeration,
            "A generated ComposeData registration must delay discovery enumeration to execution time.");

        return await factory(disposalTracker ?? new DisposalTracker()).ConfigureAwait(false);
    }

    /// <summary>
    /// Minimal <see cref="ICodeGenTestMethod"/> whose only meaningful members are the type index and
    /// method name the registration is keyed by; every other member is unreachable on the
    /// <c>GetTheoryDataRowFactories</c> lookup path and throws if something asks for it.
    /// </summary>
    private sealed class StubCodeGenTestMethod(string declaredTypeIndex, string methodName) : ICodeGenTestMethod
    {
        public string? DeclaredTypeIndex => declaredTypeIndex;

        public string MethodName => methodName;

        public string UniqueID => throw new NotSupportedException();

        public int? MethodArity => throw new NotSupportedException();

        int ICoreTestMethod.MethodArity => throw new NotSupportedException();

        public IReadOnlyDictionary<string, IReadOnlyCollection<string>> Traits => throw new NotSupportedException();

        public bool DisableParallelization => throw new NotSupportedException();

        public ITestCaseOrderer? TestCaseOrderer => throw new NotSupportedException();

        public ITestClass TestClass => throw new NotSupportedException();

        ICoreTestClass ICoreTestMethod.TestClass => throw new NotSupportedException();

        ICodeGenTestClass ICodeGenTestMethod.TestClass => throw new NotSupportedException();

        public IReadOnlyCollection<BeforeAfterTestAttribute> BeforeAfterTestAttributes => throw new NotSupportedException();

        public bool IsStatic => throw new NotSupportedException();

        public string? SourceFilePath => throw new NotSupportedException();

        public int? SourceLineNumber => throw new NotSupportedException();
    }
}
