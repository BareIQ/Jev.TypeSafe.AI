using System;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TypeSafe.AI.Internal.Serialization;

/// <summary>Helpers that produce compact JSON text without reflection.</summary>
internal static class JsonText
{
    /// <summary>
    /// Compact output that leaves non-ASCII text unescaped, as <c>JSON.stringify</c> does. The relaxed encoder
    /// is safe here because the text is sent as an API body and logged, never embedded in HTML.
    /// </summary>
    public static JsonWriterOptions WriterOptions { get; } = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Re-serializes an element compactly (as <c>JSON.stringify</c> would).</summary>
    public static string Compact(JsonElement element) => Build(element.WriteTo);

    /// <summary>Serializes a node compactly.</summary>
    public static string Compact(JsonNode node) => Build(writer => node.WriteTo(writer));

    /// <summary>Encodes a string as a JSON string literal.</summary>
    public static string Quote(string value) => Build(writer => writer.WriteStringValue(value));

    /// <summary>Parses JSON text into a standalone element that needs no disposal.</summary>
    public static JsonElement ParseElement(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    /// <summary>Runs a writer callback and returns the compact UTF-8 JSON it produced as text.</summary>
    public static string Build(Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            write(writer);
        }

        return Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length);
    }
}
