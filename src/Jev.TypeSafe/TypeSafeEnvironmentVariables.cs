namespace TypeSafe.AI;

/// <summary>Environment variables read when the corresponding <see cref="TypeSafeClientOptions"/> value is not set.</summary>
/// <remarks>Values are trimmed; empty or whitespace-only values are ignored.</remarks>
public static class TypeSafeEnvironmentVariables
{
    /// <summary>The API key (required when <see cref="TypeSafeClientOptions.ApiKey"/> is not set).</summary>
    public const string ApiKey = "TYPESAFE_API_KEY";

    /// <summary>The API root; defaults to <c>https://api.typesafe.ai</c>.</summary>
    public const string BaseUrl = "TYPESAFE_BASE_URL";

    /// <summary>The default model; defaults to <c>jev-latest</c>.</summary>
    public const string DefaultModel = "TYPESAFE_DEFAULT_MODEL";

    /// <summary>The log level: <c>debug</c>, <c>info</c>, <c>warn</c>, <c>error</c>, or <c>off</c>; defaults to <c>warn</c>.</summary>
    public const string LogLevel = "TYPESAFE_LOG_LEVEL";
}
