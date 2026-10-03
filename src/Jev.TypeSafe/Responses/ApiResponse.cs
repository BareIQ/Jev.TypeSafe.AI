namespace TypeSafe.AI;

/// <summary>A parsed result together with the HTTP response it came from.</summary>
/// <typeparam name="T">The result type.</typeparam>
public sealed class ApiResponse<T>
{
    internal ApiResponse(T value, RawResponse rawResponse)
    {
        Value = value;
        RawResponse = rawResponse;
    }

    /// <summary>Gets the parsed result.</summary>
    public T Value { get; }

    /// <summary>Gets the buffered HTTP response.</summary>
    public RawResponse RawResponse { get; }

    /// <summary>Gets the request ID from the <c>x-typesafe-request-id</c> header, or <see langword="null"/>.</summary>
    public string? RequestId => RawResponse.RequestId;
}
