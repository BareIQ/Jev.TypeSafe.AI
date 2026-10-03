using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using TypeSafe.AI.Internal.Errors;
using TypeSafe.AI.Internal.Serialization;
using TypeSafe.AI.Internal.Transport;
using TypeSafe.AI.Testing;
using Xunit;

namespace TypeSafe.AI.Tests.Internal;

public class ErrorMessageExtractorTests
{
    private static ParsedBody Json(string json) => ParsedBody.FromJson(JsonText.ParseElement(json));

    [Theory]
    [InlineData("""{"error":"bad key"}""", "401 bad key")]
    [InlineData("""{"error":{"message":"nested"}}""", "401 nested")]
    [InlineData("""{"message":"plain message"}""", "401 plain message")]
    [InlineData("""{"detail":"detail text"}""", "401 detail text")]
    [InlineData("""{"detail":{"message":"detail object"}}""", "401 detail object")]
    [InlineData("""{"error":"first","message":"second","detail":"third"}""", "401 first")]
    [InlineData("""{"error":{"code":1},"message":"fallback"}""", "401 fallback")]
    [InlineData("\"just a string\"", "401 just a string")]
    public void Describe_KnownShapes_ExtractsTheFirstMessage(string body, string expected)
    {
        Assert.Equal(expected, ErrorMessageExtractor.Describe(401, Json(body)));
    }

    [Theory]
    [InlineData("""{"detail":[{"loc":["body","questions","q","score","criteria",0],"msg":"Input should be a valid string"}]}""",
        "422 questions.q.score.criteria.0: Input should be a valid string")]
    [InlineData("""{"detail":[{"loc":["body"],"msg":"only body"}]}""", "422 only body")]
    [InlineData("""{"detail":[{"msg":"no location"}]}""", "422 no location")]
    [InlineData("""{"detail":[{"loc":["query","limit"],"msg":"too big"},{"loc":["body","x"],"msg":"missing"}]}""",
        "422 query.limit: too big; x: missing")]
    [InlineData("""{"detail":[{"loc":["a",null,true],"msg":"odd"}]}""", "422 a..true: odd")]
    [InlineData("""{"detail":[{"loc":"not-an-array","msg":"flat"}]}""", "422 flat")]
    [InlineData("""{"detail":[{"loc":["x"]},{"msg":"kept"}]}""", "422 kept")]
    public void Describe_ValidationErrors_JoinsPathAndMessage(string body, string expected)
    {
        Assert.Equal(expected, ErrorMessageExtractor.Describe(422, Json(body)));
    }

    [Theory]
    [InlineData("""{"unrelated":true}""", """422 {"unrelated":true}""")]
    [InlineData("[1,2]", "422 [1,2]")]
    [InlineData("null", "422 null")]
    [InlineData("42", "422 42")]
    [InlineData("""{"detail":[{"loc":["x"]}]}""", """422 {"detail":[{"loc":["x"]}]}""")]
    [InlineData("""{"detail":5}""", """422 {"detail":5}""")]
    [InlineData("""{"error":5}""", """422 {"error":5}""")]
    public void Describe_NoMessage_FallsBackToTheRawBody(string body, string expected)
    {
        Assert.Equal(expected, ErrorMessageExtractor.Describe(422, Json(body)));
    }

    [Fact]
    public void Describe_TextBody_UsesTheText()
    {
        Assert.Equal("502 Bad Gateway", ErrorMessageExtractor.Describe(502, ParsedBody.FromText("Bad Gateway")));
    }

    [Fact]
    public void Describe_NoBody_SaysSo()
    {
        Assert.Equal("500 status code (no body)", ErrorMessageExtractor.Describe(500, ParsedBody.None));
    }

    [Fact]
    public void Describe_LongRawBody_IsTruncatedToTwoHundredCharactersWithAnEllipsis()
    {
        string body = "{\"x\":\"" + new string('a', 300) + "\"}";

        string message = ErrorMessageExtractor.Describe(409, Json(body));

        Assert.Equal("409 " + body.Substring(0, 200) + "\u2026", message);
    }

    [Fact]
    public void Describe_RawBodyOfExactlyTwoHundredCharacters_IsNotTruncated()
    {
        string body = "[\"" + new string('b', 196) + "\"]";
        Assert.Equal(200, body.Length);

        Assert.Equal("409 " + body, ErrorMessageExtractor.Describe(409, Json(body)));
    }
}

