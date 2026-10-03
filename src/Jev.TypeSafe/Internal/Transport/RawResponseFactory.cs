using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using TypeSafe.AI.Internal.Collections;

namespace TypeSafe.AI.Internal.Transport;

/// <summary>Creates <see cref="RawResponse"/> snapshots from HTTP responses.</summary>
internal static class RawResponseFactory
{
    public static RawResponse Create(HttpResponseMessage response, byte[] content)
    {
        IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers = response.Headers;
        if (response.Content is not null)
        {
            headers = headers.Concat(response.Content.Headers);
        }

        return Create((int)response.StatusCode, headers, content);
    }

    public static RawResponse Create(int statusCode, IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers, ReadOnlyMemory<byte> content)
    {
        var grouped = new List<KeyValuePair<string, IReadOnlyList<string>>>();
        var indexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, IEnumerable<string>> header in headers)
        {
            if (indexes.TryGetValue(header.Key, out int index))
            {
                grouped[index] = new(grouped[index].Key, [.. grouped[index].Value, .. header.Value]);
            }
            else
            {
                indexes.Add(header.Key, grouped.Count);
                grouped.Add(new(header.Key, [.. header.Value]));
            }
        }

        return new RawResponse(
            statusCode,
            new OrderedReadOnlyDictionary<string, IReadOnlyList<string>>(grouped, StringComparer.OrdinalIgnoreCase),
            content);
    }
}
