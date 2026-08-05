namespace Compono.Samples.BasicUsage;

// A reusable profile - the shape docs/how-to/use-profiles.md and docs/concepts/profiles.md
// describe for configuration shared across more than one test class, rather than repeated per
// test. Demonstrates both a type registration (Register) and a member rule (For<T>().Member(...))
// applied through the same profile.
public sealed class SampleApplicationProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) =>
        builder
            .Register<Repository>(() => new Repository())
            .For<Customer>().Member(x => x.Email).Use("sample.customer@example.com");
}
