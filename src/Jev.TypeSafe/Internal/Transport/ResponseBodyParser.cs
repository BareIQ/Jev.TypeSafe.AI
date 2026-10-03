using System.Text.Json;
using TypeSafe.AI.Internal.Serialization;

namespace TypeSafe.AI.Internal.Transport;

/// <summary>Parses response bodies leniently, because servers and proxies do not always set a content type.</summary>
internal static class ResponseBodyParser
{
    /// <summary>Returns <see cref="ParsedBody.None"/> for an empty body, JSON when it parses, and text otherwise.</summary>
    public static ParsedBody Parse(RawResponse response)
    {
        if (response.Content.IsEmpty)
        {
            return ParsedBody.None;
        }

        string text = response.ReadContentAsString();
        if (text.Length == 0)
        {
            return ParsedBody.None;
        }

        try
        {
            return ParsedBody.FromJson(JsonText.ParseElement(text));
        }
        catch (JsonException)
        {
            return ParsedBody.FromText(text);
        }
    }
}
