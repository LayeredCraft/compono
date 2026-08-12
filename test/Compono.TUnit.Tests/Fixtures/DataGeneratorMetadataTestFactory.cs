using System.Reflection;
using TUnit.Core.Enums;

namespace Compono.TUnit.Tests.Fixtures;

// Hand-builds a real DataGeneratorMetadata for a real method, so ComposeAttribute.GetDataRowsAsync
// can be exercised directly as a unit test - the same shape of test Compono.XunitV3.Tests'
// ComposeAttributeBindingTests.cs runs via ComposeAttribute.GetData(MethodInfo, DisposalTracker),
// adapted to what TUnit's data-source pipeline actually hands an attribute (DataGeneratorMetadata,
// not a bare MethodInfo).
internal static class DataGeneratorMetadataTestFactory
{
    public static DataGeneratorMetadata Create(MethodInfo method)
    {
        var testInformation = MethodMetadataTestFactory.Create(method);

        return new DataGeneratorMetadata
        {
            TestBuilderContext = new TestBuilderContextAccessor(new TestBuilderContext { TestMetadata = testInformation }),
            MembersToGenerate = [],
            TestInformation = testInformation,
            Type = DataGeneratorType.TestParameters,
            TestSessionId = "test-session",
            TestClassInstance = null,
            ClassInstanceArguments = null,
        };
    }
}
