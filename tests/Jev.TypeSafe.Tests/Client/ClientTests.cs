using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using TypeSafe.AI.Tests.Support;
using Xunit;

namespace TypeSafe.AI.Tests.Client;

public class ClientConfigurationTests
{
    [Fact]
    public void Properties_ReflectTheResolvedConfiguration()
    {
        var handler = new StubHttpMessageHandler(_ => Responses.Json("{}"));
        using TypeSafeClient client = TestClient.Create(handler, o =>
        {
            o.DefaultModel = "jev-test";
            o.Timeout = TimeSpan.FromSeconds(3);
            o.RetryPolicy = RetryPolicy.None;
            o.LogLevel = TypeSafeLogLevel.Error;
            o.DefaultHeaders["X-Team"] = "blue";
        });

        Assert.Equal(new Uri("https://api.test"), client.BaseUri);
        Assert.Equal("jev-test", client.DefaultModel);
        Assert.Equal(TimeSpan.FromSeconds(3), client.Timeout);
        Assert.Same(RetryPolicy.None, client.RetryPolicy);
        Assert.Equal(TypeSafeLogLevel.Error, client.LogLevel);
        Assert.Equal("blue", client.DefaultHeaders["x-team"]);
        Assert.NotNull(client.Models);
    }

    [Fact]
    public void Defaults_MatchTheDocumentedValues()
    {
        var handler = new StubHttpMessageHandler(_ => Responses.Json("{}"));
        var options = new TypeSafeClientOptions { ApiKey = "k" };

        using var client = new TypeSafeClient(options, new HttpClient(handler), TestClient.Dependencies());

        Assert.Equal(new Uri("https://api.typesafe.ai"), client.BaseUri);
        Assert.Equal("jev-latest", client.DefaultModel);
        Assert.Equal(TimeSpan.FromSeconds(10), client.Timeout);
        Assert.Equal(RetryPolicy.Default, client.RetryPolicy);
        Assert.Equal(TypeSafeLogLevel.Warn, client.LogLevel);
        Assert.Empty(client.DefaultHeaders);
    }

    [Fact]
    public async Task EnvironmentVariables_AreUsedWhenOptionsAreNotSet()
    {
        FakeEnvironmentReader environment = new FakeEnvironmentReader()
            .Set(TypeSafeEnvironmentVariables.ApiKey, "env-key")
            .Set(TypeSafeEnvironmentVariables.BaseUrl, "https://env.test/")
            .Set(TypeSafeEnvironmentVariables.DefaultModel, "env-model");
        var handler = new StubHttpMessageHandler(_ => Responses.Json(Responses.EmptyModels));

        using var client = new TypeSafeClient(new TypeSafeClientOptions(), new HttpClient(handler), TestClient.Dependencies(environment: environment));
        await client.Models.ListAsync();

        Assert.Equal("env-model", client.DefaultModel);
        Assert.Equal("https://env.test/v1/models", handler.Single.Uri.ToString());
        Assert.Equal("Bearer env-key", handler.Single.Header("Authorization"));
    }

