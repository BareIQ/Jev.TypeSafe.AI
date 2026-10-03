using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json;
using TypeSafe.AI.Internal.Serialization;
using TypeSafe.AI.Internal.Transport;

namespace TypeSafe.AI.Internal.Errors;

/// <summary>Builds readable messages for unsuccessful responses.</summary>
internal static class ErrorMessageExtractor
{
    private const int MaxRawBodyLength = 200;
    private const string Ellipsis = "\u2026";

    /// <summary>
    /// Formats <c>"{status} {detail}"</c> using the first message found in a text, error, or validation body,
    /// falling back to the raw body truncated to 200 characters.
    /// </summary>
    public static string Describe(int statusCode, ParsedBody body)
    {
        string? detail = ExtractMessage(body);
        if (!string.IsNullOrEmpty(detail))
        {
            return $"{statusCode} {detail}";
        }

        if (body.Kind == ParsedBodyKind.None)
        {
            return $"{statusCode} status code (no body)";
        }

        string raw = body.ToDisplayString();
        return $"{statusCode} {(raw.Length > MaxRawBodyLength ? raw.Substring(0, MaxRawBodyLength) + Ellipsis : raw)}";
    }

    private static string? ExtractMessage(ParsedBody body)
    {
        if (body.Kind == ParsedBodyKind.Text)
        {
            return body.Text;
        }

        if (body.Kind != ParsedBodyKind.Json)
        {
            return null;
        }

        JsonElement json = body.Json;
        return json.ValueKind switch
        {
            JsonValueKind.String => json.GetString(),
            JsonValueKind.Object => ExtractFromObject(json),
            _ => null,
        };
    }

    private static string? ExtractFromObject(JsonElement body)
    {
        if (TryGetString(body, "error", out string? error))
        {
            return error;
        }

        if (body.TryGetProperty("error", out JsonElement errorObject) && TryGetString(errorObject, "message", out string? errorMessage))
        {
            return errorMessage;
        }

        if (TryGetString(body, "message", out string? message))
        {
            return message;
        }

        if (!body.TryGetProperty("detail", out JsonElement detail))
        {
            return null;
        }

        return detail.ValueKind switch
        {
            JsonValueKind.String => detail.GetString(),
            JsonValueKind.Object => TryGetString(detail, "message", out string? detailMessage) ? detailMessage : null,
            JsonValueKind.Array => DescribeValidationErrors(detail),
            _ => null,
        };
    }

    /// <summary>Formats validation errors as semicolon-separated <c>path: message</c> entries.</summary>
    private static string? DescribeValidationErrors(JsonElement errors)
    {
        var parts = new List<string>();
        foreach (JsonElement error in errors.EnumerateArray())
        {
            if (!TryGetString(error, "msg", out string? msg))
            {
                continue;
            }

            string location = error.TryGetProperty("loc", out JsonElement loc) && loc.ValueKind == JsonValueKind.Array
                ? string.Join(".", loc.EnumerateArray().Where(segment => !IsBodySegment(segment)).Select(FormatSegment))
                : string.Empty;
            parts.Add(location.Length > 0 ? $"{location}: {msg}" : msg);
        }

        return parts.Count > 0 ? string.Join("; ", parts) : null;
    }

    private static bool IsBodySegment(JsonElement segment)
        => segment.ValueKind == JsonValueKind.String && segment.GetString() == "body";

    /// <summary>Formats a location segment as JavaScript's <c>Array.prototype.join</c> would.</summary>
    private static string FormatSegment(JsonElement segment) => segment.ValueKind switch
    {
        JsonValueKind.String => segment.GetString() ?? string.Empty,
        JsonValueKind.Null => string.Empty,
        _ => JsonText.Compact(segment),
    };

    private static bool TryGetString(JsonElement element, string propertyName, [NotNullWhen(true)] out string? value)
    {
        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out JsonElement property)
            && property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString() ?? string.Empty;
            return true;
        }

        value = null;
        return false;
    }
}
