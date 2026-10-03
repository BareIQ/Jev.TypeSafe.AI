using System;
using Microsoft.Extensions.Logging;
using TypeSafe.AI.Internal.Http;
using TypeSafe.AI.Internal.Logging;
using TypeSafe.AI.Internal.Retry;

namespace TypeSafe.AI.Internal.Configuration;

/// <summary>The process-wide collaborators a client uses; replaced in tests.</summary>
internal sealed class ClientDependencies
{
    public ClientDependencies(IEnvironmentReader environment, IRandomSource random, bool isBrowser, Func<ILogger> createConsoleLogger)
    {
        Environment = environment;
        Random = random;
        IsBrowser = isBrowser;
        CreateConsoleLogger = createConsoleLogger;
    }

    public static ClientDependencies Default { get; } = new(
        ProcessEnvironmentReader.Instance,
        ThreadSafeRandomSource.Instance,
        RuntimeDescriptor.IsBrowser,
        CreateStandardConsoleLogger);

    public IEnvironmentReader Environment { get; }

    public IRandomSource Random { get; }

    public bool IsBrowser { get; }

    public Func<ILogger> CreateConsoleLogger { get; }

    private static ConsoleLogger CreateStandardConsoleLogger() => new(Console.Out, Console.Error);
}
