using System;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace TypeSafe.AI.Internal.Http;

/// <summary>Describes the runtime for the <c>X-TypeSafe-Runtime</c> header, e.g. <c>dotnet/8.0.11 (linux; x64)</c>.</summary>
internal static class RuntimeDescriptor
{
    private const string Unknown = "unknown";
    private static readonly OSPlatform s_browser = OSPlatform.Create("BROWSER");
    private static readonly OSPlatform s_freeBsd = OSPlatform.Create("FREEBSD");
    private static readonly Lazy<string> s_current = new(DescribeCurrent);
    private static readonly Regex s_framework = new(
        @"^(?<name>.*?)\s*(?<version>\d+(?:\.\d+)*)",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    /// <summary>Gets the description of the current process, computed once.</summary>
    public static string Current => s_current.Value;

    /// <summary>Gets a value indicating whether the process runs in a browser (Blazor WebAssembly).</summary>
    public static bool IsBrowser => RuntimeInformation.IsOSPlatform(s_browser);

    /// <summary>Formats a runtime description from its parts.</summary>
    /// <param name="frameworkDescription">A value such as <see cref="RuntimeInformation.FrameworkDescription"/>.</param>
    /// <param name="operatingSystem">The operating system name.</param>
    /// <param name="architecture">The process architecture.</param>
    /// <param name="isBrowser">Whether the runtime is a browser.</param>
    public static string Describe(string frameworkDescription, string operatingSystem, string architecture, bool isBrowser)
    {
        if (isBrowser)
        {
            return "browser";
        }

        Match match = s_framework.Match(frameworkDescription);
        if (!match.Success)
        {
            return Unknown;
        }

        string name = RuntimeName(match.Groups["name"].Value.Trim());
        return name == Unknown ? Unknown : $"{name}/{match.Groups["version"].Value} ({operatingSystem}; {architecture})";
    }

    private static string DescribeCurrent()
        => Describe(RuntimeInformation.FrameworkDescription, OperatingSystemName(), ArchitectureName(RuntimeInformation.ProcessArchitecture), IsBrowser);

    private static string RuntimeName(string frameworkName) => frameworkName switch
    {
        ".NET Framework" => "dotnet-framework",
        ".NET Core" or ".NET" => "dotnet",
        "Mono" => "mono",
        _ => Unknown,
    };

    private static string OperatingSystemName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "windows";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "linux";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return "osx";
        }

        return RuntimeInformation.IsOSPlatform(s_freeBsd) ? "freebsd" : Unknown;
    }

    private static string ArchitectureName(Architecture architecture) => architecture switch
    {
        Architecture.X86 => "x86",
        Architecture.X64 => "x64",
        Architecture.Arm => "arm",
        Architecture.Arm64 => "arm64",
        _ => Unknown,
    };
}
