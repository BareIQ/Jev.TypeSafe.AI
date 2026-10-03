namespace TypeSafe.AI.Internal.Transport;

/// <summary>The result of one attempt: a buffered response, or a retryable connection failure.</summary>
internal readonly struct AttemptOutcome
{
    private AttemptOutcome(RawResponse? response, ApiConnectionException? failure)
    {
        Response = response;
        Failure = failure;
    }

    public RawResponse? Response { get; }

    public ApiConnectionException? Failure { get; }

    public static AttemptOutcome Succeeded(RawResponse response) => new(response, null);

    public static AttemptOutcome Failed(ApiConnectionException failure) => new(null, failure);
}
