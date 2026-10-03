using System;

namespace TypeSafe.AI.Internal.Retry;

/// <summary>A thread-safe <see cref="IRandomSource"/>; jitter does not need cryptographic randomness.</summary>
internal sealed class ThreadSafeRandomSource : IRandomSource
{
#pragma warning disable CA5394 // Jitter only spreads retries; it has no security purpose.
    private readonly Random _random = new();
#pragma warning restore CA5394
    private readonly object _gate = new();

    public static ThreadSafeRandomSource Instance { get; } = new();

    public double NextDouble()
    {
        lock (_gate)
        {
#pragma warning disable CA5394 // See above.
            return _random.NextDouble();
#pragma warning restore CA5394
        }
    }
}
