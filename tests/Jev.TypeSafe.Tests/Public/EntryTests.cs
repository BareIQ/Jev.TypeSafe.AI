using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Xunit;

namespace TypeSafe.AI.Tests.Public;

public class EntryTests
{
    [Fact]
    public void Default_IsOmitted()
    {
        Entry entry = default;

        Assert.True(entry.IsOmitted);
        Assert.Equal(EntryKind.Omitted, entry.Kind);
        Assert.Equal(Entry.Omitted, entry);
        Assert.Equal(string.Empty, entry.ToJsonString());
        Assert.Null(entry.AsText());
        Assert.Null(entry.ToJsonNode());
    }

    [Fact]
    public void Null_IsExplicitAndDistinctFromOmitted()
    {
        Assert.False(Entry.Null.IsOmitted);
        Assert.Equal(EntryKind.Null, Entry.Null.Kind);
        Assert.NotEqual(Entry.Omitted, Entry.Null);
        Assert.Equal("null", Entry.Null.ToJsonString());
        Assert.Null(Entry.Null.ToJsonNode());
    }

    [Fact]
    public void FromText_NullBecomesNull_AndTextKeepsItsValue()
    {
        Assert.Equal(Entry.Null, Entry.FromText(null));

        Entry entry = Entry.FromText("hello");
        Assert.Equal(EntryKind.Text, entry.Kind);
        Assert.Equal("hello", entry.AsText());
        Assert.Equal("hello", entry.ToJsonNode()!.GetValue<string>());
    }

    [Fact]
    public void FromText_Empty_IsTextNotNull()
    {
        Entry entry = Entry.FromText(string.Empty);

        Assert.Equal(EntryKind.Text, entry.Kind);
        Assert.Equal("\"\"", entry.ToJsonString());
    }

    [Theory]
    [InlineData("plain", "\"plain\"")]
    [InlineData("say \"hi\"", "\"say \\\"hi\\\"\"")]
    [InlineData("line\nbreak", "\"line\\nbreak\"")]
    [InlineData("héllo 你好", "\"héllo 你好\"")]
    public void ToJsonString_Text_IsQuotedAndEscaped(string text, string expected)
    {
        Assert.Equal(expected, Entry.FromText(text).ToJsonString());
        Assert.Equal(expected, Entry.FromText(text).ToString());
    }

    [Fact]
    public void FromJson_Node_AcceptsObjectsArraysStringsAndNull()
    {
        Assert.Equal(EntryKind.Object, Entry.FromJson(new JsonObject { ["a"] = 1 }).Kind);
        Assert.Equal(EntryKind.Array, Entry.FromJson(new JsonArray(1, 2)).Kind);
        Assert.Equal(EntryKind.Text, Entry.FromJson(JsonValue.Create("text")).Kind);
        Assert.Equal(EntryKind.Null, Entry.FromJson((JsonNode?)null).Kind);
        Assert.Equal("""{"a":1}""", Entry.FromJson(new JsonObject { ["a"] = 1 }).ToJsonString());
        Assert.Equal("[1,2]", Entry.FromJson(new JsonArray(1, 2)).ToJsonString());
    }

