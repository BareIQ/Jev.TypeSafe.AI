using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using TypeSafe.AI.Internal.Transport;
using TypeSafe.AI.Testing;
using Xunit;

namespace TypeSafe.AI.Tests.Public;

public class ExceptionTests
{
    private static RawResponse Response(int status = 500, string? body = null) => TypeSafeModelFactory.RawResponse(status, body);

    [Fact]
    public void TypeSafeException_Constructors_SetMessageAndInnerException()
    {
        var inner = new InvalidOperationException("inner");

        Assert.NotNull(new TypeSafeException().Message);
        Assert.Equal("m", new TypeSafeException("m").Message);
        Assert.Same(inner, new TypeSafeException("m", inner).InnerException);
    }

    [Fact]
    public void ApiConnectionException_DefaultMessageIsConnectionError()
    {
        Assert.Equal("Connection error.", new ApiConnectionException().Message);
        Assert.Equal("custom", new ApiConnectionException("custom").Message);
        Assert.IsAssignableFrom<TypeSafeException>(new ApiConnectionException());
    }

    [Theory]
    [InlineData(10_000, "Request timed out after 10000ms.")]
    [InlineData(100, "Request timed out after 100ms.")]
    public void ApiTimeoutException_DescribesTheTimeoutInMilliseconds(double milliseconds, string message)
    {
        var exception = new ApiTimeoutException(TimeSpan.FromMilliseconds(milliseconds));

        Assert.Equal(message, exception.Message);
        Assert.Equal(TimeSpan.FromMilliseconds(milliseconds), exception.Timeout);
        Assert.IsAssignableFrom<ApiConnectionException>(exception);
    }

    [Fact]
    public void ApiTimeoutException_KeepsItsCause()
    {
        var cause = new OperationCanceledException();

        Assert.Same(cause, new ApiTimeoutException(TimeSpan.FromSeconds(1), cause).InnerException);
    }

    [Fact]
    public void ApiUserAbortException_DefaultMessageAndToken()
    {
        using var source = new CancellationTokenSource();
        var cause = new OperationCanceledException(source.Token);

        var withToken = new ApiUserAbortException(cause, source.Token);

        Assert.Equal("Request was aborted.", new ApiUserAbortException().Message);
        Assert.Equal("Request was aborted.", withToken.Message);
        Assert.Same(cause, withToken.InnerException);
        Assert.Equal(source.Token, withToken.CancellationToken);
        Assert.Equal(CancellationToken.None, new ApiUserAbortException("m").CancellationToken);
        Assert.Same(cause, new ApiUserAbortException("m", cause).InnerException);
        Assert.IsAssignableFrom<TypeSafeException>(withToken);
    }

