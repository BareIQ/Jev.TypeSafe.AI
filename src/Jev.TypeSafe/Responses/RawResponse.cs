using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using TypeSafe.AI.Internal.Http;
using TypeSafe.AI.Internal.Validation;

namespace TypeSafe.AI;

/// <summary>An HTTP response with its full body buffered in memory.</summary>
public sealed class RawResponse
{
    internal RawResponse(int statusCode, IReadOnlyDictionary<string, IReadOnlyList<string>> headers, ReadOnlyMemory<byte> content)
    {
        StatusCode = statusCode;
        Headers = headers;
        Content = content;
        RequestId = TryGetHeader(SdkHeaders.RequestId, out string? requestId) ? requestId : null;
    }

    /// <summary>Gets the HTTP status code.</summary>
    public int StatusCode { get; }

    /// <summary>Gets response and content headers, with case-insensitive names.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Headers { get; }

    /// <summary>Gets the response body.</summary>
    public ReadOnlyMemory<byte> Content { get; }

    /// <summary>Gets the request ID from the <c>x-typesafe-request-id</c> header, or <see langword="null"/>.</summary>
    public string? RequestId { get; }

    /// <summary>Gets a header value; multiple values are joined with <c>", "</c>.</summary>
    /// <param name="name">The header name, compared case-insensitively.</param>
    /// <param name="value">The value, when present.</param>
    /// <returns><see langword="true"/> when the header is present.</returns>
    public bool TryGetHeader(string name, [NotNullWhen(true)] out string? value)
    {
        if (Headers.TryGetValue(Guard.NotNull(name), out IReadOnlyList<string>? values))
        {
            value = string.Join(", ", values);
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>Decodes the body using the <c>Content-Type</c> charset, or UTF-8. A leading byte order mark is removed.</summary>
    /// <returns>The body text.</returns>
    public string ReadContentAsString()
    {
        Encoding encoding = GetEncoding();
        string text = MemoryMarshal.TryGetArray(Content, out ArraySegment<byte> segment) && segment.Array is not null
            ? encoding.GetString(segment.Array, segment.Offset, segment.Count)
            : encoding.GetString(Content.ToArray());
        return text.Length > 0 && text[0] == '\uFEFF' ? text.Substring(1) : text;
    }

    /// <summary>Parses the body as JSON.</summary>
    /// <returns>A document the caller must dispose.</returns>
    /// <exception cref="JsonException">The body is not valid JSON.</exception>
    public JsonDocument ParseContentAsJson() => JsonDocument.Parse(Content);

    private Encoding GetEncoding()
    {
        if (TryGetHeader("Content-Type", out string? contentType)
            && MediaTypeHeaderValue.TryParse(contentType, out MediaTypeHeaderValue? mediaType)
            && mediaType.CharSet is { Length: > 0 } charSet)
        {
            try
            {
                return Encoding.GetEncoding(charSet.Trim('"'));
            }
            catch (ArgumentException)
            {
                // Unknown charsets fall back to UTF-8, the JSON default.
            }
        }

        return Encoding.UTF8;
    }
}
