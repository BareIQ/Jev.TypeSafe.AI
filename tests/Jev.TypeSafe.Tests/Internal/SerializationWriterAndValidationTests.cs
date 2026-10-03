using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using TypeSafe.AI.Internal.Collections;
using TypeSafe.AI.Internal.Serialization;
using TypeSafe.AI.Internal.Validation;
using Xunit;

namespace TypeSafe.AI.Tests.Internal;

public class SystemOneRequestWriterTests
{
    private static string Write(SystemOneRequest request, string model = "jev-latest")
        => System.Text.Encoding.UTF8.GetString(SystemOneRequestWriter.Write(request, model));

    private static SystemOneRequest Request(Entry state, Action<QuestionSet> configure)
    {
        var questions = new QuestionSet();
        configure(questions);
        return new SystemOneRequest(state, questions);
    }

    [Fact]
    public void Write_AllQuestionTypes_ProducesTheExactWireFormat()
    {
        var request = Request(
            new JsonObject { ["subject"] = "Charged twice" },
            questions =>
            {
                questions.Add("isBilling", Question.Noul("Is this about billing?", new NoulCriteria { True = new JsonObject { ["meaning"] = "money" } }));
                questions.Add("tone", Question.Choice("Tone?", new ChoiceCriteria { { "calm", "relaxed" }, { "angry", Entry.Null } }));
                questions.Add("urgency", Question.Score("How urgent?", "can wait", "this week", "today"));
            });

        Assert.Equal(
            """{"state":{"subject":"Charged twice"},"questions":{"isBilling":{"type":"noul","instructions":"Is this about billing?","criteria":{"true":{"meaning":"money"}}},"tone":{"type":"choice","instructions":"Tone?","criteria":{"calm":"relaxed","angry":null}},"urgency":{"type":"score","instructions":"How urgent?","criteria":["can wait","this week","today"]}},"model":"jev-latest"}""",
            Write(request));
    }

    [Fact]
    public void Write_NoulWithoutArguments_SendsNullInstructionsAndOmitsCriteria()
    {
        var request = Request("s", q => q.Add("q", Question.Noul()));

        Assert.Contains("""q":{"type":"noul","instructions":null}""", Write(request), StringComparison.Ordinal);
    }

