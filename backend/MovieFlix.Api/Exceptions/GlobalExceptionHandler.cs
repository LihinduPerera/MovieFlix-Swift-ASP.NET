using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace MovieFlix.Api.Exceptions
{
    public class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
        {
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            ProblemDetails problemDetails = new ProblemDetails
            {
                Title = "An error occurred.",
                Status = StatusCodes.Status500InternalServerError,
                Detail = "An unexpected error occurred."
            };

            if (exception is MovieAlreadyExistsException movieException)
            {
                problemDetails = new ProblemDetails
                {
                    Title = "Movie already exists.",
                    Status = StatusCodes.Status409Conflict,
                    Detail = movieException.Message
                };
            }
            else if (exception is FavoriteAlreadyExistsException favoriteException)
            {
                problemDetails = new ProblemDetails
                {
                    Title = "Favorite already exists.",
                    Status = StatusCodes.Status409Conflict,
                    Detail = favoriteException.Message
                };
            }
            else if (exception is UserAlreadyExistsException userException)
            {
                problemDetails = new ProblemDetails
                {
                    Title = "User already exists.",
                    Status = StatusCodes.Status409Conflict,
                    Detail = userException.Message
                };
            }
            else if (exception is IdentityValidationException identityValidation)
            {
                problemDetails = new ProblemDetails
                {
                    Title = "Validation failed.",
                    Status = StatusCodes.Status400BadRequest,
                    Detail = "One or more validation errors occurred."
                };

                var errors = identityValidation.Errors
                    .Select(e => new { e.Code, e.Description })
                    .ToList();

                problemDetails.Extensions["errors"] = errors;
            }
            else if (exception is TmdbServiceException tmdbException)
            {
                problemDetails = new ProblemDetails
                {
                    Title = "TMDb service error.",
                    Status = StatusCodes.Status502BadGateway,
                    Detail = tmdbException.Message
                };
            }
            else if (exception is ReviewAlreadyExistsException reviewException)
            {
                problemDetails = new ProblemDetails
                {
                    Title = "Review already exists.",
                    Status = StatusCodes.Status409Conflict,
                    Detail = reviewException.Message
                };
            }
            else if (exception is ForbiddenAccessException forbiddenException)
            {
                problemDetails = new ProblemDetails
                {
                    Title = "Forbidden.",
                    Status = StatusCodes.Status403Forbidden,
                    Detail = forbiddenException.Message
                };
            }
            else if (exception is InvalidOperationException)
            {
                problemDetails = new ProblemDetails
                {
                    Title = "Invalid operation.",
                    Status = StatusCodes.Status400BadRequest,
                    Detail = exception.Message
                };
            }
            else if (exception is UnauthorizedAccessException)
            {
                problemDetails = new ProblemDetails
                {
                    Title = "Unauthorized.",
                    Status = StatusCodes.Status401Unauthorized,
                    Detail = exception.Message
                };
            }

            if (problemDetails.Status >= 500)
            {
                _logger.LogError(
                    exception,
                    "A server error occurred while processing {HttpMethod} {RequestPath}. TraceId: {TraceId}",
                    httpContext.Request.Method,
                    httpContext.Request.Path,
                    httpContext.TraceIdentifier);
            }
            else
            {
                _logger.LogWarning(
                    exception,
                    "A handled error occurred while processing {HttpMethod} {RequestPath}. Status: {Status}. TraceId: {TraceId}",
                    httpContext.Request.Method,
                    httpContext.Request.Path,
                    problemDetails.Status,
                    httpContext.TraceIdentifier);
            }

            httpContext.Response.StatusCode = problemDetails.Status!.Value;

            problemDetails.Extensions["traceId"] =
                httpContext.TraceIdentifier;

            await httpContext.Response.WriteAsJsonAsync(
                problemDetails,
                cancellationToken);

            return true;
        }
    }
}