    [Fact]
    public void MissingApiKey_ThrowsAtConstruction()
    {
        var exception = Assert.Throws<TypeSafeException>(
            () => new TypeSafeClient(new TypeSafeClientOptions(), new HttpClient(), TestClient.Dependencies()));

        Assert.Contains("TYPESAFE_API_KEY", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Browser_IsRefusedUnlessAllowed()
    {
        Assert.Throws<TypeSafeException>(
            () => new TypeSafeClient(TestClient.Options(), new HttpClient(), TestClient.Dependencies(isBrowser: true)));

        using var allowed = new TypeSafeClient(
            TestClient.Options(o => o.DangerouslyAllowBrowser = true),
            new HttpClient(),
            TestClient.Dependencies(isBrowser: true));
        Assert.NotNull(allowed);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void InvalidTimeout_ThrowsAtConstruction(int seconds)
    {
        Assert.Throws<TypeSafeException>(() => TestClient.Create(new StubHttpMessageHandler(_ => Responses.Json("{}")), o => o.Timeout = TimeSpan.FromSeconds(seconds)));
    }

    [Fact]
    public void ToString_NeverIncludesTheApiKey()
    {
        using TypeSafeClient client = TestClient.Create(new StubHttpMessageHandler(_ => Responses.Json("{}")));

        string text = client.ToString();

        Assert.DoesNotContain(TestClient.ApiKey, text, StringComparison.Ordinal);
        Assert.Contains("https://api.test", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Options_AreSnapshottedAtConstruction()
    {
        var handler = new StubHttpMessageHandler(_ => Responses.Json(Responses.EmptyModels));
        TypeSafeClientOptions options = TestClient.Options();
        options.DefaultHeaders["X-Before"] = "yes";
        using var client = new TypeSafeClient(options, new HttpClient(handler), TestClient.Dependencies());

        options.ApiKey = "changed";
        options.DefaultModel = "changed";
        options.DefaultHeaders["X-After"] = "yes";
        await client.Models.ListAsync();

        Assert.Equal($"Bearer {TestClient.ApiKey}", handler.Single.Header("Authorization"));
        Assert.True(handler.Single.HasHeader("X-Before"));
        Assert.False(handler.Single.HasHeader("X-After"));
        Assert.Equal("jev-latest", client.DefaultModel);
    }

    [Fact]
    public void ParameterlessConstructor_ReadsTheApiKeyFromTheEnvironment()
    {
        bool hasKey = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(TypeSafeEnvironmentVariables.ApiKey));

        if (hasKey)
        {
            using var client = new TypeSafeClient();
            Assert.NotNull(client.Models);
        }
        else
        {
            var exception = Assert.Throws<TypeSafeException>(() => new TypeSafeClient());
            Assert.Contains("TYPESAFE_API_KEY", exception.Message, StringComparison.Ordinal);
        }
    }
    [Fact]
    public void PublicConstructors_ValidateArguments()
    {
        Assert.Throws<ArgumentNullException>(() => new TypeSafeClient(null!));
        Assert.Throws<ArgumentNullException>(() => new TypeSafeClient(TestClient.Options(), null!));
        Assert.Throws<ArgumentNullException>(() => new TypeSafeClient(null!, new HttpClient()));
    }

    [Fact]
    public async Task PublicConstructor_WithHttpClient_SendsThroughIt()
    {
        var handler = new StubHttpMessageHandler(_ => Responses.Json(Responses.EmptyModels));
        using var httpClient = new HttpClient(handler);
        using var client = new TypeSafeClient(TestClient.Options(), httpClient);

        await client.Models.ListAsync();

        Assert.Equal(1, handler.Count);
    }

    [Fact]
    public void PublicConstructor_WithoutHttpClient_CreatesAndDisposesItsOwn()
    {
        var client = new TypeSafeClient(TestClient.Options());

        client.Dispose();
        client.Dispose();

        Sync.Throws<ObjectDisposedException>(() => client.Models.ListAsync());
    }
}

public class SystemOneClientTests
{
    private const string Response = """
        {"model":"jev-1","answers":{
          "isBilling":{"type":"noul","noul":0.93},
          "tone":{"type":"choice","choice":"frustrated","confidence":0.7,"probabilities":{"Calm":0.1,"frustrated":0.7,"Angry":0.2}},
          "urgency":{"type":"score","score":1.7,"confidence":0.6,"legend":{"0":"can wait","1":"this week","2":"today"},"probabilities":{"0":0.1,"1":0.2,"2":0.7}}
        },"usage":{"input_tokens":12,"output_tokens":3}}
        """;

    private static (SystemOneRequest Request, QuestionKey<NoulAnswer> Billing, QuestionKey<ChoiceAnswer<Tone>> Tone, QuestionKey<ScoreAnswer> Urgency) Build()
    {
        var questions = new QuestionSet();
        var billing = questions.Add("isBilling", Question.Noul("Is this about billing?"));
        var tone = questions.Add("tone", Question.Choice<Tone>("Tone?"));
        var urgency = questions.Add("urgency", Question.Score("How urgent?", "can wait", "this week", "today"));
        return (new SystemOneRequest(new JsonObject { ["subject"] = "Charged twice" }, questions), billing, tone, urgency);
    }

    [Fact]
    public async Task SystemOneAsync_PostsTheExactPayloadWithTheDefaultModel()
    {
        var handler = new StubHttpMessageHandler(_ => Responses.Json(Response));
        using TypeSafeClient client = TestClient.Create(handler);

        await client.SystemOneAsync(Build().Request);

        RecordedRequest sent = handler.Single;
        Assert.Equal(HttpMethod.Post, sent.Method);
        Assert.Equal("https://api.test/v1/systemone", sent.Uri.ToString());
        Assert.Equal(
            """{"state":{"subject":"Charged twice"},"questions":{"isBilling":{"type":"noul","instructions":"Is this about billing?"},"tone":{"type":"choice","instructions":"Tone?","criteria":{"Calm":null,"frustrated":null,"Angry":null}},"urgency":{"type":"score","instructions":"How urgent?","criteria":["can wait","this week","today"]}},"model":"jev-latest"}""",
            sent.Body);
        Assert.StartsWith("application/json", sent.Header("Content-Type"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SystemOneAsync_ReturnsTypedAnswersModelAndUsage()
    {
        var (request, billing, tone, urgency) = Build();
        using TypeSafeClient client = TestClient.Create(new StubHttpMessageHandler(_ => Responses.Json(Response)));

        SystemOneResult result = await client.SystemOneAsync(request);

        Assert.Equal("jev-1", result.Model);
        Assert.Equal(12, result.Usage.InputTokens);
        Assert.Equal(3, result.Usage.OutputTokens);
        Assert.Equal(0.93, result.Answers.Get(billing).Noul);
        Assert.Equal(Tone.Frustrated, result.Answers.Get(tone).Value);
        Assert.Equal(1.7, result.Answers.Get(urgency).Score);
    }

    [Fact]
    public async Task SystemOneAsync_ModelOverrides_ClientDefaultAndPerRequest()
    {
        var handler = new StubHttpMessageHandler(_ => Responses.Json(Response));
        using TypeSafeClient client = TestClient.Create(handler, o => o.DefaultModel = "client-default");

        await client.SystemOneAsync(Build().Request);
        await client.SystemOneAsync(new SystemOneRequest("s", Build().Request.Questions) { Model = "per-request" });

        Assert.Contains("\"model\":\"client-default\"", handler.Requests[0].Body, StringComparison.Ordinal);
        Assert.Contains("\"model\":\"per-request\"", handler.Requests[1].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SystemOneWithResponseAsync_ExposesTheRawResponseAndRequestId()
    {
        var handler = new StubHttpMessageHandler(_ => Responses.Json(Response, 200, ("x-typesafe-request-id", "req_abc")));
        using TypeSafeClient client = TestClient.Create(handler);

        ApiResponse<SystemOneResult> response = await client.SystemOneWithResponseAsync(Build().Request);

        Assert.Equal("req_abc", response.RequestId);
        Assert.Equal(200, response.RawResponse.StatusCode);
        Assert.Equal("jev-1", response.Value.Model);
        using JsonDocument document = response.RawResponse.ParseContentAsJson();
        Assert.Equal(12, document.RootElement.GetProperty("usage").GetProperty("input_tokens").GetInt32());
        Assert.True(response.RawResponse.TryGetHeader("Content-Type", out string? contentType));
        Assert.StartsWith("application/json", contentType, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SystemOneAsync_AdditionalProperties_AreForwardedAndDoNotLeakBetweenRequests()
    {
        var handler = new StubHttpMessageHandler(_ => Responses.Json(Response));
        using TypeSafeClient client = TestClient.Create(handler);
        SystemOneRequest withExtras = Build().Request;
        withExtras.AdditionalProperties["future_option"] = null;
        withExtras.AdditionalProperties["nested"] = new JsonObject { ["enabled"] = true };

        await client.SystemOneAsync(withExtras);
        await client.SystemOneAsync(Build().Request);

        Assert.Contains(""","future_option":null,"nested":{"enabled":true}}""", handler.Requests[0].Body, StringComparison.Ordinal);
        Assert.DoesNotContain("future_option", handler.Requests[1].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SystemOneAsync_QuestionNamesLikeProtoAreSentVerbatim()
    {
        var handler = new StubHttpMessageHandler(_ => Responses.Json(Response));
        using TypeSafeClient client = TestClient.Create(handler);
        var questions = new QuestionSet();
        questions.Add("__proto__", Question.Noul("?"));
        questions.Add("score", Question.Score("?", "no", "yes"));

        await client.SystemOneAsync(new SystemOneRequest("s", questions));

        using JsonDocument body = JsonDocument.Parse(handler.Single.Body!);
        JsonElement sent = body.RootElement.GetProperty("questions");
        Assert.Equal(["__proto__", "score"], sent.EnumerateObject().Select(p => p.Name));
        Assert.Equal("noul", sent.EnumerateObject().First().Value.GetProperty("type").GetString());
        Assert.Equal(2, sent.GetProperty("score").GetProperty("criteria").GetArrayLength());
    }

    [Fact]
    public async Task SystemOneAsync_DoesNotMutateTheCallersObjects()
    {
        var handler = new StubHttpMessageHandler(_ => Responses.Json(Response));
        using TypeSafeClient client = TestClient.Create(handler);
        var state = new JsonObject { ["a"] = 1 };
        var questions = new QuestionSet();
        questions.Add("q", Question.Noul("?"));
        var request = new SystemOneRequest(state, questions);
        request.AdditionalProperties["x"] = new JsonObject { ["y"] = 1 };

        await client.SystemOneAsync(request);
        await client.SystemOneAsync(request);

        Assert.Equal(handler.Requests[0].Body, handler.Requests[1].Body);
        Assert.Equal("""{"a":1}""", state.ToJsonString());
        Assert.Single(questions);
    }

    [Fact]
    public void SystemOneAsync_InvalidRequests_ThrowSynchronouslyBeforeSending()
    {
        var handler = new StubHttpMessageHandler(_ => Responses.Json(Response));
        using TypeSafeClient client = TestClient.Create(handler);
        var oneScore = new QuestionSet();
        oneScore.Add("q", Question.Score("?", "only"));
        SystemOneRequest reserved = Build().Request;
        reserved.AdditionalProperties["model"] = "sneaky";

        Sync.Throws<ArgumentNullException>(() => client.SystemOneAsync(null!));
        Sync.Throws<TypeSafeException>(() => client.SystemOneAsync(new SystemOneRequest("s", new QuestionSet())));
        Sync.Throws<TypeSafeException>(() => client.SystemOneAsync(new SystemOneRequest("s", oneScore)));
        Sync.Throws<ArgumentException>(() => client.SystemOneAsync(reserved));
        Sync.Throws<TypeSafeException>(() => client.SystemOneAsync(Build().Request, new RequestOptions { Timeout = TimeSpan.Zero }));
        Sync.Throws<ArgumentNullException>(() => client.SystemOneWithResponseAsync(null!));

        Assert.Equal(0, handler.Count);
    }

    [Fact]
    public async Task SystemOneAsync_UnexpectedSuccessBody_ThrowsTypeSafeException()
    {
        using TypeSafeClient client = TestClient.Create(new StubHttpMessageHandler(_ => Responses.Json("""{"ok":true}""")));

        var exception = await Assert.ThrowsAsync<TypeSafeException>(() => client.SystemOneAsync(Build().Request));

        Assert.StartsWith("Unexpected response shape from POST /v1/systemone", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SystemOneAsync_EmptySuccessBody_ThrowsTypeSafeException()
    {
        using TypeSafeClient client = TestClient.Create(new StubHttpMessageHandler(_ => Responses.Empty()));

        await Assert.ThrowsAsync<TypeSafeException>(() => client.SystemOneAsync(Build().Request));
    }

    [Fact]
    public async Task SystemOneAsync_ServerErrors_SurfaceAsApiExceptionsWithDetails()
    {
        var handler = new StubHttpMessageHandler(_ => Responses.Json("""{"detail":"Unknown model: no-such-model"}""", 400, ("x-typesafe-request-id", "req_9")));
        using TypeSafeClient client = TestClient.Create(handler);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => client.SystemOneAsync(new SystemOneRequest("hello", Build().Request.Questions) { Model = "no-such-model" }));

        Assert.Equal("400 Unknown model: no-such-model", exception.Message);
        Assert.Equal("req_9", exception.RequestId);
        Assert.Equal(1, handler.Count);
    }

    [Fact]
    public async Task SystemOneAsync_ConcurrentCalls_AreIndependent()
    {
        var handler = new StubHttpMessageHandler(_ => Responses.Json(Response));
        using TypeSafeClient client = TestClient.Create(handler);
        SystemOneRequest request = Build().Request;

        SystemOneResult[] results = await Task.WhenAll(Enumerable.Range(0, 25).Select(_ => client.SystemOneAsync(request)));

        Assert.Equal(25, results.Length);
        Assert.Equal(25, handler.Count);
        Assert.All(results, result => Assert.Equal("jev-1", result.Model));
    }
}

public class ModelsClientTests
{
    private const string Models = """{"models":[{"name":"jev-1","description":"first","release_date":"2026-01-01","tags":["internal"]}]}""";

    [Fact]
    public async Task ListAsync_GetsModelsWithoutABodyOrContentType()
    {
        var handler = new StubHttpMessageHandler(_ => Responses.Json(Models));
        using TypeSafeClient client = TestClient.Create(handler);

        IReadOnlyList<ModelCard> models = await client.Models.ListAsync();

        Assert.Equal(HttpMethod.Get, handler.Single.Method);
        Assert.Equal("https://api.test/v1/models", handler.Single.Uri.ToString());
        Assert.Null(handler.Single.Body);
        Assert.False(handler.Single.HasHeader("Content-Type"));
        ModelCard card = Assert.Single(models);
        Assert.Equal(("jev-1", "first", "2026-01-01"), (card.Name, card.Description, card.ReleaseDate));
        Assert.True(card.AdditionalProperties.ContainsKey("tags"));
    }

    [Fact]
    public async Task ListWithResponseAsync_KeepsEmployeeOnlyFieldsInTheRawBody()
    {
        var handler = new StubHttpMessageHandler(_ => Responses.Json(Models, 200, ("x-typesafe-request-id", "req_1")));
        using TypeSafeClient client = TestClient.Create(handler);

        ApiResponse<IReadOnlyList<ModelCard>> response = await client.Models.ListWithResponseAsync();

        Assert.Equal("req_1", response.RequestId);
        Assert.Single(response.Value);
        Assert.Contains("\"tags\":[\"internal\"]", response.RawResponse.ReadContentAsString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListAsync_EmptyList_IsEmpty()
    {
        using TypeSafeClient client = TestClient.Create(new StubHttpMessageHandler(_ => Responses.Json(Responses.EmptyModels)));

        Assert.Empty(await client.Models.ListAsync());
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("""{"models":{"models":[]}}""")]
    [InlineData("""{"models":null}""")]
    [InlineData("""{"models":"bad"}""")]
    [InlineData("""{"ok":true}""")]
    public async Task ListAsync_UnrecognizedShape_FailsClearly(string wire)
    {
        using TypeSafeClient client = TestClient.Create(new StubHttpMessageHandler(_ => Responses.Json(wire)));

        var exception = await Assert.ThrowsAsync<TypeSafeException>(() => client.Models.ListAsync());

        Assert.StartsWith("Unexpected response shape from GET /v1/models", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListAsync_NonJsonSuccessBody_FailsClearly()
    {
        using TypeSafeClient client = TestClient.Create(new StubHttpMessageHandler(_ => Responses.Text("<html>proxy</html>")));

        await Assert.ThrowsAsync<TypeSafeException>(() => client.Models.ListAsync());
    }

    [Fact]
    public async Task ListAsync_BadApiKey_ThrowsAuthenticationException()
    {
        var handler = new StubHttpMessageHandler(_ => Responses.Json("""{"error":"Invalid API key"}""", 401));
        using TypeSafeClient client = TestClient.Create(handler, o => o.RetryPolicy = RetryPolicy.None);

        var exception = await Assert.ThrowsAsync<AuthenticationException>(() => client.Models.ListAsync());

        Assert.Equal("401 Invalid API key", exception.Message);
        Assert.Equal(1, handler.Count);
    }

    [Fact]
    public async Task ListWithResponseAsync_ErrorStatuses_FaultToo()
    {
        using TypeSafeClient client = TestClient.Create(
            new StubHttpMessageHandler(_ => Responses.Json("{}", 404)),
            o => o.RetryPolicy = RetryPolicy.None);

        await Assert.ThrowsAsync<NotFoundException>(() => client.Models.ListWithResponseAsync());
    }

    [Fact]
    public void ListAsync_InvalidOptions_ThrowSynchronously()
    {
        using TypeSafeClient client = TestClient.Create(new StubHttpMessageHandler(_ => Responses.Json(Models)));

        Sync.Throws<TypeSafeException>(() => client.Models.ListAsync(new RequestOptions { Timeout = TimeSpan.FromSeconds(-1) }));
    }
}
