namespace TypeSafe.AI;

/// <summary>Token usage for a request.</summary>
public sealed class Usage
{
    internal Usage(long inputTokens, long outputTokens)
    {
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
    }

    /// <summary>Gets the number of input tokens used.</summary>
    public long InputTokens { get; }

    /// <summary>Gets the number of output tokens used.</summary>
    public long OutputTokens { get; }
}
