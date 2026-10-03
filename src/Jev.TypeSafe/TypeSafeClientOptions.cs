using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace TypeSafe.AI;

/// <summary>
/// Options for <see cref="TypeSafeClient"/>. Values set here take precedence over environment variables
/// (<see cref="TypeSafeEnvironmentVariables"/>), which take precedence over SDK defaults.
/// </summary>
/// <remarks>The client copies and validates these options when it is constructed; later changes have no effect.</remarks>
public sealed class TypeSafeClientOptions
{
    /// <summary>Gets or sets the API key. Falls back to <c>TYPESAFE_API_KEY</c>; required.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Gets or sets the absolute HTTP(S) API root. Falls back to <c>TYPESAFE_BASE_URL</c>, then <c>https://api.typesafe.ai</c>.</summary>
    public Uri? BaseUri { get; set; }

    /// <summary>Gets or sets the model used when a request does not set one. Falls back to <c>TYPESAFE_DEFAULT_MODEL</c>, then <c>jev-latest</c>.</summary>
    public string? DefaultModel { get; set; }

    /// <summary>Gets or sets the log level. Falls back to <c>TYPESAFE_LOG_LEVEL</c>, then <see cref="TypeSafeLogLevel.Warn"/>.</summary>
    /// <remarks>Credential headers are redacted at every level; bodies are logged in full at <see cref="TypeSafeLogLevel.Debug"/>.</remarks>
    public TypeSafeLogLevel? LogLevel { get; set; }

    /// <summary>Gets or sets the logger. When neither this nor <see cref="LoggerFactory"/> is set, a minimal console logger is used.</summary>
    public ILogger? Logger { get; set; }

    /// <summary>Gets or sets a logger factory used to create the SDK logger when <see cref="Logger"/> is not set.</summary>
    public ILoggerFactory? LoggerFactory { get; set; }

    /// <summary>Gets or sets the retry policy. Default: <see cref="RetryPolicy.Default"/>.</summary>
    public RetryPolicy? RetryPolicy { get; set; }

    /// <summary>Gets or sets the timeout for each attempt, including reading the full body. Default: 10 seconds.</summary>
    /// <remarks>There is no total budget across retries.</remarks>
    public TimeSpan? Timeout { get; set; }

    /// <summary>Gets headers sent with every request; per-request headers take precedence. SDK headers cannot be overridden.</summary>
    /// <remarks>Content headers (for example <c>Content-Language</c>) are only sent on requests that have a body.</remarks>
    public IDictionary<string, string> DefaultHeaders { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets or sets a value indicating whether to allow running in a browser, which exposes the API key to page users.</summary>
    public bool DangerouslyAllowBrowser { get; set; }

    /// <summary>Gets or sets the time provider for timeouts and delays. Default: <see cref="TimeProvider.System"/>.</summary>
    public TimeProvider? TimeProvider { get; set; }

    /// <inheritdoc/>
    public override string ToString()
        => $"TypeSafeClientOptions {{ ApiKey = {(ApiKey is null ? "(not set)" : "***")}, BaseUri = {BaseUri}, DefaultModel = {DefaultModel}, LogLevel = {LogLevel}, Timeout = {Timeout} }}";
}
