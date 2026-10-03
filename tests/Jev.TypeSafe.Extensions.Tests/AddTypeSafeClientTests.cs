using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace TypeSafe.AI.Extensions.Tests;

public sealed class AddTypeSafeClientTests
{
    private const string ModelsJson = """{"models":[{"name":"jev-test","description":"d","release_date":"2025-01-01"}]}""";

    [Fact]
    public void ResolvesBothServiceTypesAsTheSameSingleton()
    {
        using ServiceProvider provider = Build(o => o.ApiKey = "k");

        var concrete = provider.GetRequiredService<TypeSafeClient>();
        var abstraction = provider.GetRequiredService<ITypeSafeClient>();

        Assert.Same(concrete, abstraction);
        Assert.Same(concrete, provider.GetRequiredService<TypeSafeClient>());
    }

    [Fact]
    public async Task ConfigureDelegateAppliesAndRequestsGoThroughTheFactoryPipeline()
    {
        var stub = new StubHandler();
        using ServiceProvider provider = Build(o =>
        {
            o.ApiKey = "delegate-key";
            o.BaseUri = new Uri("https://example.test/");
        }, stub);

        IReadOnlyList<ModelCard> models = await provider.GetRequiredService<ITypeSafeClient>().Models.ListAsync();

        Assert.Equal("jev-test", Assert.Single(models).Name);
        HttpRequestMessage request = Assert.Single(stub.Requests);
        Assert.Equal("example.test", request.RequestUri!.Host);
        Assert.Contains("delegate-key", request.Headers.Authorization!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task BindsOptionsFromConfiguration()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApiKey"] = "config-key",
                ["BaseUri"] = "https://config.test/",
                ["DefaultModel"] = "jev-config",
                ["Timeout"] = "00:00:07",
                ["LogLevel"] = "Error",
                ["DefaultHeaders:X-From-Config"] = "yes",
                ["RetryPolicy:MaxRetries"] = "5",
            })
            .Build();
        var stub = new StubHandler();
        var services = new ServiceCollection();
        services.AddTypeSafeClient(configuration).ConfigurePrimaryHttpMessageHandler(() => stub);
        using ServiceProvider provider = services.BuildServiceProvider();

        await provider.GetRequiredService<ITypeSafeClient>().Models.ListAsync();

