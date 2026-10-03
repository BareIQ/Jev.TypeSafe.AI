using System.Reflection;

namespace TypeSafe.AI;

/// <summary>Information about this SDK build.</summary>
public static class SdkInfo
{
    /// <summary>The version of the upstream JavaScript SDK whose behavior this release tracks.</summary>
    public const string UpstreamVersion = "0.6.0";

    /// <summary>Gets the SDK version, without build metadata.</summary>
    public static string Version { get; } = ReadVersion();

    private static string ReadVersion()
    {
        string? informational = typeof(SdkInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (informational is null || informational.Length == 0)
        {
            return typeof(SdkInfo).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        }

        int metadata = informational.IndexOf('+');
        return metadata < 0 ? informational : informational.Substring(0, metadata);
    }
}
