using System;

namespace TypeSafe.AI;

/// <summary>HTTP 400: the request is invalid.</summary>
public sealed class BadRequestException : ApiException
{
    /// <summary>Initializes a new instance of the <see cref="BadRequestException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="response">The HTTP response.</param>
    public BadRequestException(string message, RawResponse response)
        : base(message, response)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="BadRequestException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="response">The HTTP response.</param>
    /// <param name="innerException">The underlying cause, if any.</param>
    public BadRequestException(string message, RawResponse response, Exception? innerException)
        : base(message, response, innerException)
    {
    }
}
