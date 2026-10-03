using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TypeSafe.AI.Internal.Configuration;
using TypeSafe.AI.Internal.Logging;
using TypeSafe.AI.Internal.Retry;

namespace TypeSafe.AI.Internal.Transport;

/// <summary>The production <see cref="IApiTransport"/>: numbers each call for log correlation and runs it through the retry loop.</summary>
internal sealed class HttpApiTransport : IApiTransport
{
    private readonly RetryExecutor _retryExecutor;
    private readonly ILogger _logger;
    private int _requestCount;

    public HttpApiTransport(ClientConfiguration configuration, HttpClient httpClient, IRandomSource random, ClientLifetime lifetime)
    {
        _logger = configuration.Logger;
        var attemptExecutor = new AttemptExecutor(httpClient, configuration.TimeProvider, _logger);
        _retryExecutor = new RetryExecutor(new RequestFactory(configuration), attemptExecutor, configuration.TimeProvider, random, _logger, lifetime);
    }

    public async Task<TransportResponse> SendAsync(ApiRequest request, CancellationToken cancellationToken)
    {
        // Numbered so concurrent calls, and the attempts within one, can be told apart in the logs.
        int number = Interlocked.Increment(ref _requestCount);
        string tag = FormattableString.Invariant($"#{number} {request.Method.Method} {request.Path}");
        TransportResponse response = await _retryExecutor.ExecuteAsync(request, tag, cancellationToken).ConfigureAwait(false);
        Log.ResponseBody(_logger, tag, response.Body);
        return response;
    }
}
