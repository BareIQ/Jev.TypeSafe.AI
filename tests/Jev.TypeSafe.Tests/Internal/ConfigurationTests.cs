using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using TypeSafe.AI.Internal.Configuration;
using TypeSafe.AI.Tests.Support;
using Xunit;

namespace TypeSafe.AI.Tests.Internal;

public class OptionsValidatorTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(10_000)]
    [InlineData(int.MaxValue)]
    public void ValidateTimeout_PositiveWithinTimerLimit_IsAccepted(int milliseconds)
    {
        var timeout = TimeSpan.FromMilliseconds(milliseconds);

        Assert.Equal(timeout, OptionsValidator.ValidateTimeout(timeout));
    }

    [Theory]
    [InlineData(0, "0")]
    [InlineData(-1, "-1")]
    [InlineData(-5000, "-5000")]
    [InlineData(2_147_483_648, "2147483648")]
    public void ValidateTimeout_OutOfRange_ThrowsWithTheOffendingValue(long milliseconds, string shown)
    {
        var exception = Assert.Throws<TypeSafeException>(() => OptionsValidator.ValidateTimeout(TimeSpan.FromMilliseconds(milliseconds)));

        Assert.Equal($"`timeout` must be a positive number of milliseconds, got {shown}.", exception.Message);
    }

    [Theory]
    [InlineData("https://api.typesafe.ai", "https://api.typesafe.ai")]
    [InlineData("https://api.typesafe.ai/", "https://api.typesafe.ai")]
    [InlineData("http://localhost:8080///", "http://localhost:8080")]
    [InlineData("https://x.test/api//", "https://x.test/api")]
    public void ValidateBaseUrl_Valid_StripsTrailingSlashes(string input, string expected)
    {
        Assert.Equal(expected, OptionsValidator.ValidateBaseUrl(input, "the source"));
    }

    [Theory]
    [InlineData("ftp://x.test")]
    [InlineData("not a url")]
    [InlineData("/relative/path")]
    [InlineData("")]
    public void ValidateBaseUrl_Invalid_ThrowsNamingTheSource(string input)
    {
        var exception = Assert.Throws<TypeSafeException>(() => OptionsValidator.ValidateBaseUrl(input, "TYPESAFE_BASE_URL"));

        Assert.Contains("TYPESAFE_BASE_URL", exception.Message, StringComparison.Ordinal);
        Assert.Contains("absolute http or https URL", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(TypeSafeLogLevel.Debug)]
    [InlineData(TypeSafeLogLevel.Off)]
    public void ValidateLogLevel_Defined_IsAccepted(TypeSafeLogLevel level)
    {
        Assert.Equal(level, OptionsValidator.ValidateLogLevel(level));
    }

    [Fact]
    public void ValidateLogLevel_Undefined_Throws()
    {
        var exception = Assert.Throws<TypeSafeException>(() => OptionsValidator.ValidateLogLevel((TypeSafeLogLevel)99));

        Assert.Contains("Expected one of: debug, info, warn, error, off.", exception.Message, StringComparison.Ordinal);
    }
}

public class ClientConfigurationResolverTests
{
    private static ClientConfiguration Resolve(
        TypeSafeClientOptions? options = null,
        FakeEnvironmentReader? environment = null,
        bool isBrowser = false,
        Func<ILogger>? console = null)
        => ClientConfigurationResolver.Resolve(
            options ?? new TypeSafeClientOptions { ApiKey = "key" },
            TestClient.Dependencies(environment: environment, isBrowser: isBrowser, createConsoleLogger: console));

    [Fact]
    public void Resolve_OnlyApiKey_UsesSdkDefaults()
    {
        ClientConfiguration configuration = Resolve();

        Assert.Equal("https://api.typesafe.ai", configuration.BaseUrl);
        Assert.Equal("jev-latest", configuration.DefaultModel);
        Assert.Equal(TypeSafeLogLevel.Warn, configuration.LogLevel);
        Assert.Equal(TimeSpan.FromSeconds(10), configuration.Timeout);
        Assert.Equal(RetryPolicy.Default, configuration.RetryPolicy);
        Assert.Same(TimeProvider.System, configuration.TimeProvider);
        Assert.Empty(configuration.DefaultHeaders);
    }

