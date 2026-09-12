using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Compono.Http.Tests;

internal sealed record OrderDto(int CustomerId, string Sku);

[JsonSerializable(typeof(OrderDto))]
internal partial class TestHttpHandlerTestsJsonContext : JsonSerializerContext;

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

    // PLAN-0063/ADR-0044 Amendment 22: AtLeast/AtMost are reachable through
    // HttpResponseRegistration.Verify() with zero package-side code changes (CallVerifier is
    // returned directly by HttpResponseRegistration.Verify()).
    [Fact]
    public async Task Verify_AtLeastAndAtMost_AreReachableWithNoPackageCodeChanges()
    {
        using var handler = new TestHttpHandler();
        var registration = handler.OnGet("/users/42").Respond(HttpStatusCode.OK);

        using var client = handler.CreateClient(new Uri("https://api.example.com/"));
        await client.GetAsync("/users/42", TestContext.Current.CancellationToken);
        await client.GetAsync("/users/42", TestContext.Current.CancellationToken);

        registration.Verify().AtLeast(2);
        registration.Verify().AtMost(2);

        var act = () => registration.Verify().AtLeast(3);
        act.Should().Throw<TestDoubleVerificationException>();
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

    // --- Compatibility regressions under the now-async-capable dispatch path (PLAN-0065 task 4) ---

    [Fact]
    public async Task LastMatchWins_WithMixedSyncAndChainedRegistrations()
    {
        using var handler = new TestHttpHandler();
        handler.OnGet("/orders/1").Respond(HttpStatusCode.InternalServerError);
        var specific = handler.OnGet("/orders/1").WithHeader("X-Trace", "abc").Respond(HttpStatusCode.OK);

        using var client = handler.CreateClient(new Uri("https://api.example.com/"));
        using var request = new HttpRequestMessage(HttpMethod.Get, "/orders/1");
        request.Headers.Add("X-Trace", "abc");
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        specific.Verify().Once();
    }

    [Fact]
    public async Task UnmatchedRequest_WhenEveryRegistrationChainFails_StillThrows()
    {
        using var handler = new TestHttpHandler();
        handler.OnGet("/orders/1").WithHeader("X-Trace", "expected").Respond(HttpStatusCode.OK);

        using var client = handler.CreateClient(new Uri("https://api.example.com/"));
        var act = async () => await client.GetAsync("/orders/1", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<UnmatchedHttpRequestException>();
    }

    [Fact]
    public async Task Requests_StillRecordsBeforeMatching_EvenWithAsyncCondition()
    {
        using var handler = new TestHttpHandler();
        handler.OnGet("/orders/1").WithHeader("X-Trace", "expected").Respond(HttpStatusCode.OK);

        using var client = handler.CreateClient(new Uri("https://api.example.com/"));
        try
        {
            await client.GetAsync("/orders/1", TestContext.Current.CancellationToken);
        }
        catch (UnmatchedHttpRequestException)
        {
            // expected - the request should still be recorded despite throwing.
        }

        handler.Requests.Should().ContainSingle(r => r.RequestUri!.PathAndQuery == "/orders/1");
    }

    [Fact]
    public async Task Verify_CountCorrect_WithChainedConditionRegistration()
    {
        using var handler = new TestHttpHandler();
        var registration = handler.OnPost("/orders")
            .WithHeader("X-Trace", "abc")
            .WithFormBody(form => form["sku"].SingleOrDefault() == "widget")
            .Respond(HttpStatusCode.Created);

        using var client = handler.CreateClient(new Uri("https://api.example.com/"));

        using (var matching = new HttpRequestMessage(HttpMethod.Post, "/orders"))
        {
            matching.Headers.Add("X-Trace", "abc");
            matching.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["sku"] = "widget" });
            await client.SendAsync(matching, TestContext.Current.CancellationToken);
        }

        using (var nonMatching = new HttpRequestMessage(HttpMethod.Post, "/orders"))
        {
            nonMatching.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["sku"] = "other" });
            try
            {
                await client.SendAsync(nonMatching, TestContext.Current.CancellationToken);
            }
            catch (UnmatchedHttpRequestException)
            {
            }
        }

        registration.Verify().Once();
    }

    [Fact]
    public async Task ChainedRegistration_RepeatedMatches_GetFreshResponseEachTime()
    {
        using var handler = new TestHttpHandler();
        handler.OnGet("/orders/1").WithHeader("X-Trace", "abc").RespondJson(new { value = 1 });

        using var client = handler.CreateClient(new Uri("https://api.example.com/"));

        using var first = new HttpRequestMessage(HttpMethod.Get, "/orders/1");
        first.Headers.Add("X-Trace", "abc");
        var firstResponse = await client.SendAsync(first, TestContext.Current.CancellationToken);
        firstResponse.Content.Headers.ContentType!.CharSet = "iso-8859-1";

        using var second = new HttpRequestMessage(HttpMethod.Get, "/orders/1");
        second.Headers.Add("X-Trace", "abc");
        var secondResponse = await client.SendAsync(second, TestContext.Current.CancellationToken);

        secondResponse.Content.Headers.ContentType!.CharSet.Should().Be("utf-8");
    }

    [Fact]
    public async Task ConcurrentSendAsync_WithMixOfSyncAndChainedRegistrations()
    {
        using var handler = new TestHttpHandler();
        handler.OnGet("/plain").Respond(HttpStatusCode.OK);
        var chained = handler.OnGet("/traced").WithHeader("X-Trace", "abc").Respond(HttpStatusCode.OK);

        using var client = handler.CreateClient(new Uri("https://api.example.com/"));

        var tasks = new List<Task>();
        for (var i = 0; i < 20; i++)
        {
            tasks.Add(client.GetAsync("/plain", TestContext.Current.CancellationToken));

            using var tracedRequest = new HttpRequestMessage(HttpMethod.Get, "/traced");
            tracedRequest.Headers.Add("X-Trace", "abc");
            tasks.Add(client.SendAsync(tracedRequest, TestContext.Current.CancellationToken));
        }

        await Task.WhenAll(tasks);

        chained.Verify().Exactly(20);
        handler.Requests.Should().HaveCount(40);
    }

    [Fact]
    public async Task PostFinish_WithHeader_ThrowsAndRegistrationUnaffected()
    {
        using var handler = new TestHttpHandler();
        var builder = handler.OnGet("/orders/1");
        var registration = builder.Respond(HttpStatusCode.OK);

        var act = () => builder.WithHeader("X-Test", "abc");

        act.Should().Throw<InvalidOperationException>();

        using var client = handler.CreateClient(new Uri("https://api.example.com/"));
        var response = await client.GetAsync("/orders/1", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        registration.Verify().Once();
    }

    // --- WithHeader (PLAN-0065 task 5) ---

    [Fact]
    public async Task WithHeader_ExactMatch_Matches()
    {
        using var handler = new TestHttpHandler();
        handler.OnGet("/x").WithHeader("X-Trace", "abc").Respond(HttpStatusCode.OK);

        using var client = handler.CreateClient(new Uri("https://api.example.com/"));
        using var request = new HttpRequestMessage(HttpMethod.Get, "/x");
        request.Headers.Add("X-Trace", "abc");
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task WithHeader_CaseInsensitiveName_OrdinalValue()
    {
        using var handler = new TestHttpHandler();
        handler.OnGet("/x").WithHeader("x-trace", "abc").Respond(HttpStatusCode.OK);

        using var client = handler.CreateClient(new Uri("https://api.example.com/"));
        using var request = new HttpRequestMessage(HttpMethod.Get, "/x");
        request.Headers.Add("X-Trace", "abc");
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task WithHeader_ValueInContentHeadersOnly_Matches()
    {
        using var handler = new TestHttpHandler();
        handler.OnPost("/x").WithHeader("Content-Type", "text/plain; charset=utf-8").Respond(HttpStatusCode.OK);

        using var client = handler.CreateClient(new Uri("https://api.example.com/"));
        using var request = new HttpRequestMessage(HttpMethod.Post, "/x") { Content = new StringContent("body") };
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task WithHeader_NamePresentInBothCollectionsWithDifferentValue_MatchingValueInOther_Matches()
    {
        // The exact scenario the ADR-0062 review caught: the header name exists in BOTH
        // collections, request-level has a non-matching value, content-level has the expected
        // value - a precedence-based fallback would wrongly return false here.
        using var handler = new TestHttpHandler();
        handler.OnPost("/x").WithHeader("X-Marker", "expected").Respond(HttpStatusCode.OK);

        using var client = handler.CreateClient(new Uri("https://api.example.com/"));
        using var request = new HttpRequestMessage(HttpMethod.Post, "/x") { Content = new StringContent("body") };
        request.Headers.Add("X-Marker", "not-expected");
        request.Content.Headers.Add("X-Marker", "expected");
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task WithHeader_MultipleValues_ExpectedValueAmongSeveral_Matches()
    {
        using var handler = new TestHttpHandler();
        handler.OnGet("/x").WithHeader("Accept", "application/json").Respond(HttpStatusCode.OK);

        using var client = handler.CreateClient(new Uri("https://api.example.com/"));
        using var request = new HttpRequestMessage(HttpMethod.Get, "/x");
        request.Headers.Add("Accept", "text/plain");
        request.Headers.Add("Accept", "application/json");
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task WithHeader_MissingHeader_NoMatchNoThrow()
    {
        using var handler = new TestHttpHandler();
        handler.OnGet("/x").WithHeader("X-Trace", "abc").Respond(HttpStatusCode.OK);

        using var client = handler.CreateClient(new Uri("https://api.example.com/"));
        var act = async () => await client.GetAsync("/x", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<UnmatchedHttpRequestException>();
    }

    [Theory]
    [InlineData("Authorization")]
    [InlineData("Proxy-Authorization")]
    [InlineData("Cookie")]
    [InlineData("Set-Cookie")]
    public async Task WithHeader_SensitiveHeaderName_RedactsValueInVerifyFailureMessage(string headerName)
    {
        using var handler = new TestHttpHandler();
        var registration = handler.OnGet("/x").WithHeader(headerName, "super-secret-value").Respond(HttpStatusCode.OK);

        using var client = handler.CreateClient(new Uri("https://api.example.com/"));
        using var request = new HttpRequestMessage(HttpMethod.Get, "/x");
        request.Headers.TryAddWithoutValidation(headerName, "super-secret-value");
        await client.SendAsync(request, TestContext.Current.CancellationToken);

        var act = () => registration.Verify().Never();

        act.Should().Throw<TestDoubleVerificationException>()
            .Which.Message.Should().Contain("<redacted>").And.NotContain("super-secret-value");
    }

    [Fact]
    public async Task WithHeader_NonSensitiveHeaderName_ShowsRealValueInVerifyFailureMessage()
    {
        using var handler = new TestHttpHandler();
        var registration = handler.OnGet("/x").WithHeader("X-Trace", "abc123").Respond(HttpStatusCode.OK);

        using var client = handler.CreateClient(new Uri("https://api.example.com/"));
        using var request = new HttpRequestMessage(HttpMethod.Get, "/x");
        request.Headers.Add("X-Trace", "abc123");
        await client.SendAsync(request, TestContext.Current.CancellationToken);

        var act = () => registration.Verify().Never();

        act.Should().Throw<TestDoubleVerificationException>().Which.Message.Should().Contain("abc123");
    }

    // --- WithBody (PLAN-0065 task 5) ---

    [Fact]
    public async Task WithBody_ByteMatch_Matches()
    {
        using var handler = new TestHttpHandler();
        handler.OnPost("/x").WithBody(bytes => bytes is [1, 2, 3]).Respond(HttpStatusCode.OK);

        using var client = handler.CreateClient(new Uri("https://api.example.com/"));
        using var request = new HttpRequestMessage(HttpMethod.Post, "/x") { Content = new ByteArrayContent([1, 2, 3]) };
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task WithBody_PredicateFalse_NoMatch()
    {
        using var handler = new TestHttpHandler();
        handler.OnPost("/x").WithBody(bytes => bytes is [9, 9, 9]).Respond(HttpStatusCode.OK);

        using var client = handler.CreateClient(new Uri("https://api.example.com/"));
        using var request = new HttpRequestMessage(HttpMethod.Post, "/x") { Content = new ByteArrayContent([1, 2, 3]) };
        var act = async () => await client.SendAsync(request, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<UnmatchedHttpRequestException>();
    }

    [Fact]
    public async Task WithBody_NoContent_NoMatchPredicateNeverInvoked()
    {
        using var handler = new TestHttpHandler();
        var invoked = false;
        handler.OnGet("/x").WithBody(_ =>
        {
            invoked = true;
            return true;
        }).Respond(HttpStatusCode.OK);

        using var client = handler.CreateClient(new Uri("https://api.example.com/"));
        var act = async () => await client.GetAsync("/x", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<UnmatchedHttpRequestException>();
        invoked.Should().BeFalse();
    }

    // --- WithFormBody (PLAN-0065 task 5) ---

    [Theory]
    [InlineData("name=Nick+Cipollina", "name", "Nick Cipollina")]
    [InlineData("value=a%2Bb", "value", "a+b")]
    [InlineData("empty=", "empty", "")]
    [InlineData("flag", "flag", "")]
    public async Task WithFormBody_DecodesFieldCorrectly(string body, string key, string expectedValue)
    {
        using var handler = new TestHttpHandler();
        handler.OnPost("/x").WithFormBody(form => form[key].SingleOrDefault() == expectedValue).Respond(HttpStatusCode.OK);

        using var client = handler.CreateClient(new Uri("https://api.example.com/"));
        using var request = new HttpRequestMessage(HttpMethod.Post, "/x")
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/x-www-form-urlencoded"),
        };
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task WithFormBody_DuplicateKeys_BothValuesRetainedInOrder()
    {
        using var handler = new TestHttpHandler();
        handler.OnPost("/x")
            .WithFormBody(form => form["tags"].SequenceEqual(["a", "b"]))
            .Respond(HttpStatusCode.OK);

        using var client = handler.CreateClient(new Uri("https://api.example.com/"));
        using var request = new HttpRequestMessage(HttpMethod.Post, "/x")
        {
            Content = new StringContent("tags=a&tags=b", System.Text.Encoding.UTF8, "application/x-www-form-urlencoded"),
        };
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task WithFormBody_WrongContentType_NoMatchWithoutReadingBody()
    {
        using var handler = new TestHttpHandler();
        var invoked = false;
        handler.OnPost("/x").WithFormBody(_ =>
        {
            invoked = true;
            return true;
        }).Respond(HttpStatusCode.OK);

        using var client = handler.CreateClient(new Uri("https://api.example.com/"));
        using var request = new HttpRequestMessage(HttpMethod.Post, "/x")
        {
            Content = new StringContent("grant_type=refresh_token", System.Text.Encoding.UTF8, "text/plain"),
        };
        var act = async () => await client.SendAsync(request, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<UnmatchedHttpRequestException>();
        invoked.Should().BeFalse();
    }

    [Fact]
    public async Task WithFormBody_CorrectContentTypeAnyCasing_Matches()
    {
        using var handler = new TestHttpHandler();
        handler.OnPost("/x").WithFormBody(form => form["k"].SingleOrDefault() == "v").Respond(HttpStatusCode.OK);

        using var client = handler.CreateClient(new Uri("https://api.example.com/"));
        using var request = new HttpRequestMessage(HttpMethod.Post, "/x")
        {
            Content = new StringContent("k=v", System.Text.Encoding.UTF8, "Application/X-WWW-Form-Urlencoded"),
        };
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // --- WithJsonBody<T> (PLAN-0065 task 5) ---

    [Fact]
    public async Task WithJsonBody_JsonTypeInfoOverload_SatisfyingPredicate_Matches()
    {
        using var handler = new TestHttpHandler();
        handler.OnPost("/orders")
            .WithJsonBody<OrderDto>(o => o is { CustomerId: 42 }, TestHttpHandlerTestsJsonContext.Default.OrderDto)
            .Respond(HttpStatusCode.Created);

        using var client = handler.CreateClient(new Uri("https://api.example.com/"));
        var response = await client.PostAsJsonAsync("/orders", new OrderDto(42, "widget"), TestHttpHandlerTestsJsonContext.Default.OrderDto, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task WithJsonBody_JsonSerializerOptionsOverload_SatisfyingPredicate_Matches()
    {
        using var handler = new TestHttpHandler();
        handler.OnPost("/orders")
            .WithJsonBody<OrderDto>(o => o is { CustomerId: 42 })
            .Respond(HttpStatusCode.Created);

        using var client = handler.CreateClient(new Uri("https://api.example.com/"));
        using var request = new HttpRequestMessage(HttpMethod.Post, "/orders")
        {
            Content = new StringContent(JsonSerializer.Serialize(new OrderDto(42, "widget")), System.Text.Encoding.UTF8, "application/json"),
        };
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task WithJsonBody_PredicateFalse_NoMatch()
    {
        using var handler = new TestHttpHandler();
        handler.OnPost("/orders")
            .WithJsonBody<OrderDto>(o => o is { CustomerId: 999 }, TestHttpHandlerTestsJsonContext.Default.OrderDto)
            .Respond(HttpStatusCode.Created);

        using var client = handler.CreateClient(new Uri("https://api.example.com/"));
        var act = async () => await client.PostAsJsonAsync("/orders", new OrderDto(42, "widget"), TestHttpHandlerTestsJsonContext.Default.OrderDto, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<UnmatchedHttpRequestException>();
    }

    [Fact]
    public async Task WithJsonBody_TextPlainContentType_NoMatchWithoutDeserializing()
    {
        using var handler = new TestHttpHandler();
        var invoked = false;
        handler.OnPost("/orders").WithJsonBody<OrderDto>(_ =>
        {
            invoked = true;
            return true;
        }, TestHttpHandlerTestsJsonContext.Default.OrderDto).Respond(HttpStatusCode.Created);

        using var client = handler.CreateClient(new Uri("https://api.example.com/"));
        using var request = new HttpRequestMessage(HttpMethod.Post, "/orders")
        {
            Content = new StringContent("""{"CustomerId":42,"Sku":"widget"}""", System.Text.Encoding.UTF8, "text/plain"),
        };
        var act = async () => await client.SendAsync(request, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<UnmatchedHttpRequestException>();
        invoked.Should().BeFalse();
    }

    [Fact]
    public async Task WithJsonBody_PlusJsonSuffixContentType_Matches()
    {
        using var handler = new TestHttpHandler();
        handler.OnPost("/orders")
            .WithJsonBody<OrderDto>(o => o is { CustomerId: 42 }, TestHttpHandlerTestsJsonContext.Default.OrderDto)
            .Respond(HttpStatusCode.Created);

        using var client = handler.CreateClient(new Uri("https://api.example.com/"));
        using var request = new HttpRequestMessage(HttpMethod.Post, "/orders")
        {
            Content = new StringContent("""{"CustomerId":42,"Sku":"widget"}""", System.Text.Encoding.UTF8, "application/vnd.api+json"),
        };
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task WithJsonBody_MalformedJson_ThrowsJsonExceptionNotUnmatched()
    {
        using var handler = new TestHttpHandler();
        handler.OnPost("/orders")
            .WithJsonBody<OrderDto>(_ => true, TestHttpHandlerTestsJsonContext.Default.OrderDto)
            .Respond(HttpStatusCode.Created);

        using var client = handler.CreateClient(new Uri("https://api.example.com/"));
        using var request = new HttpRequestMessage(HttpMethod.Post, "/orders")
        {
            Content = new StringContent("not valid json", System.Text.Encoding.UTF8, "application/json"),
        };
        var act = async () => await client.SendAsync(request, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<JsonException>();
    }

    // --- WhenAsync (PLAN-0065 task 5) ---

    [Fact]
    public async Task WhenAsync_RealAsyncPredicate_Matches()
    {
        using var handler = new TestHttpHandler();
        handler.WhenAsync(async (request, cancellationToken) =>
        {
            await Task.Yield();
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return body == "expected";
        }).Respond(HttpStatusCode.OK);

        using var client = handler.CreateClient(new Uri("https://api.example.com/"));
        using var request = new HttpRequestMessage(HttpMethod.Post, "/x") { Content = new StringContent("expected") };
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task WhenAsync_ObservesCancellation()
    {
        using var handler = new TestHttpHandler();
        handler.WhenAsync(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return true;
        }).Respond(HttpStatusCode.OK);

        using var client = handler.CreateClient(new Uri("https://api.example.com/"));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = async () => await client.GetAsync("/x", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // --- Chaining/short-circuit (PLAN-0065 task 5) ---

    [Fact]
    public async Task Chain_FailingHeaderCondition_NeverReadsBody()
    {
        using var handler = new TestHttpHandler();
        handler.OnPost("/x")
            .WithHeader("X-Trace", "expected")
            .WithFormBody(_ => throw new InvalidOperationException("body should never be read"))
            .Respond(HttpStatusCode.OK);

        using var client = handler.CreateClient(new Uri("https://api.example.com/"));
        using var request = new HttpRequestMessage(HttpMethod.Post, "/x")
        {
            Content = new StringContent("k=v", System.Text.Encoding.UTF8, "application/x-www-form-urlencoded"),
        };
        var act = async () => await client.SendAsync(request, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<UnmatchedHttpRequestException>();
    }

    [Fact]
    public async Task Chain_PassingHeaderFailingBody_NoMatch()
    {
        using var handler = new TestHttpHandler();
        handler.OnPost("/x")
            .WithHeader("X-Trace", "expected")
            .WithFormBody(form => form["k"].SingleOrDefault() == "wrong")
            .Respond(HttpStatusCode.OK);

        using var client = handler.CreateClient(new Uri("https://api.example.com/"));
        using var request = new HttpRequestMessage(HttpMethod.Post, "/x")
        {
            Content = new StringContent("k=v", System.Text.Encoding.UTF8, "application/x-www-form-urlencoded"),
        };
        request.Headers.Add("X-Trace", "expected");
        var act = async () => await client.SendAsync(request, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<UnmatchedHttpRequestException>();
    }

    [Fact]
    public async Task Chain_BothConditionsPassing_Matches()
    {
        using var handler = new TestHttpHandler();
        var registration = handler.OnPost("/x")
            .WithHeader("X-Trace", "expected")
            .WithFormBody(form => form["k"].SingleOrDefault() == "v")
            .Respond(HttpStatusCode.OK);

        using var client = handler.CreateClient(new Uri("https://api.example.com/"));
        using var request = new HttpRequestMessage(HttpMethod.Post, "/x")
        {
            Content = new StringContent("k=v", System.Text.Encoding.UTF8, "application/x-www-form-urlencoded"),
        };
        request.Headers.Add("X-Trace", "expected");
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        registration.Verify().Once();
    }
}
