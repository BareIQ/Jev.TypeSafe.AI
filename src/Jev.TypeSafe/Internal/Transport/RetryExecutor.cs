using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TypeSafe.AI.Internal.Errors;
using TypeSafe.AI.Internal.Logging;
using TypeSafe.AI.Internal.Retry;

namespace TypeSafe.AI.Internal.Transport;

/// <summary>Runs attempts until one succeeds, the policy declines to retry, or retries run out.</summary>
internal sealed class RetryExecutor
{
    private readonly RequestFactory _requestFactory;
    private readonly AttemptExecutor _attemptExecutor;
    private readonly TimeProvider _timeProvider;
    private readonly IRandomSource _random;
    private readonly ILogger _logger;
    private readonly ClientLifetime _lifetime;

    public RetryExecutor(
        RequestFactory requestFactory,
        AttemptExecutor attemptExecutor,
        TimeProvider timeProvider,
        IRandomSource random,
        ILogger logger,
        ClientLifetime lifetime)
    {
        _requestFactory = requestFactory;
        _attemptExecutor = attemptExecutor;
        _timeProvider = timeProvider;
        _random = random;
        _logger = logger;
        _lifetime = lifetime;
    }

    /// <exception cref="ApiException">The final response was unsuccessful.</exception>
    /// <exception cref="ApiConnectionException">The final attempt failed to connect or timed out.</exception>
    /// <exception cref="ApiUserAbortException">The caller cancelled the request.</exception>
    /// <exception cref="ObjectDisposedException">The client was disposed while the request was in flight.</exception>
    public async Task<TransportResponse> ExecuteAsync(ApiRequest request, string tag, CancellationToken cancellationToken)
    {
        RetryPolicy policy = request.Options.RetryPolicy;
        for (int attempt = 0; ; attempt++)
        {
            int retriesLeft = policy.MaxRetries - attempt;
            AttemptOutcome outcome = await AttemptAsync(request, tag, attempt, cancellationToken).ConfigureAwait(false);
            if (outcome.Failure is { } failure)
            {
                // A failure caused by disposing the client mid-request is reported as disposal, never retried.
                _lifetime.ThrowIfDisposed();
                if (retriesLeft <= 0 || !policy.ShouldRetry(failure))
                {
                    throw failure;
                }

                await BackOffAsync(tag, attempt, retriesLeft, failure.Message, retryAfter: null, policy, cancellationToken).ConfigureAwait(false);
                continue;
            }

            RawResponse response = outcome.Response ?? throw new InvalidOperationException("An attempt produced neither a response nor a failure.");
            ParsedBody body = ResponseBodyParser.Parse(response);
            if (IsSuccessStatusCode(response.StatusCode))
            {
                return new TransportResponse(response, body);
            }

            Log.ErrorBody(_logger, tag, body);
            DateTimeOffset now = _timeProvider.GetUtcNow();
            ApiException error = ApiExceptionFactory.Create(response, body, now);
            if (retriesLeft <= 0 || !policy.ShouldRetry(response.StatusCode))
            {
                throw error;
            }

            TimeSpan? retryAfter = RetryAfterParser.Parse(response, now);
            string reason = response.StatusCode.ToString(CultureInfo.InvariantCulture);
            await BackOffAsync(tag, attempt, retriesLeft, reason, retryAfter, policy, cancellationToken).ConfigureAwait(false);
        }
    }

    private static bool IsSuccessStatusCode(int statusCode) => statusCode is >= 200 and <= 299;

    private async Task<AttemptOutcome> AttemptAsync(ApiRequest request, string tag, int attempt, CancellationToken cancellationToken)
    {
        _lifetime.ThrowIfDisposed();
        Uri uri = _requestFactory.BuildUri(request);
        IReadOnlyList<KeyValuePair<string, string>> headers = _requestFactory.BuildHeaders(request, attempt);
        Log.RequestSent(_logger, tag, uri, headers, request.Body);
        using HttpRequestMessage message = RequestFactory.Create(request, uri, headers);
        return await _attemptExecutor.ExecuteAsync(message, request.Options.Timeout, tag, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Waits before the next attempt. Cancellation during the wait is reported as a caller abort.</summary>
    private async Task BackOffAsync(
        string tag,
        int attempt,
        int retriesLeft,
        string reason,
        TimeSpan? retryAfter,
        RetryPolicy policy,
        CancellationToken cancellationToken)
    {
        TimeSpan delay = RetryMath.ComputeDelay(attempt, retryAfter, policy, _random.NextDouble());
        Log.Retrying(_logger, tag, delay, attempt + 1, attempt + retriesLeft, reason);
        try
        {
            await _timeProvider.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        {
            Log.AbortedDuringBackoff(_logger, tag);
            throw new ApiUserAbortException(exception, cancellationToken);
        }
    }
}
