using System;
using TypeSafe.AI.Internal;

namespace TypeSafe.AI;

/// <summary>The full response did not arrive within the per-attempt timeout. A kind of <see cref="ApiConnectionException"/>.</summary>
public sealed class ApiTimeoutException : ApiConnectionException
{
    /// <summary>Initializes a new instance of the <see cref="ApiTimeoutException"/> class.</summary>
    /// <param name="timeout">The timeout that elapsed.</param>
    public ApiTimeoutException(TimeSpan timeout)
        : this(timeout, null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ApiTimeoutException"/> class.</summary>
    /// <param name="timeout">The timeout that elapsed.</param>
    /// <param name="innerException">The underlying cause, if any.</param>
    public ApiTimeoutException(TimeSpan timeout, Exception? innerException)
        : base($"Request timed out after {Milliseconds.Format(timeout)}ms.", innerException)
        => Timeout = timeout;

    /// <summary>Gets the timeout that elapsed.</summary>
    public TimeSpan Timeout { get; }
}
