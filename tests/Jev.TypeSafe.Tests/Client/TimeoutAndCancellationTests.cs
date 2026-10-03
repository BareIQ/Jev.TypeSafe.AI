using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using TypeSafe.AI.Tests.Support;
using Xunit;

namespace TypeSafe.AI.Tests.Client;

public class TimeoutBehaviorTests
{
    private static readonly TimeSpan s_timeout = TimeSpan.FromMilliseconds(100);

    private static async Task NotCompletedAfterAShortWaitAsync(Task task)
    {
        await Task.Delay(30);
        Assert.False(task.IsCompleted, "The task completed earlier than expected.");
    }

    [Fact]
    public async Task HungRequest_TimesOutExactlyAtTheConfiguredTimeout()
    {
        var time = new FakeTimeProvider();
        var handler = StubHttpMessageHandler.Hanging();
        using TypeSafeClient client = TestClient.Create(handler, o => { o.Timeout = s_timeout; o.RetryPolicy = RetryPolicy.None; }, time);

        Task call = client.Models.ListAsync();
        await Async.UntilAsync(() => handler.Count == 1, "the request");
        time.Advance(TimeSpan.FromMilliseconds(99));
        await NotCompletedAfterAShortWaitAsync(call);
        time.Advance(TimeSpan.FromMilliseconds(1));

        var exception = await Assert.ThrowsAsync<ApiTimeoutException>(() => call.WithDeadlineAsync());
        Assert.Equal("Request timed out after 100ms.", exception.Message);
        Assert.Equal(s_timeout, exception.Timeout);
        Assert.IsAssignableFrom<ApiConnectionException>(exception);
        Assert.IsAssignableFrom<OperationCanceledException>(exception.InnerException);
    }

    [Fact]
    public async Task Timeout_IsRetried_AndEachAttemptGetsItsOwnTimeout()
    {
        var time = new FakeTimeProvider();
        var handler = StubHttpMessageHandler.Hanging();
        using TypeSafeClient client = TestClient.Create(
            handler,
            o => { o.Timeout = s_timeout; o.RetryPolicy = RetryPolicy.Default with { MaxRetries = 1, InitialBackoff = TimeSpan.Zero }; },
            time);

        Task call = client.Models.ListAsync();
        await Async.UntilAsync(() => handler.Count == 1, "the first attempt");
        time.Advance(s_timeout);
        await Async.UntilAsync(() => handler.Count == 2, "the retry");
        time.Advance(TimeSpan.FromMilliseconds(99));
        await NotCompletedAfterAShortWaitAsync(call);
        time.Advance(TimeSpan.FromMilliseconds(1));

        await Assert.ThrowsAsync<ApiTimeoutException>(() => call.WithDeadlineAsync());
        Assert.Equal(2, handler.Count);
    }

    [Fact]
    public async Task PerCallTimeout_OverridesTheClientTimeout()
    {
        var time = new FakeTimeProvider();
        var handler = StubHttpMessageHandler.Hanging();
        using TypeSafeClient client = TestClient.Create(handler, o => o.RetryPolicy = RetryPolicy.None, time);

        Task call = client.Models.ListAsync(new RequestOptions { Timeout = s_timeout });
        await Async.UntilAsync(() => handler.Count == 1, "the request");
        time.Advance(s_timeout);

        await Assert.ThrowsAsync<ApiTimeoutException>(() => call.WithDeadlineAsync());
    }

    [Fact]
    public async Task DisabledTimeoutRetries_FailAfterTheFirstTimeout()
    {
        var time = new FakeTimeProvider();
        var handler = StubHttpMessageHandler.Hanging();
        using TypeSafeClient client = TestClient.Create(
            handler,
            o => { o.Timeout = s_timeout; o.RetryPolicy = RetryPolicy.Default with { RetryOnTimeout = false }; },
            time);

        Task call = client.Models.ListAsync();
        await Async.UntilAsync(() => handler.Count == 1, "the request");
        time.Advance(s_timeout);

        await Assert.ThrowsAsync<ApiTimeoutException>(() => call.WithDeadlineAsync());
        Assert.Equal(1, handler.Count);
    }

    [Fact]
    public async Task DisabledConnectionRetries_StillRetryTimeouts()
    {
        var time = new FakeTimeProvider();
        var handler = StubHttpMessageHandler.Hanging();
        using TypeSafeClient client = TestClient.Create(
            handler,
            o => { o.Timeout = s_timeout; o.RetryPolicy = RetryPolicy.Default with { RetryOnConnectionError = false, MaxRetries = 1, InitialBackoff = TimeSpan.Zero }; },
            time);

        Task call = client.Models.ListAsync();
        await Async.UntilAsync(() => handler.Count == 1, "the first attempt");
        time.Advance(s_timeout);
        await Async.UntilAsync(() => handler.Count == 2, "the retry");
        time.Advance(s_timeout);

        await Assert.ThrowsAsync<ApiTimeoutException>(() => call.WithDeadlineAsync());
        Assert.Equal(2, handler.Count);
    }

