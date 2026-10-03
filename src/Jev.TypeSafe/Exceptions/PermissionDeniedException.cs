using System;

namespace TypeSafe.AI;

/// <summary>HTTP 403: access is denied.</summary>
public sealed class PermissionDeniedException : ApiException
{
    /// <summary>Initializes a new instance of the <see cref="PermissionDeniedException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="response">The HTTP response.</param>
    public PermissionDeniedException(string message, RawResponse response)
        : base(message, response)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="PermissionDeniedException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="response">The HTTP response.</param>
    /// <param name="innerException">The underlying cause, if any.</param>
    public PermissionDeniedException(string message, RawResponse response, Exception? innerException)
        : base(message, response, innerException)
    {
    }
}