        HttpRequestMessage request = Assert.Single(stub.Requests);
        Assert.Equal("config.test", request.RequestUri!.Host);
        Assert.Equal("yes", request.Headers.GetValues("X-From-Config").Single());
        TypeSafeClientOptions options = provider.GetRequiredService<IOptions<TypeSafeClientOptions>>().Value;
        Assert.Equal("jev-config", options.DefaultModel);
        Assert.Equal(TimeSpan.FromSeconds(7), options.Timeout);
        Assert.Equal(TypeSafeLogLevel.Error, options.LogLevel);
        Assert.Equal(5, options.RetryPolicy!.MaxRetries);
    }

    [Fact]
    public void ConfigureDelegateOverridesConfiguration()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ApiKey"] = "config-key", ["DefaultModel"] = "from-config" })
            .Build();
        var services = new ServiceCollection();
        services.AddTypeSafeClient(configuration);
        services.AddTypeSafeClient(o => o.DefaultModel = "from-delegate");
        using ServiceProvider provider = services.BuildServiceProvider();

        TypeSafeClientOptions options = provider.GetRequiredService<IOptions<TypeSafeClientOptions>>().Value;

        Assert.Equal("config-key", options.ApiKey);
        Assert.Equal("from-delegate", options.DefaultModel);
    }

    [Fact]
    public void ProviderAwareDelegateCanUseOtherServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new SecretSource("from-service"));
        services.AddTypeSafeClient((o, provider) => o.ApiKey = provider.GetRequiredService<SecretSource>().Value);
        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Equal("from-service", provider.GetRequiredService<IOptions<TypeSafeClientOptions>>().Value.ApiKey);
    }

    [Fact]
    public async Task ReturnedBuilderAllowsAddingMessageHandlers()
    {
        var stub = new StubHandler();
        var services = new ServiceCollection();
        services.AddTypeSafeClient(o => o.ApiKey = "k")
            .AddHttpMessageHandler(() => new HeaderHandler("X-Added-By-Handler", "1"))
            .ConfigurePrimaryHttpMessageHandler(() => stub);
        using ServiceProvider provider = services.BuildServiceProvider();

        await provider.GetRequiredService<ITypeSafeClient>().Models.ListAsync();

        Assert.Equal("1", Assert.Single(stub.Requests).Headers.GetValues("X-Added-By-Handler").Single());
    }

    [Fact]
    public void TakesLoggerFactoryAndTimeProviderFromTheContainer()
    {
        var timeProvider = new FakeTimeProvider();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(timeProvider);
        services.AddTypeSafeClient(o => o.ApiKey = "k");
        using ServiceProvider provider = services.BuildServiceProvider();

        TypeSafeClientOptions options = provider.GetRequiredService<IOptions<TypeSafeClientOptions>>().Value;

        Assert.Same(provider.GetRequiredService<ILoggerFactory>(), options.LoggerFactory);
        Assert.Same(timeProvider, options.TimeProvider);
    }

    [Fact]
    public void ExplicitLoggerFactoryAndTimeProviderWinOverTheContainer()
    {
        using var ownFactory = new LoggerFactory();
        var ownTime = new FakeTimeProvider();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(new FakeTimeProvider());
        services.AddTypeSafeClient(o =>
        {
            o.ApiKey = "k";
            o.LoggerFactory = ownFactory;
            o.TimeProvider = ownTime;
        });
        using ServiceProvider provider = services.BuildServiceProvider();

        TypeSafeClientOptions options = provider.GetRequiredService<IOptions<TypeSafeClientOptions>>().Value;

        Assert.Same(ownFactory, options.LoggerFactory);
        Assert.Same(ownTime, options.TimeProvider);
    }

    [Fact]
    public void WorksWithoutATimeProviderRegistered()
    {
        using ServiceProvider provider = Build(o => o.ApiKey = "k");

        TypeSafeClientOptions options = provider.GetRequiredService<IOptions<TypeSafeClientOptions>>().Value;

        // AddHttpClient registers logging, so a logger factory is always present; a time provider only when the app adds one.
        Assert.NotNull(options.LoggerFactory);
        Assert.Null(options.TimeProvider);
        Assert.NotNull(provider.GetRequiredService<ITypeSafeClient>());
    }

    [Fact]
    public void FactoryHttpClientHasAnInfiniteTimeout()
    {
        using ServiceProvider provider = Build(o => o.ApiKey = "k");

        using HttpClient client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("TypeSafe.AI");

        Assert.Equal(Timeout.InfiniteTimeSpan, client.Timeout);
    }

    [Fact]
    public void InvalidOptionsFailOnFirstResolve()
    {
        using ServiceProvider provider = Build(o =>
        {
            o.ApiKey = "k";
            o.BaseUri = new Uri("/relative", UriKind.Relative);
        });

        Assert.Throws<TypeSafeException>(() => provider.GetRequiredService<ITypeSafeClient>());
    }

    [Fact]
    public void RepeatedRegistrationDoesNotDuplicateTheClient()
    {
        var services = new ServiceCollection();
        services.AddTypeSafeClient(o => o.ApiKey = "k");
        services.AddTypeSafeClient(o => o.DefaultModel = "m");

        Assert.Single(services, d => d.ServiceType == typeof(ITypeSafeClient));
        Assert.Single(services, d => d.ServiceType == typeof(TypeSafeClient));
    }

    [Fact]
    public async Task DisposingTheProviderDisposesTheClient()
    {
        ServiceProvider provider = Build(o => o.ApiKey = "k");
        var client = provider.GetRequiredService<ITypeSafeClient>();

        await provider.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.Models.ListAsync());
    }

    [Fact]
    public void RejectsNullArguments()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() => TypeSafeServiceCollectionExtensions.AddTypeSafeClient(null!));
        Assert.Throws<ArgumentNullException>(() => TypeSafeServiceCollectionExtensions.AddTypeSafeClient(null!, o => { }));
        Assert.Throws<ArgumentNullException>(() => services.AddTypeSafeClient((Action<TypeSafeClientOptions>)null!));
        Assert.Throws<ArgumentNullException>(() => services.AddTypeSafeClient((Action<TypeSafeClientOptions, IServiceProvider>)null!));
        Assert.Throws<ArgumentNullException>(() => services.AddTypeSafeClient((IConfiguration)null!));
    }

    private static ServiceProvider Build(Action<TypeSafeClientOptions> configure, StubHandler? stub = null)
    {
        var services = new ServiceCollection();
        IHttpClientBuilder builder = services.AddTypeSafeClient(configure);
        if (stub is not null)
        {
            builder.ConfigurePrimaryHttpMessageHandler(() => stub);
        }

        return services.BuildServiceProvider();
    }

    private sealed record SecretSource(string Value);

    private sealed class HeaderHandler(string name, string value) : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.TryAddWithoutValidation(name, value);
            return base.SendAsync(request, cancellationToken);
        }
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly List<HttpRequestMessage> _requests = [];

        public IReadOnlyList<HttpRequestMessage> Requests => _requests;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ModelsJson, Encoding.UTF8, "application/json"),
            });
        }
    }
}