    [Theory]
    [InlineData(200)]
    [InlineData(503)]
    public async Task StalledBody_IsAbortedByTheTimeoutEvenWhenTheStreamIgnoresTokens(int status)
    {
        var time = new FakeTimeProvider();
        var streams = new System.Collections.Concurrent.ConcurrentBag<StallingStream>();
        var handler = new StubHttpMessageHandler(_ =>
        {
            var stream = new StallingStream();
            streams.Add(stream);
            return new HttpResponseMessage((System.Net.HttpStatusCode)status) { Content = new StreamContent(stream) };
        });
        using TypeSafeClient client = TestClient.Create(
            handler,
            o => { o.Timeout = s_timeout; o.RetryPolicy = RetryPolicy.Default with { MaxRetries = 1, InitialBackoff = TimeSpan.Zero }; },
            time);

        Task call = client.Models.ListAsync();
        await Async.UntilAsync(() => handler.Count == 1, "the first attempt");
        time.Advance(s_timeout);
        await Async.UntilAsync(() => handler.Count == 2, "the retry");
        time.Advance(s_timeout);

        await Assert.ThrowsAsync<ApiTimeoutException>(() => call.WithDeadlineAsync());
        Assert.Equal(2, handler.Count);
        Assert.Equal(2, streams.Count);
        Assert.All(streams, stream => Assert.True(stream.IsDisposed));
    }

    [Fact]
    public async Task FastResponse_IsNotAffectedByALaterTimeout()
    {
        var time = new FakeTimeProvider();
        var handler = new StubHttpMessageHandler(_ => Responses.Json(Responses.EmptyModels));
        using TypeSafeClient client = TestClient.Create(handler, o => o.Timeout = s_timeout, time);

        await client.Models.ListAsync();
        time.Advance(TimeSpan.FromMinutes(5));

        Assert.Equal(1, handler.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void InvalidPerCallTimeout_ThrowsSynchronously(int milliseconds)
    {
        using TypeSafeClient client = TestClient.Create(new StubHttpMessageHandler(_ => Responses.Json(Responses.EmptyModels)));

        var exception = Sync.Throws<TypeSafeException>(() => client.Models.ListAsync(new RequestOptions { Timeout = TimeSpan.FromMilliseconds(milliseconds) }));

        Assert.Equal($"`timeout` must be a positive number of milliseconds, got {milliseconds}.", exception.Message);
    }
}

public class CancellationBehaviorTests
{
    [Fact]
    public async Task CancellingAHungRequest_IsAnAbortNotATimeout_AndIsNeverRetried()
    {
        var handler = StubHttpMessageHandler.Hanging();
        using TypeSafeClient client = TestClient.Create(handler, o => o.RetryPolicy = RetryPolicy.Default with { InitialBackoff = TimeSpan.Zero });
        using var cts = new CancellationTokenSource();

        Task call = client.Models.ListAsync(cancellationToken: cts.Token);
        await Async.UntilAsync(() => handler.Count == 1, "the request");
        cts.Cancel();

        var exception = await Assert.ThrowsAsync<ApiUserAbortException>(() => call.WithDeadlineAsync());
        Assert.Equal("Request was aborted.", exception.Message);
        Assert.Equal(cts.Token, exception.CancellationToken);
        Assert.IsAssignableFrom<OperationCanceledException>(exception.InnerException);
        Assert.Equal(1, handler.Count);
    }

    [Fact]
    public async Task AlreadyCancelledToken_AbortsWithoutSending()
    {
        var handler = new StubHttpMessageHandler(_ => Responses.Json(Responses.EmptyModels));
        using TypeSafeClient client = TestClient.Create(handler);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<ApiUserAbortException>(() => client.Models.ListAsync(cancellationToken: cts.Token));

        Assert.Equal(0, handler.Count);
    }

    [Fact]
    public async Task CancellingDuringBackoff_IsAnAbort()
    {
        var time = new FakeTimeProvider();
        var logger = new RecordingLogger();
        var handler = new StubHttpMessageHandler(_ => Responses.Json("{}", 503));
        using TypeSafeClient client = TestClient.Create(handler, o => { o.Logger = logger; o.LogLevel = TypeSafeLogLevel.Info; }, time);
        using var cts = new CancellationTokenSource();

        Task call = client.Models.ListAsync(cancellationToken: cts.Token);
        await Async.UntilAsync(() => logger.Messages.Any(m => m.Contains("retrying in", StringComparison.Ordinal)), "the backoff");
        cts.Cancel();

        await Assert.ThrowsAsync<ApiUserAbortException>(() => call.WithDeadlineAsync());
        Assert.Equal(1, handler.Count);
        Assert.Contains("#1 GET /v1/models aborted by caller while waiting to retry", logger.Messages);
    }

    [Fact]
    public async Task CancellingWhileTheBodyIsBeingRead_AbortsAndDisposesTheStream()
    {
        var stream = new StallingStream();
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage { Content = new StreamContent(stream) });
        using TypeSafeClient client = TestClient.Create(handler);
        using var cts = new CancellationTokenSource();

        Task call = client.Models.ListAsync(cancellationToken: cts.Token);
        await Async.UntilAsync(() => handler.Count == 1, "the request");
        await Task.Delay(30);
        cts.Cancel();

        await Assert.ThrowsAsync<ApiUserAbortException>(() => call.WithDeadlineAsync());
        Assert.True(stream.IsDisposed);
    }

    [Fact]
    public async Task CancellingJustBeforeHeadersReturn_StillAbortsAndReleasesTheBody()
    {
        using var cts = new CancellationTokenSource();
        var stream = new StallingStream();
        var handler = new StubHttpMessageHandler(_ =>
        {
            cts.Cancel();
            return new HttpResponseMessage { Content = new StreamContent(stream) };
        });
        using TypeSafeClient client = TestClient.Create(handler);

        await Assert.ThrowsAsync<ApiUserAbortException>(() => client.Models.ListAsync(cancellationToken: cts.Token).WithDeadlineAsync());

        Assert.True(stream.IsDisposed);
    }

    [Fact]
    public async Task CancelledSystemOneCall_AbortsToo()
    {
        var handler = StubHttpMessageHandler.Hanging();
        using TypeSafeClient client = TestClient.Create(handler);
        using var cts = new CancellationTokenSource();
        var questions = new QuestionSet();
        questions.Add("q", Question.Noul("?"));

        Task call = client.SystemOneAsync(new SystemOneRequest("s", questions), cancellationToken: cts.Token);
        await Async.UntilAsync(() => handler.Count == 1, "the request");
        cts.Cancel();

        await Assert.ThrowsAsync<ApiUserAbortException>(() => call.WithDeadlineAsync());
    }
}

public class DisposalBehaviorTests
{
    private static SystemOneRequest Request()
    {
        var questions = new QuestionSet();
        questions.Add("q", Question.Noul("?"));
        return new SystemOneRequest("s", questions);
    }

