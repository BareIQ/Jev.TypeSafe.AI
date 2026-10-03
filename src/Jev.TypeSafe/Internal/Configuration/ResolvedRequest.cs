using System;
using System.Collections.Generic;
using TypeSafe.AI.Internal.Http;

namespace TypeSafe.AI.Internal.Configuration;

/// <summary>Per-request settings after merging client configuration with <see cref="RequestOptions"/>.</summary>
internal sealed class ResolvedRequest
{
    public ResolvedRequest(IReadOnlyList<KeyValuePair<string, string>> headers, TimeSpan timeout, RetryPolicy retryPolicy)
    {
        Headers = headers;
        Timeout = timeout;
        RetryPolicy = retryPolicy;
    }

    /// <summary>Gets caller headers (defaults merged with per-request), before SDK headers are applied.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Headers { get; }

    public TimeSpan Timeout { get; }

    public RetryPolicy RetryPolicy { get; }

    /// <summary>Merges per-request options over the client configuration.</summary>
    /// <exception cref="TypeSafeException">The per-request timeout is invalid.</exception>
    public static ResolvedRequest Resolve(ClientConfiguration configuration, RequestOptions? options)
    {
        if (options is null)
        {
            return new ResolvedRequest(configuration.DefaultHeaders, configuration.Timeout, configuration.RetryPolicy);
        }

        return new ResolvedRequest(
            new HeaderMerger().SetAll(configuration.DefaultHeaders).SetAll(options.Headers).Build(),
            options.Timeout is { } timeout ? OptionsValidator.ValidateTimeout(timeout) : configuration.Timeout,
            options.RetryPolicy ?? configuration.RetryPolicy);
    }
}
