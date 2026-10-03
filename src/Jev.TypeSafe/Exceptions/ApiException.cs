using System;
using System.Collections.Generic;
using TypeSafe.AI.Internal.Transport;
using TypeSafe.AI.Internal.Validation;

namespace TypeSafe.AI;

/// <summary>An unsuccessful HTTP response from the API. Status-specific subclasses exist for common codes.</summary>
public class ApiException : TypeSafeException
{
    private readonly ParsedBody _body;

    /// <summary>Initializes a new instance of the <see cref="ApiException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="response">The HTTP response.</param>
    public ApiException(string message, RawResponse response)
        : this(message, response, null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ApiException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="response">The HTTP response.</param>
    /// <param name="innerException">The underlying cause, if any.</param>
    public ApiException(string message, RawResponse response, Exception? innerException)
        : base(message, innerException)
    {
        Response = Guard.NotNull(response);
        _body = ResponseBodyParser.Parse(response);
    }

    /// <summary>Gets the buffered HTTP response.</summary>
    public RawResponse Response { get; }

    /// <summary>Gets the HTTP status code.</summary>
    public int StatusCode => Response.StatusCode;

    /// <summary>Gets the HTTP response headers.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Headers => Response.Headers;

    /// <summary>
    /// Gets the parsed body: a <see cref="System.Text.Json.JsonElement"/> for JSON, a <see cref="string"/> for text
    /// or JSON strings, or <see langword="null"/> for an empty body.
    /// </summary>
    public object? Body => _body.ToValue();

    /// <summary>Gets the body text, or <see langword="null"/> for an empty body.</summary>
    public string? BodyText => _body.Kind == ParsedBodyKind.None ? null : Response.ReadContentAsString();

    /// <summary>Gets the request ID from the <c>x-typesafe-request-id</c> header, or <see langword="null"/>.</summary>
    public string? RequestId => Response.RequestId;
}