    [Fact]
    public void ApiException_NullResponse_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new ApiException("m", null!));
    }

    [Fact]
    public void StatusExceptions_ExposeTheResponseAndCause()
    {
        Func<string, RawResponse, Exception?, ApiException>[] factories =
        [
            (m, r, e) => new ApiException(m, r, e),
            (m, r, e) => new BadRequestException(m, r, e),
            (m, r, e) => new AuthenticationException(m, r, e),
            (m, r, e) => new PermissionDeniedException(m, r, e),
            (m, r, e) => new NotFoundException(m, r, e),
            (m, r, e) => new UnprocessableEntityException(m, r, e),
            (m, r, e) => new RateLimitException(m, r, e),
            (m, r, e) => new InternalServerException(m, r, e),
        ];

        foreach (var create in factories)
        {
            RawResponse response = Response(418, "{}");
            var cause = new InvalidOperationException("why");

            ApiException exception = create("message", response, cause);

            Assert.Equal("message", exception.Message);
            Assert.Same(response, exception.Response);
            Assert.Equal(418, exception.StatusCode);
            Assert.Same(cause, exception.InnerException);
            Assert.IsAssignableFrom<TypeSafeException>(exception);
        }
    }
    [Fact]
    public void StatusExceptions_ShortConstructorsWork()
    {
        RawResponse response = Response();

        Assert.Equal("m", new BadRequestException("m", response).Message);
        Assert.Equal("m", new AuthenticationException("m", response).Message);
        Assert.Equal("m", new PermissionDeniedException("m", response).Message);
        Assert.Equal("m", new NotFoundException("m", response).Message);
        Assert.Equal("m", new UnprocessableEntityException("m", response).Message);
        Assert.Equal("m", new InternalServerException("m", response).Message);
        Assert.Equal("m", new RateLimitException("m", response).Message);
    }

    [Fact]
    public void RateLimitException_PublicConstructorParsesRetryAfter()
    {
        RawResponse withDelay = TypeSafeModelFactory.RawResponse(429, "{}", [new("retry-after-ms", "1500")]);

        Assert.Equal(TimeSpan.FromMilliseconds(1500), new RateLimitException("m", withDelay).RetryAfter);
        Assert.Null(new RateLimitException("m", Response(429)).RetryAfter);
    }

    [Fact]
    public void ApiException_BodyIsAJsonElementForObjectsAndAStringOtherwise()
    {
        Assert.IsType<JsonElement>(new ApiException("m", Response(400, """{"a":1}""")).Body);
        Assert.Equal("plain text", new ApiException("m", Response(502, "plain text")).Body);
        Assert.Equal("json string", new ApiException("m", Response(400, "\"json string\"")).Body);
        Assert.Null(new ApiException("m", Response(500)).Body);
        Assert.Null(new ApiException("m", Response(500)).BodyText);
        Assert.Equal("plain text", new ApiException("m", Response(502, "plain text")).BodyText);
        Assert.Null(new ApiException("m", Response(500)).RequestId);
    }
}

public class RawResponseTests
{
    private static RawResponse Create(byte[] content, params (string Name, string Value)[] headers)
        => RawResponseFactory.Create(
            200,
            headers.Select(h => new KeyValuePair<string, IEnumerable<string>>(h.Name, [h.Value])),
            content);

    [Fact]
    public void Headers_AreCaseInsensitiveAndMultiValuesAreJoined()
    {
        RawResponse response = RawResponseFactory.Create(
            200,
            [new("Set-Thing", ["a", "b"]), new("X-Other", ["c"]), new("set-thing", ["d"])],
            ReadOnlyMemory<byte>.Empty);

        Assert.True(response.TryGetHeader("SET-THING", out string? value));
        Assert.Equal("a, b, d", value);
        Assert.Equal(["a", "b", "d"], response.Headers["set-thing"]);
        Assert.Equal(2, response.Headers.Count);
        Assert.False(response.TryGetHeader("missing", out string? missing));
        Assert.Null(missing);
        Assert.Throws<ArgumentNullException>(() => response.TryGetHeader(null!, out _));
    }

    [Fact]
    public void RequestId_ComesFromTheTypeSafeHeader()
    {
        Assert.Equal("req_1", Create([], ("X-TypeSafe-Request-Id", "req_1")).RequestId);
        Assert.Null(Create([]).RequestId);
    }

    [Fact]
    public void ReadContentAsString_DecodesUtf8ByDefault()
    {
        Assert.Equal("héllo 你好", Create(Encoding.UTF8.GetBytes("héllo 你好")).ReadContentAsString());
    }

    [Fact]
    public void ReadContentAsString_StripsAByteOrderMark()
    {
        byte[] content = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("{}")];