    [Fact]
    public void Write_NoulCriteria_OneSidedBothSidedAndEmpty()
    {
        var request = Request("s", q =>
        {
            q.Add("yes", Question.Noul("?", new NoulCriteria { True = "described" }));
            q.Add("no", Question.Noul("?", new NoulCriteria { False = Entry.Null }));
            q.Add("both", Question.Noul("?", new NoulCriteria { True = "t", False = "f" }));
            q.Add("none", Question.Noul("?", new NoulCriteria()));
        });

        string json = Write(request);

        Assert.Contains("""yes":{"type":"noul","instructions":"?","criteria":{"true":"described"}}""", json, StringComparison.Ordinal);
        Assert.Contains("""no":{"type":"noul","instructions":"?","criteria":{"false":null}}""", json, StringComparison.Ordinal);
        Assert.Contains("""both":{"type":"noul","instructions":"?","criteria":{"true":"t","false":"f"}}""", json, StringComparison.Ordinal);
        Assert.Contains("""none":{"type":"noul","instructions":"?","criteria":{}}""", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_OmittedInstructions_AreLeftOutButExplicitNullIsKept()
    {
        var request = Request("s", q =>
        {
            q.Add("omitted", Question.Choice(default, ChoiceCriteria.FromLabels("a", "b")));
            q.Add("explicit", Question.Choice(Entry.Null, ChoiceCriteria.FromLabels("a", "b")));
        });

        string json = Write(request);

        Assert.Contains("""omitted":{"type":"choice","criteria":{"a":null,"b":null}}""", json, StringComparison.Ordinal);
        Assert.Contains("""explicit":{"type":"choice","instructions":null,"criteria":{"a":null,"b":null}}""", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_ScoreCriteria_AllowsNullAndRichEntriesInOrder()
    {
        var request = Request("s", q => q.Add("q", Question.Score("?", "low", Entry.Null, new JsonArray("a", "b"), new JsonObject { ["k"] = "v" })));

        Assert.Contains("""criteria":["low",null,["a","b"],{"k":"v"}]""", Write(request), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("hello", "\"hello\"")]
    [InlineData(null, "null")]
    public void Write_State_TextAndNull(string? text, string expected)
    {
        var request = Request(Entry.FromText(text), q => q.Add("q", Question.Noul()));

        Assert.StartsWith($"{{\"state\":{expected},\"questions\"", Write(request), StringComparison.Ordinal);
    }

    [Fact]
    public void Write_StateArray_IsWrittenAsAnArray()
    {
        var request = Request(new JsonArray(1, "two", null), q => q.Add("q", Question.Noul()));

        Assert.StartsWith("""{"state":[1,"two",null],"questions""", Write(request), StringComparison.Ordinal);
    }

    [Fact]
    public void Write_QuestionNamesAndLabels_AreSentVerbatimInInsertionOrder()
    {
        var request = Request("s", q =>
        {
            q.Add("zeta", Question.Noul());
            q.Add("__proto__", Question.Noul());
            q.Add("constructor", Question.Noul());
            q.Add("", Question.Noul());
            q.Add("with \"quotes\" and \n newline", Question.Noul());
        });

        using JsonDocument document = JsonDocument.Parse(Write(request));
        string[] names = document.RootElement.GetProperty("questions").EnumerateObject().Select(p => p.Name).ToArray();

        Assert.Equal(["zeta", "__proto__", "constructor", "", "with \"quotes\" and \n newline"], names);
    }

    [Fact]
    public void Write_NonAsciiText_IsNotEscapedWithinTheBasicMultilingualPlane()
    {
        var request = Request("héllo — 你好", q => q.Add("q", Question.Noul("¿Qué?")));

        string json = Write(request);

        Assert.Contains("héllo — 你好", json, StringComparison.Ordinal);
        Assert.Contains("¿Qué?", json, StringComparison.Ordinal);
        using JsonDocument document = JsonDocument.Parse(json);
        Assert.Equal("héllo — 你好", document.RootElement.GetProperty("state").GetString());
    }

    [Fact]
    public void Write_CharactersOutsideTheBasicMultilingualPlane_RoundTripEvenIfEscaped()
    {
        var request = Request("emoji 😀", q => q.Add("q", Question.Noul()));

        using JsonDocument document = JsonDocument.Parse(Write(request));

        Assert.Equal("emoji 😀", document.RootElement.GetProperty("state").GetString());
    }

    [Fact]
    public void Write_SpecialCharacters_AreEscapedAndRoundTrip()
    {
        string text = "quote\" backslash\\ tab\t newline\n <tag> & 'single'";
        var request = Request(text, q => q.Add("q", Question.Noul()));

        using JsonDocument document = JsonDocument.Parse(Write(request));

        Assert.Equal(text, document.RootElement.GetProperty("state").GetString());
    }

    [Fact]
    public void Write_ExplicitModel_ReplacesTheDefault()
    {
        var request = Request("s", q => q.Add("q", Question.Noul()));

        Assert.EndsWith(""","model":"jev-2"}""", Write(request, "jev-2"), StringComparison.Ordinal);
    }

    [Fact]
    public void Write_AdditionalProperties_FollowModelIncludingNullAndNested()
    {
        SystemOneRequest request = Request("s", q => q.Add("q", Question.Noul()));
        request.AdditionalProperties["future_option"] = null;
        request.AdditionalProperties["nested"] = new JsonObject { ["enabled"] = true, ["n"] = 3 };
        request.AdditionalProperties["list"] = new JsonArray(1, 2);

        Assert.EndsWith(""","model":"jev-latest","future_option":null,"nested":{"enabled":true,"n":3},"list":[1,2]}""", Write(request), StringComparison.Ordinal);
    }

    [Fact]
    public void Write_DoesNotMutateCallerObjects()
    {
        var state = new JsonObject { ["a"] = 1 };
        SystemOneRequest request = Request(state, q => q.Add("q", Question.Noul()));
        request.AdditionalProperties["extra"] = new JsonObject { ["x"] = 1 };

        string first = Write(request);
        string second = Write(request);

        Assert.Equal(first, second);
        Assert.Equal("""{"a":1}""", state.ToJsonString());
    }

    [Fact]
    public void Write_StateIsASnapshotOfTheSourceNode()
    {
        var state = new JsonObject { ["a"] = 1 };
        SystemOneRequest request = Request(state, q => q.Add("q", Question.Noul()));

        state["a"] = 2;
        state["b"] = true;

        Assert.StartsWith("""{"state":{"a":1},""", Write(request), StringComparison.Ordinal);
    }
}

public class QuestionValidatorTests
{
    private static SystemOneRequest Request(Action<QuestionSet>? configure = null)
    {
        var questions = new QuestionSet();
        configure?.Invoke(questions);
        return new SystemOneRequest("state", questions);
    }

    [Fact]
    public void Validate_ValidRequest_Passes()
    {
        QuestionValidator.Validate(Request(q =>
        {
            q.Add("a", Question.Noul());
            q.Add("b", Question.Score("?", "low", "high"));
        }));
    }

    [Fact]
    public void Validate_NoQuestions_Throws()
    {
        var exception = Assert.Throws<TypeSafeException>(() => QuestionValidator.Validate(Request()));

        Assert.Equal("At least one question is required.", exception.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Validate_ScoreWithFewerThanTwoCriteria_ThrowsNamingTheQuestion(int count)
    {
        SystemOneRequest request = Request(q => q.Add("urgency", Question.Score("?", Enumerable.Repeat((Entry)"x", count).ToArray())));

        var exception = Assert.Throws<TypeSafeException>(() => QuestionValidator.Validate(request));

        Assert.Equal($"Score question \"urgency\" has {count} criteria; at least two scores are required.", exception.Message);
    }

    [Theory]
    [InlineData("state")]
    [InlineData("questions")]
    [InlineData("model")]
    public void Validate_ReservedAdditionalProperty_Throws(string name)
    {
        SystemOneRequest request = Request(q => q.Add("a", Question.Noul()));
        request.AdditionalProperties[name] = "x";

        var exception = Assert.Throws<ArgumentException>(() => QuestionValidator.Validate(request));

        Assert.Contains($"'{name}'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_NonReservedAdditionalProperty_Passes()
    {
        SystemOneRequest request = Request(q => q.Add("a", Question.Noul()));
        request.AdditionalProperties["Model"] = "case differs";
        request.AdditionalProperties["other"] = null;

        QuestionValidator.Validate(request);
    }
}

public class OrderedReadOnlyDictionaryTests
{
    [Fact]
    public void Enumeration_FollowsInsertionOrder()
    {
        var dictionary = new OrderedReadOnlyDictionary<string, int>([new("b", 2), new("a", 1), new("c", 3)]);

        Assert.Equal(["b", "a", "c"], dictionary.Keys);
        Assert.Equal([2, 1, 3], dictionary.Values);
        Assert.Equal(["b", "a", "c"], dictionary.Select(pair => pair.Key));
        Assert.Equal(3, dictionary.Count);
    }

    [Fact]
    public void DuplicateKeys_KeepTheFirstPositionAndTheLastValue()
    {
        var dictionary = new OrderedReadOnlyDictionary<string, int>([new("a", 1), new("b", 2), new("a", 3)]);

        Assert.Equal(["a=3", "b=2"], dictionary.Select(pair => $"{pair.Key}={pair.Value}"));
    }

    [Fact]
    public void Lookup_FoundAndMissing()
    {
        var dictionary = new OrderedReadOnlyDictionary<string, int>([new("a", 1)]);

        Assert.True(dictionary.ContainsKey("a"));
        Assert.False(dictionary.ContainsKey("z"));
        Assert.True(dictionary.TryGetValue("a", out int value));
        Assert.Equal(1, value);
        Assert.False(dictionary.TryGetValue("z", out int missing));
        Assert.Equal(0, missing);
        Assert.Equal(1, dictionary["a"]);
        Assert.Throws<KeyNotFoundException>(() => dictionary["z"]);
    }

    [Fact]
    public void CustomComparer_IsUsed()
    {
        var dictionary = new OrderedReadOnlyDictionary<string, int>([new("Key", 1)], StringComparer.OrdinalIgnoreCase);

        Assert.True(dictionary.ContainsKey("KEY"));
        Assert.Equal(1, dictionary["key"]);
    }

    [Fact]
    public void Empty_HasNoItems()
    {
        Assert.Empty(OrderedReadOnlyDictionary<string, int>.Empty);
    }
}
