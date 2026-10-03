using System;
using System.Linq;
using TypeSafe.AI.Internal.Serialization;
using Xunit;

namespace TypeSafe.AI.Tests.Internal;

public class SystemOneResultReaderTests
{
    private const string FullResponse = """
        {
          "model": "jev-1",
          "answers": {
            "isBilling": {"type":"noul","noul":0.93},
            "tone": {"type":"choice","choice":"frustrated","confidence":0.7,"probabilities":{"calm":0.1,"frustrated":0.7,"angry":0.2}},
            "urgency": {"type":"score","score":1.7,"confidence":0.6,"legend":{"0":"can wait","1":{"label":"this week"},"2":null,"3":["today"]},"probabilities":{"0":0.1,"1":0.2,"2":0.1,"3":0.6}},
            "future": {"type":"ranking","order":[1,2]},
            "untyped": {"noul":1}
          },
          "usage": {"input_tokens": 12, "output_tokens": 9007199254740993}
        }
        """;

    private static SystemOneResult Read(string json) => SystemOneResultReader.Read(JsonText.ParseElement(json));

    [Fact]
    public void Read_FullResponse_ParsesEveryAnswerKind()
    {
        SystemOneResult result = Read(FullResponse);

        Assert.Equal("jev-1", result.Model);
        Assert.Equal(12, result.Usage.InputTokens);
        Assert.Equal(9007199254740993, result.Usage.OutputTokens);
        Assert.Equal(["isBilling", "tone", "urgency", "future", "untyped"], result.Answers.Keys);

        NoulAnswer noul = result.Answers.GetNoul("isBilling");
        Assert.Equal(0.93, noul.Noul);
        Assert.Equal("noul", noul.Type);

        ChoiceAnswer choice = result.Answers.GetChoice("tone");
        Assert.Equal("frustrated", choice.Choice);
        Assert.Equal(0.7, choice.Confidence);
        Assert.Equal(["calm", "frustrated", "angry"], choice.Probabilities.Keys);
        Assert.Equal(0.2, choice.Probabilities["angry"]);

        ScoreAnswer score = result.Answers.GetScore("urgency");
        Assert.Equal(1.7, score.Score);
        Assert.Equal(0.6, score.Confidence);
        Assert.Equal("can wait", score.Legend[0].AsText());
        Assert.Equal(EntryKind.Object, score.Legend[1].Kind);
        Assert.Equal(EntryKind.Null, score.Legend[2].Kind);
        Assert.Equal(EntryKind.Array, score.Legend[3].Kind);
        Assert.Equal(0.6, score.Probabilities[3]);
    }

    [Fact]
    public void Read_UnrecognizedOrMissingAnswerType_BecomesAnUnknownAnswerThatKeepsTheRawJson()
    {
        SystemOneResult result = Read(FullResponse);

        UnknownAnswer ranking = Assert.IsType<UnknownAnswer>(result.Answers["future"]);
        Assert.Equal("ranking", ranking.Type);
        Assert.Equal(2, ranking.Raw.GetProperty("order").GetArrayLength());

        UnknownAnswer untyped = Assert.IsType<UnknownAnswer>(result.Answers["untyped"]);
        Assert.Equal(string.Empty, untyped.Type);
    }

    [Fact]
    public void Read_AnswersRetainRawJsonAfterParsing()
    {
        SystemOneResult result = Read(FullResponse);

        Assert.Equal(0.93, result.Answers["isBilling"].Raw.GetProperty("noul").GetDouble());
    }

    [Fact]
    public void Read_NoAnswers_IsAnEmptySet()
    {
        SystemOneResult result = Read("""{"model":"m","answers":{},"usage":{"input_tokens":0,"output_tokens":0}}""");

        Assert.Empty(result.Answers);
    }

