using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using TypeSafe.AI.Tests.Support;
using Xunit;

namespace TypeSafe.AI.Tests.Client;

public class HeaderBehaviorTests
{
    private static TypeSafeClient Client(StubHttpMessageHandler handler, Action<TypeSafeClientOptions>? configure = null)
        => TestClient.Create(handler, configure);

    private static SystemOneRequest PostRequest()
    {
        var questions = new QuestionSet();
        questions.Add("q", Question.Noul("?"));
        return new SystemOneRequest("s", questions);
    }

    [Fact]
    public async Task Requests_CarryAuthAndIdentifyingHeaders()
    {
        var handler = new StubHttpMessageHandler(_ => Responses.Json(Responses.EmptyModels));
        using TypeSafeClient client = Client(handler);

        await client.Models.ListAsync();

        RecordedRequest sent = handler.Single;
        Assert.Equal("Bearer sk-test-1234567890", sent.Header("Authorization"));
        Assert.Equal("application/json", sent.Header("Accept"));
        Assert.Equal($"typesafe-sdk-dotnet/{SdkInfo.Version}", sent.Header("User-Agent"));
        Assert.Equal($"typesafe-sdk-dotnet/{SdkInfo.Version}", sent.Header("X-TypeSafe-SDK"));
        Assert.Matches(@"^(dotnet|dotnet-framework|mono)/\d", sent.Header("X-TypeSafe-Runtime"));
    }

    [Fact]
    public async Task DefaultAndPerCallHeaders_AreMerged_PerCallWinning_CaseInsensitively()
    {
        var handler = new StubHttpMessageHandler(_ => Responses.Json(Responses.EmptyModels));
        using TypeSafeClient client = Client(handler, o =>
        {
            o.DefaultHeaders["X-Team"] = "default";
            o.DefaultHeaders["X-Only-Default"] = "kept";
        });
        var options = new RequestOptions();
        options.Headers["x-team"] = "call";
        options.Headers["X-Only-Call"] = "added";

        await client.Models.ListAsync(options);

        Assert.Equal("call", handler.Single.Header("X-Team"));
        Assert.Equal("kept", handler.Single.Header("X-Only-Default"));
        Assert.Equal("added", handler.Single.Header("X-Only-Call"));
    }

    [Fact]
    public async Task ProtectedHeaders_CannotBeOverriddenOnAnyAttemptInAnyCasing()
    {
        int calls = 0;
        var handler = new StubHttpMessageHandler(_ => ++calls == 1
            ? Responses.Json("{}", 503)
            : Responses.Json("""{"model":"m","answers":{},"usage":{"input_tokens":0,"output_tokens":0}}"""));
        using TypeSafeClient client = Client(handler, o =>
        {
            o.RetryPolicy = RetryPolicy.Default with { MaxRetries = 1, InitialBackoff = TimeSpan.Zero };
            o.DefaultHeaders["X-Team"] = "default";
            o.DefaultHeaders["authorization"] = "bad";
            o.DefaultHeaders["content-type"] = "text/plain";
            o.DefaultHeaders["x-typesafe-retry-count"] = "99";
        });
        var options = new RequestOptions();
        foreach ((string name, string value) in new[]
        {
            ("AUTHORIZATION", "bad-again"),
            ("ACCEPT", "text/plain"),
            ("USER-AGENT", "bad"),
            ("X-TYPESAFE-SDK", "bad"),
            ("X-TYPESAFE-RUNTIME", "bad"),
            ("CONTENT-TYPE", "text/html"),
            ("X-TYPESAFE-RETRY-COUNT", "88"),
        })
        {
            options.Headers[name] = value;
        }

        await client.SystemOneAsync(PostRequest(), options);

        Assert.Equal(2, handler.Count);
        foreach (RecordedRequest sent in handler.Requests)
        {
            Assert.Equal("Bearer sk-test-1234567890", sent.Header("Authorization"));
            Assert.Equal("application/json", sent.Header("Accept"));
            Assert.Equal($"typesafe-sdk-dotnet/{SdkInfo.Version}", sent.Header("User-Agent"));
            Assert.Equal($"typesafe-sdk-dotnet/{SdkInfo.Version}", sent.Header("X-TypeSafe-SDK"));
            Assert.Matches(@"^(dotnet|dotnet-framework|mono)/\d", sent.Header("X-TypeSafe-Runtime"));
            Assert.StartsWith("application/json", sent.Header("Content-Type"), StringComparison.Ordinal);
            Assert.Equal("default", sent.Header("X-Team"));
        }

        Assert.False(handler.Requests[0].HasHeader("X-TypeSafe-Retry-Count"));
        Assert.Equal("1", handler.Requests[1].Header("X-TypeSafe-Retry-Count"));
    }

