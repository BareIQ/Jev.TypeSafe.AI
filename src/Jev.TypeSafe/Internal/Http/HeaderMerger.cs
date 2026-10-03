using System;
using System.Collections.Generic;

namespace TypeSafe.AI.Internal.Http;

/// <summary>
/// Merges headers case-insensitively. A later value replaces an earlier one in place and keeps the
/// later name's casing; a <see langword="null"/> value removes the header.
/// </summary>
internal sealed class HeaderMerger
{
    private readonly List<KeyValuePair<string, string>> _headers = [];

    public HeaderMerger Set(string name, string? value)
    {
        int index = _headers.FindIndex(header => string.Equals(header.Key, name, StringComparison.OrdinalIgnoreCase));
        if (value is null)
        {
            if (index >= 0)
            {
                _headers.RemoveAt(index);
            }
        }
        else if (index >= 0)
        {
            _headers[index] = new KeyValuePair<string, string>(name, value);
        }
        else
        {
            _headers.Add(new KeyValuePair<string, string>(name, value));
        }

        return this;
    }

    public HeaderMerger SetAll(IEnumerable<KeyValuePair<string, string>>? headers)
    {
        if (headers is not null)
        {
            foreach (KeyValuePair<string, string> header in headers)
            {
                Set(header.Key, header.Value);
            }
        }

        return this;
    }

    public IReadOnlyList<KeyValuePair<string, string>> Build() => [.. _headers];
}
