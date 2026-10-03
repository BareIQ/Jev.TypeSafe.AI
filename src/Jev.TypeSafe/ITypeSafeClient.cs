using System.Threading;
using System.Threading.Tasks;

namespace TypeSafe.AI;

/// <summary>A client for the TypeSafe AI API. Implemented by <see cref="TypeSafeClient"/>; mock it in tests.</summary>
public interface ITypeSafeClient
{
    /// <summary>Gets access to the models available to the account.</summary>
    IModelsClient Models { get; }

    /// <summary>Answers named questions about text or structured state.</summary>
    /// <param name="request">The state, questions, and optional model.</param>
    /// <param name="options">Per-request settings, or <see langword="null"/> for the client's.</param>
    /// <param name="cancellationToken">Cancels the request, including pending retries.</param>
    /// <returns>The answers, keyed by question name, with model and token usage.</returns>
    /// <exception cref="System.ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="System.ArgumentException">An additional property uses a reserved name.</exception>
    /// <exception cref="TypeSafeException">
    /// There are no questions, a score question has fewer than two criteria, the options are invalid, or the
    /// response had an unexpected shape.
    /// </exception>
    /// <exception cref="ApiException">The server returned an unsuccessful status after any retries.</exception>
    /// <exception cref="ApiConnectionException">The request failed or timed out after any retries.</exception>
    /// <exception cref="ApiUserAbortException">The request was cancelled.</exception>
    Task<SystemOneResult> SystemOneAsync(SystemOneRequest request, RequestOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>Answers named questions about text or structured state, with the HTTP response.</summary>
    /// <param name="request">The state, questions, and optional model.</param>
    /// <param name="options">Per-request settings, or <see langword="null"/> for the client's.</param>
    /// <param name="cancellationToken">Cancels the request, including pending retries.</param>
    /// <returns>The answers and the response they came from.</returns>
    /// <exception cref="System.ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="System.ArgumentException">An additional property uses a reserved name.</exception>
    /// <exception cref="TypeSafeException">
    /// There are no questions, a score question has fewer than two criteria, the options are invalid, or the
    /// response had an unexpected shape.
    /// </exception>
    /// <exception cref="ApiException">The server returned an unsuccessful status after any retries.</exception>
    /// <exception cref="ApiConnectionException">The request failed or timed out after any retries.</exception>
    /// <exception cref="ApiUserAbortException">The request was cancelled.</exception>
    Task<ApiResponse<SystemOneResult>> SystemOneWithResponseAsync(
        SystemOneRequest request,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default);
}
