using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using TypeSafe.AI.Internal.Configuration;
using TypeSafe.AI.Internal.Http;

namespace TypeSafe.AI.Internal.Transport;

/// <summary>Builds the HTTP message for each attempt. SDK headers always override caller headers.</summary>
internal sealed class RequestFactory
{
    private readonly ClientConfiguration _configuration;
    private readonly string _authorization;

    public RequestFactory(ClientConfiguration configuration)
    {
        _configuration = configuration;
        _authorization = "Bearer " + configuration.ApiKey;
    }

    public Uri BuildUri(ApiRequest request) => new(_configuration.BaseUrl + request.Path, UriKind.Absolute);

    /// <summary>Returns the headers for an attempt: caller headers, then the protected SDK headers.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> BuildHeaders(ApiRequest request, int attempt)
        => new HeaderMerger()
            .SetAll(request.Options.Headers)
            .Set(SdkHeaders.Authorization, _authorization)
            .Set(SdkHeaders.Accept, SdkHeaders.JsonMediaType)
            .Set(SdkHeaders.UserAgent, SdkHeaders.Identity)
            .Set(SdkHeaders.Sdk, SdkHeaders.Identity)
            .Set(SdkHeaders.Runtime, RuntimeDescriptor.Current)
            .Set(SdkHeaders.ContentType, request.Body is null ? null : SdkHeaders.JsonMediaType)
            .Set(SdkHeaders.RetryCount, attempt == 0 ? null : attempt.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Build();

    /// <summary>Creates a new message; messages cannot be re-sent, so each attempt needs its own.</summary>
    public static HttpRequestMessage Create(ApiRequest request, Uri uri, IReadOnlyList<KeyValuePair<string, string>> headers)
    {
        var message = new HttpRequestMessage(request.Method, uri);
        try
        {
            if (request.Body is not null)
            {
                message.Content = new ByteArrayContent(request.Body);
            }

            foreach (KeyValuePair<string, string> header in headers)
            {
                AddHeader(message, header.Key, header.Value);
            }

            return message;
        }
        catch
        {
            message.Dispose();
            throw;
        }
    }

    private static void AddHeader(HttpRequestMessage message, string name, string value)
    {
        if (string.Equals(name, SdkHeaders.ContentType, StringComparison.OrdinalIgnoreCase))
        {
            if (message.Content is not null)
            {
                message.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(value);
            }

            return;
        }

        if (!message.Headers.TryAddWithoutValidation(name, value))
        {
            // Content headers (e.g. Content-Language) only exist when there is a body.
            message.Content?.Headers.TryAddWithoutValidation(name, value);
        }
    }
}
