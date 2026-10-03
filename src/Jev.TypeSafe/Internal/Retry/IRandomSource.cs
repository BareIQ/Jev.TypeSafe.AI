namespace TypeSafe.AI.Internal.Retry;

/// <summary>A source of random numbers for backoff jitter.</summary>
internal interface IRandomSource
{
    /// <summary>Returns a random number greater than or equal to 0 and less than 1.</summary>
    double NextDouble();
}