    [Fact]
    public void Read_ExtraTopLevelFields_AreIgnored()
    {
        SystemOneResult result = Read("""{"model":"m","answers":{},"usage":{"input_tokens":1,"output_tokens":2,"cached":3},"id":"abc"}""");

        Assert.Equal(2, result.Usage.OutputTokens);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("\"text\"")]
    [InlineData("""{"answers":{},"usage":{"input_tokens":1,"output_tokens":1}}""")]
    [InlineData("""{"model":5,"answers":{},"usage":{"input_tokens":1,"output_tokens":1}}""")]
    [InlineData("""{"model":"m","answers":[],"usage":{"input_tokens":1,"output_tokens":1}}""")]
    [InlineData("""{"model":"m","usage":{"input_tokens":1,"output_tokens":1}}""")]
    [InlineData("""{"model":"m","answers":{}}""")]
    [InlineData("""{"model":"m","answers":{},"usage":[]}""")]
    [InlineData("""{"model":"m","answers":{},"usage":{"input_tokens":"1","output_tokens":1}}""")]
    [InlineData("""{"model":"m","answers":{},"usage":{"input_tokens":1.5,"output_tokens":1}}""")]
    [InlineData("""{"model":"m","answers":{},"usage":{"input_tokens":1}}""")]
    [InlineData("""{"model":"m","answers":{"q":5},"usage":{"input_tokens":1,"output_tokens":1}}""")]
    [InlineData("""{"model":"m","answers":{"q":{"type":"noul"}},"usage":{"input_tokens":1,"output_tokens":1}}""")]
    [InlineData("""{"model":"m","answers":{"q":{"type":"noul","noul":"high"}},"usage":{"input_tokens":1,"output_tokens":1}}""")]
    [InlineData("""{"model":"m","answers":{"q":{"type":"choice","choice":"a","confidence":1}},"usage":{"input_tokens":1,"output_tokens":1}}""")]
    [InlineData("""{"model":"m","answers":{"q":{"type":"choice","choice":"a","confidence":1,"probabilities":{"a":"x"}}},"usage":{"input_tokens":1,"output_tokens":1}}""")]
    [InlineData("""{"model":"m","answers":{"q":{"type":"choice","choice":1,"confidence":1,"probabilities":{}}},"usage":{"input_tokens":1,"output_tokens":1}}""")]
    [InlineData("""{"model":"m","answers":{"q":{"type":"score","score":1,"confidence":1,"probabilities":{}}},"usage":{"input_tokens":1,"output_tokens":1}}""")]
    [InlineData("""{"model":"m","answers":{"q":{"type":"score","score":1,"confidence":1,"legend":{"a":"x"},"probabilities":{}}},"usage":{"input_tokens":1,"output_tokens":1}}""")]
    [InlineData("""{"model":"m","answers":{"q":{"type":"score","score":1,"confidence":1,"legend":{"0":5},"probabilities":{}}},"usage":{"input_tokens":1,"output_tokens":1}}""")]
    [InlineData("""{"model":"m","answers":{"q":{"type":"score","score":1,"confidence":1,"legend":{},"probabilities":{"-1":0.5}}},"usage":{"input_tokens":1,"output_tokens":1}}""")]
    [InlineData("""{"model":"m","answers":{"q":{"type":"score","score":1,"confidence":1,"legend":{},"probabilities":{"0":"x"}}},"usage":{"input_tokens":1,"output_tokens":1}}""")]
    public void Read_UnexpectedShape_ThrowsADescriptiveTypeSafeException(string json)
    {
        var exception = Assert.Throws<TypeSafeException>(() => Read(json));

        Assert.StartsWith("Unexpected response shape from POST /v1/systemone; expected ", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_NoBody_Throws()
    {
        Assert.Throws<TypeSafeException>(() => SystemOneResultReader.Read(null));
    }

    [Fact]
    public void Read_ShapeError_NamesThePath()
    {
        var exception = Assert.Throws<TypeSafeException>(
            () => Read("""{"model":"m","answers":{"tone":{"type":"choice","choice":"a","confidence":1}},"usage":{"input_tokens":1,"output_tokens":1}}"""));

        Assert.Contains("answers.tone.probabilities", exception.Message, StringComparison.Ordinal);
    }
}

public class ModelsReaderTests
{
    private static System.Collections.Generic.IReadOnlyList<ModelCard> Read(string json)
        => ModelsReader.Read(JsonText.ParseElement(json));

    [Fact]
    public void Read_Models_ParsesCardsInOrderAndKeepsExtraFields()
    {
        var cards = Read("""
            {"models":[
              {"name":"jev-1","description":"first","release_date":"2026-01-01"},
              {"name":"jev-2","description":"second","release_date":"2026-02-01","tags":["internal"],"context":128000}
            ]}
            """);

        Assert.Equal(["jev-1", "jev-2"], cards.Select(card => card.Name));
        Assert.Equal("second", cards[1].Description);
        Assert.Equal("2026-02-01", cards[1].ReleaseDate);
        Assert.Empty(cards[0].AdditionalProperties);
        Assert.Equal(["tags", "context"], cards[1].AdditionalProperties.Keys);
        Assert.Equal(128000, cards[1].AdditionalProperties["context"].GetInt32());
    }

    [Fact]
    public void Read_NoModels_ReturnsAnEmptyList()
    {
        Assert.Empty(Read("""{"models":[]}"""));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("""{"models":{"models":[]}}""")]
    [InlineData("""{"models":null}""")]
    [InlineData("""{"models":"bad"}""")]
    [InlineData("""{"ok":true}""")]
    [InlineData("""{"models":[5]}""")]
    [InlineData("""{"models":[{"name":"m","description":"d"}]}""")]
    [InlineData("""{"models":[{"name":1,"description":"d","release_date":"r"}]}""")]
    public void Read_UnexpectedShape_Throws(string json)
    {
        var exception = Assert.Throws<TypeSafeException>(() => Read(json));

        Assert.StartsWith("Unexpected response shape from GET /v1/models; expected ", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_NoBody_Throws()
    {
        Assert.Throws<TypeSafeException>(() => ModelsReader.Read(null));
    }
}
