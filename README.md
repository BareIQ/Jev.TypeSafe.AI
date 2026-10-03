# Jev.TypeSafe.Unofficial

> **Unofficial.** This is a community-maintained .NET SDK for [TypeSafe AI](https://typesafe.ai) (the **Jev**
> models). It is not affiliated with, endorsed by, or supported by TypeSafe AI. It is a port of the official
> JavaScript SDK, `@typesafe-ai/sdk`; see [NOTICE.md](NOTICE.md).

Ask questions about text or structured data and get probabilities back: yes/no (`noul`), choice, and score answers,
with strongly typed results.

- **Idiomatic .NET:** `Task`-based `…Async` methods, `CancellationToken`, `HttpClient`, `System.Text.Json`, and
  `Microsoft.Extensions.Logging`.
- **Typed answers:** each question you add returns a key; reading an answer with it needs no casts. Choice questions can
  be backed by an `enum`.
- **Reliable by default:** retries with backoff and jitter, `Retry-After` support, per-attempt timeouts, and
  cancellation, all configurable per client and per call.
- **Testable:** a mockable `ITypeSafeClient`, injectable `HttpClient`, `TimeProvider`, and logger, and a factory for
  building results in your own tests.
- **Broad reach:** targets .NET Standard 2.0 (.NET Framework 4.6.2+, .NET Core 2.0+, .NET 5+, Mono, Unity, Xamarin).

## Install

```sh
dotnet add package Jev.TypeSafe.Unofficial --prerelease
```

## Quickstart

Set `TYPESAFE_API_KEY` in your environment, then create a client and ask questions:

```csharp
using System;
using System.Text.Json.Nodes;
using TypeSafe.AI;

using var client = new TypeSafeClient(); // reads TYPESAFE_API_KEY

var questions = new QuestionSet();
var category = questions.Add(
    "category",
    Question.Choice("What is this ticket about?", ChoiceCriteria.FromLabels("billing", "technical", "other")));

SystemOneResult result = await client.SystemOneAsync(new SystemOneRequest(
    new JsonObject { ["document"] = "I was charged twice. Please fix this ASAP." },
    questions));

Console.WriteLine(result.Answers.Get(category).Choice);
```

`TypeSafeClient` is thread-safe and meant to be long-lived: create one and reuse it.

A complete, runnable example is in [`examples/Demo`](examples/Demo/Program.cs).

## Questions and typed answers

The `state` is what you ask about: text, a JSON object or array, or `Entry.Null`. Each question is one of three kinds:

| Builder | Answer | What you get |
|---|---|---|
| `Question.Noul(instructions, criteria?)` | `NoulAnswer` | `Noul`: probability of *yes*, from 0 to 1 |
| `Question.Choice(instructions, criteria)` | `ChoiceAnswer` | `Choice`, `Confidence`, `Probabilities` by label |
| `Question.Score(instructions, descriptions…)` | `ScoreAnswer` | `Score` (may be fractional), `Confidence`, `Legend`, `Probabilities` by score |

```csharp
var questions = new QuestionSet();

var isBilling = questions.Add("isBilling", Question.Noul("Is this ticket about billing?"));
var tone      = questions.Add("tone", Question.Choice<Tone>("What is the customer's tone?")); // enum labels
var urgency   = questions.Add("urgency", Question.Score("How urgent is this?", "can wait", "this week", "today"));

var result = await client.SystemOneAsync(new SystemOneRequest(ticket, questions));

double billing = result.Answers.Get(isBilling).Noul;                 // NoulAnswer
Tone customerTone = result.Answers.Get(tone).Value;                  // ChoiceAnswer<Tone>
double urgencyScore = result.Answers.Get(urgency).Score;             // ScoreAnswer

public enum Tone { Calm, [EnumMember(Value = "frustrated")] Frustrated, Angry }
```

- **Choice labels** are the `enum` member names (or `[EnumMember(Value = "…")]`), or the labels you pass to
  `ChoiceCriteria`. Describe them with a collection initializer:
  `new ChoiceCriteria { { "billing", "Payments and invoices" }, { "other", Entry.Null } }`.
- **Score descriptions** are indexed by score from zero; at least two are required.
- **Entries** (`state`, instructions, descriptions) are text, JSON objects, JSON arrays, or `null`. Use
  `Entry.Null` to send an explicit `null`; an omitted entry is left out of the request.
- **Names** of questions are sent verbatim and in order; any string is allowed.
- Unrecognized answer types from newer servers become `UnknownAnswer`, with the full JSON in `Raw`.
- Extra top-level request fields can be forwarded with `request.AdditionalProperties`.

Mistakes the API would reject (no questions, a score question with fewer than two descriptions) throw
`TypeSafeException` before anything is sent.

## Configuration

Explicit options win over environment variables, which win over defaults. Blank environment values are ignored.

| Option | Environment variable | Default |
|---|---|---|
| `ApiKey` | `TYPESAFE_API_KEY` | required |
| `BaseUri` | `TYPESAFE_BASE_URL` | `https://api.typesafe.ai` |
| `DefaultModel` | `TYPESAFE_DEFAULT_MODEL` | `jev-latest` |
| `LogLevel` | `TYPESAFE_LOG_LEVEL` (`debug`, `info`, `warn`, `error`, `off`) | `warn` |
| `Timeout` (per attempt) | | 10 seconds |
| `RetryPolicy` | | `RetryPolicy.Default` |
| `DefaultHeaders`, `Logger` / `LoggerFactory`, `TimeProvider` | | none |

