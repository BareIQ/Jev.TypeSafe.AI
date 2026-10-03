using System.Collections.Generic;
using System.Linq;
using TypeSafe.AI.Internal.Http;
using Xunit;

namespace TypeSafe.AI.Tests.Internal;

public class HeaderMergerTests
{
    [Fact]
    public void Set_SameNameDifferentCase_LastValueWinsAndKeepsItsCasing()
    {
        var headers = new HeaderMerger().Set("X-Team", "one").Set("x-team", "two").Build();

        Assert.Equal([new KeyValuePair<string, string>("x-team", "two")], headers);
    }

    [Fact]
    public void Set_ReplacingAHeader_KeepsItsPosition()
    {
        var headers = new HeaderMerger().Set("A", "1").Set("B", "2").Set("a", "3").Build();

        Assert.Equal(["a", "B"], headers.Select(header => header.Key));
        Assert.Equal(["3", "2"], headers.Select(header => header.Value));
    }

    [Fact]
    public void Set_NullValue_RemovesTheHeaderCaseInsensitively()
    {
        var headers = new HeaderMerger().Set("Content-Type", "text/plain").Set("B", "2").Set("CONTENT-TYPE", null).Build();

        Assert.Equal([new KeyValuePair<string, string>("B", "2")], headers);
    }

    [Fact]
    public void Set_NullValueForMissingHeader_IsIgnored()
    {
        Assert.Empty(new HeaderMerger().Set("Missing", null).Build());
    }

    [Fact]
    public void SetAll_AppliesSourcesInOrder()
    {
        var headers = new HeaderMerger()
            .SetAll(new Dictionary<string, string> { ["A"] = "1", ["B"] = "1" })
            .SetAll(new Dictionary<string, string> { ["b"] = "2", ["C"] = "2" })
            .Build();

        Assert.Equal(["A=1", "b=2", "C=2"], headers.Select(header => $"{header.Key}={header.Value}"));
    }

    [Fact]
    public void SetAll_Null_IsIgnored()
    {
        Assert.Empty(new HeaderMerger().SetAll(null).Build());
    }

    [Fact]
    public void Build_ReturnsASnapshot()
    {
        var merger = new HeaderMerger().Set("A", "1");
        var snapshot = merger.Build();

        merger.Set("B", "2");

        Assert.Single(snapshot);
    }
}

public class HeaderRedactorTests
{
    [Theory]
    [InlineData("Authorization", "Bearer sk-test-1234567890", "Bearer ***7890")]
    [InlineData("authorization", "Bearer sk-test-1234567890", "Bearer ***7890")]
    [InlineData("Proxy-Authorization", "Basic abcdefghijkl", "Basic ***ijkl")]
    [InlineData("X-Api-Key", "abcdefghijk", "***hijk")]
    [InlineData("Authorization", "Bearer 12345678", "Bearer ***")]
    [InlineData("Authorization", "Bearer 123456789", "Bearer ***6789")]
    [InlineData("Authorization", "Bearer short", "Bearer ***")]
    [InlineData("Authorization", "short", "***")]
    [InlineData("Authorization", "Bearer", "***")]
    [InlineData("Authorization", "Bearer   spaced-secret-value", "Bearer ***alue")]
    [InlineData("Cookie", "session=abc", "***")]
    [InlineData("Set-Cookie", "session=abc; HttpOnly", "***")]
    [InlineData("Accept", "application/json", "application/json")]
    [InlineData("X-TypeSafe-SDK", "typesafe-sdk-dotnet/1.0.0", "typesafe-sdk-dotnet/1.0.0")]
    public void Redact_HeaderValue_MasksCredentialsOnly(string name, string value, string expected)
    {
        Assert.Equal(expected, HeaderRedactor.Redact(name, value));
    }

    [Fact]
    public void Redact_Collection_ReturnsMaskedCopyAndDoesNotModifyInput()
    {
        var input = new List<KeyValuePair<string, string>>
        {
            new("Authorization", "Bearer sk-test-1234567890"),
            new("Accept", "application/json"),
        };

        var redacted = HeaderRedactor.Redact(input);

        Assert.Equal("Bearer ***7890", redacted[0].Value);
        Assert.Equal("application/json", redacted[1].Value);
        Assert.Equal("Bearer sk-test-1234567890", input[0].Value);
    }
}
