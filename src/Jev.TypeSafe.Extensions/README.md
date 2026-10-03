# Jev.TypeSafe.Unofficial.Extensions

> **Unofficial.** Dependency-injection support for [`Jev.TypeSafe.Unofficial`](https://www.nuget.org/packages/Jev.TypeSafe.Unofficial),
> the community .NET SDK for [TypeSafe AI](https://typesafe.ai) (Jev). Not affiliated with or endorsed by TypeSafe AI.

Registers `ITypeSafeClient` and `TypeSafeClient` with `Microsoft.Extensions.DependencyInjection`, backed by
`IHttpClientFactory`, with options bound from `IConfiguration`. Targets `netstandard2.0`, `net8.0`, `net9.0` and `net10.0`.

## Install

```sh
dotnet add package Jev.TypeSafe.Unofficial.Extensions --prerelease
```

## Usage

```csharp
using Microsoft.Extensions.DependencyInjection;
using TypeSafe.AI;

services.AddTypeSafeClient(options => options.DefaultModel = "jev-latest");
```

`ApiKey` falls back to the `TYPESAFE_API_KEY` environment variable, like the plain client. Inject `ITypeSafeClient`
wherever you need it.

### From configuration

```json
{
  "TypeSafe": {
    "ApiKey": "...",
    "DefaultModel": "jev-latest",
    "Timeout": "00:00:10",
    "RetryPolicy": { "MaxRetries": 3 },
    "DefaultHeaders": { "X-Team": "search" }
  }
}
```

```csharp
services.AddTypeSafeClient(configuration.GetSection("TypeSafe"));
```

Keep real keys in user secrets or environment variables, not in `appsettings.json`.

### Overloads

| Overload | Use it to |
|---|---|
| `AddTypeSafeClient()` | rely on environment variables and defaults |
| `AddTypeSafeClient(Action<TypeSafeClientOptions>)` | set options in code |
| `AddTypeSafeClient(Action<TypeSafeClientOptions, IServiceProvider>)` | set options using other registered services |
| `AddTypeSafeClient(IConfiguration)` | bind a configuration section |

Option sources apply in registration order, with later ones winning. Anything still unset falls back to the
environment variables and defaults of the core client.

### Customizing the HTTP pipeline

Every overload returns an `IHttpClientBuilder`:

```csharp
services.AddTypeSafeClient(configuration.GetSection("TypeSafe"))
    .AddHttpMessageHandler<MyLoggingHandler>();
```

If you add a resilience handler, consider `RetryPolicy.None` so retries are not applied twice.

## Behavior

- **Lifetime:** one singleton, shared by `ITypeSafeClient` and `TypeSafeClient`. The container disposes it; it does not
  dispose the factory's `HttpClient`.
- **Logging and time:** `ILoggerFactory` and `TimeProvider` are taken from the container unless you set them explicitly.
  They are never bound from configuration, nor is `Logger`.
- **Timeout:** the factory `HttpClient.Timeout` is infinite because the SDK enforces its own per-attempt timeout
  (`TypeSafeClientOptions.Timeout`).
- **Connections:** on .NET 8 and later the primary handler is a `SocketsHttpHandler` with a 2-minute pooled-connection
  lifetime, so DNS changes are honored by the long-lived client. Override it with `ConfigurePrimaryHttpMessageHandler`.
- **Validation:** options are validated when the client is first resolved, not at host start. Resolve it once during
  startup if you want to fail fast.

See the [main README](https://github.com/BareIQ/Jev.TypeSafe.AI#readme) for the client API.
