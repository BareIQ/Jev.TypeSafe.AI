using System;
using System.Threading;

namespace TypeSafe.AI;

/// <summary>
/// The caller cancelled the request through its <see cref="System.Threading.CancellationToken"/>.
/// The <see cref="Exception.InnerException"/> is the original <see cref="OperationCanceledException"/>, when there is one.
/// </summary>
public sealed class ApiUserAbortException : TypeSafeException
{
    private const string DefaultMessage = "Request was aborted.";

    /// <summary>Initializes a new instance of the <see cref="ApiUserAbortException"/> class.</summary>
    public ApiUserAbortException()
        : base(DefaultMessage)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ApiUserAbortException"/> class.</summary>
    /// <param name="message">The error message.</param>
    public ApiUserAbortException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ApiUserAbortException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The underlying cause.</param>
    public ApiUserAbortException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ApiUserAbortException"/> class.</summary>
    /// <param name="innerException">The underlying cause, if any.</param>
    /// <param name="cancellationToken">The caller's token that was cancelled.</param>
    public ApiUserAbortException(Exception? innerException, CancellationToken cancellationToken)
        : base(DefaultMessage, innerException)
        => CancellationToken = cancellationToken;

    /// <summary>Gets the caller's token that was cancelled, or <see cref="CancellationToken.None"/> when unknown.</summary>
    public CancellationToken CancellationToken { get; }
}