```csharp
using var client = new TypeSafeClient(new TypeSafeClientOptions
{
    ApiKey = "…",
    DefaultModel = "jev-preview",
    Timeout = TimeSpan.FromSeconds(30),
});
```

The API key is never exposed through properties, `ToString()`, exceptions, or logs.

## Retries, timeouts, and cancellation

Connection errors, timeouts, and HTTP 408, 429, and 5xx responses are retried up to twice with exponential backoff
(500 ms doubling to 5 s, with up to 25% jitter). `Retry-After` and `retry-after-ms` are honored up to 60 seconds.
Each attempt, including reading the whole response body, gets its own timeout; there is no total budget.

`RetryPolicy` is an immutable record; derive variations with `with`:

```csharp
var policy = RetryPolicy.Default with
{
    MaxRetries = 5,
    RetryableStatusCodes = RetryPolicy.Default.RetryableStatusCodes.Add(409),
};

using var client = new TypeSafeClient(new TypeSafeClientOptions { RetryPolicy = policy });

// Per call: a different policy, timeout, and extra headers.
var options = new RequestOptions { RetryPolicy = RetryPolicy.None, Timeout = TimeSpan.FromSeconds(5) };
options.Headers["X-Correlation-Id"] = correlationId;
var result = await client.SystemOneAsync(request, options, cancellationToken);
```

Pass a `CancellationToken` to any call. Cancelling aborts the request and any pending retry and throws
`ApiUserAbortException`, which is never retried.

## Errors

Everything the SDK throws derives from `TypeSafeException`.

| Exception | When |
|---|---|
| `BadRequestException` (400), `AuthenticationException` (401), `PermissionDeniedException` (403), `NotFoundException` (404), `UnprocessableEntityException` (422), `RateLimitException` (429, with `RetryAfter`), `InternalServerException` (5xx) | The server answered with an error, after any retries. All derive from `ApiException`. |
| `ApiConnectionException` | The request failed or the body could not be read, after any retries. |
| `ApiTimeoutException` | An attempt timed out (a kind of `ApiConnectionException`). |
| `ApiUserAbortException` | You cancelled the request. |
| `TypeSafeException` | Invalid configuration or request, or an unexpected response shape. |

```csharp
try
{
    var result = await client.SystemOneAsync(request);
}
catch (ApiException e)
{
    Console.WriteLine($"{e.StatusCode} (request {e.RequestId}): {e.Message}"); // e.g. "400 Unknown model: x"
}
```

`AuthenticationException` shares its name with `System.Security.Authentication.AuthenticationException`; use a
`using` alias if you import both namespaces.

## Raw responses and request IDs

Every call has a `…WithResponseAsync` variant returning an `ApiResponse<T>` with the parsed value, the buffered
`RawResponse` (status, headers, body), and the request ID, which is useful when contacting support:

```csharp
ApiResponse<IReadOnlyList<ModelCard>> response = await client.Models.ListWithResponseAsync();
Console.WriteLine(response.RequestId);
Console.WriteLine(response.RawResponse.ReadContentAsString());
```

## Logging

The SDK logs through `Microsoft.Extensions.Logging`. Pass an `ILogger` (or `ILoggerFactory`) in the options; without
one, a minimal console logger is used. At `info` you get one line per attempt, retry, timeout, and cancellation; at
`debug` you also get headers (credentials redacted) and request and response bodies. **Bodies are logged in full at
`debug`**, so avoid it where they contain sensitive data.

## Testing your code

Depend on `ITypeSafeClient` and mock it, or build realistic results with `TypeSafe.AI.Testing.TypeSafeModelFactory`:

```csharp
using TypeSafe.AI.Testing;

SystemOneResult result = TypeSafeModelFactory.SystemOneResult(
    "jev-latest",
    [new("isBilling", TypeSafeModelFactory.NoulAnswer(0.97))]);
```

To test the SDK's HTTP behavior itself, pass your own `HttpClient` (for example one with a stub
`HttpMessageHandler`) and a `TimeProvider` (for example `FakeTimeProvider`):

```csharp
using var client = new TypeSafeClient(new TypeSafeClientOptions { ApiKey = "test", TimeProvider = fakeTime }, httpClient);
```

## Dependency injection

The SDK has no DI package; register it yourself. The client is thread-safe, so a singleton is right:

```csharp
services.AddSingleton<ITypeSafeClient>(sp => new TypeSafeClient(
    new TypeSafeClientOptions { LoggerFactory = sp.GetRequiredService<ILoggerFactory>() }));
```

When you pass your own `HttpClient`, you own it; keep its `Timeout` at least as long as the per-attempt timeout.

## Versioning and upstream

Versions follow [SemVer](https://semver.org/). Each release notes the upstream JavaScript SDK version it tracks
(`SdkInfo.UpstreamVersion`); see the [CHANGELOG](CHANGELOG.md). Requests identify themselves with
`User-Agent: typesafe-sdk-dotnet/<version>`.

## License

[MIT](LICENSE). See [NOTICE.md](NOTICE.md) for upstream attribution.
