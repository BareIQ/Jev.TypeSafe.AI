using System;

namespace TypeSafe.AI;

/// <summary>The base class for every error raised by this SDK.</summary>
public class TypeSafeException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="TypeSafeException"/> class.</summary>
    public TypeSafeException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TypeSafeException"/> class.</summary>
    /// <param name="message">The error message.</param>
    public TypeSafeException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TypeSafeException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The underlying cause.</param>
    public TypeSafeException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
