using System;

namespace TypeSafe.AI.Internal.Configuration;

/// <summary>Reads the current process's environment variables.</summary>
internal sealed class ProcessEnvironmentReader : IEnvironmentReader
{
    public static ProcessEnvironmentReader Instance { get; } = new();

    public string? Get(string name) => Environment.GetEnvironmentVariable(name);
}
