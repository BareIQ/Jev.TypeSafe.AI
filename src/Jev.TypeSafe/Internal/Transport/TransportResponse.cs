namespace TypeSafe.AI.Internal.Transport;

/// <summary>A successful response and its leniently parsed body.</summary>
internal sealed class TransportResponse
{
    public TransportResponse(RawResponse response, ParsedBody body)
    {
        Response = response;
        Body = body;
    }

    public RawResponse Response { get; }

    public ParsedBody Body { get; }
}
