namespace TypeSafe.AI.Internal.Http;

/// <summary>Header names and values the SDK sends or reads.</summary>
internal static class SdkHeaders
{
    public const string Authorization = "Authorization";
    public const string Accept = "Accept";
    public const string UserAgent = "User-Agent";
    public const string Sdk = "X-TypeSafe-SDK";
    public const string Runtime = "X-TypeSafe-Runtime";
    public const string ContentType = "Content-Type";
    public const string RetryCount = "X-TypeSafe-Retry-Count";
    public const string RequestId = "x-typesafe-request-id";
    public const string JsonMediaType = "application/json";

    /// <summary>Gets the client identity sent in <c>User-Agent</c> and <c>X-TypeSafe-SDK</c>.</summary>
    public static string Identity { get; } = "typesafe-sdk-dotnet/" + SdkInfo.Version;
}
