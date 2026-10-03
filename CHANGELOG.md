# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses
[Semantic Versioning](https://semver.org/). Each release notes the upstream JavaScript SDK version it tracks.

## [Unreleased]

## [0.1.0-preview.1]

First preview. Tracks upstream `@typesafe-ai/sdk` **0.6.0**.

### Added

- `TypeSafeClient` (`ITypeSafeClient`) with `SystemOneAsync` / `SystemOneWithResponseAsync` and
  `Models.ListAsync` / `ListWithResponseAsync`.
- Question builders for yes/no (`Question.Noul`), choice (`Question.Choice`, including enum-backed labels), and score
  (`Question.Score`) questions, with typed keys for reading answers (`QuestionSet.Add`, `AnswerSet.Get`).
- Configuration from options, then the `TYPESAFE_API_KEY`, `TYPESAFE_BASE_URL`, `TYPESAFE_DEFAULT_MODEL`, and
  `TYPESAFE_LOG_LEVEL` environment variables, then defaults.
- Retries with exponential backoff and jitter, `Retry-After` / `retry-after-ms` support, per-attempt timeouts, and
  cancellation through `CancellationToken`, all configurable per client and per call (`RetryPolicy`).
- Error hierarchy rooted at `TypeSafeException`, with status-specific `ApiException` subclasses, request IDs, and
  readable messages.
- Logging through `Microsoft.Extensions.Logging` with credential redaction.
- `TypeSafe.AI.Testing.TypeSafeModelFactory` for building results in consumer tests.
- Targets `netstandard2.0` (.NET Framework 4.6.2+, .NET Core 2.0+, .NET 5+, Mono, Unity, Xamarin).