    [Fact]
    public async Task GetRequests_DoNotSendACallerSuppliedContentTypeOrRetryCount()
    {
        var handler = new StubHttpMessageHandler(_ => Responses.Json(Responses.EmptyModels));
        using TypeSafeClient client = Client(handler);
        var options = new RequestOptions();
        options.Headers["Content-Type"] = "text/plain";
        options.Headers["X-TypeSafe-Retry-Count"] = "5";

        await client.Models.ListAsync(options);

        Assert.False(handler.Single.HasHeader("Content-Type"));
        Assert.False(handler.Single.HasHeader("X-TypeSafe-Retry-Count"));
    }

    [Fact]
    public async Task ContentHeaders_AreSentOnRequestsWithABody_ButNotOnBodylessOnes()
    {
        var handler = new StubHttpMessageHandler(n => n == 1
            ? Responses.Json("""{"model":"m","answers":{},"usage":{"input_tokens":0,"output_tokens":0}}""")
            : Responses.Json(Responses.EmptyModels));
        using TypeSafeClient client = Client(handler);
        var options = new RequestOptions();
        options.Headers["Content-Language"] = "en";

        await client.SystemOneAsync(PostRequest(), options);
        await client.Models.ListAsync(options);

        Assert.Equal("en", handler.Requests[0].Header("Content-Language"));
        Assert.False(handler.Requests[1].HasHeader("Content-Language"));
    }
    [Fact]
    public async Task EachRetryUsesAFreshRetryCount()
    {
        var handler = new StubHttpMessageHandler(_ => Responses.Json("{}", 503));
        using TypeSafeClient client = Client(handler, o => o.RetryPolicy = RetryPolicy.Default with { InitialBackoff = TimeSpan.Zero });

        await Assert.ThrowsAsync<InternalServerException>(() => client.Models.ListAsync());

        Assert.Equal([null, "1", "2"], handler.Requests.Select(r => r.Header("X-TypeSafe-Retry-Count")));
    }
}

public class RetryBehaviorTests
{
    private static readonly Regex s_delay = new(@"retrying in (\d+)ms", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private static readonly TimeSpan s_step = TimeSpan.FromMilliseconds(100);

    private static int[] Delays(RecordingLogger logger)
        => logger.Messages.Select(m => s_delay.Match(m)).Where(m => m.Success).Select(m => int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)).ToArray();

    private static (TypeSafeClient Client, RecordingLogger Logger, FakeTimeProvider Time) Create(
        StubHttpMessageHandler handler,
        Action<TypeSafeClientOptions>? configure = null,
        double random = 0)
    {
        var logger = new RecordingLogger();
        var time = new FakeTimeProvider();
        TypeSafeClient client = TestClient.Create(
            handler,
            o =>
            {
                o.Logger = logger;
                o.LogLevel = TypeSafeLogLevel.Info;
                configure?.Invoke(o);
            },
            time,
            random);
        return (client, logger, time);
    }

    [Fact]
    public async Task ServerError_IsRetriedAfterTheBackoffAndThenSucceeds()
    {
        var handler = new StubHttpMessageHandler(n => n == 1 ? Responses.Json("{}", 503) : Responses.Json(Responses.EmptyModels));
        var (client, logger, time) = Create(handler);
        using (client)
        {
            IReadOnlyList<ModelCard> models = await time.AdvanceUntilCompletedAsync(client.Models.ListAsync(), s_step);

            Assert.Empty(models);
        }

        Assert.Equal(2, handler.Count);
        Assert.Contains("#1 GET /v1/models retrying in 500ms (retry 1/2) after 503", logger.Messages);
    }

    [Fact]
    public async Task RetryAfterSeconds_IsHonoredExactly()
    {
        var time = new FakeTimeProvider();
        var logger = new RecordingLogger();
        var times = new List<DateTimeOffset>();
        var handler = new StubHttpMessageHandler(n =>
        {
            lock (times)
            {
                times.Add(time.GetUtcNow());
            }

            return n == 1 ? Responses.Json("{}", 429, ("Retry-After", "2")) : Responses.Json(Responses.EmptyModels);
        });
        using TypeSafeClient client = TestClient.Create(handler, o => { o.Logger = logger; o.LogLevel = TypeSafeLogLevel.Info; }, time);

        await time.AdvanceUntilCompletedAsync(client.Models.ListAsync(), s_step);

        Assert.Contains("#1 GET /v1/models retrying in 2000ms (retry 1/2) after 429", logger.Messages);
        Assert.True(times[1] - times[0] >= TimeSpan.FromSeconds(2), $"Retried after {times[1] - times[0]}");
    }

