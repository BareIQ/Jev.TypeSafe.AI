using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TypeSafe.AI;

/// <summary>Access to the models available to the account.</summary>
public interface IModelsClient
{
    /// <summary>Lists the models available to the account.</summary>
    /// <param name="options">Per-request settings, or <see langword="null"/> for the client's.</param>
    /// <param name="cancellationToken">Cancels the request, including pending retries.</param>
    /// <returns>The available models.</returns>
    /// <exception cref="ApiException">The server returned an unsuccessful status after any retries.</exception>
    /// <exception cref="ApiConnectionException">The request failed or timed out after any retries.</exception>
    /// <exception cref="ApiUserAbortException">The request was cancelled.</exception>
    /// <exception cref="TypeSafeException">The response had an unexpected shape, or the options are invalid.</exception>
    Task<IReadOnlyList<ModelCard>> ListAsync(RequestOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>Lists the models available to the account, with the HTTP response.</summary>
    /// <param name="options">Per-request settings, or <see langword="null"/> for the client's.</param>
    /// <param name="cancellationToken">Cancels the request, including pending retries.</param>
    /// <returns>The available models and the response they came from.</returns>
    /// <exception cref="ApiException">The server returned an unsuccessful status after any retries.</exception>
    /// <exception cref="ApiConnectionException">The request failed or timed out after any retries.</exception>
    /// <exception cref="ApiUserAbortException">The request was cancelled.</exception>
    /// <exception cref="TypeSafeException">The response had an unexpected shape, or the options are invalid.</exception>
    Task<ApiResponse<IReadOnlyList<ModelCard>>> ListWithResponseAsync(RequestOptions? options = null, CancellationToken cancellationToken = default);
}