    [Fact]
    public void Dispose_IsIdempotent_AndRejectsNewCalls()
    {
        using TypeSafeClient client = TestClient.Create(new StubHttpMessageHandler(_ => Responses.Json(Responses.EmptyModels)));

        client.Dispose();
        client.Dispose();

        Sync.Throws<ObjectDisposedException>(() => client.Models.ListAsync());
        Sync.Throws<ObjectDisposedException>(() => client.Models.ListWithResponseAsync());
        Sync.Throws<ObjectDisposedException>(() => client.SystemOneAsync(Request()));
        Sync.Throws<ObjectDisposedException>(() => client.SystemOneWithResponseAsync(Request()));
    }

    [Fact]
    public async Task Dispose_DoesNotDisposeACallerSuppliedHttpClient()
    {
        var handler = new StubHttpMessageHandler(_ => Responses.Json(Responses.EmptyModels));
        using var httpClient = new HttpClient(handler);
        TypeSafeClient client = TestClient.Create(httpClient);

        client.Dispose();

        using HttpResponseMessage response = await httpClient.GetAsync("https://other.test/ping");
        Assert.Equal(1, handler.Count);
    }

    [Fact]
    public async Task DisposingWhileARequestIsInFlight_ReportsDisposalAndDoesNotRetry()
    {
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new StubHttpMessageHandler(async (_, _) =>
        {
            await release.Task;
            throw new HttpRequestException("transport torn down");
        });
        TypeSafeClient client = TestClient.Create(handler, o => o.RetryPolicy = RetryPolicy.Default with { InitialBackoff = TimeSpan.Zero });

        Task call = client.Models.ListAsync();
        await Async.UntilAsync(() => handler.Count == 1, "the request");
        client.Dispose();
        release.SetResult(true);

        await Assert.ThrowsAsync<ObjectDisposedException>(() => call.WithDeadlineAsync());
        Assert.Equal(1, handler.Count);
    }
}