    [Fact]
    public async Task RetryAfterMilliseconds_TakesPrecedenceOverRetryAfter()
    {
        var handler = new StubHttpMessageHandler(n => n == 1
            ? Responses.Json("{}", 429, ("retry-after-ms", "250"), ("Retry-After", "30"))
            : Responses.Json(Responses.EmptyModels));
        var (client, logger, time) = Create(handler);
        using (client)
        {
            await time.AdvanceUntilCompletedAsync(client.Models.ListAsync(), s_step);
        }

        Assert.Equal([250], Delays(logger));
    }

    [Fact]
    public async Task RetryAfterHttpDate_IsConvertedRelativeToNow()
    {
        var time = new FakeTimeProvider();
        var logger = new RecordingLogger();
        string date = (time.GetUtcNow() + TimeSpan.FromSeconds(30)).ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        var handler = new StubHttpMessageHandler(n => n == 1 ? Responses.Json("{}", 429, ("Retry-After", date)) : Responses.Json(Responses.EmptyModels));
        using TypeSafeClient client = TestClient.Create(handler, o => { o.Logger = logger; o.LogLevel = TypeSafeLogLevel.Info; }, time);

        await time.AdvanceUntilCompletedAsync(client.Models.ListAsync(), TimeSpan.FromSeconds(1));

        Assert.Equal([30000], Delays(logger));
    }

    [Fact]
    public async Task Backoff_IsExponentialWithoutRetryAfter_AndTheLastErrorIsThrown()
    {
        var handler = new StubHttpMessageHandler(_ => Responses.Json("""{"error":"overloaded"}""", 503));
        var (client, logger, time) = Create(handler);
        using (client)
        {
            var exception = await Assert.ThrowsAsync<InternalServerException>(() => time.AdvanceUntilCompletedAsync(client.Models.ListAsync(), s_step));

            Assert.Equal("503 overloaded", exception.Message);
        }

        Assert.Equal(3, handler.Count);
        Assert.Equal([500, 1000], Delays(logger));
        Assert.Contains("#1 GET /v1/models retrying in 1000ms (retry 2/2) after 503", logger.Messages);
    }

    [Fact]
    public async Task Jitter_ShavesTimeOffTheBackoff()
    {
        var handler = new StubHttpMessageHandler(_ => Responses.Json("{}", 503));
        var (client, logger, time) = Create(handler, random: 0.5);
        using (client)
        {
            await Assert.ThrowsAsync<InternalServerException>(() => time.AdvanceUntilCompletedAsync(client.Models.ListAsync(), s_step));
        }

        Assert.Equal([438, 875], Delays(logger));
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(409)]
    [InlineData(422)]
    public async Task ClientErrors_AreNotRetried(int status)
    {
        var handler = new StubHttpMessageHandler(_ => Responses.Json("{}", status));
        using TypeSafeClient client = TestClient.Create(handler, o => o.RetryPolicy = RetryPolicy.Default with { InitialBackoff = TimeSpan.Zero });

        var exception = await Assert.ThrowsAnyAsync<ApiException>(() => client.Models.ListAsync());

        Assert.Equal(status, exception.StatusCode);
        Assert.Equal(1, handler.Count);
    }

    [Theory]
    [InlineData(408)]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    [InlineData(504)]
    [InlineData(529)]
    public async Task RetryableStatuses_AreRetried(int status)
    {
        var handler = new StubHttpMessageHandler(n => n == 1 ? Responses.Json("{}", status) : Responses.Json(Responses.EmptyModels));
        using TypeSafeClient client = TestClient.Create(handler, o => o.RetryPolicy = RetryPolicy.Default with { InitialBackoff = TimeSpan.Zero });

        await client.Models.ListAsync();

        Assert.Equal(2, handler.Count);
    }

    [Fact]
    public async Task ConnectionErrors_AreRetried()
    {
        var handler = new StubHttpMessageHandler(n => n == 1 ? throw new System.Net.Http.HttpRequestException("socket closed") : Responses.Json(Responses.EmptyModels));
        var (client, logger, time) = Create(handler);
        using (client)
        {
            await time.AdvanceUntilCompletedAsync(client.Models.ListAsync(), s_step);
        }

        Assert.Equal(2, handler.Count);
        Assert.Contains("#1 GET /v1/models retrying in 500ms (retry 1/2) after Connection error: socket closed", logger.Messages);
    }

