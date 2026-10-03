using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace TypeSafe.AI.Internal.Configuration;

/// <summary>Resolved, validated, immutable client settings.</summary>
internal sealed class ClientConfiguration
{
    /// <summary>Gets the API key. Never logged or included in <see cref="ToString"/>.</summary>
    public required string ApiKey { get; init; }

    /// <summary>Gets the API root without trailing slashes; paths are appended to it.</summary>
    public required string BaseUrl { get; init; }

    public required string DefaultModel { get; init; }

    public required TypeSafeLogLevel LogLevel { get; init; }

    /// <summary>Gets the level-filtered logger.</summary>
    public required ILogger Logger { get; init; }

    public required RetryPolicy RetryPolicy { get; init; }

    public required TimeSpan Timeout { get; init; }

    public required IReadOnlyList<KeyValuePair<string, string>> DefaultHeaders { get; init; }

    public required TimeProvider TimeProvider { get; init; }

    public override string ToString() => $"ClientConfiguration {{ BaseUrl = {BaseUrl}, DefaultModel = {DefaultModel} }}";
}
