using System.Text.Json;

namespace TypeSafe.AI.Internal.Serialization;

/// <summary>Reads required values from response JSON, raising a descriptive error when the shape is unexpected.</summary>
internal readonly struct JsonShapeReader
{
    private readonly string _endpoint;

    public JsonShapeReader(string endpoint) => _endpoint = endpoint;

    public JsonElement RequireObject(JsonElement element, string path)
        => element.ValueKind == JsonValueKind.Object ? element : throw Unexpected($"an object at '{path}'");

    public JsonElement RequireProperty(JsonElement owner, string name, JsonValueKind kind, string path)
    {
        RequireObject(owner, path);
        string propertyPath = path.Length == 0 ? name : $"{path}.{name}";
        return owner.TryGetProperty(name, out JsonElement value) && value.ValueKind == kind
            ? value
            : throw Unexpected($"{Describe(kind)} at '{propertyPath}'");
    }

    public string RequireString(JsonElement owner, string name, string path)
        => RequireProperty(owner, name, JsonValueKind.String, path).GetString() ?? string.Empty;

    public double RequireDouble(JsonElement owner, string name, string path)
        => RequireProperty(owner, name, JsonValueKind.Number, path).GetDouble();

    public long RequireInt64(JsonElement owner, string name, string path)
    {
        JsonElement value = RequireProperty(owner, name, JsonValueKind.Number, path);
        return value.TryGetInt64(out long result) ? result : throw Unexpected($"an integer at '{path}.{name}'");
    }

    public double RequireDouble(JsonElement value, string path)
        => value.ValueKind == JsonValueKind.Number ? value.GetDouble() : throw Unexpected($"a number at '{path}'");

    public TypeSafeException Unexpected(string expectation)
        => new($"Unexpected response shape from {_endpoint}; expected {expectation}.");

    private static string Describe(JsonValueKind kind) => kind switch
    {
        JsonValueKind.Object => "an object",
        JsonValueKind.Array => "an array",
        JsonValueKind.String => "a string",
        JsonValueKind.Number => "a number",
        _ => kind.ToString(),
    };
}