public class ApiExceptionFactoryTests
{
    private static readonly DateTimeOffset s_now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(400, nameof(BadRequestException))]
    [InlineData(401, nameof(AuthenticationException))]
    [InlineData(403, nameof(PermissionDeniedException))]
    [InlineData(404, nameof(NotFoundException))]
    [InlineData(422, nameof(UnprocessableEntityException))]
    [InlineData(429, nameof(RateLimitException))]
    [InlineData(500, nameof(InternalServerException))]
    [InlineData(502, nameof(InternalServerException))]
    [InlineData(599, nameof(InternalServerException))]
    [InlineData(402, nameof(ApiException))]
    [InlineData(409, nameof(ApiException))]
    [InlineData(418, nameof(ApiException))]
    [InlineData(301, nameof(ApiException))]
    public void Create_Status_ReturnsTheMatchingExceptionType(int status, string expectedType)
    {
        RawResponse response = TypeSafeModelFactory.RawResponse(status, """{"error":"boom"}""");

        ApiException exception = ApiExceptionFactory.Create(response, ResponseBodyParser.Parse(response), s_now);

        Assert.Equal(expectedType, exception.GetType().Name);
        Assert.Equal(status, exception.StatusCode);
        Assert.Equal($"{status} boom", exception.Message);
    }

    [Fact]
    public void Create_RateLimit_ParsesRetryAfterRelativeToNow()
    {
        RawResponse response = TypeSafeModelFactory.RawResponse(
            429,
            "{}",
            [new("Retry-After", "Thu, 01 Jan 2026 00:00:10 GMT")]);

        var exception = Assert.IsType<RateLimitException>(ApiExceptionFactory.Create(response, ResponseBodyParser.Parse(response), s_now));

        Assert.Equal(TimeSpan.FromSeconds(10), exception.RetryAfter);
    }

    [Fact]
    public void Create_ExposesRequestIdHeadersAndBody()
    {
        RawResponse response = TypeSafeModelFactory.RawResponse(
            400,
            """{"detail":"Unknown model: x"}""",
            [new("x-typesafe-request-id", "req_42"), new("Content-Type", "application/json")]);

        ApiException exception = ApiExceptionFactory.Create(response, ResponseBodyParser.Parse(response), s_now);

        Assert.Equal("req_42", exception.RequestId);
        Assert.Equal("req_42", exception.Headers["X-TypeSafe-Request-Id"].Single());
        Assert.Equal("""{"detail":"Unknown model: x"}""", exception.BodyText);
        Assert.Equal("Unknown model: x", Assert.IsType<JsonElement>(exception.Body).GetProperty("detail").GetString());
    }
}

public class ResponseBodyParserTests
{
    [Fact]
    public void Parse_EmptyBody_IsNone()
    {
        Assert.Equal(ParsedBodyKind.None, ResponseBodyParser.Parse(TypeSafeModelFactory.RawResponse(200)).Kind);
    }

    [Fact]
    public void Parse_BomOnly_IsNone()
    {
        RawResponse response = RawResponseFactory.Create(
            200,
            new List<KeyValuePair<string, IEnumerable<string>>>(),
            new byte[] { 0xEF, 0xBB, 0xBF });

        Assert.Equal(ParsedBodyKind.None, ResponseBodyParser.Parse(response).Kind);
    }

    [Fact]
    public void Parse_Json_IsParsedEvenWithoutAContentType()
    {
        ParsedBody body = ResponseBodyParser.Parse(TypeSafeModelFactory.RawResponse(200, """{"a":1}"""));

        Assert.Equal(ParsedBodyKind.Json, body.Kind);
        Assert.Equal(1, body.Json.GetProperty("a").GetInt32());
        Assert.NotNull(body.AsJson());
    }

    [Fact]
    public void Parse_NonJson_IsKeptAsText()
    {
        ParsedBody body = ResponseBodyParser.Parse(TypeSafeModelFactory.RawResponse(502, "<html>Bad Gateway</html>"));

        Assert.Equal(ParsedBodyKind.Text, body.Kind);
        Assert.Equal("<html>Bad Gateway</html>", body.Text);
        Assert.Null(body.AsJson());
    }

    [Fact]
    public void Parse_TruncatedJson_IsKeptAsText()
    {
        Assert.Equal(ParsedBodyKind.Text, ResponseBodyParser.Parse(TypeSafeModelFactory.RawResponse(200, """{"a":""")).Kind);
    }

    [Fact]
    public void ToValue_ReturnsElementsStringsOrNull()
    {
        Assert.Null(ParsedBody.None.ToValue());
        Assert.Equal("text", ParsedBody.FromText("text").ToValue());
        Assert.Equal("json string", ResponseBodyParser.Parse(TypeSafeModelFactory.RawResponse(200, "\"json string\"")).ToValue());
        Assert.IsType<JsonElement>(ResponseBodyParser.Parse(TypeSafeModelFactory.RawResponse(200, "{}")).ToValue());
    }

    [Fact]
    public void ToDisplayString_DescribesEachKind()
    {
        Assert.Equal("(empty)", ParsedBody.None.ToDisplayString());
        Assert.Equal("text", ParsedBody.FromText("text").ToDisplayString());
        Assert.Equal("""{"a":1}""", ResponseBodyParser.Parse(TypeSafeModelFactory.RawResponse(200, """{ "a" : 1 }""")).ToDisplayString());
    }
}
