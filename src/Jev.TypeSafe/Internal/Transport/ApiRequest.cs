using System.Net.Http;
using TypeSafe.AI.Internal.Configuration;

namespace TypeSafe.AI.Internal.Transport;

/// <summary>One logical API call: method, path, serialized body, and resolved options.</summary>
internal sealed class ApiRequest
{
    public ApiRequest(HttpMethod method, string path, byte[]? body, ResolvedRequest options)
    {
        Method = method;
        Path = path;
        Body = body;
        Options = options;
    }

    public HttpMethod Method { get; }

    public string Path { get; }

    /// <summary>Gets the UTF-8 JSON body, or <see langword="null"/> for requests without one.</summary>
    public byte[]? Body { get; }

    public ResolvedRequest Options { get; }
}
