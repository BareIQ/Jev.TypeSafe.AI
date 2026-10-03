namespace TypeSafe.AI.Internal.Configuration;

/// <summary>Reads environment variables; abstracted so tests never mutate process state.</summary>
internal interface IEnvironmentReader
{
    /// <summary>Returns the raw value of a variable, or <see langword="null"/> when unset.</summary>
    string? Get(string name);
}
