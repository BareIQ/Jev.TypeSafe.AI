using System.Threading;
using System.Threading.Tasks;

namespace TypeSafe.AI.Internal.Transport;

/// <summary>Sends API requests with retries and returns successful, parsed responses.</summary>
internal interface IApiTransport
{
    /// <summary>Sends a request.</summary>
    /// <exception cref="ApiException">The server returned an unsuccessful status after any retries.</exception>
    /// <exception cref="ApiConnectionException">The request failed or timed out after any retries.</exception>
    /// <exception cref="ApiUserAbortException">The caller cancelled the request.</exception>
    Task<TransportResponse> SendAsync(ApiRequest request, CancellationToken cancellationToken);
}
