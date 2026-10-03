using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TypeSafe.AI.Internal.Configuration;
using TypeSafe.AI.Internal.Transport;
using TypeSafe.AI.Testing;
using TypeSafe.AI.Tests.Support;
using Xunit;

namespace TypeSafe.AI.Tests.Client;

/// <summary>Verifies the application layer in isolation, with a fake <see cref="IApiTransport"/> and no HTTP.</summary>
public class TransportSeamTests
{
    private sealed class FakeTransport : IApiTransport
    {
        private readonly Func<ApiRequest, TransportResponse> _respond;

        public FakeTransport(Func<ApiRequest, TransportResponse> respond) => _respond = respond;

        public List<ApiRequest> Requests { get; } = [];

        public CancellationToken LastToken { get; private set; }

        public Task<TransportResponse> SendAsync(ApiRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            LastToken = cancellationToken;
            return Task.FromResult(_respond(request));
        }
    }

    private static TransportResponse Ok(string json)
    {
        RawResponse raw = TypeSafeModelFactory.RawResponse(200, json, [new("x-typesafe-request-id", "req_seam")]);
        return new TransportResponse(raw, ResponseBodyParser.Parse(raw));
    }

    private static TypeSafeClient Client(FakeTransport transport, Action<TypeSafeClientOptions>? configure = null)
    {
        ClientConfiguration configuration = ClientConfigurationResolver.Resolve(TestClient.Options(configure), TestClient.Dependencies());
        return new TypeSafeClient(configuration, transport);
    }

    [Fact]
    public async Task SystemOne_HandsTheTransportAPostRequestWithTheSerializedBodyAndResolvedOptions()
    {
        var transport = new FakeTransport(_ => Ok("""{"model":"m","answers":{"q":{"type":"noul","noul":0.5}},"usage":{"input_tokens":1,"output_tokens":1}}"""));
        using TypeSafeClient client = Client(transport, o => o.Timeout = TimeSpan.FromSeconds(7));
        var questions = new QuestionSet();
        var q = questions.Add("q", Question.Noul("?"));
        using var cts = new CancellationTokenSource();
        var options = new RequestOptions { Timeout = TimeSpan.FromSeconds(3) };

        ApiResponse<SystemOneResult> response = await client.SystemOneWithResponseAsync(new SystemOneRequest("s", questions), options, cts.Token);

        ApiRequest request = Assert.Single(transport.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/v1/systemone", request.Path);
        Assert.Equal("""{"state":"s","questions":{"q":{"type":"noul","instructions":"?"}},"model":"jev-latest"}""", System.Text.Encoding.UTF8.GetString(request.Body!));
        Assert.Equal(TimeSpan.FromSeconds(3), request.Options.Timeout);
        Assert.Equal(cts.Token, transport.LastToken);
        Assert.Equal(0.5, response.Value.Answers.Get(q).Noul);
        Assert.Equal("req_seam", response.RequestId);
    }

    [Fact]
    public async Task Models_HandsTheTransportABodylessGetRequest()
    {
        var transport = new FakeTransport(_ => Ok("""{"models":[{"name":"a","description":"b","release_date":"c"}]}"""));
        using TypeSafeClient client = Client(transport);

        IReadOnlyList<ModelCard> models = await client.Models.ListAsync();

        ApiRequest request = Assert.Single(transport.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/v1/models", request.Path);
        Assert.Null(request.Body);
        Assert.Equal("a", Assert.Single(models).Name);
    }

    [Fact]
    public async Task TransportFailures_PropagateUnchanged()
    {
        var failure = new ApiConnectionException("down");
        var transport = new FakeTransport(_ => throw failure);
        using TypeSafeClient client = Client(transport);

        Assert.Same(failure, await Assert.ThrowsAsync<ApiConnectionException>(() => client.Models.ListAsync()));
    }

    [Fact]
    public async Task MalformedSuccessBodies_BecomeShapeErrors()
    {
        var transport = new FakeTransport(_ => Ok("[]"));
        using TypeSafeClient client = Client(transport);

        await Assert.ThrowsAsync<TypeSafeException>(() => client.Models.ListAsync());
        await Assert.ThrowsAsync<TypeSafeException>(() => client.SystemOneAsync(new SystemOneRequest("s", OneQuestion())));
    }

    [Fact]
    public void DisposedClients_NeverReachTheTransport()
    {
        var transport = new FakeTransport(_ => Ok("{}"));
        TypeSafeClient client = Client(transport);
        client.Dispose();

        Sync.Throws<ObjectDisposedException>(() => client.Models.ListAsync());
        Sync.Throws<ObjectDisposedException>(() => client.SystemOneAsync(new SystemOneRequest("s", OneQuestion())));

        Assert.Empty(transport.Requests);
    }

    private static QuestionSet OneQuestion()
    {
        var questions = new QuestionSet();
        questions.Add("q", Question.Noul("?"));
        return questions;
    }
}
