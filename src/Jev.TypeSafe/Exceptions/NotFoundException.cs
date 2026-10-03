using System;

namespace TypeSafe.AI;

/// <summary>HTTP 404: the resource was not found.</summary>
public sealed class NotFoundException : ApiException
{
    /// <summary>Initializes a new instance of the <see cref="NotFoundException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="response">The HTTP response.</param>
    public NotFoundException(string message, RawResponse response)
        : base(message, response)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="NotFoundException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="response">The HTTP response.</param>
    /// <param name="innerException">The underlying cause, if any.</param>
    public NotFoundException(string message, RawResponse response, Exception? innerException)
        : base(message, response, innerException)
    {
    }
}
