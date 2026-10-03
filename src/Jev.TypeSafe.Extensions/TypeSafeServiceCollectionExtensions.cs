using System;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TypeSafe.AI;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers the TypeSafe AI client with an <see cref="IServiceCollection"/>.</summary>
/// <remarks>
/// <para>
/// The client is registered as a singleton (both <see cref="ITypeSafeClient"/> and <see cref="TypeSafeClient"/> resolve to
/// the same instance) and sends requests through a named <see cref="IHttpClientFactory"/> client. The returned
/// <see cref="IHttpClientBuilder"/> lets you add message handlers, a proxy, or a resilience pipeline.
/// </para>
/// <para>
/// Options are resolved in this order: the configuration section, then the configure delegates (in registration order),
/// then the environment variables and defaults that <see cref="TypeSafeClient"/> applies itself. The client validates
/// its options when it is first resolved, not when the host starts.
/// </para>
/// </remarks>
public static class TypeSafeServiceCollectionExtensions
{
    private const string HttpClientName = "TypeSafe.AI";

    /// <summary>Registers the client using only environment variables and defaults.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>A builder for the underlying <see cref="HttpClient"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IHttpClientBuilder AddTypeSafeClient(this IServiceCollection services)
    {
        Require(services, nameof(services));
        return Register(services);
    }

    /// <summary>Registers the client and configures its options.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configures <see cref="TypeSafeClientOptions"/>.</param>
    /// <returns>A builder for the underlying <see cref="HttpClient"/>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static IHttpClientBuilder AddTypeSafeClient(this IServiceCollection services, Action<TypeSafeClientOptions> configure)
    {
        Require(services, nameof(services));
        Require(configure, nameof(configure));
        services.Configure(configure);
        return Register(services);
    }

    /// <summary>Registers the client and configures its options with access to other services.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configures <see cref="TypeSafeClientOptions"/> using the root <see cref="IServiceProvider"/>.</param>
    /// <returns>A builder for the underlying <see cref="HttpClient"/>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static IHttpClientBuilder AddTypeSafeClient(this IServiceCollection services, Action<TypeSafeClientOptions, IServiceProvider> configure)
    {
        Require(services, nameof(services));
        Require(configure, nameof(configure));
        services.AddOptions<TypeSafeClientOptions>().Configure(configure);
        return Register(services);
    }

    /// <summary>Registers the client and binds its options from a configuration section.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">
    /// The section to bind, for example <c>configuration.GetSection("TypeSafe")</c>. <c>Timeout</c> uses the
    /// <c>hh:mm:ss</c> format. <c>Logger</c>, <c>LoggerFactory</c> and <c>TimeProvider</c> are never bound; they come from the container.
    /// </param>
    /// <returns>A builder for the underlying <see cref="HttpClient"/>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static IHttpClientBuilder AddTypeSafeClient(this IServiceCollection services, IConfiguration configuration)
    {
        Require(services, nameof(services));
        Require(configuration, nameof(configuration));
        services.Configure<TypeSafeClientOptions>(configuration);
        return Register(services);
    }

    private static IHttpClientBuilder Register(IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IConfigureOptions<TypeSafeClientOptions>, ContainerDefaultsConfigureOptions>());

        IHttpClientBuilder builder = services.AddHttpClient(HttpClientName, static client =>
        {
            // The SDK enforces its own per-attempt timeout, so HttpClient must not cut requests short.
            client.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
        });

#if NET8_0_OR_GREATER
        // The client is a singleton and holds one HttpClient, so recycle pooled connections to honor DNS changes.
        builder.ConfigurePrimaryHttpMessageHandler(static () => new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(2) });
#endif

        services.TryAddSingleton(static provider => new TypeSafeClient(
            provider.GetRequiredService<IOptions<TypeSafeClientOptions>>().Value,
            provider.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName)));
        services.TryAddSingleton<ITypeSafeClient>(static provider => provider.GetRequiredService<TypeSafeClient>());
        return builder;
    }

    private static void Require(object? value, string name)
    {
        if (value is null)
        {
            throw new ArgumentNullException(name);
        }
    }

    /// <summary>Fills the options the configuration cannot supply: the logger factory and time provider from the container.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by the DI container.")]
    private sealed class ContainerDefaultsConfigureOptions : IConfigureOptions<TypeSafeClientOptions>
    {
        private readonly IServiceProvider _services;

        public ContainerDefaultsConfigureOptions(IServiceProvider services) => _services = services;

        public void Configure(TypeSafeClientOptions options)
        {
            options.LoggerFactory ??= _services.GetService<ILoggerFactory>();
            options.TimeProvider ??= _services.GetService<TimeProvider>();
        }
    }
}