        Assert.Equal("{}", Create(content).ReadContentAsString());
    }

    [Theory]
    [InlineData("text/plain; charset=iso-8859-1")]
    [InlineData("text/plain; charset=\"iso-8859-1\"")]
    public void ReadContentAsString_HonorsTheContentTypeCharset(string contentType)
    {
        byte[] latin1 = [0x63, 0x61, 0x66, 0xE9];

        Assert.Equal("café", Create(latin1, ("Content-Type", contentType)).ReadContentAsString());
    }

    [Fact]
    public void ReadContentAsString_UnknownCharsetFallsBackToUtf8()
    {
        Assert.Equal("ok", Create(Encoding.UTF8.GetBytes("ok"), ("Content-Type", "text/plain; charset=made-up-charset")).ReadContentAsString());
    }

    [Fact]
    public void ReadContentAsString_UnparsableContentTypeFallsBackToUtf8()
    {
        Assert.Equal("ok", Create(Encoding.UTF8.GetBytes("ok"), ("Content-Type", "///")).ReadContentAsString());
    }

    [Fact]
    public void ReadContentAsString_EmptyBodyIsEmpty()
    {
        Assert.Equal(string.Empty, Create([]).ReadContentAsString());
    }

    [Fact]
    public void ReadContentAsString_WorksWithASlicedBuffer()
    {
        byte[] buffer = Encoding.UTF8.GetBytes("xxhelloxx");
        RawResponse response = RawResponseFactory.Create(200, [], new ReadOnlyMemory<byte>(buffer, 2, 5));

        Assert.Equal("hello", response.ReadContentAsString());
    }

    [Fact]
    public void ParseContentAsJson_ParsesTheBody()
    {
        using JsonDocument document = TypeSafeModelFactory.RawResponse(200, """{"a":[1,2]}""").ParseContentAsJson();

        Assert.Equal(2, document.RootElement.GetProperty("a").GetArrayLength());
    }

    [Fact]
    public void ParseContentAsJson_InvalidJson_Throws()
    {
        RawResponse response = TypeSafeModelFactory.RawResponse(200, "not json");

        Assert.ThrowsAny<JsonException>(() => response.ParseContentAsJson());
    }
}

public class TypeSafeModelFactoryTests
{
    [Fact]
    public void NoulAnswer_CarriesItsValueAndRawJson()
    {
        NoulAnswer answer = TypeSafeModelFactory.NoulAnswer(0.4);

        Assert.Equal(0.4, answer.Noul);
        Assert.Equal("noul", answer.Raw.GetProperty("type").GetString());
    }

    [Fact]
    public void ChoiceAnswer_CarriesLabelsInOrder()
    {
        ChoiceAnswer answer = TypeSafeModelFactory.ChoiceAnswer("b", 0.8, [new("a", 0.2), new("b", 0.8)]);

        Assert.Equal("b", answer.Choice);
        Assert.Equal(0.8, answer.Confidence);
        Assert.Equal(["a", "b"], answer.Probabilities.Keys);
    }

    [Fact]
    public void ScoreAnswer_CarriesLegendAndProbabilities()
    {
        ScoreAnswer answer = TypeSafeModelFactory.ScoreAnswer(
            1.5,
            0.6,
            [new(0, "low"), new(1, Entry.Null)],
            [new(0, 0.5), new(1, 0.5)]);

        Assert.Equal(1.5, answer.Score);
        Assert.Equal("low", answer.Legend[0].AsText());
        Assert.Equal(Entry.Null, answer.Legend[1]);
        Assert.Equal(0.5, answer.Probabilities[1]);
    }

    [Fact]
    public void SystemOneResult_ComposesAnswersAndUsage()
    {
        SystemOneResult result = TypeSafeModelFactory.SystemOneResult(
            "jev-1",
            [new("n", TypeSafeModelFactory.NoulAnswer(0.1))],
            inputTokens: 5,
            outputTokens: 7);

        Assert.Equal("jev-1", result.Model);
        Assert.Equal(0.1, result.Answers.GetNoul("n").Noul);
        Assert.Equal(5, result.Usage.InputTokens);
        Assert.Equal(7, result.Usage.OutputTokens);
    }

    [Fact]
    public void ModelCard_KeepsAdditionalProperties()
    {
        using JsonDocument document = JsonDocument.Parse("[1]");

        ModelCard card = TypeSafeModelFactory.ModelCard("m", "d", "2026", [new("extra", document.RootElement.Clone())]);

        Assert.Equal("m", card.Name);
        Assert.Equal("d", card.Description);
        Assert.Equal("2026", card.ReleaseDate);
        Assert.True(card.AdditionalProperties.ContainsKey("extra"));
        Assert.Empty(TypeSafeModelFactory.ModelCard("m", "d", "r").AdditionalProperties);
    }

