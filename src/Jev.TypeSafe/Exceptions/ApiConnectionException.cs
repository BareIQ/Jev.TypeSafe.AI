using System;

namespace TypeSafe.AI;

/// <summary>The request or the delivery of the response body failed (DNS, TLS, connection reset, and so on).</summary>
public class ApiConnectionException : TypeSafeException
{
    private const string DefaultMessage = "Connection error.";

    /// <summary>Initializes a new instance of the <see cref="ApiConnectionException"/> class.</summary>
    public ApiConnectionException()
        : base(DefaultMessage)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ApiConnectionException"/> class.</summary>
    /// <param name="message">The error message.</param>
    public ApiConnectionException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ApiConnectionException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The underlying cause.</param>
    public ApiConnectionException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