    [Theory]
    [InlineData(5)]
    [InlineData(true)]
    public void FromJson_NodeThatIsANumberOrBoolean_IsRejected(object value)
    {
        JsonNode node = value is int number ? JsonValue.Create(number) : JsonValue.Create((bool)value);

        var exception = Assert.Throws<ArgumentException>(() => Entry.FromJson(node));

        Assert.Contains("must be text, a JSON object, a JSON array, or null", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{"a":[1,2]}""", EntryKind.Object)]
    [InlineData("[1]", EntryKind.Array)]
    [InlineData("\"s\"", EntryKind.Text)]
    [InlineData("null", EntryKind.Null)]
    public void FromJson_Element_MapsValueKinds(string json, EntryKind expected)
    {
        using JsonDocument document = JsonDocument.Parse(json);

        Assert.Equal(expected, Entry.FromJson(document.RootElement).Kind);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("true")]
    [InlineData("false")]
    public void FromJson_ElementThatIsANumberOrBoolean_IsRejected(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);

        Assert.Throws<ArgumentException>(() => Entry.FromJson(document.RootElement));
    }

    [Fact]
    public void FromJson_UndefinedElement_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => Entry.FromJson(default(JsonElement)));
    }

    [Fact]
    public void FromJson_Element_IsDetachedFromItsDocument()
    {
        Entry entry;
        using (JsonDocument document = JsonDocument.Parse("""{"a":1}"""))
        {
            entry = Entry.FromJson(document.RootElement);
        }

        Assert.Equal("""{"a":1}""", entry.ToJsonString());
    }

    [Fact]
    public void FromObject_WithTypeInfo_SerializesWithoutReflectionOptions()
    {
        var typeInfo = (JsonTypeInfo<Dictionary<string, int>>)new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() }
            .GetTypeInfo(typeof(Dictionary<string, int>));

        Entry entry = Entry.FromObject(new Dictionary<string, int> { ["a"] = 1 }, typeInfo);

        Assert.Equal("""{"a":1}""", entry.ToJsonString());
    }

    [Fact]
    public void FromObject_Reflection_SerializesObjectsCollectionsAndStrings()
    {
        Assert.Equal("""{"name":"x","count":2}""", Entry.FromObject(new { name = "x", count = 2 }).ToJsonString());
        Assert.Equal(EntryKind.Array, Entry.FromObject(new[] { 1, 2 }).Kind);
        Assert.Equal("\"text\"", Entry.FromObject("text").ToJsonString());
        Assert.Equal("""{"Name":"y"}""", Entry.FromObject(new { Name = "y" }, new JsonSerializerOptions()).ToJsonString());
        Assert.Equal("""{"name":"y"}""", Entry.FromObject(new { Name = "y" }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }).ToJsonString());
    }

    [Fact]
    public void FromObject_ScalarThatIsNotText_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => Entry.FromObject(42));
    }

    [Fact]
    public void ToJsonNode_ReturnsAFreshCopyEachTime()
    {
        Entry entry = Entry.FromJson(new JsonObject { ["a"] = 1 });

        JsonObject first = Assert.IsType<JsonObject>(entry.ToJsonNode());
        first["a"] = 99;

        Assert.Equal(1, entry.ToJsonNode()!["a"]!.GetValue<int>());
        Assert.Equal("""{"a":1}""", entry.ToJsonString());
    }

    [Fact]
    public void Entry_IsASnapshotOfItsSource()
    {
        var source = new JsonObject { ["a"] = 1 };
        Entry entry = Entry.FromJson(source);

        source["a"] = 2;

        Assert.Equal("""{"a":1}""", entry.ToJsonString());
    }

    [Fact]
    public void ImplicitConversions_CreateEntries()
    {
        Entry text = "text";
        Entry fromNullString = (string?)null;
        Entry obj = new JsonObject();
        Entry array = new JsonArray();
        Entry fromNullObject = (JsonObject?)null;
        Entry fromNullArray = (JsonArray?)null;

        Assert.Equal(EntryKind.Text, text.Kind);
        Assert.Equal(EntryKind.Null, fromNullString.Kind);
        Assert.Equal(EntryKind.Object, obj.Kind);
        Assert.Equal(EntryKind.Array, array.Kind);
        Assert.Equal(EntryKind.Null, fromNullObject.Kind);
        Assert.Equal(EntryKind.Null, fromNullArray.Kind);
    }

    [Fact]
    public void Equality_IsStructuralOnKindAndJson()
    {
        Entry a = Entry.FromJson(new JsonObject { ["k"] = "v" });
        Entry b = Entry.FromJson(new JsonObject { ["k"] = "v" });
        Entry c = Entry.FromJson(new JsonObject { ["k"] = "w" });

        Assert.True(a == b);
        Assert.True(a != c);
        Assert.True(a.Equals(b));
        Assert.True(a.Equals((object)b));
        Assert.False(a.Equals("not an entry"));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(Entry.FromText("{}"), Entry.FromJson(new JsonObject()));
        Assert.Equal(Entry.Null.GetHashCode(), Entry.Null.GetHashCode());
    }
}