    [Fact]
    public async Task ConnectionErrors_AreWrappedWithTheirCause_WhenRetriesAreExhausted()
    {
        var handler = new StubHttpMessageHandler(_ => throw new System.Net.Http.HttpRequestException("socket closed"));
        using TypeSafeClient client = TestClient.Create(handler, o => o.RetryPolicy = RetryPolicy.None);

        var exception = await Assert.ThrowsAsync<ApiConnectionException>(() => client.Models.ListAsync());

        Assert.Equal("Connection error: socket closed", exception.Message);
        Assert.IsType<System.Net.Http.HttpRequestException>(exception.InnerException);
    }

    [Fact]
    public async Task DisabledConnectionRetries_FailImmediately()
    {
        var handler = new StubHttpMessageHandler(_ => throw new System.Net.Http.HttpRequestException("socket closed"));
        using TypeSafeClient client = TestClient.Create(handler, o => o.RetryPolicy = RetryPolicy.Default with { RetryOnConnectionError = false });

        await Assert.ThrowsAsync<ApiConnectionException>(() => client.Models.ListAsync());

        Assert.Equal(1, handler.Count);
    }

    [Fact]
    public async Task PerCallPolicy_OverridesTheClientPolicyWithoutChangingIt()
    {
        var handler = new StubHttpMessageHandler(_ => Responses.Json("{}", 503));
        using TypeSafeClient client = TestClient.Create(handler, o => o.RetryPolicy = RetryPolicy.Default with { InitialBackoff = TimeSpan.Zero });

        await Assert.ThrowsAsync<InternalServerException>(() => client.Models.ListAsync(new RequestOptions { RetryPolicy = RetryPolicy.None }));
        Assert.Equal(1, handler.Count);

        await Assert.ThrowsAsync<InternalServerException>(() => client.Models.ListAsync());
        Assert.Equal(4, handler.Count);
        Assert.Equal(2, client.RetryPolicy.MaxRetries);
    }

    [Fact]
    public async Task PerCallPolicy_CanEnableRetriesOnAClientThatDisablesThem()
    {
        var handler = new StubHttpMessageHandler(n => n < 3 ? Responses.Json("{}", 503) : Responses.Json(Responses.EmptyModels));
        using TypeSafeClient client = TestClient.Create(handler, o => o.RetryPolicy = RetryPolicy.None);
        var options = new RequestOptions { RetryPolicy = RetryPolicy.Default with { MaxRetries = 5, InitialBackoff = TimeSpan.Zero } };

        await client.Models.ListAsync(options);

        Assert.Equal(3, handler.Count);
    }

    [Fact]
    public async Task CustomStatusSet_RetriesOnlyThoseStatuses()
    {
        var handler = new StubHttpMessageHandler(n => n == 1 ? Responses.Json("{}", 409) : Responses.Json("{}", 500));
        RetryPolicy policy = RetryPolicy.Default with
        {
            InitialBackoff = TimeSpan.Zero,
            RetryableStatusCodes = RetryPolicy.Default.RetryableStatusCodes.Remove(500).Add(409),
        };
        using TypeSafeClient client = TestClient.Create(handler, o => o.RetryPolicy = policy);

        await Assert.ThrowsAsync<InternalServerException>(() => client.Models.ListAsync());

        Assert.Equal(2, handler.Count);
    }

    [Fact]
    public async Task IgnoringRetryAfter_UsesBackoffInstead()
    {
        var handler = new StubHttpMessageHandler(n => n == 1 ? Responses.Json("{}", 429, ("Retry-After", "30")) : Responses.Json(Responses.EmptyModels));
        var (client, logger, time) = Create(handler, o => o.RetryPolicy = RetryPolicy.Default with { RespectRetryAfter = false });
        using (client)
        {
            await time.AdvanceUntilCompletedAsync(client.Models.ListAsync(), s_step);
        }

        Assert.Equal([500], Delays(logger));
    }

    [Fact]
    public async Task RetryAfterAboveTheCeiling_FallsBackToBackoff()
    {
        var handler = new StubHttpMessageHandler(n => n == 1 ? Responses.Json("{}", 429, ("Retry-After", "120")) : Responses.Json(Responses.EmptyModels));
        var (client, logger, time) = Create(handler);
        using (client)
        {
            await time.AdvanceUntilCompletedAsync(client.Models.ListAsync(), s_step);
        }

        Assert.Equal([500], Delays(logger));
    }

    [Fact]
    public async Task RateLimitException_ExposesRetryAfterWhenRetriesRunOut()
    {
        var handler = new StubHttpMessageHandler(_ => Responses.Json("{}", 429, ("retry-after-ms", "1500")));
        using TypeSafeClient client = TestClient.Create(handler, o => o.RetryPolicy = RetryPolicy.None);

        var exception = await Assert.ThrowsAsync<RateLimitException>(() => client.Models.ListAsync());

        Assert.Equal(TimeSpan.FromMilliseconds(1500), exception.RetryAfter);
    }
}
