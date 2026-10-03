using System;
using System.Collections.Generic;

namespace TypeSafe.AI;

/// <summary>Per-request settings that override the client's.</summary>
public sealed class RequestOptions
{
    /// <summary>Gets or sets the timeout for each attempt of this request.</summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>Gets or sets the retry policy for this request, replacing the client's. Derive one with <c>client.RetryPolicy with { … }</c>.</summary>
    public RetryPolicy? RetryPolicy { get; set; }

    /// <summary>Gets additional headers, merged over the client's default headers. SDK headers cannot be overridden.</summary>
    /// <remarks>Content headers (for example <c>Content-Language</c>) are only sent on requests that have a body.</remarks>
    public IDictionary<string, string> Headers { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
