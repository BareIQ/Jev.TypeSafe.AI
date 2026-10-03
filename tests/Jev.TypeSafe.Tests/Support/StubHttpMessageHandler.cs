using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace TypeSafe.AI.Tests.Support;

/// <summary>A request as the stub handler saw it, with all headers (request and content) flattened.</summary>
internal sealed record RecordedRequest(HttpMethod Method, Uri Uri, IReadOnlyDictionary<string, string> Headers, string? Body)
{
    public string? Header(string name) => Headers.TryGetValue(name, out string? value) ? value : null;

    public bool HasHeader(string name) => Headers.ContainsKey(name);
}

/// <summary>An <see cref="HttpMessageHandler"/> that records requests and answers from a delegate.</summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<int, CancellationToken, Task<HttpResponseMessage>> _respond;
    private readonly List<RecordedRequest> _requests = [];
    private readonly object _gate = new();

    /// <summary>Answers each request (numbered from 1) asynchronously.</summary>
    public StubHttpMessageHandler(Func<int, CancellationToken, Task<HttpResponseMessage>> respond) => _respond = respond;

    /// <summary>Answers each request (numbered from 1) synchronously.</summary>
    public StubHttpMessageHandler(Func<int, HttpResponseMessage> respond)
        : this((number, _) => Task.FromResult(respond(number)))
    {
    }

    public IReadOnlyList<RecordedRequest> Requests
    {
        get
        {
            lock (_gate)
            {
                return _requests.ToArray();
            }
        }
    }

    public int Count => Requests.Count;

    public RecordedRequest Single => Requests.Single();

    public static StubHttpMessageHandler Always(Func<HttpResponseMessage> create) => new(_ => create());

    /// <summary>A handler that never answers until the request is cancelled.</summary>
    public static StubHttpMessageHandler Hanging() => new(async (_, ct) =>
    {
        await Task.Delay(Timeout.Infinite, ct);
        return Responses.Json("{}");
    });

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, IEnumerable<string>> header in request.Headers)
        {
            headers[header.Key] = string.Join(", ", header.Value);
        }

        if (request.Content is not null)
        {
            foreach (KeyValuePair<string, IEnumerable<string>> header in request.Content.Headers)
            {
                headers[header.Key] = string.Join(", ", header.Value);
            }
        }

        string? body = request.Content is null ? null : await request.Content.ReadAsStringAsync();
        int number;
        lock (_gate)
        {
            _requests.Add(new RecordedRequest(request.Method, request.RequestUri!, headers, body));
            number = _requests.Count;
        }

        return await _respond(number, cancellationToken);
    }
}

/// <summary>Canned HTTP responses.</summary>
internal static class Responses
{
    public const string EmptyModels = """{"models":[]}""";

    public static HttpResponseMessage Json(string json, int status = 200, params (string Name, string Value)[] headers)
    {
        var response = new HttpResponseMessage((HttpStatusCode)status)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        };
        foreach ((string name, string value) in headers)
        {
            response.Headers.TryAddWithoutValidation(name, value);
        }

        return response;
    }

    public static HttpResponseMessage Text(string text, int status = 200)
        => new((HttpStatusCode)status) { Content = new StringContent(text, System.Text.Encoding.UTF8, "text/plain") };

    public static HttpResponseMessage Empty(int status = 200)
        => new((HttpStatusCode)status) { Content = new ByteArrayContent([]) };
}
