using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TypeSafe.AI.Tests.Support;
using Xunit;

namespace TypeSafe.AI.Tests.Transport;

/// <summary>Tests that use the real <c>HttpClient</c> transport against a loopback server.</summary>
public class LoopbackTransportTests
{
    private static async Task WriteJsonAsync(System.Net.HttpListenerContext context, string json, int status = 200)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        context.Response.Headers["x-typesafe-request-id"] = "req_loop";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
        context.Response.Close();
    }

    private static TypeSafeClient Client(LoopbackServer server, Action<TypeSafeClientOptions>? configure = null)
    {
        TypeSafeClientOptions options = TestClient.Options(o => o.BaseUri = server.Uri);
        configure?.Invoke(options);
        return new TypeSafeClient(options);
    }

    [Fact]
    public async Task OnTheWire_ExactlyOneAuthorizationHeaderAndTheOverridingCustomHeaderAreSent()
    {
        using var server = LoopbackServer.Start((context, _) => WriteJsonAsync(context, Responses.EmptyModels));
        using TypeSafeClient client = Client(server, o => o.DefaultHeaders["authorization"] = "bad");
        var options = new RequestOptions();
        options.Headers["X-Team"] = "call";
        options.Headers["AUTHORIZATION"] = "bad-again";

        await client.Models.ListAsync(options);

        LoopbackRequest received = Assert.Single(server.Requests);
        Assert.Equal("GET", received.Method);
        Assert.Equal("/v1/models", received.Path);
        Assert.Equal(["Bearer sk-test-1234567890"], received.HeaderValues("Authorization"));
        Assert.Equal(["call"], received.HeaderValues("X-Team"));
        Assert.Equal([$"typesafe-sdk-dotnet/{SdkInfo.Version}"], received.HeaderValues("X-TypeSafe-SDK"));
        Assert.Empty(received.HeaderValues("Content-Type"));
    }

    [Fact]
    public async Task PostRequests_SendTheJsonBodyWithAJsonContentType()
    {
        using var server = LoopbackServer.Start((context, _) => WriteJsonAsync(
            context,
            """{"model":"m","answers":{"q":{"type":"noul","noul":0.25}},"usage":{"input_tokens":1,"output_tokens":1}}"""));
        using TypeSafeClient client = Client(server);
        var questions = new QuestionSet();
        var q = questions.Add("q", Question.Noul("héllo?"));

        SystemOneResult result = await client.SystemOneAsync(new SystemOneRequest("state", questions));

        Assert.Equal(0.25, result.Answers.Get(q).Noul);
        LoopbackRequest received = Assert.Single(server.Requests);
        Assert.Equal("POST", received.Method);
        Assert.Equal("/v1/systemone", received.Path);
        Assert.StartsWith("application/json", received.HeaderValues("Content-Type").Single(), StringComparison.Ordinal);
        Assert.Equal("""{"state":"state","questions":{"q":{"type":"noul","instructions":"héllo?"}},"model":"jev-latest"}""", received.Body);
    }

    [Fact]
    public async Task ChunkedResponses_AreFullyBufferedBeforeHandoff()
    {
        using var server = LoopbackServer.Start(async (context, _) =>
        {
            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            context.Response.SendChunked = true;
            foreach (string chunk in new[] { """{"models":[""", """{"name":"a","description":"d",""", "\"release_date\":\"r\"}]}" })
            {
                byte[] bytes = Encoding.UTF8.GetBytes(chunk);
                await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
                await context.Response.OutputStream.FlushAsync();
                await Task.Delay(50);
            }

            context.Response.Close();
        });
        using TypeSafeClient client = Client(server);

        ApiResponse<IReadOnlyList<ModelCard>> response = await client.Models.ListWithResponseAsync();

        Assert.Equal("a", Assert.Single(response.Value).Name);
        Assert.Equal("""{"models":[{"name":"a","description":"d","release_date":"r"}]}""", response.RawResponse.ReadContentAsString());
    }

    [Fact]
    public async Task ServerErrors_AreMappedAndRetriedOverTheRealTransport()
    {
        int calls = 0;
        using var server = LoopbackServer.Start((context, _) => Interlocked.Increment(ref calls) == 1
            ? WriteJsonAsync(context, """{"error":"warming up"}""", 503)
            : WriteJsonAsync(context, Responses.EmptyModels));
        using TypeSafeClient client = Client(server, o => o.RetryPolicy = RetryPolicy.Default with { InitialBackoff = TimeSpan.FromMilliseconds(10) });

        await client.Models.ListAsync();

        Assert.Equal(2, server.Requests.Count);
        Assert.Equal(["1"], server.Requests[1].HeaderValues("X-TypeSafe-Retry-Count"));
    }

    [Fact]
    public async Task UnauthorizedResponses_BecomeAuthenticationExceptions()
    {
        using var server = LoopbackServer.Start((context, _) => WriteJsonAsync(context, """{"error":"Invalid API key"}""", 401));
        using TypeSafeClient client = Client(server);

        var exception = await Assert.ThrowsAsync<AuthenticationException>(() => client.Models.ListAsync());

        Assert.Equal("401 Invalid API key", exception.Message);
        Assert.Equal("req_loop", exception.RequestId);
        Assert.Single(server.Requests);
    }

    [Fact]
    public async Task ASilentServer_TimesOutWithTheRealClock()
    {
        using var server = LoopbackServer.Start((_, _) => Task.Delay(TimeSpan.FromSeconds(30)));
        using TypeSafeClient client = Client(server, o => { o.Timeout = TimeSpan.FromMilliseconds(250); o.RetryPolicy = RetryPolicy.None; });

        var exception = await Assert.ThrowsAsync<ApiTimeoutException>(() => client.Models.ListAsync().WithDeadlineAsync());

        Assert.Equal("Request timed out after 250ms.", exception.Message);
    }

    [Fact]
    public async Task CancellingARealRequest_IsAnAbort()
    {
        using var server = LoopbackServer.Start((_, _) => Task.Delay(TimeSpan.FromSeconds(30)));
        using TypeSafeClient client = Client(server, o => o.RetryPolicy = RetryPolicy.None);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAsync<ApiUserAbortException>(() => client.Models.ListAsync(cancellationToken: cts.Token).WithDeadlineAsync());
    }

    [Fact]
    public async Task ARefusedConnection_IsAConnectionError()
    {
        using TypeSafeClient client = new(TestClient.Options(o =>
        {
            o.BaseUri = new Uri($"http://127.0.0.1:{LoopbackServer.FreePort()}");
            o.RetryPolicy = RetryPolicy.None;
        }));

        var exception = await Assert.ThrowsAsync<ApiConnectionException>(() => client.Models.ListAsync().WithDeadlineAsync());

        Assert.StartsWith("Connection error: ", exception.Message, StringComparison.Ordinal);
        Assert.NotNull(exception.InnerException);
    }

    [Fact]
    public async Task ConcurrentRequests_AllCompleteOverTheRealTransport()
    {
        using var server = LoopbackServer.Start((context, _) => WriteJsonAsync(context, Responses.EmptyModels));
        using TypeSafeClient client = Client(server);

        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => client.Models.ListAsync()));

        Assert.Equal(20, server.Requests.Count);
    }
}
