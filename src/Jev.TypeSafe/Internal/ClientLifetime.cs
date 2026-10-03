using System;
using System.Threading;

namespace TypeSafe.AI.Internal;

/// <summary>Tracks disposal of a client so that it and its sub-clients reject calls afterwards.</summary>
internal sealed class ClientLifetime
{
    private int _disposed;

    /// <summary>Marks the client disposed; returns <see langword="true"/> only for the first call.</summary>
    public bool TryMarkDisposed() => Interlocked.Exchange(ref _disposed, 1) == 0;

    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    public void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            throw new ObjectDisposedException(nameof(TypeSafeClient));
        }
    }
}
