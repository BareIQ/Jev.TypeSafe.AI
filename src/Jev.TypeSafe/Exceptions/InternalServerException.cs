using System;

namespace TypeSafe.AI;

/// <summary>HTTP 5xx: the server failed to handle the request.</summary>
public sealed class InternalServerException : ApiException
{
    /// <summary>Initializes a new instance of the <see cref="InternalServerException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="response">The HTTP response.</param>
    public InternalServerException(string message, RawResponse response)
        : base(message, response)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="InternalServerException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="response">The HTTP response.</param>
    /// <param name="innerException">The underlying cause, if any.</param>
    public InternalServerException(string message, RawResponse response, Exception? innerException)
        : base(message, response, innerException)
    {
    }
}
