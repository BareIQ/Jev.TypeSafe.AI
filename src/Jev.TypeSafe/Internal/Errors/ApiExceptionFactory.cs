using System;
using TypeSafe.AI.Internal.Transport;

namespace TypeSafe.AI.Internal.Errors;

/// <summary>Creates the <see cref="ApiException"/> subclass for an unsuccessful response.</summary>
internal static class ApiExceptionFactory
{
    public static ApiException Create(RawResponse response, ParsedBody body, DateTimeOffset now)
    {
        string message = ErrorMessageExtractor.Describe(response.StatusCode, body);
        return response.StatusCode switch
        {
            400 => new BadRequestException(message, response),
            401 => new AuthenticationException(message, response),
            403 => new PermissionDeniedException(message, response),
            404 => new NotFoundException(message, response),
            422 => new UnprocessableEntityException(message, response),
            429 => new RateLimitException(message, response, null, now),
            >= 500 => new InternalServerException(message, response),
            _ => new ApiException(message, response),
        };
    }
}
