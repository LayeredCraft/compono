namespace Compono.XunitV3.SampleTests;

public sealed record NotificationSettings(string SenderAddress);

// Reached only through ProfileTests.ComposesTheProfileConfiguredValue's own [Compose<TProfile>]
// theory parameter - proves ComposeAttribute<TProfile> actually applies TProfile.Configure through the
// real packaged pipeline, not just Compono.XunitV3.Tests' in-process GetComposer()/CreateRow checks.
public sealed class ApplicationTestProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) =>
        builder.Register(() => new NotificationSettings("sample-test-sender@example.com"));
}

public sealed class ProfileTests
{
    [Theory]
    [Compose<ApplicationTestProfile>]
    public void ComposesTheProfileConfiguredValue(NotificationSettings settings)
    {
        settings.SenderAddress.Should().Be("sample-test-sender@example.com");
    }
}
