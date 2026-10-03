using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TypeSafe.AI.Internal;
using TypeSafe.AI.Internal.Configuration;
using TypeSafe.AI.Internal.Http;
using TypeSafe.AI.Internal.Serialization;
using TypeSafe.AI.Internal.Transport;

namespace TypeSafe.AI;

/// <summary>The <see cref="IModelsClient"/> implementation used by <see cref="TypeSafeClient"/>.</summary>
internal sealed class ModelsClient : IModelsClient
{
    private readonly IApiTransport _transport;
    private readonly ClientConfiguration _configuration;
    private readonly ClientLifetime _lifetime;

    public ModelsClient(IApiTransport transport, ClientConfiguration configuration, ClientLifetime lifetime)
    {
        _transport = transport;
        _configuration = configuration;
        _lifetime = lifetime;
    }

    public Task<IReadOnlyList<ModelCard>> ListAsync(RequestOptions? options = null, CancellationToken cancellationToken = default)
        => ListValueAsync(CreateListRequest(options), cancellationToken);

    public Task<ApiResponse<IReadOnlyList<ModelCard>>> ListWithResponseAsync(RequestOptions? options = null, CancellationToken cancellationToken = default)
        => SendListAsync(CreateListRequest(options), cancellationToken);

    /// <summary>Validates synchronously, so invalid calls throw before a task is returned.</summary>
    private ApiRequest CreateListRequest(RequestOptions? options)
    {
        _lifetime.ThrowIfDisposed();
        return new ApiRequest(HttpMethod.Get, ApiPaths.Models, body: null, ResolvedRequest.Resolve(_configuration, options));
    }

    private async Task<IReadOnlyList<ModelCard>> ListValueAsync(ApiRequest request, CancellationToken cancellationToken)
        => (await SendListAsync(request, cancellationToken).ConfigureAwait(false)).Value;

    private async Task<ApiResponse<IReadOnlyList<ModelCard>>> SendListAsync(ApiRequest request, CancellationToken cancellationToken)
    {
        TransportResponse response = await _transport.SendAsync(request, cancellationToken).ConfigureAwait(false);
        return new ApiResponse<IReadOnlyList<ModelCard>>(ModelsReader.Read(response.Body.AsJson()), response.Response);
    }
}
