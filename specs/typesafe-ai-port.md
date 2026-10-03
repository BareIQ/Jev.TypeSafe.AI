# Spec: Jev.TypeSafe.Unofficial — .NET SDK for TypeSafe AI (Jev)

| Field | Value |
|---|---|
| Status | v2. Phases 1–4 implemented and verified (Phase 4: package metadata, SourceLink, package validation, CI and publish workflows). Decisions in §17. |
| Upstream reference | `D:\work\typesafe-sdk-js` (`@typesafe-ai/sdk` **v0.6.0**, MIT, © TypeSafe) |
| Package ID / assembly | `Jev.TypeSafe.Unofficial` |
| Root namespace | `TypeSafe.AI` |
| Target framework | `netstandard2.0` (only) |
| Phase 1 scope | The library itself (functional equivalent of upstream `src/`). Tests, examples, docs, and CI follow. |

---

## 1. Purpose and guiding stance

Build an **unofficial .NET SDK** for the TypeSafe AI API (models branded **Jev**, e.g. `jev-latest`) and publish it to NuGet.

The JavaScript SDK is the **functional reference, not a structural template**. This SDK covers the same features and wire behavior. Its structure, naming, abstractions, and error model are designed **from .NET standards first**:
- the [.NET Framework Design Guidelines](https://learn.microsoft.com/dotnet/standard/design-guidelines/),
- the [Azure SDK .NET guidelines](https://azure.github.io/azure-sdk/dotnet_introduction.html) (an established reference for HTTP client libraries),
- `System.Net.Http`, `System.Text.Json`, `Microsoft.Extensions.Logging`, and `TimeProvider` conventions.

Where upstream behavior is **observable on the wire or by the caller** (payloads, headers, retry and timeout semantics, error messages, configuration precedence), it is preserved. Everything internal is designed for clarity, testability, and maintainability.

### Goals
1. **Functional parity** with upstream for every capability in §6.
2. **Idiomatic .NET API**: `Task`-returning `…Async` methods, a `CancellationToken` on every async method, `TimeSpan`s, exceptions, immutable result models, and PascalCase.
3. **Highest code quality**: SOLID, small single-purpose types, immutability, no hidden global state, and all analyzers on with warnings as errors (§4).
4. **Testable by design**: every source of non-determinism (HTTP, time, randomness, environment, logging) sits behind an injectable seam (§5.3). Consumers can mock the client (§7.10).
5. **Maintainable**: a clear layering (§5), one responsibility per type, internal by default, public API tracked by analyzers, and an XML-documented public surface.
6. **Broad reach**: one `netstandard2.0` build (.NET Framework 4.6.2+, .NET Core 2.0+, .NET 5–10+, Mono, Unity, Xamarin/MAUI).

### Non-goals
- DI integration in the core package. It lives in the separate `Jev.TypeSafe.Unofficial.Extensions` package (decision 16); the core client stays DI-friendly with plain constructor injection and an interface.
- Extra target frameworks (`net8.0` etc.) and trimming/AOT annotations.
- Streaming, or endpoints beyond upstream (`GET /v1/models`, `POST /v1/systemone`).
- A response-size limit (upstream has none).

---

## 2. Phasing

| Phase | Scope | Exit criteria |
|---|---|---|
| **1 – Library (priority)** | All functionality in §6, built to §4–§11 | Zero-warning Release build. A local smoke run against the live API passes. |
| 2 – Tests | Unit and integration test suites (§13) | **Done.** 549 tests (543 offline + 6 live) green on .NET 10 and .NET Framework 4.8; 97.2% line and 91.8% branch coverage. |
| 3 – Examples and docs | `examples/Demo`, README, CHANGELOG, NOTICE (§14) | **Done.** Demo and the packed nupkg verified against the live API on .NET 10 and .NET Framework 4.8; README snippets compile. |
| 4 – Packaging and CI | NuGet metadata, SourceLink, CI, and a publish workflow (§15) | `0.1.0-preview.1` published. |

Phase 1 is designed so that Phases 2–4 require **no public API changes**.

---

## 3. Target framework and dependencies

### 3.1 `netstandard2.0` only
`netstandard2.0` gives maximum reach, including .NET Framework. `netstandard2.1` was rejected because it drops .NET Framework, older Unity, and older Xamarin. The APIs this SDK misses on 2.0 (`ReadAsByteArrayAsync(CancellationToken)`, `Random.Shared`, `TimeProvider`, `SocketsHttpHandler`) are absent from 2.1 too, so it would buy nothing.

### 3.2 Runtime dependencies (keep minimal)
| Package | Purpose |
|---|---|
| `System.Text.Json` (10.0.x LTS line) | Serialization, `JsonNode`/`JsonElement`, `Utf8JsonWriter` |
| `Microsoft.Extensions.Logging.Abstractions` (10.0.x) | `ILogger`, `ILoggerFactory`, `NullLogger`, `LoggerMessage` |
| `Microsoft.Bcl.TimeProvider` (10.0.x) | `TimeProvider` for timers, delays, and the clock |
| `System.Collections.Immutable` (10.0.x) | Immutable result collections and status sets |

Build-only (`PrivateAssets="all"`): `PolySharp` (language-feature polyfills such as `init`, `required`, nullable attributes, and `IsExternalInit`), `Microsoft.CodeAnalysis.PublicApiAnalyzers`, and `Microsoft.VisualStudio.Threading.Analyzers`.

Versions are pinned centrally in `Directory.Packages.props` (Central Package Management).

### 3.3 netstandard2.0 API gaps and the chosen approach
| Gap | Approach |
|---|---|
| `ReadAsByteArrayAsync(CancellationToken)` | `ReadAsStreamAsync()` → `CopyToAsync(buffer, 81920, ct)`, plus `ct.Register` to dispose the response so that a stalled body is aborted |
| `Random.Shared` | The `IRandomSource` seam (§5.3), with a thread-safe default implementation |
| `OperatingSystem.IsBrowser()` | `RuntimeInformation.IsOSPlatform(OSPlatform.Create("BROWSER"))` |
| `ArgumentNullException.ThrowIfNull` | An internal `Guard` helper with `[CallerArgumentExpression]` (polyfilled) |
| Generic `Enum` APIs | A cached non-generic `Enum.GetValues(typeof(T))` map |
| `string.Contains(string, StringComparison)` | `IndexOf(…, StringComparison.Ordinal…) >= 0` |
| `Task.Delay` with `TimeProvider` | `TimeProviderTaskExtensions.Delay` (`Microsoft.Bcl.TimeProvider`) |

---

## 4. Engineering standards (non-negotiable)

### 4.1 Design principles
- **Single Responsibility:** each internal type does one thing: build a request, run one attempt, decide on retries, compute delays, parse a body, map an error, redact headers, and so on (§5.2).
- **Open/Closed:** behaviors vary through configuration objects (`RetryPolicy`) and seams, not flags scattered through code. New answer and question kinds are added by new sealed types plus converter registration.
- **Liskov:** exception and answer hierarchies are shallow and substitutable. No subclass narrows its base's contract.
- **Interface Segregation:** consumer-facing interfaces are small (`ITypeSafeClient`, `IModelsClient`). Internal seams are narrow (`IRandomSource`, `IEnvironmentReader`, `IApiTransport`).
- **Dependency Inversion:** high-level types (`TypeSafeClient`, `ModelsClient`) depend on internal abstractions (`IApiTransport`), not on `HttpClient` directly.
- **Avoid over-abstraction:** an interface exists only where there is a real second implementation (production + test double) or a consumer-mocking need.
- **Pure core, imperative shell:** retry math, header merging, redaction, Retry-After parsing, error-message extraction, and validation are **pure static functions** with no I/O. They are trivially unit-testable.

### 4.2 Code conventions
- Every type is **`sealed` by default** and **`internal` by default**. A type is `public` only if it is in §7.
- **Immutability:** public result models are immutable (get-only, or `init`-only on internal constructors). Options objects are mutable POCOs that are **snapshotted and validated once** when the client is constructed. The client holds only immutable state.
- **No static mutable state.** The only static members are constants, pure functions, and lazily computed process-wide facts (the runtime description).
- **Nullable reference types enabled.** Public APIs have accurate nullability annotations. `!` (null-forgiving) is banned except with a justification comment.
- **Argument validation:** public methods validate arguments eagerly (`ArgumentNullException`, `ArgumentException`, and `ArgumentOutOfRangeException` for programmer errors). For async methods, validation is thrown **synchronously** via the "validate, then call the private `async` core" pattern. Domain-level rejections that upstream raises as SDK errors (empty questions, too few score criteria, bad configuration) throw `TypeSafeException`, preserving upstream semantics and messages.
- **Async:** `ConfigureAwait(false)` everywhere. No `async void`, no sync-over-async, `ValueTask` only where justified. Every async API takes `CancellationToken cancellationToken = default` as its last parameter.
- **Disposal:** deterministic disposal of `HttpRequestMessage`, `HttpResponseMessage`, `CancellationTokenSource`, and token registrations (`using` declarations).
- **Logging:** only via `LoggerMessage.Define`-generated delegates in a single `Log` class (allocation-free when disabled, with stable event IDs).
- **Strings:** culture-invariant formatting and parsing (`CultureInfo.InvariantCulture`) and ordinal comparisons. Header names are compared with `StringComparer.OrdinalIgnoreCase`.
- **No magic values:** header names, paths, defaults, and env var names live in dedicated `static class` constant holders.
- **Method size:** aim for ≤ 30 lines, with cyclomatic complexity kept low. Extract named helpers instead of writing comments that explain blocks.
- **Comments:** XML docs on every public member (`<summary>`, `<param>`, `<returns>`, `<exception>`). Inline comments only explain *why*.

### 4.3 Analyzers and build gates
```xml
<Nullable>enable</Nullable>
<LangVersion>latest</LangVersion>
<TreatWarningsAsErrors>true</TreatWarningsAsErrors>
<AnalysisLevel>latest-all</AnalysisLevel>            <!-- CA rules, with a curated .editorconfig for opt-outs -->
<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
<GenerateDocumentationFile>true</GenerateDocumentationFile>
<Deterministic>true</Deterministic>
<ImplicitUsings>disable</ImplicitUsings>
```
- `.editorconfig` defines naming, `var` usage, file-scoped namespaces, expression-bodied members, and severity overrides. Any rule disabled there must carry a comment explaining why.
- `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt` (PublicApiAnalyzers), so that any public surface change is explicit in review.
- `Microsoft.VisualStudio.Threading.Analyzers` catches async misuse.
- `InternalsVisibleTo` exposes internals to the test project only.

---

## 5. Architecture

### 5.1 Layers
```
┌───────────────────────────────────────────────────────────────────────┐
│ Public API                                                            │
│  TypeSafeClient : ITypeSafeClient   ModelsClient : IModelsClient      │
│  TypeSafeClientOptions  RequestOptions  RetryPolicy                   │
│  Models (Entry, Question*, Answer*, SystemOneRequest/Result, …)       │
│  Exceptions                                                           │
├───────────────────────────────────────────────────────────────────────┤
│ Application (internal)                                                │
│  ClientConfiguration (resolved, validated, immutable snapshot)        │
│  ClientConfigurationResolver (options + env + defaults → config)      │
│  QuestionValidator                                                    │
├───────────────────────────────────────────────────────────────────────┤
│ Transport (internal)                                                  │
│  IApiTransport ← HttpApiTransport                                     │
│     ├─ RequestFactory      (URL, headers, body → HttpRequestMessage)  │
│     ├─ RetryExecutor       (loop, decision, back-off wait)            │
│     ├─ AttemptExecutor     (send + buffer under timeout; classify)    │
│     ├─ ResponseBodyParser  (lenient JSON-or-text)                     │
│     └─ ApiExceptionFactory (status → exception, message extraction)   │
├───────────────────────────────────────────────────────────────────────┤
│ Core utilities (internal, pure)                                       │
│  RetryMath  RetryAfterParser  HeaderMerger  HeaderRedactor            │
│  RuntimeDescriptor  ErrorMessageExtractor  Guard                      │
├───────────────────────────────────────────────────────────────────────┤
│ Serialization (internal)                                              │
│  Hand-written Utf8JsonWriter writers + JsonElement readers            │
├───────────────────────────────────────────────────────────────────────┤
│ Seams (internal interfaces; defaults in production)                   │
│  HttpMessageHandler/HttpClient  TimeProvider  IRandomSource           │
│  IEnvironmentReader  ILogger                                          │
└───────────────────────────────────────────────────────────────────────┘
```
Dependencies point downward only. Public models know nothing about transport, and transport knows nothing about questions or answers (it moves bytes and `JsonElement`s).

### 5.2 Component responsibilities
| Component | Responsibility | Collaborators |
|---|---|---|
| `TypeSafeClient` | Public facade. Owns configuration and the transport. Exposes `SystemOneAsync` and `Models`. Validates arguments and owns the `HttpClient` lifetime. | `ClientConfigurationResolver`, `IApiTransport`, `QuestionValidator`, serializer |
| `ModelsClient` (internal; exposed as `IModelsClient`) | `GET /v1/models` and unwrapping | `IApiTransport` |
| `ClientConfigurationResolver` | Merges options → env → defaults, validates, and returns an immutable `ClientConfiguration` | `IEnvironmentReader`, `OptionsValidator` |
| `OptionsValidator` | Pure validation of timeouts, retry fields, status codes, base URL, and log level. Produces the upstream messages. | — |
| `RequestOptionsResolver` | Merges client config with per-call `RequestOptions` into an immutable `ResolvedRequest` | `OptionsValidator` |
| `QuestionValidator` | Pure validation of a `SystemOneRequest` (§8.6) | — |
| `HttpApiTransport : IApiTransport` | Orchestrates one logical call: tagging, retries, parse, and error mapping. Returns `TransportResponse` (status, headers, body bytes, parsed `JsonElement?`/text). | `RetryExecutor`, `RequestFactory`, `ResponseBodyParser`, `ApiExceptionFactory`, `Log` |
| `RequestFactory` | Builds a fresh `HttpRequestMessage` per attempt: URL, merged and protected headers, retry-count header, and content | `HeaderMerger`, `SdkHeaders` |
| `AttemptExecutor` | Sends one request, buffers the full body under a per-attempt timeout linked to the caller token, and classifies failures into abort, timeout, or connection | `HttpClient`, `TimeProvider` |
| `RetryExecutor` | Runs attempts, decides whether to retry, computes the delay, and waits (cancellable) | `AttemptExecutor`, `RetryMath`, `TimeProvider`, `IRandomSource` |
| `RetryMath` / `RetryAfterParser` | Pure functions (§8.4) | — |
| `ResponseBodyParser` | Lenient body parsing (§8.5) | — |
| `ApiExceptionFactory` / `ErrorMessageExtractor` | Status → exception type, and message extraction (§8.7) | `RetryAfterParser` |
| `HeaderMerger` / `HeaderRedactor` | Case-insensitive merge, and credential masking (§8.2, §8.8) | — |
| `RuntimeDescriptor` | Computes the `X-TypeSafe-Runtime` value once (`Lazy<string>`) | — |
| `LevelFilteredLogger` | Wraps the user's `ILogger` and enforces the SDK level | — |
| `ConsoleLogger` | Minimal fallback sink with the `[typesafe-sdk]` prefix | `TextWriter` (injectable) |
| `Log` | `LoggerMessage.Define` delegates with stable `EventId`s | — |

### 5.3 Testability seams
| Seam | Production default | Test double | Injected via |
|---|---|---|---|
| HTTP | SDK-owned `HttpClient(new HttpClientHandler())` | Stub `HttpMessageHandler` | Public ctor `TypeSafeClient(options, HttpClient)` |
| Time (delays, timeouts, elapsed) | `TimeProvider.System` | `FakeTimeProvider` | `TypeSafeClientOptions.TimeProvider` |
| Randomness (jitter) | `ThreadSafeRandomSource` | Fixed sequence | Internal ctor (via `InternalsVisibleTo`) |
| Environment variables | `ProcessEnvironmentReader` | In-memory dictionary | Internal ctor (via `InternalsVisibleTo`) |
| Logging | `NullLogger` / console fallback | In-memory `ILogger` | `TypeSafeClientOptions.Logger` / `LoggerFactory` |
| Console output | `Console.Out` / `Console.Error` | `StringWriter` | Internal `ConsoleLogger` ctor |
| Transport (for resource tests) | `HttpApiTransport` | Fake `IApiTransport` | Internal ctor |

Tests **never** mutate process-wide state (environment variables, `Console`). This makes the tests parallel-safe.

### 5.4 Source layout
```
Jev.TypeSafe.Unofficial.slnx
Directory.Build.props / Directory.Build.targets / Directory.Packages.props
global.json  .editorconfig  LICENSE  NOTICE.md  README.md  CHANGELOG.md
specs/typesafe-ai-port.md
src/Jev.TypeSafe.Unofficial/
  Jev.TypeSafe.Unofficial.csproj
  PublicAPI.Shipped.txt  PublicAPI.Unshipped.txt
  TypeSafeClient.cs  ITypeSafeClient.cs  TypeSafeClientOptions.cs  RequestOptions.cs
  RetryPolicy.cs  TypeSafeLogLevel.cs  TypeSafeEnvironmentVariables.cs  SdkInfo.cs
  Models/       ModelsClient.cs  IModelsClient.cs  ModelCard.cs
  SystemOne/    SystemOneRequest.cs  SystemOneResult.cs  Usage.cs  Entry.cs  EntryKind.cs
  Questions/    Question.cs  NoulQuestion.cs  ChoiceQuestion.cs  ChoiceQuestion{TEnum}.cs  ScoreQuestion.cs
                NoulCriteria.cs  ChoiceCriteria.cs  ScoreCriteria.cs  QuestionSet.cs  QuestionKey.cs
  Answers/      Answer.cs  NoulAnswer.cs  ChoiceAnswer.cs  ChoiceAnswer{TEnum}.cs  ScoreAnswer.cs
                UnknownAnswer.cs  AnswerSet.cs
  Responses/    ApiResponse.cs  RawResponse.cs
  Exceptions/   TypeSafeException.cs  ApiException.cs  BadRequestException.cs  AuthenticationException.cs
                PermissionDeniedException.cs  NotFoundException.cs  UnprocessableEntityException.cs
                RateLimitException.cs  InternalServerException.cs  ApiConnectionException.cs
                ApiTimeoutException.cs  ApiUserAbortException.cs
  Testing/      TypeSafeModelFactory.cs        (public: construct results for consumer tests)
  Internal/
    Configuration/  ClientConfiguration.cs  ClientConfigurationResolver.cs  OptionsValidator.cs
                    RequestOptionsResolver.cs  ResolvedRequest.cs  IEnvironmentReader.cs  ProcessEnvironmentReader.cs
    Transport/      IApiTransport.cs  HttpApiTransport.cs  RequestFactory.cs  AttemptExecutor.cs
                    RetryExecutor.cs  TransportResponse.cs  AttemptFailure.cs  ResponseBodyParser.cs
    Retry/          RetryMath.cs  RetryAfterParser.cs  IRandomSource.cs  ThreadSafeRandomSource.cs
    Errors/         ApiExceptionFactory.cs  ErrorMessageExtractor.cs
    Http/           HeaderMerger.cs  HeaderRedactor.cs  SdkHeaders.cs  ApiPaths.cs  RuntimeDescriptor.cs
    Logging/        Log.cs  LevelFilteredLogger.cs  ConsoleLogger.cs  LogLevelParser.cs
    Validation/     QuestionValidator.cs  Guard.cs
    Serialization/  JsonText.cs  JsonShapeReader.cs  SystemOneRequestWriter.cs  SystemOneResultReader.cs  ModelsReader.cs
                    SystemOneRequestWriter.cs  AnswerConverter.cs  SystemOneResultReader.cs  ModelsEnvelope.cs
tests/Jev.TypeSafe.Unofficial.Tests/              (Phase 2; live tests live in its Live/ folder)
examples/Demo/                                     (Phase 3)
```
Namespaces follow folders, except that public types under `Models/`, `SystemOne/`, `Questions/`, `Answers/`, `Responses/`, and `Exceptions/` all live in the root namespace `TypeSafe.AI`. This means consumers need a single `using`. Internal types use `TypeSafe.AI.Internal.*`. The `Testing/` type lives in `TypeSafe.AI.Testing`. The project sets `<RootNamespace>TypeSafe.AI</RootNamespace>`, while the assembly and package stay `Jev.TypeSafe.Unofficial`.

---

## 6. Functional coverage (upstream → .NET)

This is a **capability checklist**, not a file mirror. Every row must be implemented in Phase 1.

| # | Capability (upstream) | .NET realization |
|---|---|---|
| F1 | Client construction with options → env → defaults (`client.ts`, `env.ts`) | `TypeSafeClient` ctor + `ClientConfigurationResolver` (§8.1) |
| F2 | Missing API key error naming the env var | `TypeSafeException` (§8.1) |
| F3 | Base URL default and trailing-slash stripping | §8.1 |
| F4 | Default model `jev-latest`, with per-request override | §8.1, §7.5 |
| F5 | Log level from option/env, validated | `TypeSafeLogLevel`, `LogLevelParser` (§8.8) |
| F6 | Logger injection, level filtering, and console default | `ILogger`/`ILoggerFactory`, `LevelFilteredLogger`, `ConsoleLogger` |
| F7 | Header redaction in logs | `HeaderRedactor` (§8.8) |
| F8 | Retry policy defaults, overrides, validation, and isolation | `RetryPolicy` (§7.9, §8.4) |
| F9 | Exponential back-off with jitter, and Retry-After / retry-after-ms | `RetryMath`, `RetryAfterParser` |
| F10 | Per-attempt timeout; abort vs timeout vs connection classification | `AttemptExecutor` (§8.3) |
| F11 | Cancellation, including during back-off | `CancellationToken` → `ApiUserAbortException` |
| F12 | Full-body buffering under the timeout, and stalled-body protection | `AttemptExecutor` |
| F13 | Protected SDK headers, user header merging, and retry-count header | `RequestFactory`, `HeaderMerger` (§8.2) |
| F14 | Runtime identification header | `RuntimeDescriptor` |
| F15 | Browser guard + `dangerouslyAllowBrowser` | `DangerouslyAllowBrowser` option |
| F16 | Lenient JSON-or-text body parsing | `ResponseBodyParser` (§8.5) |
| F17 | Error hierarchy by status, message extraction, request ID, and `RateLimit` retry-after | Exceptions + `ApiExceptionFactory` (§8.7) |
| F18 | Question builders `noul` / `choice` / `score` | `Question.Noul/Choice/Score` (§7.3) |
| F19 | Question validation before sending | `QuestionValidator` (§8.6) |
| F20 | `systemOne` with typed answers | `SystemOneAsync`, `QuestionKey<T>`, `AnswerSet` (§7.4–7.6) |
| F21 | Extra request fields forwarded verbatim | `SystemOneRequest.AdditionalProperties` |
| F22 | `models.list()` with shape validation | `IModelsClient.ListAsync` (§7.7) |
| F23 | Raw response and request-ID access (`withResponse` / `asResponse`) | `…WithResponseAsync` → `ApiResponse<T>` + `RawResponse` (§7.8) |
| F24 | `map()` on a call | Not needed. Standard `Task` composition covers it, and the `ListAsync` unwrap is internal. |
| F25 | API key never exposed or serialized | Private field, `ToString`/`DebuggerDisplay` redaction, and logs (§8.1, §8.8) |
| F26 | `VERSION` constant | `SdkInfo.Version` |
| F27 | Exported env var names (`ENV`) | `TypeSafeEnvironmentVariables` constants |

---

## 7. Public API

> Signatures are normative. Any change must update this section and `PublicAPI.Unshipped.txt` together.

### 7.1 `Entry`: text, JSON object, JSON array, or null
The wire allows `string | object | array | null`, and the SDK must distinguish **omitted** from **explicit null**.
```csharp
public readonly struct Entry : IEquatable<Entry>
{
    public static Entry Omitted { get; }          // == default
    public static Entry Null { get; }
    public EntryKind Kind { get; }                // Omitted, Null, Text, Object, Array
    public bool IsOmitted { get; }

    public static Entry FromText(string? text);                         // null → Null
    public static Entry FromJson(JsonNode? node);                       // object/array/string/null; numbers & booleans → ArgumentException
    public static Entry FromJson(JsonElement element);
    public static Entry FromObject<T>(T value, JsonTypeInfo<T> typeInfo);
    public static Entry FromObject<T>(T value);                                       // reflection-based; not trim-safe
    public static Entry FromObject<T>(T value, JsonSerializerOptions? options);       // reflection-based; not trim-safe

    public string? AsText();
    public JsonNode? ToJsonNode();                // returns a fresh copy
    public string ToJsonString();
    public override string ToString();            // == ToJsonString()

    public static implicit operator Entry(string? text);
    public static implicit operator Entry(JsonObject? value);
    public static implicit operator Entry(JsonArray? value);
    // Equals/GetHashCode/==/!= — structural JSON equality
}
public enum EntryKind { Omitted = 0, Null, Text, Object, Array }
```
- The value is stored internally as an immutable `JsonElement` snapshot. It is thread-safe, and callers can never mutate it after construction.

### 7.2 Question model
```csharp
public abstract class Question                      // closed hierarchy: internal constructor
{
    private protected Question(Entry instructions);
    public abstract QuestionType Type { get; }
    public Entry Instructions { get; }

    public static NoulQuestion Noul(Entry instructions = default, NoulCriteria? criteria = null);
    public static ChoiceQuestion Choice(Entry instructions, ChoiceCriteria criteria);
    public static ChoiceQuestion<TEnum> Choice<TEnum>(Entry instructions) where TEnum : struct, Enum;
    public static ChoiceQuestion<TEnum> Choice<TEnum>(Entry instructions, IReadOnlyDictionary<TEnum, Entry> descriptions) where TEnum : struct, Enum;
    public static ScoreQuestion Score(Entry instructions, ScoreCriteria criteria);
    public static ScoreQuestion Score(Entry instructions, params Entry[] criteria);
}
public enum QuestionType { Noul, Choice, Score }     // wire: "noul" | "choice" | "score"

public sealed class NoulQuestion   : Question { public NoulCriteria? Criteria { get; } }
public class        ChoiceQuestion : Question { public ChoiceCriteria Criteria { get; } }
public sealed class ChoiceQuestion<TEnum> : ChoiceQuestion where TEnum : struct, Enum { }
public sealed class ScoreQuestion  : Question { public ScoreCriteria Criteria { get; } }

public sealed class NoulCriteria
{
    public Entry True { get; init; }                 // Omitted → key not sent
    public Entry False { get; init; }
}

public sealed class ChoiceCriteria : IReadOnlyList<KeyValuePair<string, Entry>>, IEnumerable
{
    public ChoiceCriteria();
    public void Add(string label, Entry description); // collection initializer; duplicate → ArgumentException
    public static ChoiceCriteria FromLabels(params string[] labels);   // each label → Null description
    public bool TryGetDescription(string label, out Entry description);
}

public sealed class ScoreCriteria : IReadOnlyList<Entry>
{
    public ScoreCriteria(IEnumerable<Entry> descriptions);
    public static implicit operator ScoreCriteria(Entry[] descriptions);
    public static implicit operator ScoreCriteria(string[] descriptions);
}
```
- Questions are **immutable after construction** (criteria are copied in), which preserves the "caller objects never mutated" invariant structurally.
- `Noul()` defaults instructions to `Entry.Null` (sent as `null`) and omits criteria (upstream parity).
- `Choice<TEnum>`: labels come from enum member names, overridable per member with `[EnumMember(Value = "…")]`. Descriptions are optional per member (absent → `null`). Duplicate labels after mapping throw `ArgumentException`. `[Flags]` enums are rejected.

### 7.3 Question sets and typed keys
```csharp
public sealed class QuestionSet : IReadOnlyCollection<KeyValuePair<string, Question>>
{
    public QuestionKey<NoulAnswer>          Add(string name, NoulQuestion question);
    public QuestionKey<ChoiceAnswer>        Add(string name, ChoiceQuestion question);
    public QuestionKey<ChoiceAnswer<TEnum>> Add<TEnum>(string name, ChoiceQuestion<TEnum> question) where TEnum : struct, Enum;
    public QuestionKey<ScoreAnswer>         Add(string name, ScoreQuestion question);
    public bool Contains(string name);
    public bool TryGetQuestion(string name, [NotNullWhen(true)] out Question? question);
}

public readonly struct QuestionKey<TAnswer> : IEquatable<QuestionKey<TAnswer>> where TAnswer : Answer
{
    public string Name { get; }
}
```
- Names are kept **verbatim and in insertion order**. Any string is legal (`__proto__`, Unicode, whitespace). A duplicate name → `ArgumentException`.
- A `QuestionSet` can be reused across requests. It is not mutated by the SDK.

### 7.4 Request
```csharp
public sealed class SystemOneRequest
{
    public SystemOneRequest(Entry state, QuestionSet questions);
    public Entry State { get; }
    public QuestionSet Questions { get; }
    public string? Model { get; init; }                                   // null → client default model
    public IDictionary<string, JsonNode?> AdditionalProperties { get; }   // forwarded verbatim (snapshotted at send)
}
```

### 7.5 Result and answers
```csharp
public sealed class SystemOneResult
{
    public string Model { get; }
    public AnswerSet Answers { get; }
    public Usage Usage { get; }
}
public sealed class Usage { public long InputTokens { get; } public long OutputTokens { get; } }

public sealed class AnswerSet : IReadOnlyDictionary<string, Answer>
{
    public TAnswer Get<TAnswer>(QuestionKey<TAnswer> key) where TAnswer : Answer;
    public bool TryGet<TAnswer>(QuestionKey<TAnswer> key, [NotNullWhen(true)] out TAnswer? answer) where TAnswer : Answer;
    public NoulAnswer GetNoul(string name);
    public ChoiceAnswer GetChoice(string name);
    public ChoiceAnswer<TEnum> GetChoice<TEnum>(string name) where TEnum : struct, Enum;
    public ScoreAnswer GetScore(string name);
}

public abstract class Answer
{
    private protected Answer(JsonElement raw);
    public abstract string Type { get; }              // wire type; unknown types preserved
    public JsonElement Raw { get; }                   // full original JSON (forward compatibility)
}
public sealed class NoulAnswer : Answer { public double Noul { get; } }
public class ChoiceAnswer : Answer
{
    public string Choice { get; }
    public double Confidence { get; }
    public IReadOnlyDictionary<string, double> Probabilities { get; }
}
public sealed class ChoiceAnswer<TEnum> : ChoiceAnswer where TEnum : struct, Enum
{
    public TEnum Value { get; }                                       // mapped Choice
    public IReadOnlyDictionary<TEnum, double> ProbabilitiesByValue { get; }
}
public sealed class ScoreAnswer : Answer
{
    public double Score { get; }
    public double Confidence { get; }
    public IReadOnlyDictionary<int, Entry> Legend { get; }
    public IReadOnlyDictionary<int, double> Probabilities { get; }
}
public sealed class UnknownAnswer : Answer { }
```
- A missing name → `KeyNotFoundException`. A kind mismatch → `InvalidOperationException` naming the question, the expected type, and the actual type.
- Typed enum answers are materialized lazily on first access. An unmappable label → `TypeSafeException`.
- Avoiding member/type name clashes (CA rules): `ChoiceAnswer<TEnum>.Value` rather than shadowing `Choice` with `new`.

### 7.6 Client interface and implementation
```csharp
public interface ITypeSafeClient
{
    IModelsClient Models { get; }
    Task<SystemOneResult> SystemOneAsync(SystemOneRequest request, RequestOptions? options = null, CancellationToken cancellationToken = default);
    Task<ApiResponse<SystemOneResult>> SystemOneWithResponseAsync(SystemOneRequest request, RequestOptions? options = null, CancellationToken cancellationToken = default);
}

public sealed class TypeSafeClient : ITypeSafeClient, IDisposable
{
    public TypeSafeClient();                                              // env + defaults
    public TypeSafeClient(TypeSafeClientOptions options);
    public TypeSafeClient(TypeSafeClientOptions options, HttpClient httpClient);   // caller owns httpClient

    public Uri BaseUri { get; }
    public string DefaultModel { get; }
    public TypeSafeLogLevel LogLevel { get; }
    public RetryPolicy RetryPolicy { get; }
    public TimeSpan Timeout { get; }
    public IReadOnlyDictionary<string, string> DefaultHeaders { get; }
    public IModelsClient Models { get; }

    public Task<SystemOneResult> SystemOneAsync(...);
    public Task<ApiResponse<SystemOneResult>> SystemOneWithResponseAsync(...);
    public void Dispose();                                                 // disposes only an SDK-owned HttpClient; idempotent
    public override string ToString();                                     // base URI + model; never the key
}
```
- **Why `Task<T>` plus a `…WithResponseAsync` overload, rather than a custom awaitable:** standard `Task` composes with `Task.WhenAll`, Polly, and mocking frameworks, and matches the Framework Design Guidelines. Because the body is fully buffered, a separate "raw only" method adds nothing. `ApiResponse<T>.Response` provides it.
- Methods throw `ObjectDisposedException` after `Dispose()`.
- **Thread-safe**, and intended to be long-lived (one per app or API key).

### 7.7 Models
```csharp
public interface IModelsClient
{
    Task<IReadOnlyList<ModelCard>> ListAsync(RequestOptions? options = null, CancellationToken cancellationToken = default);
    Task<ApiResponse<IReadOnlyList<ModelCard>>> ListWithResponseAsync(RequestOptions? options = null, CancellationToken cancellationToken = default);
}
public sealed class ModelCard
{
    public string Name { get; }
    public string Description { get; }
    public string ReleaseDate { get; }                                 // kept as string (no format guarantee)
    public IReadOnlyDictionary<string, JsonElement> AdditionalProperties { get; }
}
```

### 7.8 Responses
```csharp
public sealed class ApiResponse<T>
{
    public T Value { get; }
    public RawResponse RawResponse { get; }
    public string? RequestId { get; }                                  // shortcut for RawResponse.RequestId
}
public sealed class RawResponse
{
    public int StatusCode { get; }
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Headers { get; } // case-insensitive; response + content headers
    public ReadOnlyMemory<byte> Content { get; }                        // fully buffered
    public string? RequestId { get; }                                   // x-typesafe-request-id
    public bool TryGetHeader(string name, [NotNullWhen(true)] out string? value);
    public string ReadContentAsString();                                // charset from Content-Type, else UTF-8
    public JsonDocument ParseContentAsJson();                           // caller disposes
}
```

### 7.9 Options and policy
```csharp
public sealed class TypeSafeClientOptions
{
    public string? ApiKey { get; set; }
    public Uri? BaseUri { get; set; }                                 // absolute http(s); trailing "/" stripped
    public string? DefaultModel { get; set; }
    public TypeSafeLogLevel? LogLevel { get; set; }
    public ILogger? Logger { get; set; }
    public ILoggerFactory? LoggerFactory { get; set; }
    public RetryPolicy? RetryPolicy { get; set; }                     // null → RetryPolicy.Default
    public TimeSpan? Timeout { get; set; }                            // per attempt; null → 10 s
    public IDictionary<string, string> DefaultHeaders { get; }        // OrdinalIgnoreCase
    public bool DangerouslyAllowBrowser { get; set; }
    public TimeProvider? TimeProvider { get; set; }
    public override string ToString();                                // ApiKey redacted
}

public sealed class RequestOptions
{
    public TimeSpan? Timeout { get; set; }
    public RetryPolicy? RetryPolicy { get; set; }                     // replaces the client policy for this call
    public IDictionary<string, string> Headers { get; }              // OrdinalIgnoreCase
}

public sealed record RetryPolicy
{
    public static RetryPolicy Default { get; }
    public static RetryPolicy None { get; }                         // MaxRetries = 0
    public int MaxRetries { get; init; }                            // 2
    public TimeSpan InitialBackoff { get; init; }                   // 500 ms
    public TimeSpan MaxBackoff { get; init; }                       // 5 s
    public double BackoffJitter { get; init; }                      // 0.25
    public IImmutableSet<int> RetryableStatusCodes { get; init; }   // 408, 429, 500–599
    public bool RespectRetryAfter { get; init; }                    // true
    public TimeSpan MaxRetryAfter { get; init; }                    // 60 s
    public bool RetryOnConnectionError { get; init; }               // true
    public bool RetryOnTimeout { get; init; }                       // true
}

public enum TypeSafeLogLevel { Debug = 0, Info, Warn, Error, Off }

public static class TypeSafeEnvironmentVariables
{
    public const string ApiKey = "TYPESAFE_API_KEY";
    public const string BaseUrl = "TYPESAFE_BASE_URL";
    public const string DefaultModel = "TYPESAFE_DEFAULT_MODEL";
    public const string LogLevel = "TYPESAFE_LOG_LEVEL";
}

public static class SdkInfo
{
    public static string Version { get; }          // from AssemblyInformationalVersion (no +commit suffix)
    public const string UpstreamVersion = "0.6.0";
}
```
- **Partial overrides the .NET way:** upstream `Partial<RetryPolicy>` maps to a record with `with` expressions, e.g. `RetryPolicy = client.RetryPolicy with { MaxRetries = 0 }` or `RetryPolicy.Default with { RetryableStatusCodes = RetryPolicy.Default.RetryableStatusCodes.Add(409) }`. Per-field inheritance follows naturally from `with`. `IImmutableSet` gives isolation for free.
- `RetryPolicy` validates in its property `init` accessors (throwing `ArgumentOutOfRangeException` with the upstream message text), so an invalid policy cannot exist.

### 7.10 Consumer testability
- `ITypeSafeClient` / `IModelsClient` can be mocked with any framework.
- `TypeSafe.AI.Testing.TypeSafeModelFactory` provides static factory methods (`SystemOneResult(...)`, `NoulAnswer(...)`, `ChoiceAnswer(...)`, `ScoreAnswer(...)`, `ModelCard(...)`, `ApiResponse<T>(...)`, `RawResponse(...)`). Consumers can build realistic results without the SDK making public constructors (Azure SDK `ModelFactory` pattern).
- The `ApiException` family has public constructors for test construction.

---

## 8. Behavior specification (observable parity)

### 8.1 Configuration
Precedence: **explicit option → environment variable → default**. Resolution is performed once, in the constructor, by `ClientConfigurationResolver`.

| Setting | Option | Env var | Default |
|---|---|---|---|
| API key | `ApiKey` | `TYPESAFE_API_KEY` | **required** |
| Base URL | `BaseUri` | `TYPESAFE_BASE_URL` | `https://api.typesafe.ai` |
| Default model | `DefaultModel` | `TYPESAFE_DEFAULT_MODEL` | `jev-latest` |
| Log level | `LogLevel` | `TYPESAFE_LOG_LEVEL` | `Warn` |
| Timeout (per attempt) | `Timeout` | — | 10 s |
| Retry policy | `RetryPolicy` | — | `RetryPolicy.Default` |

- Env values are trimmed. Empty or whitespace-only values are **unset**.
- Missing key → `TypeSafeException("No API key was provided. Pass `ApiKey` to the TypeSafeClient constructor or set the TYPESAFE_API_KEY environment variable.")`.
- Base URL: trailing `/` characters are stripped from either source. It must be an absolute `http`/`https` URI, otherwise `TypeSafeException`.
- Env log level must be one of `debug|info|warn|error|off` (case-sensitive), otherwise `TypeSafeException("Invalid log level \"{v}\" from TYPESAFE_LOG_LEVEL. Expected one of: debug, info, warn, error, off.")`. An option value is an enum, so it is validated by `Enum.IsDefined`.
- Timeout: `> 0`, and `≤ int.MaxValue` ms; `Timeout.InfiniteTimeSpan` is rejected. Message: `` `timeout` must be a positive number of milliseconds, got {v}. ``
- Browser guard: if the platform is `BROWSER` and `DangerouslyAllowBrowser == false` → `TypeSafeException` with the upstream wording about exposing the key.
- A caller-supplied `HttpClient`'s `BaseAddress`/`Timeout` are **not** relied upon. The SDK builds absolute URIs and enforces its own timeouts. The docs advise `httpClient.Timeout ≥ per-attempt timeout`. An SDK-owned client uses `Timeout.InfiniteTimeSpan`.
- The API key lives only in `ClientConfiguration` (internal). It never appears in `ToString()`, `[DebuggerDisplay]`, exceptions, or logs.

### 8.2 Headers
Merge order (case-insensitive; **last wins**; the winning name keeps its casing): `DefaultHeaders` → `RequestOptions.Headers` → **protected SDK headers**.

| Header | Value |
|---|---|
| `Authorization` | `Bearer {apiKey}` |
| `Accept` | `application/json` |
| `User-Agent` | `typesafe-sdk-dotnet/{SdkInfo.Version}` |
| `X-TypeSafe-SDK` | `typesafe-sdk-dotnet/{SdkInfo.Version}` |
| `X-TypeSafe-Runtime` | `RuntimeDescriptor` value, e.g. `dotnet/8.0.11 (linux; x64)`, `dotnet-framework/4.8.9290.0 (windows; x64)`, `mono/6.12.0 (osx; arm64)`, `browser` |
| `Content-Type` | `application/json` **only when there is a body**. A user value is removed otherwise. |
| `X-TypeSafe-Retry-Count` | User values are always removed. Set to `n` on retry attempt `n ≥ 1`. |

- User values can never override protected headers on any attempt, in any casing.
- Headers are applied via `TryAddWithoutValidation`. `Content-Type` goes on `HttpContent.Headers`.

### 8.3 Request execution
1. **Synchronous phase** (in the public method, before returning the `Task`): null checks, `ObjectDisposedException`, `QuestionValidator`, and per-call option resolution. Serialize the body to UTF-8 bytes once.
2. **Tag:** `#{n} {METHOD} {path}`, where `n` is a per-client counter incremented with `Interlocked.Increment`.
3. **Attempt loop** (`RetryExecutor`), with `attempt = 0..MaxRetries`:
   - Debug log: tag, URL, redacted headers, and request body.
   - `AttemptExecutor`: a linked CTS of {caller token, per-attempt timeout via `TimeProvider.CreateCancellationTokenSource`}. Calls `SendAsync(…, ResponseHeadersRead, ct)`, then buffers the **whole body** under the same token. A registration disposes the response on cancel, so a handler or stream that ignores the token is still aborted.
   - Failure classification (in this order):
     1. Caller token cancelled → `ApiUserAbortException` (never retried). Info log: `aborted by caller after {ms}ms`.
     2. Timeout CTS fired → `ApiTimeoutException(timeout)`. Info log: `timed out after {ms}ms`.
     3. Otherwise (`HttpRequestException`, `IOException`, `SocketException`, `ObjectDisposedException` from an aborted stream, …) → `ApiConnectionException("Connection error: {inner.Message}", inner)`. Info log: `connection error after {ms}ms` with the exception.
   - Response: info log `"{tag} <- {status} in {ms}ms"` (+ ` (request {id})`). 2xx → done. Non-2xx → parse the body, debug-log the error body, and create the exception via `ApiExceptionFactory`.
   - Retry decision: connection error → `RetryOnConnectionError`; timeout → `RetryOnTimeout`; status → in `RetryableStatusCodes`. All require retries remaining. Otherwise throw the last error.
   - Back-off: delay from `RetryMath`. Info log `"{tag} retrying in {ms}ms (retry {n}/{total}) after {reason}"`. Wait with `TimeProvider` + the caller token. Cancellation → info log `aborted by caller while waiting to retry`, and throw `ApiUserAbortException`.
4. **Success:** parse the body (§8.5), debug-log it, then deserialize to the result model. Shape errors → `TypeSafeException`.
- Each attempt uses a **new** `HttpRequestMessage` (they cannot be re-sent), so the timeout is per attempt with no total budget.
- All CTSs, registrations, and messages are disposed. No timers outlive the call.

### 8.4 Retry math (pure)
`RetryAfterParser.Parse(headers, now) → TimeSpan?`:
1. `retry-after-ms` present and a finite number ≥ 0 → that many ms. It takes precedence. An empty value is treated as 0 (JS `Number("")` parity; documented in code).
2. Else `Retry-After`: a finite number → if ≥ 0, seconds × 1000, else `null`. Otherwise an HTTP date (RFC 1123, invariant culture) → `max(0, date − now)`. Otherwise `null`.
3. Neither header → `null`.

`RetryMath.ComputeDelay(attempt, retryAfter, policy, random) → TimeSpan`:
- If `RespectRetryAfter` and `retryAfter ≤ MaxRetryAfter` → exactly `retryAfter` (no jitter).
- Else `exp = min(InitialBackoff × 2^attempt, MaxBackoff)`, and `delay = round(exp_ms × (1 − random × BackoffJitter))` using `MidpointRounding.AwayFromZero`. Overflow-safe: cap the exponent before multiplying.

Defaults: `MaxRetries=2`, `InitialBackoff=500ms`, `MaxBackoff=5s`, `BackoffJitter=0.25`, statuses `{408, 429, 500…599}`, `RespectRetryAfter=true`, `MaxRetryAfter=60s`, and both error-retry flags `true`.

Validation messages (names match upstream for familiarity):
- `` `retry.maxRetries` must be a non-negative integer, got {v}. ``
- `` `retry.backoffInitialMs` / `retry.backoffMaxMs` / `retry.maxRetryAfterMs` must be a non-negative number of milliseconds, got {v}. ``
- `` `retry.backoffJitter` must be between 0 and 1, got {v}. ``
- `` `retry.httpStatuses` must contain HTTP status codes, got {v}. `` (valid range is 100–999)

### 8.5 Body parsing
- Empty body → `null`.
- Otherwise try JSON (regardless of `Content-Type`). Failure → the raw text.
- Result type internally: a discriminated `ParsedBody` (`None` | `Json(JsonElement)` | `Text(string)`).

### 8.6 Request validation (`QuestionValidator`, synchronous)
- `request` null → `ArgumentNullException`.
- `State.IsOmitted` → `ArgumentException("State is required; use Entry.Null to send null.")`.
- No questions → `TypeSafeException("At least one question is required.")`.
- A score question with fewer than 2 criteria → `TypeSafeException("Score question \"{name}\" has {n} criteria; at least two scores are required.")`.
- Choice criteria map or score list shape errors are impossible by construction (the type system enforces them).

### 8.7 Exceptions
```
Exception
└── TypeSafeException                                  base for all SDK errors (upstream TypeSafeError)
    ├── ApiException                                   StatusCode, Headers, Body (object?: JsonElement|string), BodyText, RequestId
    │   ├── BadRequestException            400
    │   ├── AuthenticationException        401
    │   ├── PermissionDeniedException      403
    │   ├── NotFoundException              404
    │   ├── UnprocessableEntityException   422
    │   ├── RateLimitException             429         + TimeSpan? RetryAfter
    │   └── InternalServerException        ≥ 500
    ├── ApiConnectionException                         "Connection error." (default)
    │   └── ApiTimeoutException                        Timeout; "Request timed out after {ms}ms."
    └── ApiUserAbortException                          "Request was aborted."; InnerException = OperationCanceledException; CancellationToken
```
- **Following upstream:** `ApiUserAbortException` derives from `TypeSafeException`, so `catch (TypeSafeException)` catches every SDK failure. Its `InnerException` is the original `OperationCanceledException`, and it exposes the caller's `CancellationToken` so that callers can correlate.
- Standard exception constructors (`()`, `(string)`, `(string, Exception)`) on every type, plus the domain constructors. No binary-serialization constructors (obsolete pattern).
- Message composition (`ErrorMessageExtractor`), as `"{status} {detail}"`, where detail comes from the first match in this order: string body; `error` string; `error.message`; `message`; `detail` string; `detail.message`; `detail[]` validation entries → `"{loc without 'body', dot-joined}: {msg}"` joined with `"; "`. Fallbacks: no body → `"{status} status code (no body)"`. Otherwise the raw text/JSON, truncated to 200 chars + `…`.
- `AuthenticationException` shares its name with `System.Security.Authentication.AuthenticationException`. It is kept for consistency, and the README shows a `using` alias.

### 8.8 Logging
- Sink selection: `Logger` → `LoggerFactory.CreateLogger("TypeSafe.AI")` → built-in `ConsoleLogger` (if the effective level ≠ `Off`) → `NullLogger.Instance`.
- `LevelFilteredLogger` enforces the SDK level regardless of the sink's configuration. Mapping: Debug→`Debug`, Info→`Information`, Warn→`Warning`, Error→`Error`, Off→`None`.
- The SDK emits **Information** (attempt summaries, retries, timeouts, aborts, connection errors) and **Debug** (redacted headers and bodies) only. The default `Warn` level is therefore silent.
- `ConsoleLogger` prefixes `[typesafe-sdk] `. Debug and Info go to stdout; Warn and Error go to stderr.
- Event IDs: 1001 request sent, 1002 response received, 1003 response body, 1004 error body, 1005 retrying, 1006 timed out, 1007 connection error, 1008 aborted, 1009 aborted during back-off.
- `HeaderRedactor`: `authorization`, `proxy-authorization`, `x-api-key` → keep the scheme, then `***` + the last 4 chars only when the secret is longer than 8 chars. `cookie`, `set-cookie` → `***`. It returns a new map. Bodies are **not** redacted (documented).
- Invariant: the raw API key never appears in log output at any level.

---

## 9. Wire contract

### 9.1 `GET /v1/models`
Response: `{ "models": [ { "name", "description", "release_date", …extra } ] }`. Any other shape (`null`, a top-level array, `models` null, a string, or an object, or missing) → `TypeSafeException("Unexpected response shape from GET /v1/models; expected { models: [...] }.")`.

### 9.2 `POST /v1/systemone`
Request (compact JSON; property names exactly as shown):
```json
{ "state": <entry>, "questions": { "<name>": <question>, … }, "model": "<resolved>", …additionalProperties }
```
Question encodings (`type` first; omitted entries are not written; explicit nulls are written):
```json
{ "type": "noul",   "instructions": <entry>, "criteria": { "true": <entry>, "false": <entry> } }
{ "type": "choice", "instructions": <entry>, "criteria": { "<label>": <entry>, … } }
{ "type": "score",  "instructions": <entry>, "criteria": [ <entry>, <entry>, … ] }
```
Response:
```json
{ "model": "…", "answers": { "<name>": { "type": "noul", "noul": 0.93 }
                           , "<name>": { "type": "choice", "choice": "…", "confidence": 0.8, "probabilities": { "<label>": 0.8 } }
                           , "<name>": { "type": "score", "score": 1.7, "confidence": 0.6,
                                         "legend": { "0": <entry> }, "probabilities": { "0": 0.1 } } },
  "usage": { "input_tokens": 1, "output_tokens": 1 } }
```
Request-ID header: `x-typesafe-request-id`.

### 9.3 Invariants
1. Omitted ≠ null (§7.1). `Question.Noul("x")` → `{"type":"noul","instructions":"x"}`. `Question.Noul()` → `{"type":"noul","instructions":null}`.
2. Question names, choice labels, and score order are preserved exactly.
3. `model` is always present. It is the resolved value, and an `AdditionalProperties` entry named `model` cannot override it. Extras named `state`, `questions`, or `model` are rejected with `ArgumentException` (use the request properties instead). This is a safer, explicit .NET contract than JS spread order.
4. Additional properties (including `null` values) are written after the known fields. They never leak between requests.
5. No SDK object supplied by the caller is mutated.

---

## 10. Serialization design
- No `JsonSerializerContext` and no reflection: requests are written and responses read by hand. Public model types carry no STJ attributes, which keeps the wire format decoupled from the public API.
- Hand-written `Utf8JsonWriter` writers for requests (`SystemOneRequestWriter`) give deterministic ordering and omitted/null control without reflection.
- `SystemOneResultReader` reads via `JsonDocument`. It dispatches answers on `type` to typed answer constructors, and falls back to `UnknownAnswer`. The body is parsed once into a cloned root `JsonElement`, so retained elements (`Answer.Raw`) need no disposal. Shape errors raise `TypeSafeException("Unexpected response shape from {endpoint}; expected …")` via `JsonShapeReader`.
- Score keys `"0"`, `"1"`, … → `int` (invariant). A non-integer key → `TypeSafeException`.
- Strict reading: case-sensitive names and no trailing commas. Unknown properties are ignored but kept in `Raw`/`AdditionalProperties`.

---

## 11. Resource and threading rules
- `TypeSafeClient` is thread-safe. All mutable state is limited to an `Interlocked` counter and the disposed flag.
- SDK-owned `HttpClient`: created once per client and disposed by `Dispose()`. The README recommends a singleton client to avoid socket exhaustion.
- Disposal: calls after `Dispose()` throw `ObjectDisposedException`. A request whose attempt fails because the client was disposed mid-flight also surfaces `ObjectDisposedException` and is never retried; the lifetime is checked before each attempt and after each failed attempt.
- A caller-supplied `HttpClient` is never disposed by the SDK.
- No `Task.Run` and no thread blocking. Every await uses `ConfigureAwait(false)`.

---

## 12. Deliberate deviations from upstream
| # | Upstream | .NET | Reason |
|---|---|---|---|
| D1 | `AbortSignal` in options | `CancellationToken` method parameter | .NET convention |
| D2 | Custom `fetch` | `HttpClient` / `HttpMessageHandler` injection | .NET convention; also the test seam |
| D3 | Millisecond numbers | `TimeSpan` | .NET convention. Messages still report ms. |
| D4 | `APIPromise` (`asResponse` / `withResponse` / `map`) | `Task<T>` + `…WithResponseAsync` → `ApiResponse<T>` | Framework Design Guidelines; composable and mockable |
| D5 | Literal type inference of answers | `QuestionKey<T>`, `Choice<TEnum>`, typed getters | C# type-system limits |
| D6 | `Partial<RetryPolicy>` overrides | `RetryPolicy` record + `with` | Idiomatic immutable update |
| D7 | `typesafe-sdk/{v}` identity | `typesafe-sdk-dotnet/{v}` | Identifies the .NET client |
| D8 | Console default logger | `ILogger` + minimal console fallback | .NET convention |
| D9 | Runtime header (node/bun/deno/edge) | .NET / .NET Framework / Mono / browser | Different runtimes |
| D10 | `criteria: null` on noul is expressible | `null` criteria → omitted | Negligible. Revisit if the server distinguishes them. |
| D11 | Spread lets extras override known keys | Extras cannot override `model`; `state`/`questions` collisions are rejected | Safer, explicit contract |
| D12 | JS-side runtime guards for wrong criteria shapes | Prevented by the type system | Static typing |
| D13 | Errors named `*Error` | `*Exception` | .NET naming guidelines |
| D14 | `models.list()` checks only the `{ models: [...] }` envelope | Each card must also be an object with string `name`, `description`, `release_date` | Public `ModelCard` properties are non-null; extra fields remain in `AdditionalProperties` |
| D15 | `fetch` forwards any header on bodyless requests | Content headers (e.g. `Content-Language`) are sent only when the request has a body | `HttpClient` keeps content headers on `HttpContent`; documented on the header options |

---

## 13. Testing strategy (Phase 2, implemented)
- **Frameworks:** xUnit v3 on Microsoft.Testing.Platform (`dotnet test` is opted in through `global.json`), `FluentAssertions`-free (xUnit `Assert` only), `Microsoft.Extensions.TimeProvider.Testing` (`FakeTimeProvider`), and coverage via `Microsoft.Testing.Extensions.CodeCoverage`.
- **Run:** `dotnet test --project tests/Jev.TypeSafe.Unofficial.Tests -f net10.0` (add `-f net48` on Windows). Coverage: append `--coverage --coverage-output-format cobertura`. Live tests run when `TYPESAFE_API_KEY` is set and are skipped otherwise.
- **TFMs:** `net10.0` everywhere, plus `net48` on Windows (this exercises the netstandard build on .NET Framework).
- **Layout** (`tests/Jev.TypeSafe.Unofficial.Tests`):
  - `Internal/`: pure unit tests of retry math, `Retry-After` parsing, header merging and redaction, error-message extraction, option and configuration resolution, logging pieces, runtime description, and the request writer and response readers (golden JSON).
  - `Public/`: public models (`Entry`, questions and criteria, `QuestionSet`, answers, `RetryPolicy`, exceptions, `RawResponse`, `TypeSafeModelFactory`).
  - `Client/`: the public API end to end against a stub `HttpMessageHandler`, with fake time: configuration, `SystemOneAsync`, models, headers, retries, timeouts, cancellation, disposal, logging, response-body regressions, and a fake `IApiTransport` for the application layer.
  - `Transport/`: a real loopback `HttpListener` server for on-the-wire headers, chunked bodies, real timeouts and cancellation, and refused connections.
  - `Live/`: the live API (models, typed answers, rich descriptions, bad key, unknown model, pre-send validation).
  - `Support/`: the stub handler, fake environment/random/logger/streams, `Async` time helpers, and `Sync.Throws` (asserts a method throws *before* returning a task).
- **Upstream behavior coverage:** every upstream test scenario has a .NET counterpart, except those the type system now prevents (for example score criteria given as a map, or numeric entries). One upstream live check, the 422 validation message, cannot be provoked through the typed API; its message extraction is covered by unit tests.
- **Rules:** time is driven by `FakeTimeProvider` (the few real delays are short polling waits and the loopback tests); no process-wide state is mutated (environment variables and the console are injected); tests are parallel-safe; one behavior per test; names follow `Method_Scenario_Expected`.
- **Gates:** ≥ 90% line and ≥ 85% branch coverage on `src/`. Measured: 97.2% / 91.8%. The uncovered lines are OS-specific branches (Linux, macOS, FreeBSD, other CPU architectures in `RuntimeDescriptor`) and defensive fallbacks.

---
## 14. Examples and docs (Phase 3)
- `examples/Demo`: lists models, then asks noul, choice (including an enum), and score questions about a support ticket. It prints the answers, the token usage, and the request ID, and handles `ApiException`.
- `README.md`: an **"Unofficial — not affiliated with or endorsed by TypeSafe AI"** banner, install, quickstart, configuration, typed answers, errors, retries and timeouts, cancellation, logging, raw responses, testing with `ITypeSafeClient` / `TypeSafeModelFactory`, and supported platforms.
- `NOTICE.md`: attributes the upstream MIT project (© TypeSafe) and reproduces its license. `LICENSE` is MIT for this repository.
- `CHANGELOG.md`: Keep a Changelog format. Each entry states the tracked upstream version.

Target quickstart:
```csharp
using TypeSafe.AI;

using var client = new TypeSafeClient();   // TYPESAFE_API_KEY from the environment

var questions = new QuestionSet();
var category = questions.Add("category",
    Question.Choice("What is this ticket about?", ChoiceCriteria.FromLabels("billing", "technical", "other")));

var result = await client.SystemOneAsync(new SystemOneRequest(
    new JsonObject { ["document"] = "I was charged twice. Please fix this ASAP." },
    questions));

Console.WriteLine(result.Answers.Get(category).Choice);
```

---

## 15. Packaging and CI (Phase 4 — done)
**Implemented:** repository URL `https://github.com/BareIQ/Jev.TypeSafe.AI`, SourceLink (built into the .NET SDK), `PublishRepositoryUrl`, `EnablePackageValidation`, `.github/workflows/ci.yml` and `publish.yml`. The packed nuspec was inspected: repository and commit metadata are present. Publishing needs a GitHub environment named `nuget` and a `NUGET_USER` secret, plus a trusted-publishing policy on nuget.org.

- NuGet: `PackageId=Jev.TypeSafe.Unofficial`, MIT license expression, README, tags `typesafe;typesafe-ai;jev;ai;llm;classification;sdk;unofficial`, the repository URL, SourceLink, `snupkg` symbols, `EmbedUntrackedSources`, and `ContinuousIntegrationBuild` on CI.
- `EnablePackageValidation` (with a baseline from the first stable release).
- Versioning: SemVer from `0.1.0-preview.1`. `SdkInfo.Version` is derived from the assembly informational version, and a CI step asserts it matches the package version.
- GitHub Actions: `ci.yml` (build + test on a ubuntu/windows/macos matrix, `net48` tests on Windows, pack, and upload the artifact). `publish.yml` (on a `v*` tag, pack, push via NuGet trusted publishing (OIDC), and create a GitHub release).

---

## 16. Acceptance criteria

**Phase 1 is done when:**
- [ ] F1–F27 (§6) are implemented.
- [ ] `dotnet build -c Release` passes with zero warnings under §4.3.
- [ ] `PublicAPI.Unshipped.txt` matches §7 exactly.
- [ ] Every public member has XML docs.
- [ ] No public type is unsealed without justification. No public mutable state exists on the client.
- [ ] Every seam in §5.3 is injectable.
- [ ] A local smoke run (live API) succeeds for `Models.ListAsync` and a noul/choice/score `SystemOneAsync`.
- [ ] The API key is absent from `ToString()`, the debugger display, and Debug-level logs (manually verified; automated in Phase 2).

**Phases 2 and 3 are done** (see §2 for the evidence). **Phase 4 is done** (see §15).

---

## 17. Decision log
| # | Question | Decision |
|---|---|---|
| 1 | Package ID / namespace | Package and assembly: `Jev.TypeSafe.Unofficial`. Root namespace: `TypeSafe.AI`. |
| 2 | SDK identity header | `typesafe-sdk-dotnet/{version}` |
| 3 | DI integration package | **None.** The client stays DI-friendly via its ctor + `ITypeSafeClient`. |
| 4 | Cancellation exception, extra TFMs, response-size limit | **Follow upstream:** `ApiUserAbortException : TypeSafeException`; `netstandard2.0` only; no size limit |
| 5 | Target framework | `netstandard2.0` (2.1 rejected; see §3.1) |
| 6 | Upstream mapping | Functional coverage (§6), not a structural file mirror |
| 7 | Async return shape | `Task<T>` + `…WithResponseAsync` (instead of the custom awaitable from draft v1) |
| 8 | Dependency versions | 10.0.x LTS packages (8.0.x reaches end of support in Nov 2026); all still target netstandard2.0 |
| 9 | Base URL option type | `Uri? BaseUri` (CA1056; idiomatic) instead of `string? BaseUrl` |
| 10 | Serialization | Hand-written writers/readers only; no source-generated context needed |
| 11 | Analyzer opt-outs | Kept minimal and justified inline in `.editorconfig` (CA1303, CA1062, CA1710, CA1032, CA1716, CA2225, CA5399/CA5400) |
| 12 | Package sources | Repo `nuget.config` pins nuget.org with source mapping (required by Central Package Management) |
| 13 | Test runner | xUnit v3 on Microsoft.Testing.Platform; dotnet test opts in via global.json (the .NET 10 SDK no longer supports the VSTest path for MTP projects) |
| 14 | Already-cancelled token | The SDK checks the token before sending, so a custom HttpMessageHandler that ignores tokens never receives the request (upstream etch behaves the same) |
| 15 | JSON escaping | Request bodies and logged JSON use the relaxed encoder, so non-ASCII text in the basic multilingual plane is not escaped (as JSON.stringify does); characters outside it are still escaped as surrogate pairs |
| 16 | DI integration | Separate package `Jev.TypeSafe.Unofficial.Extensions` (folder `src/Jev.TypeSafe.Extensions`), targeting `netstandard2.0;net8.0;net9.0;net10.0`. `AddTypeSafeClient` overloads in the `Microsoft.Extensions.DependencyInjection` namespace return `IHttpClientBuilder`; singleton client on an `IHttpClientFactory` client with an infinite `HttpClient.Timeout`; options from `IConfiguration` then delegates; `ILoggerFactory` and `TimeProvider` from the container. Released with the same tag and version as the core. Out of scope: named/keyed clients, `ValidateOnStart`. |
