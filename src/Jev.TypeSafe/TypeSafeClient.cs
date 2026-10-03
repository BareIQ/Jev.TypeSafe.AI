using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TypeSafe.AI.Internal;
using TypeSafe.AI.Internal.Collections;
using TypeSafe.AI.Internal.Configuration;
using TypeSafe.AI.Internal.Http;
using TypeSafe.AI.Internal.Serialization;
using TypeSafe.AI.Internal.Transport;
using TypeSafe.AI.Internal.Validation;

namespace TypeSafe.AI;

/// <summary>
/// The client for the TypeSafe AI API. Thread-safe; create one per API key and reuse it.
/// </summary>
/// <example>
/// <code>
/// using var client = new TypeSafeClient(); // reads TYPESAFE_API_KEY
/// var questions = new QuestionSet();
/// var billing = questions.Add("billing", Question.Noul("Is this about billing?"));
/// var result = await client.SystemOneAsync(new SystemOneRequest("I was charged twice.", questions));
/// Console.WriteLine(result.Answers.Get(billing).Noul);
/// </code>
/// </example>
[DebuggerDisplay("{ToString(),nq}")]
public sealed class TypeSafeClient : ITypeSafeClient, IDisposable
{
    private readonly ClientConfiguration _configuration;
    private readonly IApiTransport _transport;
    private readonly HttpClient? _ownedHttpClient;
    private readonly ClientLifetime _lifetime = new();

    /// <summary>Initializes a new instance of the <see cref="TypeSafeClient"/> class from environment variables and defaults.</summary>
    /// <exception cref="TypeSafeException">No API key is available, configuration is invalid, or the runtime is a browser.</exception>
    public TypeSafeClient()
        : this(new TypeSafeClientOptions())
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TypeSafeClient"/> class.</summary>
    /// <param name="options">The options; unset values fall back to environment variables, then defaults.</param>
    /// <exception cref="TypeSafeException">No API key is available, configuration is invalid, or the runtime is a browser.</exception>
    public TypeSafeClient(TypeSafeClientOptions options)
        : this(Guard.NotNull(options), httpClient: null, ClientDependencies.Default)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TypeSafeClient"/> class that sends requests through <paramref name="httpClient"/>.</summary>
    /// <param name="options">The options; unset values fall back to environment variables, then defaults.</param>
    /// <param name="httpClient">
    /// The HTTP client to use. The caller owns it; it is not disposed with this client. Its own
    /// <see cref="HttpClient.Timeout"/> should be at least the per-attempt timeout.
    /// </param>
    /// <exception cref="TypeSafeException">No API key is available, configuration is invalid, or the runtime is a browser.</exception>
    public TypeSafeClient(TypeSafeClientOptions options, HttpClient httpClient)
        : this(Guard.NotNull(options), Guard.NotNull(httpClient), ClientDependencies.Default)
    {
    }

    internal TypeSafeClient(TypeSafeClientOptions options, HttpClient? httpClient, ClientDependencies dependencies)
    {
        _configuration = ClientConfigurationResolver.Resolve(options, dependencies);
        HttpClient client = httpClient ?? (_ownedHttpClient = CreateHttpClient());
        _transport = new HttpApiTransport(_configuration, client, dependencies.Random, _lifetime);
        BaseUri = new Uri(_configuration.BaseUrl, UriKind.Absolute);
        DefaultHeaders = new OrderedReadOnlyDictionary<string, string>(_configuration.DefaultHeaders, StringComparer.OrdinalIgnoreCase);
        Models = new ModelsClient(_transport, _configuration, _lifetime);
    }

    /// <summary>Initializes a client over a given transport, so the application layer can be tested without HTTP.</summary>
    internal TypeSafeClient(ClientConfiguration configuration, IApiTransport transport)
    {
        _configuration = configuration;
        _transport = transport;
        BaseUri = new Uri(_configuration.BaseUrl, UriKind.Absolute);
        DefaultHeaders = new OrderedReadOnlyDictionary<string, string>(_configuration.DefaultHeaders, StringComparer.OrdinalIgnoreCase);
        Models = new ModelsClient(_transport, _configuration, _lifetime);
    }

    /// <summary>Gets the API root.</summary>
    public Uri BaseUri { get; }

    /// <summary>Gets the model used when a request does not set one.</summary>
    public string DefaultModel => _configuration.DefaultModel;

    /// <summary>Gets the log level.</summary>
    public TypeSafeLogLevel LogLevel => _configuration.LogLevel;

    /// <summary>Gets the retry policy used when a request does not set one.</summary>
    public RetryPolicy RetryPolicy => _configuration.RetryPolicy;

    /// <summary>Gets the timeout for each attempt.</summary>
    public TimeSpan Timeout => _configuration.Timeout;

    /// <summary>Gets the headers sent with every request.</summary>
    public IReadOnlyDictionary<string, string> DefaultHeaders { get; }

    /// <inheritdoc/>
    public IModelsClient Models { get; }

    /// <inheritdoc/>
    public Task<SystemOneResult> SystemOneAsync(SystemOneRequest request, RequestOptions? options = null, CancellationToken cancellationToken = default)
        => SystemOneValueAsync(CreateSystemOneRequest(request, options), cancellationToken);

    /// <inheritdoc/>
    public Task<ApiResponse<SystemOneResult>> SystemOneWithResponseAsync(
        SystemOneRequest request,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
        => SendSystemOneAsync(CreateSystemOneRequest(request, options), cancellationToken);

    /// <summary>Disposes the HTTP client this instance created; a caller-supplied client is left open.</summary>
    public void Dispose()
    {
        if (_lifetime.TryMarkDisposed())
        {
            _ownedHttpClient?.Dispose();
        }
    }

    /// <inheritdoc/>
    public override string ToString() => $"TypeSafeClient {{ BaseUri = {_configuration.BaseUrl}, DefaultModel = {DefaultModel} }}";

    private static HttpClient CreateHttpClient()
        => new(new HttpClientHandler(), disposeHandler: true) { Timeout = System.Threading.Timeout.InfiniteTimeSpan };

    /// <summary>Validates and serializes synchronously, so invalid calls throw before a task is returned.</summary>
    private ApiRequest CreateSystemOneRequest(SystemOneRequest request, RequestOptions? options)
    {
        _lifetime.ThrowIfDisposed();
        Guard.NotNull(request);
        QuestionValidator.Validate(request);
        var resolved = ResolvedRequest.Resolve(_configuration, options);
        byte[] body = SystemOneRequestWriter.Write(request, request.Model ?? _configuration.DefaultModel);
        return new ApiRequest(HttpMethod.Post, ApiPaths.SystemOne, body, resolved);
    }

    private async Task<SystemOneResult> SystemOneValueAsync(ApiRequest request, CancellationToken cancellationToken)
        => (await SendSystemOneAsync(request, cancellationToken).ConfigureAwait(false)).Value;

    private async Task<ApiResponse<SystemOneResult>> SendSystemOneAsync(ApiRequest request, CancellationToken cancellationToken)
    {
        TransportResponse response = await _transport.SendAsync(request, cancellationToken).ConfigureAwait(false);
        return new ApiResponse<SystemOneResult>(SystemOneResultReader.Read(response.Body.AsJson()), response.Response);
    }
}
