using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;
using TypeSafe.AI.Internal.Http;
using TypeSafe.AI.Internal.Serialization;
using TypeSafe.AI.Internal.Transport;

namespace TypeSafe.AI.Internal.Logging;

/// <summary>
/// The SDK's log messages, defined once with <see cref="LoggerMessage"/>. Info-level messages summarize
/// attempts; debug-level messages add redacted headers and bodies. Expensive arguments are only built
/// when the level is enabled.
/// </summary>
internal static class Log
{
    private static readonly Action<ILogger, string, Uri, string, string, Exception?> s_requestSent =
        LoggerMessage.Define<string, Uri, string, string>(
            LogLevel.Debug, new EventId(1001, nameof(RequestSent)), "{Tag} -> {Url} headers={Headers} body={Body}");

    private static readonly Action<ILogger, string, int, long, Exception?> s_responseReceived =
        LoggerMessage.Define<string, int, long>(
            LogLevel.Information, new EventId(1002, nameof(ResponseReceived)), "{Tag} <- {StatusCode} in {ElapsedMs}ms");

    private static readonly Action<ILogger, string, int, long, string, Exception?> s_responseReceivedWithRequestId =
        LoggerMessage.Define<string, int, long, string>(
            LogLevel.Information, new EventId(1002, nameof(ResponseReceived)), "{Tag} <- {StatusCode} in {ElapsedMs}ms (request {RequestId})");

    private static readonly Action<ILogger, string, string, Exception?> s_responseBody =
        LoggerMessage.Define<string, string>(LogLevel.Debug, new EventId(1003, nameof(ResponseBody)), "{Tag} <- body {Body}");

    private static readonly Action<ILogger, string, string, Exception?> s_errorBody =
        LoggerMessage.Define<string, string>(LogLevel.Debug, new EventId(1004, nameof(ErrorBody)), "{Tag} <- error body {Body}");

    private static readonly Action<ILogger, string, long, int, int, string, Exception?> s_retrying =
        LoggerMessage.Define<string, long, int, int, string>(
            LogLevel.Information, new EventId(1005, nameof(Retrying)), "{Tag} retrying in {DelayMs}ms (retry {Retry}/{MaxRetries}) after {Reason}");

    private static readonly Action<ILogger, string, long, Exception?> s_timedOut =
        LoggerMessage.Define<string, long>(LogLevel.Information, new EventId(1006, nameof(TimedOut)), "{Tag} timed out after {ElapsedMs}ms");

    private static readonly Action<ILogger, string, long, Exception?> s_connectionError =
        LoggerMessage.Define<string, long>(
            LogLevel.Information, new EventId(1007, nameof(ConnectionError)), "{Tag} connection error after {ElapsedMs}ms");

    private static readonly Action<ILogger, string, long, Exception?> s_aborted =
        LoggerMessage.Define<string, long>(LogLevel.Information, new EventId(1008, nameof(Aborted)), "{Tag} aborted by caller after {ElapsedMs}ms");

    private static readonly Action<ILogger, string, Exception?> s_abortedDuringBackoff =
        LoggerMessage.Define<string>(
            LogLevel.Information, new EventId(1009, nameof(AbortedDuringBackoff)), "{Tag} aborted by caller while waiting to retry");

    public static void RequestSent(ILogger logger, string tag, Uri url, IEnumerable<KeyValuePair<string, string>> headers, byte[]? body)
    {
        if (logger.IsEnabled(LogLevel.Debug))
        {
            s_requestSent(logger, tag, url, FormatHeaders(headers), body is null ? "(none)" : Encoding.UTF8.GetString(body), null);
        }
    }

    public static void ResponseReceived(ILogger logger, string tag, int statusCode, TimeSpan elapsed, string? requestId)
    {
        if (requestId is null)
        {
            s_responseReceived(logger, tag, statusCode, Milliseconds.Round(elapsed), null);
        }
        else
        {
            s_responseReceivedWithRequestId(logger, tag, statusCode, Milliseconds.Round(elapsed), requestId, null);
        }
    }

    public static void ResponseBody(ILogger logger, string tag, ParsedBody body)
    {
        if (logger.IsEnabled(LogLevel.Debug))
        {
            s_responseBody(logger, tag, body.ToDisplayString(), null);
        }
    }

    public static void ErrorBody(ILogger logger, string tag, ParsedBody body)
    {
        if (logger.IsEnabled(LogLevel.Debug))
        {
            s_errorBody(logger, tag, body.ToDisplayString(), null);
        }
    }

    public static void Retrying(ILogger logger, string tag, TimeSpan delay, int retry, int maxRetries, string reason)
        => s_retrying(logger, tag, Milliseconds.Round(delay), retry, maxRetries, reason, null);

    public static void TimedOut(ILogger logger, string tag, TimeSpan elapsed)
        => s_timedOut(logger, tag, Milliseconds.Round(elapsed), null);

    public static void ConnectionError(ILogger logger, string tag, TimeSpan elapsed, Exception exception)
        => s_connectionError(logger, tag, Milliseconds.Round(elapsed), exception);

    public static void Aborted(ILogger logger, string tag, TimeSpan elapsed)
        => s_aborted(logger, tag, Milliseconds.Round(elapsed), null);

    public static void AbortedDuringBackoff(ILogger logger, string tag)
        => s_abortedDuringBackoff(logger, tag, null);

    private static string FormatHeaders(IEnumerable<KeyValuePair<string, string>> headers)
        => JsonText.Build(writer =>
        {
            writer.WriteStartObject();
            foreach (KeyValuePair<string, string> header in HeaderRedactor.Redact(headers))
            {
                writer.WriteString(header.Key, header.Value);
            }

            writer.WriteEndObject();
        });
}
