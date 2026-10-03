using System;

namespace TypeSafe.AI;

/// <summary>HTTP 422: request validation failed.</summary>
public sealed class UnprocessableEntityException : ApiException
{
    /// <summary>Initializes a new instance of the <see cref="UnprocessableEntityException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="response">The HTTP response.</param>
    public UnprocessableEntityException(string message, RawResponse response)
        : base(message, response)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="UnprocessableEntityException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="response">The HTTP response.</param>
    /// <param name="innerException">The underlying cause, if any.</param>
    public UnprocessableEntityException(string message, RawResponse response, Exception? innerException)
        : base(message, response, innerException)
    {
    }
}
