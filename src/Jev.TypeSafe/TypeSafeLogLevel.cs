namespace TypeSafe.AI;

/// <summary>SDK log verbosity. The SDK logs request summaries at <see cref="Info"/> and headers and bodies at <see cref="Debug"/>.</summary>
public enum TypeSafeLogLevel
{
    /// <summary>Everything, including redacted headers and full request and response bodies.</summary>
    Debug = 0,

    /// <summary>One-line summaries of attempts, retries, timeouts, and cancellations.</summary>
    Info,

    /// <summary>Warnings and errors (the default; the SDK itself emits none, so it is silent).</summary>
    Warn,

    /// <summary>Errors only.</summary>
    Error,

    /// <summary>No logging.</summary>
    Off,
}
