using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TypeSafe.AI.Internal.Logging;

namespace TypeSafe.AI.Internal.Transport;

/// <summary>
/// Performs one HTTP round trip, including delivery of the full body, within a per-attempt timeout.
/// The caller's token and the timeout cancel the same linked token; which one fired decides the error.
/// </summary>
internal sealed class AttemptExecutor
{
    private const int CopyBufferSize = 81920;
    private readonly HttpClient _httpClient;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;

    public AttemptExecutor(HttpClient httpClient, TimeProvider timeProvider, ILogger logger)
    {
        _httpClient = httpClient;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>Sends a message and buffers its response.</summary>
    /// <returns>The buffered response, or a connection or timeout failure for the retry loop to judge.</returns>
    /// <exception cref="ApiUserAbortException">The caller cancelled the request.</exception>
    public async Task<AttemptOutcome> ExecuteAsync(HttpRequestMessage message, TimeSpan timeout, string tag, CancellationToken cancellationToken)
    {
        // A custom handler may ignore tokens, so never hand it a request the caller has already cancelled.
        if (cancellationToken.IsCancellationRequested)
        {
            Log.Aborted(_logger, tag, TimeSpan.Zero);
            throw new ApiUserAbortException(new OperationCanceledException(cancellationToken), cancellationToken);
        }

        using CancellationTokenSource timeoutSource = _timeProvider.CreateCancellationTokenSource(timeout);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);
        long started = _timeProvider.GetTimestamp();
        try
        {
            RawResponse response = await SendAndBufferAsync(message, linkedSource.Token).ConfigureAwait(false);
            Log.ResponseReceived(_logger, tag, response.StatusCode, _timeProvider.GetElapsedTime(started), response.RequestId);
            return AttemptOutcome.Succeeded(response);
        }
        catch (Exception exception) when (IsTransportFailure(exception))
        {
            TimeSpan elapsed = _timeProvider.GetElapsedTime(started);
            if (cancellationToken.IsCancellationRequested)
            {
                Log.Aborted(_logger, tag, elapsed);
                throw new ApiUserAbortException(exception, cancellationToken);
            }

            if (timeoutSource.IsCancellationRequested)
            {
                Log.TimedOut(_logger, tag, elapsed);
                return AttemptOutcome.Failed(new ApiTimeoutException(timeout, exception));
            }

            Log.ConnectionError(_logger, tag, elapsed, exception);
            return AttemptOutcome.Failed(new ApiConnectionException($"Connection error: {exception.Message}", exception));
        }
    }

    /// <summary>Everything except fatal runtime failures is a transport failure, matching how fetch errors are wrapped upstream.</summary>
    private static bool IsTransportFailure(Exception exception)
        => exception is not (OutOfMemoryException or StackOverflowException or AccessViolationException);

    private async Task<RawResponse> SendAndBufferAsync(HttpRequestMessage message, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _httpClient
            .SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        byte[] content = await ReadContentAsync(response, cancellationToken).ConfigureAwait(false);
        return RawResponseFactory.Create(response, content);
    }

    /// <summary>
    /// Reads the whole body under the attempt's token. Cancellation also disposes the response, so a stream
    /// that ignores the token is still aborted.
    /// </summary>
    private static async Task<byte[]> ReadContentAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.Content is null)
        {
            return [];
        }

        using CancellationTokenRegistration registration = cancellationToken.Register(static state => (state as IDisposable)?.Dispose(), response);
        using Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, CopyBufferSize, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return buffer.ToArray();
    }
}