    [Fact]
    public void Resolve_EnvironmentOnly_ReadsEverySetting()
    {
        FakeEnvironmentReader environment = new FakeEnvironmentReader()
            .Set(TypeSafeEnvironmentVariables.ApiKey, "env-key")
            .Set(TypeSafeEnvironmentVariables.BaseUrl, "https://env.test/")
            .Set(TypeSafeEnvironmentVariables.DefaultModel, "env-model")
            .Set(TypeSafeEnvironmentVariables.LogLevel, "debug");

        ClientConfiguration configuration = Resolve(new TypeSafeClientOptions(), environment);

        Assert.Equal("env-key", configuration.ApiKey);
        Assert.Equal("https://env.test", configuration.BaseUrl);
        Assert.Equal("env-model", configuration.DefaultModel);
        Assert.Equal(TypeSafeLogLevel.Debug, configuration.LogLevel);
    }

    [Fact]
    public void Resolve_OptionsAndEnvironment_OptionsWin()
    {
        FakeEnvironmentReader environment = new FakeEnvironmentReader()
            .Set(TypeSafeEnvironmentVariables.ApiKey, "env-key")
            .Set(TypeSafeEnvironmentVariables.BaseUrl, "https://env.test")
            .Set(TypeSafeEnvironmentVariables.DefaultModel, "env-model")
            .Set(TypeSafeEnvironmentVariables.LogLevel, "debug");
        var options = new TypeSafeClientOptions
        {
            ApiKey = "code-key",
            BaseUri = new Uri("https://code.test/"),
            DefaultModel = "code-model",
            LogLevel = TypeSafeLogLevel.Error,
        };

        ClientConfiguration configuration = Resolve(options, environment);

        Assert.Equal("code-key", configuration.ApiKey);
        Assert.Equal("https://code.test", configuration.BaseUrl);
        Assert.Equal("code-model", configuration.DefaultModel);
        Assert.Equal(TypeSafeLogLevel.Error, configuration.LogLevel);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void Resolve_BlankEnvironmentValues_AreTreatedAsUnset(string blank)
    {
        FakeEnvironmentReader environment = new FakeEnvironmentReader()
            .Set(TypeSafeEnvironmentVariables.BaseUrl, blank)
            .Set(TypeSafeEnvironmentVariables.DefaultModel, blank)
            .Set(TypeSafeEnvironmentVariables.LogLevel, blank);

        ClientConfiguration configuration = Resolve(environment: environment);

        Assert.Equal("https://api.typesafe.ai", configuration.BaseUrl);
        Assert.Equal("jev-latest", configuration.DefaultModel);
        Assert.Equal(TypeSafeLogLevel.Warn, configuration.LogLevel);
    }

    [Fact]
    public void Resolve_EnvironmentValues_AreTrimmed()
    {
        FakeEnvironmentReader environment = new FakeEnvironmentReader()
            .Set(TypeSafeEnvironmentVariables.ApiKey, "  padded-key \n")
            .Set(TypeSafeEnvironmentVariables.LogLevel, " info ");

        ClientConfiguration configuration = Resolve(new TypeSafeClientOptions(), environment);

        Assert.Equal("padded-key", configuration.ApiKey);
        Assert.Equal(TypeSafeLogLevel.Info, configuration.LogLevel);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_NoApiKey_ThrowsNamingTheEnvironmentVariable(string? envValue)
    {
        FakeEnvironmentReader environment = new FakeEnvironmentReader().Set(TypeSafeEnvironmentVariables.ApiKey, envValue);

        var exception = Assert.Throws<TypeSafeException>(() => Resolve(new TypeSafeClientOptions(), environment));

        Assert.Equal(
            "No API key was provided. Pass `ApiKey` to the TypeSafeClient constructor or set the TYPESAFE_API_KEY environment variable.",
            exception.Message);
    }

    [Fact]
    public void Resolve_EmptyExplicitApiKey_IsKeptAsProvided()
    {
        Assert.Equal(string.Empty, Resolve(new TypeSafeClientOptions { ApiKey = string.Empty }).ApiKey);
    }

    [Fact]
    public void Resolve_InvalidEnvironmentLogLevel_NamesTheVariable()
    {
        FakeEnvironmentReader environment = new FakeEnvironmentReader().Set(TypeSafeEnvironmentVariables.LogLevel, "verbose");

        var exception = Assert.Throws<TypeSafeException>(() => Resolve(environment: environment));

        Assert.Equal(
            "Invalid log level \"verbose\" from TYPESAFE_LOG_LEVEL. Expected one of: debug, info, warn, error, off.",
            exception.Message);
    }

    [Fact]
    public void Resolve_InvalidEnvironmentBaseUrl_NamesTheVariable()
    {
        FakeEnvironmentReader environment = new FakeEnvironmentReader().Set(TypeSafeEnvironmentVariables.BaseUrl, "ftp://nope");

        var exception = Assert.Throws<TypeSafeException>(() => Resolve(environment: environment));

        Assert.Contains("TYPESAFE_BASE_URL", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_RelativeBaseUri_IsRejected()
    {
        var options = new TypeSafeClientOptions { ApiKey = "k", BaseUri = new Uri("/relative", UriKind.Relative) };

        Assert.Throws<TypeSafeException>(() => Resolve(options));
    }

    [Fact]
    public void Resolve_BaseUriWithPath_KeepsThePath()
    {
        var options = new TypeSafeClientOptions { ApiKey = "k", BaseUri = new Uri("https://gateway.test/typesafe/") };

        Assert.Equal("https://gateway.test/typesafe", Resolve(options).BaseUrl);
    }

    [Fact]
    public void Resolve_UndefinedLogLevelOption_Throws()
    {
        var options = new TypeSafeClientOptions { ApiKey = "k", LogLevel = (TypeSafeLogLevel)42 };

        Assert.Throws<TypeSafeException>(() => Resolve(options));
    }

    [Fact]
    public void Resolve_InvalidTimeout_Throws()
    {
        var options = new TypeSafeClientOptions { ApiKey = "k", Timeout = TimeSpan.Zero };

        Assert.Throws<TypeSafeException>(() => Resolve(options));
    }

    [Fact]
    public void Resolve_CustomTimeoutPolicyAndTimeProvider_AreUsed()
    {
        RetryPolicy policy = RetryPolicy.None;
        var time = new Microsoft.Extensions.Time.Testing.FakeTimeProvider();
        var options = new TypeSafeClientOptions { ApiKey = "k", Timeout = TimeSpan.FromSeconds(3), RetryPolicy = policy, TimeProvider = time };

        ClientConfiguration configuration = Resolve(options);

        Assert.Equal(TimeSpan.FromSeconds(3), configuration.Timeout);
        Assert.Same(policy, configuration.RetryPolicy);
        Assert.Same(time, configuration.TimeProvider);
    }

    [Fact]
    public void Resolve_DefaultHeaders_AreMergedCaseInsensitively()
    {
        var options = new TypeSafeClientOptions { ApiKey = "k" };
        options.DefaultHeaders["X-Team"] = "one";
        options.DefaultHeaders["x-TEAM"] = "two";
        options.DefaultHeaders["Other"] = "3";

        ClientConfiguration configuration = Resolve(options);

        Assert.Equal(2, configuration.DefaultHeaders.Count);
    }

    [Fact]
    public void Resolve_Browser_IsRefusedUnlessExplicitlyAllowed()
    {
        var exception = Assert.Throws<TypeSafeException>(() => Resolve(isBrowser: true));
        Assert.Contains("running in a browser", exception.Message, StringComparison.Ordinal);

        var allowed = new TypeSafeClientOptions { ApiKey = "k", DangerouslyAllowBrowser = true };
        Assert.NotNull(Resolve(allowed, isBrowser: true));
    }

    [Fact]
    public void Resolve_ExplicitLogger_IsUsedThroughTheLevelFilter()
    {
        var sink = new RecordingLogger();
        var options = new TypeSafeClientOptions { ApiKey = "k", Logger = sink, LogLevel = TypeSafeLogLevel.Info };

        ILogger logger = Resolve(options).Logger;
        logger.LogDebug("hidden");
        logger.LogInformation("shown");

        Assert.Equal(["shown"], sink.Messages);
    }

    [Fact]
    public void Resolve_LoggerFactory_CreatesTheSdkCategory()
    {
        var factory = new RecordingLoggerFactory();
        var options = new TypeSafeClientOptions { ApiKey = "k", LoggerFactory = factory, LogLevel = TypeSafeLogLevel.Debug };

        Resolve(options).Logger.LogDebug("hello");

        Assert.Equal(["TypeSafe.AI"], factory.Categories);
        Assert.Equal(["hello"], factory.Logger.Messages);
    }

    [Fact]
    public void Resolve_ExplicitLoggerBeatsLoggerFactory()
    {
        var sink = new RecordingLogger();
        var factory = new RecordingLoggerFactory();
        var options = new TypeSafeClientOptions { ApiKey = "k", Logger = sink, LoggerFactory = factory, LogLevel = TypeSafeLogLevel.Debug };

        Resolve(options).Logger.LogDebug("hello");

        Assert.Empty(factory.Categories);
        Assert.Single(sink.Messages);
    }

    [Fact]
    public void Resolve_NoLogger_UsesTheConsoleFallbackUnlessLoggingIsOff()
    {
        var console = new RecordingLogger();
        int created = 0;
        Func<ILogger> createConsole = () =>
        {
            created++;
            return console;
        };

        Resolve(new TypeSafeClientOptions { ApiKey = "k", LogLevel = TypeSafeLogLevel.Warn }, console: createConsole).Logger.LogWarning("w");
        Resolve(new TypeSafeClientOptions { ApiKey = "k", LogLevel = TypeSafeLogLevel.Off }, console: createConsole).Logger.LogError("e");

        Assert.Equal(1, created);
        Assert.Equal(["w"], console.Messages);
    }

    [Fact]
    public void ToString_NeverIncludesTheApiKey()
    {
        ClientConfiguration configuration = Resolve(new TypeSafeClientOptions { ApiKey = "sk-super-secret" });

        Assert.DoesNotContain("sk-super-secret", configuration.ToString(), StringComparison.Ordinal);
    }
}

public class ResolvedRequestTests
{
    [Fact]
    public void Resolve_NoOptions_InheritsClientSettings()
    {
        ClientConfiguration configuration = ClientConfigurationResolver.Resolve(TestClient.Options(o => o.Timeout = TimeSpan.FromSeconds(3)), TestClient.Dependencies());

        ResolvedRequest request = ResolvedRequest.Resolve(configuration, null);

        Assert.Equal(TimeSpan.FromSeconds(3), request.Timeout);
        Assert.Same(configuration.RetryPolicy, request.RetryPolicy);
    }

    [Fact]
    public void Resolve_PerRequestOptions_OverrideAndMergeHeaders()
    {
        ClientConfiguration configuration = ClientConfigurationResolver.Resolve(
            TestClient.Options(o => o.DefaultHeaders["X-A"] = "client"),
            TestClient.Dependencies());
        var options = new RequestOptions { Timeout = TimeSpan.FromSeconds(1), RetryPolicy = RetryPolicy.None };
        options.Headers["x-a"] = "call";
        options.Headers["X-B"] = "call";

        ResolvedRequest request = ResolvedRequest.Resolve(configuration, options);

        Assert.Equal(TimeSpan.FromSeconds(1), request.Timeout);
        Assert.Same(RetryPolicy.None, request.RetryPolicy);
        Assert.Equal(["x-a=call", "X-B=call"], request.Headers.Select(h => $"{h.Key}={h.Value}"));
    }

    [Fact]
    public void Resolve_OptionsWithoutOverrides_InheritClientSettings()
    {
        ClientConfiguration configuration = ClientConfigurationResolver.Resolve(TestClient.Options(), TestClient.Dependencies());

        ResolvedRequest request = ResolvedRequest.Resolve(configuration, new RequestOptions());

        Assert.Equal(configuration.Timeout, request.Timeout);
        Assert.Same(configuration.RetryPolicy, request.RetryPolicy);
    }

    [Fact]
    public void Resolve_InvalidPerRequestTimeout_Throws()
    {
        ClientConfiguration configuration = ClientConfigurationResolver.Resolve(TestClient.Options(), TestClient.Dependencies());

        Assert.Throws<TypeSafeException>(() => ResolvedRequest.Resolve(configuration, new RequestOptions { Timeout = TimeSpan.FromSeconds(-1) }));
    }
}