    [Fact]
    public void RawResponse_And_ApiResponse_CarryStatusHeadersAndContent()
    {
        RawResponse raw = TypeSafeModelFactory.RawResponse(201, "body", [new("x-typesafe-request-id", "req_7")]);

        ApiResponse<string> response = TypeSafeModelFactory.ApiResponse("value", raw);

        Assert.Equal(201, raw.StatusCode);
        Assert.Equal("body", raw.ReadContentAsString());
        Assert.Equal("value", response.Value);
        Assert.Same(raw, response.RawResponse);
        Assert.Equal("req_7", response.RequestId);
    }

    [Fact]
    public void ApiResponse_WithoutResponse_DefaultsToAnEmptyOk()
    {
        ApiResponse<int> response = TypeSafeModelFactory.ApiResponse(3);

        Assert.Equal(200, response.RawResponse.StatusCode);
        Assert.Null(response.RequestId);
        Assert.True(response.RawResponse.Content.IsEmpty);
    }

    [Fact]
    public void NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => TypeSafeModelFactory.ChoiceAnswer(null!, 1, []));
        Assert.Throws<ArgumentNullException>(() => TypeSafeModelFactory.ChoiceAnswer("a", 1, null!));
        Assert.Throws<ArgumentNullException>(() => TypeSafeModelFactory.SystemOneResult(null!, []));
        Assert.Throws<ArgumentNullException>(() => TypeSafeModelFactory.SystemOneResult("m", null!));
        Assert.Throws<ArgumentNullException>(() => TypeSafeModelFactory.ScoreAnswer(1, 1, null!, []));
        Assert.Throws<ArgumentNullException>(() => TypeSafeModelFactory.ScoreAnswer(1, 1, [], null!));
        Assert.Throws<ArgumentNullException>(() => TypeSafeModelFactory.ModelCard(null!, "d", "r"));
    }
}

public class MiscPublicTypesTests
{
    [Fact]
    public void SdkInfo_VersionHasNoBuildMetadata_AndTracksTheUpstreamVersion()
    {
        Assert.False(string.IsNullOrWhiteSpace(SdkInfo.Version));
        Assert.DoesNotContain("+", SdkInfo.Version, StringComparison.Ordinal);
        Assert.Equal("0.6.0", SdkInfo.UpstreamVersion);
    }

    [Fact]
    public void EnvironmentVariableNames_MatchTheUpstreamSdk()
    {
        Assert.Equal("TYPESAFE_API_KEY", TypeSafeEnvironmentVariables.ApiKey);
        Assert.Equal("TYPESAFE_BASE_URL", TypeSafeEnvironmentVariables.BaseUrl);
        Assert.Equal("TYPESAFE_DEFAULT_MODEL", TypeSafeEnvironmentVariables.DefaultModel);
        Assert.Equal("TYPESAFE_LOG_LEVEL", TypeSafeEnvironmentVariables.LogLevel);
    }

    [Fact]
    public void ClientOptions_ToStringRedactsTheApiKey()
    {
        var options = new TypeSafeClientOptions { ApiKey = "sk-secret-value", DefaultModel = "m" };

        Assert.DoesNotContain("sk-secret-value", options.ToString(), StringComparison.Ordinal);
        Assert.Contains("ApiKey = ***", options.ToString(), StringComparison.Ordinal);
        Assert.Contains("ApiKey = (not set)", new TypeSafeClientOptions().ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ClientOptions_DefaultHeadersAreCaseInsensitive()
    {
        var options = new TypeSafeClientOptions();
        options.DefaultHeaders["X-A"] = "1";
        options.DefaultHeaders["x-a"] = "2";

        Assert.Single(options.DefaultHeaders);
    }

    [Fact]
    public void RequestOptions_HeadersAreCaseInsensitive()
    {
        var options = new RequestOptions();
        options.Headers["X-A"] = "1";
        options.Headers["x-a"] = "2";

        Assert.Single(options.Headers);
        Assert.Null(options.Timeout);
        Assert.Null(options.RetryPolicy);
    }

    [Fact]
    public void SystemOneRequest_RequiresStateAndQuestions()
    {
        Assert.Throws<ArgumentException>(() => new SystemOneRequest(default, new QuestionSet()));
        Assert.Throws<ArgumentNullException>(() => new SystemOneRequest("s", null!));
        Assert.Equal(Entry.Null, new SystemOneRequest(Entry.Null, new QuestionSet()).State);
    }
}
