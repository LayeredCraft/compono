namespace Compono.XunitV3.SampleTests;

// Deliberately fails, on every run, via ConfigProfileBinder's own pre-composition constructor-shape
// validation (ProfileWithNoMatchingConstructor has no constructor accepting a RepositoryTestConfig)
// - not a genuine composition failure. Proves the diagnostic reaches a real xUnit v3 runner's actual
// output before the test body ever executes, through the real packaged pipeline, mirroring
// FailingCompositionTests' own separate-class pattern for exactly this reason: this project's CI
// "Local-feed packed-consumer smoke test" step (.github/workflows/package-validation.yaml) filters
// out every class whose name starts with "Failing", so a deliberately-failing proof test lives in
// its own class matching that naming convention rather than inside an otherwise-green test class -
// keeping it in ConfigProfileTests.cs's own class caused the CI gate itself to fail (PR #65 review;
// caught live in CI after this file didn't exist yet).
public sealed class FailingConfigProfileTests
{
    [Theory]
    [Compose<ProfileWithNoMatchingConstructor, RepositoryTestConfig>(RepositoryKind.Player)]
    public void MismatchedProfileConstructorShape_FailsBeforeTheTestExecutes(string repositoryName)
    {
        repositoryName.Should().BeNull("GetData throws before this body ever runs - this line never executes");
    }
}
