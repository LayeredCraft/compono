using System.Net;

namespace Compono.Http.Tests;

public sealed class TestHttpHandlerTests
{
    [Fact]
    public async Task OnGet_ExactPath_MatchesAndResponds()
    {
        using var handler = new TestHttpHandler();
        handler.OnGet("/users/42").RespondText("hello");

        using var client = handler.CreateClient(new Uri("https://api.example.com/"));
        var response = await client.GetAsync("/users/42", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Be("hello");
    }

    [Fact]
    public async Task OnGet_MatchAny_MatchesAnyPath()
    {
        using var handler = new TestHttpHandler();
        handler.OnGet(Match.Any<string>()).Respond(HttpStatusCode.OK);

        using var client = handler.CreateClient(new Uri("https://api.example.com/"));
        var responseA = await client.GetAsync("/one", TestContext.Current.CancellationToken);
        var responseB = await client.GetAsync("/two", TestContext.Current.CancellationToken);

        responseA.StatusCode.Should().Be(HttpStatusCode.OK);
        responseB.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task OnGet_MatchIs_MatchesByPredicate()
    {
        using var handler = new TestHttpHandler();
        handler.OnGet(Match.Is<string>(p => p.StartsWith("/users/"))).RespondText("matched");

        using var client = handler.CreateClient(new Uri("https://api.example.com/"));
        var response = await client.GetAsync("/users/99", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Be("matched");
    }

    [Fact]
    public async Task OnGet_MatchIs_DoesNotMatchOutsidePredicate()
    {
        using var handler = new TestHttpHandler();
        handler.OnGet(Match.Is<string>(p => p.StartsWith("/users/"))).RespondText("matched");

        using var client = handler.CreateClient(new Uri("https://api.example.com/"));
        var act = () => client.GetAsync("/orders/1", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<UnmatchedHttpRequestException>();
    }

    [Fact]
    public async Task When_WholeRequestPredicate_MatchesMethodAndContentType()
    {
        using var handler = new TestHttpHandler();
        handler.When(r => r.Method == HttpMethod.Post && r.Content is FormUrlEncodedContent)
            .Respond(HttpStatusCode.OK);

        using var client = handler.CreateClient(new Uri("https://api.example.com/"));
        var content = new FormUrlEncodedContent(new Dictionary<string, string> { ["a"] = "b" });
        var response = await client.PostAsync("/token", content, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task MethodHelpers_FixTheCorrectMethod(string method)
    {
        using var handler = new TestHttpHandler();
        HttpResponseRegistration registration = method switch
        {
            "GET" => handler.OnGet("/x").Respond(HttpStatusCode.OK),
            "POST" => handler.OnPost("/x").Respond(HttpStatusCode.OK),
            "PUT" => handler.OnPut("/x").Respond(HttpStatusCode.OK),
            "PATCH" => handler.OnPatch("/x").Respond(HttpStatusCode.OK),
            "DELETE" => handler.OnDelete("/x").Respond(HttpStatusCode.OK),
            _ => throw new InvalidOperationException(),
        };

        using var client = handler.CreateClient(new Uri("https://api.example.com/"));
        var request = new HttpRequestMessage(new HttpMethod(method), "/x");
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        registration.Verify().Once();
    }

    [Fact]
    public async Task LastMatchWins_LaterRegistrationOverridesEarlierBroaderOne()
    {
        using var handler = new TestHttpHandler();
        handler.When(_ => true).Respond(HttpStatusCode.InternalServerError);
        handler.OnGet("/users/42").Respond(HttpStatusCode.OK);

        using var client = handler.CreateClient(new Uri("https://api.example.com/"));
        var specific = await client.GetAsync("/users/42", TestContext.Current.CancellationToken);
        var fallback = await client.GetAsync("/other", TestContext.Current.CancellationToken);

        specific.StatusCode.Should().Be(HttpStatusCode.OK);
        fallback.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task ExplicitFallback_ComposesWithMoreSpecificOverrideRegisteredAfterIt()
    {
        using var handler = new TestHttpHandler();
        handler.When(_ => true).Respond(HttpStatusCode.NotFound);
        var specific = handler.OnGet("/users/42").RespondText("found");

        using var client = handler.CreateClient(new Uri("https://api.example.com/"));
        var response = await client.GetAsync("/users/42", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        specific.Verify().Once();
    }

    [Fact]
    public async Task UnmatchedRequest_ThrowsWithMethodAndUriInMessage()
    {
        using var handler = new TestHttpHandler();

        using var client = handler.CreateClient(new Uri("https://api.example.com/"));
        var act = () => client.GetAsync("/unconfigured/path", TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnmatchedHttpRequestException>())
            .WithMessage("*GET*unconfigured/path*");
    }

    [Fact]
    public async Task UnmatchedRequest_StillAppearsInRequests()
    {
        using var handler = new TestHttpHandler();
        using var client = handler.CreateClient(new Uri("https://api.example.com/"));

        var act = () => client.GetAsync("/unconfigured/path", TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<UnmatchedHttpRequestException>();

        handler.Requests.Should().ContainSingle(r => r.RequestUri!.PathAndQuery == "/unconfigured/path");
    }

    [Fact]
    public async Task RepeatedMatches_GetFreshResponseAndContentEachTime()
    {
        using var handler = new TestHttpHandler();
        var registration = handler.OnGet("/users/42").RespondText("hello");

        using var client = handler.CreateClient(new Uri("https://api.example.com/"));

        var first = await client.GetAsync("/users/42", TestContext.Current.CancellationToken);
        var firstBody = await first.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        first.Dispose();

        var second = await client.GetAsync("/users/42", TestContext.Current.CancellationToken);
        var secondBody = await second.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        firstBody.Should().Be("hello");
        secondBody.Should().Be("hello");
        registration.Verify().Exactly(2);
    }

    [Fact]
    public async Task RespondJson_SetsJsonContentTypeWithUtf8Charset()
    {
        using var handler = new TestHttpHandler();
        handler.OnGet("/users/42").RespondJson(new { Name = "Ada" });

        using var client = handler.CreateClient(new Uri("https://api.example.com/"));
        var response = await client.GetAsync("/users/42", TestContext.Current.CancellationToken);

        response.Content.Headers.ContentType.Should().NotBeNull();
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        response.Content.Headers.ContentType!.CharSet.Should().Be("utf-8");
    }

    [Fact]
    public async Task RespondBytes_RoundTripsContentAndDefaultsToOctetStreamContentType()
    {
        byte[] payload = [0x00, 0x01, 0xFF, 0x7F, 0x80];
        using var handler = new TestHttpHandler();
        handler.OnGet("/cert.pem").RespondBytes(payload);

        using var client = handler.CreateClient(new Uri("https://api.example.com/"));
        var response = await client.GetAsync("/cert.pem", TestContext.Current.CancellationToken);
        var received = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);

        received.Should().Equal(payload);
        response.Content.Headers.ContentType.Should().NotBeNull();
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/octet-stream");
    }

    [Fact]
    public async Task RespondBytes_MutatingCallersArrayAfterRegistration_DoesNotAffectResponse()
    {
        byte[] original = [0x01, 0x02, 0x03];
        byte[] expected = [.. original];
        using var handler = new TestHttpHandler();
        handler.OnGet("/cert.pem").RespondBytes(original);

        original[0] = 0xFF;

        using var client = handler.CreateClient(new Uri("https://api.example.com/"));
        var response = await client.GetAsync("/cert.pem", TestContext.Current.CancellationToken);
        var received = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);

        received.Should().Equal(expected);
    }

    [Fact]
    public async Task RespondBytes_UsesSuppliedMediaType()
    {
        using var handler = new TestHttpHandler();
        handler.OnGet("/cert.pem").RespondBytes([0x01], "application/pkix-cert");

        using var client = handler.CreateClient(new Uri("https://api.example.com/"));
        var response = await client.GetAsync("/cert.pem", TestContext.Current.CancellationToken);

        response.Content.Headers.ContentType!.MediaType.Should().Be("application/pkix-cert");
    }

    [Fact]
    public async Task Throws_RethrowsTheExactSameExceptionInstanceOnEachMatch()
    {
        using var handler = new TestHttpHandler();
        var exception = new HttpRequestException("simulated failure", null, HttpStatusCode.NotFound);
        handler.OnGet("/users/42").Throws(exception);

        using var client = handler.CreateClient(new Uri("https://api.example.com/"));

        var first = await Record.ExceptionAsync(() => client.GetAsync("/users/42", TestContext.Current.CancellationToken));
        var second = await Record.ExceptionAsync(() => client.GetAsync("/users/42", TestContext.Current.CancellationToken));

        first.Should().BeSameAs(exception);
        second.Should().BeSameAs(exception);
    }

    [Fact]
    public void Verify_Never_PassesWhenNeverMatched()
    {
        using var handler = new TestHttpHandler();
        var registration = handler.OnGet("/users/42").RespondText("hello");

        registration.Verify().Never();
    }

    [Fact]
    public async Task Verify_Once_ThrowsWhenMatchedMoreThanOnce()
    {
        using var handler = new TestHttpHandler();
        var registration = handler.OnGet("/users/42").RespondText("hello");

        using var client = handler.CreateClient(new Uri("https://api.example.com/"));
        await client.GetAsync("/users/42", TestContext.Current.CancellationToken);
        await client.GetAsync("/users/42", TestContext.Current.CancellationToken);

        var act = () => registration.Verify().Once();

        act.Should().Throw<TestDoubleVerificationException>();
    }

    [Fact]
    public async Task ConcurrentSendAsync_RecordsEveryRequestAndCountsEveryMatchCorrectly()
    {
        using var handler = new TestHttpHandler();
        var registration = handler.OnGet(Match.Any<string>()).Respond(HttpStatusCode.OK);
        using var client = handler.CreateClient(new Uri("https://api.example.com/"));

        const int concurrentRequests = 50;
        var tasks = Enumerable.Range(0, concurrentRequests)
            .Select(i => client.GetAsync($"/item/{i}", TestContext.Current.CancellationToken))
            .ToArray();
        await Task.WhenAll(tasks);

        handler.Requests.Should().HaveCount(concurrentRequests);
        registration.Verify().Exactly(concurrentRequests);
    }

    [Fact]
    public async Task Requests_ReturnsStableSnapshot_UnaffectedByLaterRequests()
    {
        using var handler = new TestHttpHandler();
        handler.OnGet(Match.Any<string>()).Respond(HttpStatusCode.OK);
        using var client = handler.CreateClient(new Uri("https://api.example.com/"));

        await client.GetAsync("/first", TestContext.Current.CancellationToken);
        var snapshot = handler.Requests;
        await client.GetAsync("/second", TestContext.Current.CancellationToken);

        snapshot.Should().HaveCount(1);
        handler.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task DisposedHandler_SendAsyncThrowsObjectDisposedException()
    {
        var handler = new TestHttpHandler();
        handler.OnGet(Match.Any<string>()).Respond(HttpStatusCode.OK);
        using var client = handler.CreateClient(new Uri("https://api.example.com/"));

        handler.Dispose();
        var act = () => client.GetAsync("/anything", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task DisposingHttpClient_DoesNotDisposeTheHandler()
    {
        using var handler = new TestHttpHandler();
        handler.OnGet(Match.Any<string>()).Respond(HttpStatusCode.OK);

        var firstClient = handler.CreateClient(new Uri("https://api.example.com/"));
        firstClient.Dispose();

        using var secondClient = handler.CreateClient(new Uri("https://api.example.com/"));
        var response = await secondClient.GetAsync("/still-works", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TwoLiteralGetRegistrations_VerifyFailureMessages_IdentifyTheirOwnPath()
    {
        using var handler = new TestHttpHandler();
        var customers = handler.OnGet("/v1/customers/42").RespondText("customer");
        var orders = handler.OnGet("/v1/orders/7").RespondText("order");

        using var client = handler.CreateClient(new Uri("https://api.example.com/"));
        await client.GetAsync("/v1/customers/42", TestContext.Current.CancellationToken);

        var customersAct = () => customers.Verify().Exactly(2);
        var ordersAct = () => orders.Verify().Once();

        customersAct.Should().Throw<TestDoubleVerificationException>()
            .WithMessage("*GET /v1/customers/42*");
        ordersAct.Should().Throw<TestDoubleVerificationException>()
            .WithMessage("*GET /v1/orders/7*");
    }

    [Fact]
    public void OnGet_MatchAny_VerifyFailureMessage_IsHonestNotFabricated()
    {
        using var handler = new TestHttpHandler();
        var registration = handler.OnGet(Match.Any<string>()).Respond(HttpStatusCode.OK);

        var act = () => registration.Verify().Once();

        act.Should().Throw<TestDoubleVerificationException>()
            .WithMessage("*GET request matching a custom path condition*");
    }

    [Fact]
    public void OnGet_MatchIs_VerifyFailureMessage_IsHonestNotFabricated()
    {
        using var handler = new TestHttpHandler();
        var registration = handler.OnGet(Match.Is<string>(p => p.StartsWith("/users/"))).Respond(HttpStatusCode.OK);

        var act = () => registration.Verify().Once();

        // Match<T> exposes no way to tell an Any() match from an Is(predicate) match apart (by
        // design - see Match<T>'s own XML doc) - both share one honest, non-fabricated description
        // rather than one falsely claiming to know which kind this is.
        act.Should().Throw<TestDoubleVerificationException>()
            .WithMessage("*GET request matching a custom path condition*");
    }

    [Fact]
    public async Task RespondJson_MutatingOneResponsesContentType_DoesNotAffectOtherResponses()
    {
        using var handler = new TestHttpHandler();
        handler.OnGet(Match.Any<string>()).RespondJson(new { value = 1 });

        using var client = handler.CreateClient(new Uri("https://api.example.com/"));
        var first = await client.GetAsync("/one", TestContext.Current.CancellationToken);
        first.Content.Headers.ContentType!.CharSet = "iso-8859-1";

        var second = await client.GetAsync("/two", TestContext.Current.CancellationToken);

        second.Content.Headers.ContentType!.CharSet.Should().Be("utf-8");
    }

    [Fact]
    public void FinalizingTheSameBuilderTwice_ThrowsInvalidOperationException()
    {
        using var handler = new TestHttpHandler();
        var builder = handler.OnGet("/x");
        builder.Respond(HttpStatusCode.OK);

        var act = () => builder.Respond(HttpStatusCode.NotFound);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task FinalizingTheSameBuilderTwice_FirstRegistrationHandleKeepsItsOriginalResponse()
    {
        using var handler = new TestHttpHandler();
        var builder = handler.OnGet("/x");
        var first = builder.Respond(HttpStatusCode.OK);
        var secondAct = () => builder.Respond(HttpStatusCode.NotFound);
        secondAct.Should().Throw<InvalidOperationException>();

        using var client = handler.CreateClient(new Uri("https://api.example.com/"));
        var response = await client.GetAsync("/x", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        first.Verify().Once();
    }
}
