using System;

namespace TypeSafe.AI;

/// <summary>HTTP 401: authentication failed.</summary>
public sealed class AuthenticationException : ApiException
{
    /// <summary>Initializes a new instance of the <see cref="AuthenticationException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="response">The HTTP response.</param>
    public AuthenticationException(string message, RawResponse response)
        : base(message, response)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="AuthenticationException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="response">The HTTP response.</param>
    /// <param name="innerException">The underlying cause, if any.</param>
    public AuthenticationException(string message, RawResponse response, Exception? innerException)
        : base(message, response, innerException)
    {
    }
}
