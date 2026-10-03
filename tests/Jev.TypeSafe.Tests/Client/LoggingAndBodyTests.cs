using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using TypeSafe.AI.Internal.Logging;
using TypeSafe.AI.Tests.Support;
using Xunit;

namespace TypeSafe.AI.Tests.Client;

public class LoggingBehaviorTests
{
    private static (TypeSafeClient Client, RecordingLogger Logger) Create(
        StubHttpMessageHandler handler,
        TypeSafeLogLevel? level,
        Action<TypeSafeClientOptions>? configure = null,
        FakeTimeProvider? time = null)
    {
        var logger = new RecordingLogger();
        TypeSafeClient client = TestClient.Create(
            handler,
            o =>
            {
                o.Logger = logger;
                o.LogLevel = level;
                configure?.Invoke(o);
            },
            time ?? new FakeTimeProvider());
        return (client, logger);
    }

    private static StubHttpMessageHandler Ok() => new(_ => Responses.Json(Responses.EmptyModels, 200, ("x-typesafe-request-id", "req_9")));

    [Fact]
    public async Task DefaultLevel_IsSilentForASuccessfulRequest()
    {
        var (client, logger) = Create(Ok(), level: null);
        using (client)
        {
            await client.Models.ListAsync();
        }

        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task OffLevel_DropsEverythingEvenFromAnExplicitLogger()
    {
        var (client, logger) = Create(new StubHttpMessageHandler(_ => Responses.Json("{}", 503)), TypeSafeLogLevel.Off, o => o.RetryPolicy = RetryPolicy.None);
        using (client)
        {
            await Assert.ThrowsAsync<InternalServerException>(() => client.Models.ListAsync());
        }

        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task InfoLevel_LogsOneSummaryLinePerAttemptAndNothingAtDebug()
    {
        var (client, logger) = Create(Ok(), TypeSafeLogLevel.Info);
        using (client)
        {
            await client.Models.ListAsync();
        }

        Assert.Equal(["#1 GET /v1/models <- 200 in 0ms (request req_9)"], logger.Messages);
        Assert.All(logger.Entries, entry => Assert.Equal(LogLevel.Information, entry.Level));
        Assert.Equal(1002, logger.Entries[0].EventId.Id);
    }

    [Fact]
    public async Task InfoLevel_OmitsTheRequestIdWhenTheServerSendsNone()
    {
        var (client, logger) = Create(new StubHttpMessageHandler(_ => Responses.Json(Responses.EmptyModels)), TypeSafeLogLevel.Info);
        using (client)
        {
            await client.Models.ListAsync();
        }

        Assert.Equal(["#1 GET /v1/models <- 200 in 0ms"], logger.Messages);
    }

    [Fact]
    public async Task DebugLevel_AddsRedactedHeadersAndBodies()
    {
        var (client, logger) = Create(new StubHttpMessageHandler(_ => Responses.Json("""{"model":"m","answers":{},"usage":{"input_tokens":1,"output_tokens":2}}""")), TypeSafeLogLevel.Debug);
        var questions = new QuestionSet();
        questions.Add("q", Question.Noul("?"));
        using (client)
        {
            await client.SystemOneAsync(new SystemOneRequest("hello", questions));
        }

        string[] messages = logger.Messages.ToArray();
        Assert.Equal(3, messages.Length);
        Assert.StartsWith("#1 POST /v1/systemone -> https://api.test/v1/systemone headers={", messages[0], StringComparison.Ordinal);
        Assert.Contains("\"Authorization\":\"Bearer ***7890\"", messages[0], StringComparison.Ordinal);
        Assert.Contains("\"Accept\":\"application/json\"", messages[0], StringComparison.Ordinal);
        Assert.EndsWith("body={\"state\":\"hello\",\"questions\":{\"q\":{\"type\":\"noul\",\"instructions\":\"?\"}},\"model\":\"jev-latest\"}", messages[0], StringComparison.Ordinal);
        Assert.Equal("#1 POST /v1/systemone <- 200 in 0ms", messages[1]);
        Assert.Equal("""#1 POST /v1/systemone <- body {"model":"m","answers":{},"usage":{"input_tokens":1,"output_tokens":2}}""", messages[2]);
        Assert.Equal([1001, 1002, 1003], logger.Entries.Select(entry => entry.EventId.Id));
    }

    [Fact]
    public async Task DebugLevel_GetRequestsHaveNoBody()
    {
        var (client, logger) = Create(Ok(), TypeSafeLogLevel.Debug);
        using (client)
        {
            await client.Models.ListAsync();
        }

        Assert.EndsWith("body=(none)", logger.Messages[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheApiKey_IsNeverLoggedAtAnyLevel()
    {
        int calls = 0;
        var handler = new StubHttpMessageHandler(_ => ++calls switch
        {
            1 => Responses.Json("""{"error":"bad key"}""", 401),
            2 => Responses.Json("{}", 503),
            3 => throw new HttpRequestException("socket closed"),
            _ => Responses.Json(Responses.EmptyModels),
        });
        var (client, logger) = Create(handler, TypeSafeLogLevel.Debug, o => o.RetryPolicy = RetryPolicy.Default with { InitialBackoff = TimeSpan.Zero, MaxRetries = 0 });
        using (client)
        {
            await Assert.ThrowsAsync<AuthenticationException>(() => client.Models.ListAsync());
            await Assert.ThrowsAsync<InternalServerException>(() => client.Models.ListAsync());
            await Assert.ThrowsAsync<ApiConnectionException>(() => client.Models.ListAsync());
            await client.Models.ListAsync();
        }

        Assert.DoesNotContain(TestClient.ApiKey, logger.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(TestClient.ApiKey, string.Join("\n", logger.Entries.Select(e => e.Exception?.ToString())), StringComparison.Ordinal);
        Assert.Contains("Bearer ***7890", logger.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CredentialLookalikeHeaders_AreRedactedToo()
    {
        var (client, logger) = Create(Ok(), TypeSafeLogLevel.Debug, o => o.DefaultHeaders["X-Api-Key"] = "another-secret-value");
        using (client)
        {
            await client.Models.ListAsync();
        }

        Assert.DoesNotContain("another-secret-value", logger.Text, StringComparison.Ordinal);
        Assert.Contains("\"X-Api-Key\":\"***alue\"", logger.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Requests_AreNumberedSoConcurrentCallsCanBeToldApart()
    {
        var (client, logger) = Create(Ok(), TypeSafeLogLevel.Info);
        using (client)
        {
            await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => client.Models.ListAsync()));
        }

        var numbers = logger.Messages.Select(m => m.Split(' ')[0]).ToHashSet();
        Assert.Equal(20, numbers.Count);
        Assert.Contains("#1", numbers);
        Assert.Contains("#20", numbers);
    }

    [Fact]
    public async Task ErrorResponses_LogASummaryAndTheBodyAtDebugWithoutAnyWarning()
    {
        var (client, logger) = Create(new StubHttpMessageHandler(_ => Responses.Json("""{"error":"nope"}""", 404)), TypeSafeLogLevel.Debug, o => o.RetryPolicy = RetryPolicy.None);
        using (client)
        {
            await Assert.ThrowsAsync<NotFoundException>(() => client.Models.ListAsync());
        }

        Assert.Contains("#1 GET /v1/models <- 404 in 0ms", logger.Messages);
        Assert.Contains("""#1 GET /v1/models <- error body {"error":"nope"}""", logger.Messages);
        Assert.DoesNotContain(logger.Entries, entry => entry.Level >= LogLevel.Warning);
        Assert.Contains(logger.Entries, entry => entry.EventId.Id == 1004);
    }

    [Fact]
    public async Task ConnectionFailures_LogTheUnderlyingException()
    {
        var (client, logger) = Create(new StubHttpMessageHandler(_ => throw new HttpRequestException("socket closed")), TypeSafeLogLevel.Info, o => o.RetryPolicy = RetryPolicy.None);
        using (client)
        {
            await Assert.ThrowsAsync<ApiConnectionException>(() => client.Models.ListAsync());
        }

        LogEntry entry = Assert.Single(logger.Entries);
        Assert.Equal("#1 GET /v1/models connection error after 0ms", entry.Message);
        Assert.IsType<HttpRequestException>(entry.Exception);
        Assert.Equal(1007, entry.EventId.Id);
    }

    [Fact]
    public async Task Timeouts_AreLogged()
    {
        var time = new FakeTimeProvider();
        var handler = StubHttpMessageHandler.Hanging();
        var (client, logger) = Create(handler, TypeSafeLogLevel.Info, o => { o.Timeout = TimeSpan.FromMilliseconds(100); o.RetryPolicy = RetryPolicy.None; }, time);
        using (client)
        {
            Task call = client.Models.ListAsync();
            await Async.UntilAsync(() => handler.Count == 1, "the request");
            time.Advance(TimeSpan.FromMilliseconds(100));
            await Assert.ThrowsAsync<ApiTimeoutException>(() => call.WithDeadlineAsync());
        }

        Assert.Equal(["#1 GET /v1/models timed out after 100ms"], logger.Messages);
        Assert.Equal(1006, logger.Entries.Single().EventId.Id);
    }

    [Fact]
    public async Task CallerAborts_AreLogged()
    {
        var handler = StubHttpMessageHandler.Hanging();
        var (client, logger) = Create(handler, TypeSafeLogLevel.Info);
        using var cts = new System.Threading.CancellationTokenSource();
        using (client)
        {
            Task call = client.Models.ListAsync(cancellationToken: cts.Token);
            await Async.UntilAsync(() => handler.Count == 1, "the request");
            cts.Cancel();
            await Assert.ThrowsAsync<ApiUserAbortException>(() => call.WithDeadlineAsync());
        }

        Assert.Equal(["#1 GET /v1/models aborted by caller after 0ms"], logger.Messages);
        Assert.Equal(1008, logger.Entries.Single().EventId.Id);
    }

    [Fact]
    public async Task LoggerFactory_IsUsedWithTheSdkCategory()
    {
        var factory = new RecordingLoggerFactory();
        using TypeSafeClient client = TestClient.Create(Ok(), o => { o.LoggerFactory = factory; o.LogLevel = TypeSafeLogLevel.Info; });

        await client.Models.ListAsync();

        Assert.Equal(["TypeSafe.AI"], factory.Categories);
        Assert.Single(factory.Logger.Entries);
    }

    [Fact]
    public async Task WithoutALogger_TheConsoleFallbackPrefixesLines()
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var options = TestClient.Options(o => o.LogLevel = TypeSafeLogLevel.Info);
        var dependencies = TestClient.Dependencies(createConsoleLogger: () => new ConsoleLogger(output, error));
        using var client = new TypeSafeClient(options, new HttpClient(Ok()), dependencies);

        await client.Models.ListAsync();

        Assert.StartsWith("[typesafe-sdk] #1 GET /v1/models <- 200 in ", output.ToString(), StringComparison.Ordinal);
        Assert.Empty(error.ToString());
    }

    [Fact]
    public void ClientLogLevel_ReflectsTheConfiguration()
    {
        var (client, _) = Create(Ok(), TypeSafeLogLevel.Debug);
        using (client)
        {
            Assert.Equal(TypeSafeLogLevel.Debug, client.LogLevel);
        }
    }
}

public class ResponseBodyRegressionTests
{
    [Theory]
    [InlineData(200)]
    [InlineData(503)]
    public async Task BodyFailures_AreWrappedWithTheirCauseAndHonorDisabledConnectionRetries(int status)
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage((System.Net.HttpStatusCode)status) { Content = new StreamContent(new FailingStream()) });
        using TypeSafeClient client = TestClient.Create(handler, o => o.RetryPolicy = RetryPolicy.Default with { RetryOnConnectionError = false });

        var exception = await Assert.ThrowsAsync<ApiConnectionException>(() => client.Models.ListAsync());

        Assert.Equal("Connection error: connection reset", exception.Message);
        Assert.IsType<IOException>(exception.InnerException);
        Assert.Equal(1, handler.Count);
    }

    [Fact]
    public async Task BodyFailures_AreRetriedWhenConnectionRetriesAreEnabled()
    {
        var handler = new StubHttpMessageHandler(n => n == 1
            ? new HttpResponseMessage { Content = new StreamContent(new FailingStream()) }
            : Responses.Json(Responses.EmptyModels));
        using TypeSafeClient client = TestClient.Create(handler, o => o.RetryPolicy = RetryPolicy.Default with { InitialBackoff = TimeSpan.Zero });

        await client.Models.ListAsync();

        Assert.Equal(2, handler.Count);
    }

    [Fact]
    public async Task ResponsesWithoutContent_AreHandled()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage((System.Net.HttpStatusCode)503) { Content = null });
        using TypeSafeClient client = TestClient.Create(handler, o => o.RetryPolicy = RetryPolicy.None);

        var exception = await Assert.ThrowsAsync<InternalServerException>(() => client.Models.ListAsync());

        Assert.Equal("503 status code (no body)", exception.Message);
        Assert.Null(exception.Body);
    }

    [Fact]
    public async Task NonJsonErrorBodies_AreKeptAsText()
    {
        var handler = new StubHttpMessageHandler(_ => Responses.Text("upstream exploded", 502));
        using TypeSafeClient client = TestClient.Create(handler, o => o.RetryPolicy = RetryPolicy.None);

        var exception = await Assert.ThrowsAsync<InternalServerException>(() => client.Models.ListAsync());

        Assert.Equal("502 upstream exploded", exception.Message);
        Assert.Equal("upstream exploded", exception.Body);
    }

    [Fact]
    public async Task JsonErrorBodies_ParseEvenWithoutAContentType()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage((System.Net.HttpStatusCode)400) { Content = new StringContent("""{"error":"bad"}""") });
        using TypeSafeClient client = TestClient.Create(handler, o => o.RetryPolicy = RetryPolicy.None);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => client.Models.ListAsync());

        Assert.Equal("400 bad", exception.Message);
    }

    [Fact]
    public async Task ResponseHeaders_AreAvailableOnTheRawResponse()
    {
        var handler = new StubHttpMessageHandler(_ => Responses.Json(Responses.EmptyModels, 200, ("X-Custom", "a"), ("x-typesafe-request-id", "req_5")));
        using TypeSafeClient client = TestClient.Create(handler);

        ApiResponse<IReadOnlyList<ModelCard>> response = await client.Models.ListWithResponseAsync();

        Assert.True(response.RawResponse.TryGetHeader("x-custom", out string? custom));
        Assert.Equal("a", custom);
        Assert.Equal("req_5", response.RequestId);
        Assert.Contains("Content-Type", response.RawResponse.Headers.Keys, StringComparer.OrdinalIgnoreCase);
    }
}
