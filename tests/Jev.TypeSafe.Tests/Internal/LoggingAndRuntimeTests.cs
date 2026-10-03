using System;
using System.IO;
using Microsoft.Extensions.Logging;
using TypeSafe.AI.Internal.Http;
using TypeSafe.AI.Internal.Logging;
using TypeSafe.AI.Tests.Support;
using Xunit;

namespace TypeSafe.AI.Tests.Internal;

public class LogLevelParserTests
{
    [Theory]
    [InlineData("debug", TypeSafeLogLevel.Debug)]
    [InlineData("info", TypeSafeLogLevel.Info)]
    [InlineData("warn", TypeSafeLogLevel.Warn)]
    [InlineData("error", TypeSafeLogLevel.Error)]
    [InlineData("off", TypeSafeLogLevel.Off)]
    public void Parse_KnownLevels_ReturnsTheLevel(string value, TypeSafeLogLevel expected)
    {
        Assert.Equal(expected, LogLevelParser.Parse(value, "the source"));
    }

    [Theory]
    [InlineData("INFO")]
    [InlineData("Warn")]
    [InlineData("warning")]
    [InlineData("")]
    public void Parse_UnknownOrDifferentlyCasedLevels_AreRejected(string value)
    {
        var exception = Assert.Throws<TypeSafeException>(() => LogLevelParser.Parse(value, "TYPESAFE_LOG_LEVEL"));

        Assert.Equal(
            $"Invalid log level \"{value}\" from TYPESAFE_LOG_LEVEL. Expected one of: debug, info, warn, error, off.",
            exception.Message);
    }

    [Theory]
    [InlineData(TypeSafeLogLevel.Debug, LogLevel.Debug)]
    [InlineData(TypeSafeLogLevel.Info, LogLevel.Information)]
    [InlineData(TypeSafeLogLevel.Warn, LogLevel.Warning)]
    [InlineData(TypeSafeLogLevel.Error, LogLevel.Error)]
    [InlineData(TypeSafeLogLevel.Off, LogLevel.None)]
    public void ToMinimumLevel_MapsToLoggingLevels(TypeSafeLogLevel level, LogLevel expected)
    {
        Assert.Equal(expected, LogLevelParser.ToMinimumLevel(level));
    }
}

public class LevelFilteredLoggerTests
{
    [Theory]
    [InlineData(LogLevel.Debug, LogLevel.Debug, true)]
    [InlineData(LogLevel.Information, LogLevel.Debug, true)]
    [InlineData(LogLevel.Debug, LogLevel.Information, false)]
    [InlineData(LogLevel.Information, LogLevel.Warning, false)]
    [InlineData(LogLevel.Warning, LogLevel.Warning, true)]
    [InlineData(LogLevel.Error, LogLevel.Warning, true)]
    [InlineData(LogLevel.Critical, LogLevel.None, false)]
    [InlineData(LogLevel.None, LogLevel.Debug, false)]
    public void Log_PassesOnlyMessagesAtOrAboveTheMinimum(LogLevel message, LogLevel minimum, bool passes)
    {
        var sink = new RecordingLogger();
        var logger = new LevelFilteredLogger(sink, minimum);

        logger.Log(message, new EventId(1), "text", null, (state, _) => state);

        Assert.Equal(passes, logger.IsEnabled(message));
        Assert.Equal(passes ? 1 : 0, sink.Entries.Count);
    }

    [Fact]
    public void IsEnabled_RespectsTheUnderlyingLogger()
    {
        var logger = new LevelFilteredLogger(new DisabledLogger(), LogLevel.Debug);

        Assert.False(logger.IsEnabled(LogLevel.Error));
    }

    [Fact]
    public void BeginScope_DelegatesToTheUnderlyingLogger()
    {
        var logger = new LevelFilteredLogger(new RecordingLogger(), LogLevel.Debug);

        Assert.Null(logger.BeginScope("scope"));
    }

    private sealed class DisabledLogger : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => false;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => throw new InvalidOperationException("Should not be called.");
    }
}

public class ConsoleLoggerTests
{
    [Fact]
    public void Log_InfoAndDebug_GoToStandardOutputWithThePrefix()
    {
        var (logger, output, error) = Create();

        logger.LogDebug("one");
        logger.LogInformation("two");

        Assert.Equal($"[typesafe-sdk] one{Environment.NewLine}[typesafe-sdk] two{Environment.NewLine}", output.ToString());
        Assert.Empty(error.ToString());
    }

    [Fact]
    public void Log_WarningsAndErrors_GoToTheErrorStream()
    {
        var (logger, output, error) = Create();

        logger.LogWarning("careful");
        logger.LogError("broken");

        Assert.Empty(output.ToString());
        Assert.Equal($"[typesafe-sdk] careful{Environment.NewLine}[typesafe-sdk] broken{Environment.NewLine}", error.ToString());
    }

    [Fact]
    public void Log_WithException_AppendsIt()
    {
        var (logger, output, _) = Create();

        logger.LogInformation(new InvalidOperationException("kaput"), "failed");

        Assert.Contains("[typesafe-sdk] failed", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("kaput", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void IsEnabled_EverythingExceptNone()
    {
        var (logger, _, _) = Create();

        Assert.True(logger.IsEnabled(LogLevel.Trace));
        Assert.False(logger.IsEnabled(LogLevel.None));
        logger.Log(LogLevel.None, new EventId(1), "x", null, (state, _) => state);
    }

    [Fact]
    public void BeginScope_IsNotSupported()
    {
        Assert.Null(Create().Logger.BeginScope("scope"));
    }

    private static (ConsoleLogger Logger, StringWriter Output, StringWriter Error) Create()
    {
        var output = new StringWriter();
        var error = new StringWriter();
        return (new ConsoleLogger(output, error), output, error);
    }
}

public class RuntimeDescriptorTests
{
    [Theory]
    [InlineData(".NET 8.0.11", "linux", "x64", "dotnet/8.0.11 (linux; x64)")]
    [InlineData(".NET 10.0.0-rc.1.25451.107", "osx", "arm64", "dotnet/10.0.0 (osx; arm64)")]
    [InlineData(".NET Core 3.1.32", "windows", "x86", "dotnet/3.1.32 (windows; x86)")]
    [InlineData(".NET Framework 4.8.9290.0", "windows", "x64", "dotnet-framework/4.8.9290.0 (windows; x64)")]
    [InlineData("Mono 6.12.0.182 (tarball)", "linux", "arm", "mono/6.12.0.182 (linux; arm)")]
    [InlineData(".NET Native 1.7", "windows", "x64", "unknown")]
    [InlineData("no version here", "linux", "x64", "unknown")]
    [InlineData("", "linux", "x64", "unknown")]
    public void Describe_FrameworkDescription_ProducesTheHeaderValue(string framework, string os, string arch, string expected)
    {
        Assert.Equal(expected, RuntimeDescriptor.Describe(framework, os, arch, isBrowser: false));
    }

    [Fact]
    public void Describe_Browser_IsJustBrowser()
    {
        Assert.Equal("browser", RuntimeDescriptor.Describe(".NET 8.0.0", "browser", "wasm", isBrowser: true));
    }

    [Fact]
    public void Current_DescribesThisProcess()
    {
        Assert.Matches(@"^(dotnet|dotnet-framework|mono)/\d+(\.\d+)* \((windows|linux|osx|freebsd|unknown); (x86|x64|arm|arm64|unknown)\)$", RuntimeDescriptor.Current);
        Assert.False(RuntimeDescriptor.IsBrowser);
    }
}
