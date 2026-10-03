using System.Text.Json;
using TypeSafe.AI.Internal.Serialization;

namespace TypeSafe.AI.Internal.Transport;

/// <summary>The kind of a parsed response body.</summary>
internal enum ParsedBodyKind
{
    None = 0,
    Json,
    Text,
}

/// <summary>A response body parsed leniently: empty, JSON, or text that is not valid JSON.</summary>
internal readonly struct ParsedBody
{
    private ParsedBody(ParsedBodyKind kind, JsonElement json, string? text)
    {
        Kind = kind;
        Json = json;
        Text = text;
    }

    public static ParsedBody None => default;

    public ParsedBodyKind Kind { get; }

    /// <summary>Gets the JSON value when <see cref="Kind"/> is <see cref="ParsedBodyKind.Json"/>.</summary>
    public JsonElement Json { get; }

    /// <summary>Gets the text when <see cref="Kind"/> is <see cref="ParsedBodyKind.Text"/>.</summary>
    public string? Text { get; }

    public static ParsedBody FromJson(JsonElement json) => new(ParsedBodyKind.Json, json, null);

    public static ParsedBody FromText(string text) => new(ParsedBodyKind.Text, default, text);

    /// <summary>Returns the JSON value, or <see langword="null"/> when the body is empty or not JSON.</summary>
    public JsonElement? AsJson() => Kind == ParsedBodyKind.Json ? Json : null;

    /// <summary>Returns the body as a JSON element, a string (for text or JSON strings), or <see langword="null"/>.</summary>
    public object? ToValue() => Kind switch
    {
        ParsedBodyKind.Json when Json.ValueKind == JsonValueKind.String => Json.GetString(),
        ParsedBodyKind.Json => Json,
        ParsedBodyKind.Text => Text,
        _ => null,
    };

    /// <summary>Renders the body for diagnostics.</summary>
    public string ToDisplayString() => Kind switch
    {
        ParsedBodyKind.Json => JsonText.Compact(Json),
        ParsedBodyKind.Text => Text ?? string.Empty,
        _ => "(empty)",
    };
}
