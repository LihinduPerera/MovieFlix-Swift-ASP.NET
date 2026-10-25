using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace MovieFlix.Api.Exceptions
{
    /// <summary>
    /// Converts domain exceptions into RFC 7807 ProblemDetails responses with a
    /// stable problem type, trace id and (for validation) an errors dictionary.
    /// </summary>
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
            var (problem, retryAfterSeconds) = Map(exception, httpContext);

            problem.Extensions["traceId"] = httpContext.TraceIdentifier;
            problem.Type ??= ProblemTypes.ForStatus(problem.Status);

            if (problem.Status >= 500)
            {
                _logger.LogError(
                    exception,
                    "Server error processing {HttpMethod} {RequestPath}. Status: {Status}. TraceId: {TraceId}",
                    httpContext.Request.Method,
                    httpContext.Request.Path,
                    problem.Status,
                    httpContext.TraceIdentifier);
            }
            else
            {
                _logger.LogWarning(
                    exception,
                    "Handled error processing {HttpMethod} {RequestPath}. Status: {Status}. TraceId: {TraceId}",
                    httpContext.Request.Method,
                    httpContext.Request.Path,
                    problem.Status,
                    httpContext.TraceIdentifier);
            }

            httpContext.Response.StatusCode = problem.Status!.Value;
            httpContext.Response.ContentType = "application/problem+json";

            if (retryAfterSeconds is not null)
            {
                httpContext.Response.Headers.RetryAfter =
                    retryAfterSeconds.Value.ToString();
            }

            await httpContext.Response.WriteAsJsonAsync(
                problem,
                options: null,
                contentType: "application/problem+json",
                cancellationToken);

            return true;
        }

        private static (ProblemDetails Problem, int? RetryAfterSeconds) Map(
            Exception exception,
            HttpContext httpContext)
        {
            switch (exception)
            {
                case TmdbNotFoundException:
                    return (Create(
                        StatusCodes.Status404NotFound,
                        "Title not found.",
                        "TMDB does not have a title with that identifier."), null);

                case TmdbRateLimitedException rateLimited:
                    return (Create(
                        StatusCodes.Status503ServiceUnavailable,
                        "Upstream rate limit reached.",
                        "TMDB is rate limiting requests. Try again later."),
                        rateLimited.RetryAfterSeconds);

                case TmdbUnavailableException:
                    return (Create(
                        StatusCodes.Status503ServiceUnavailable,
                        "TMDB unavailable.",
                        "The TMDB provider is temporarily unavailable. Try again later."), null);

                case TmdbServiceException:
                    return (Create(
                        StatusCodes.Status502BadGateway,
                        "TMDB error.",
                        "TMDB could not complete the request."), null);

                case LockedOutException:
                    return (Create(
                        StatusCodes.Status423Locked,
                        "Account locked.",
                        "This account is temporarily locked. Try again later."), null);

                case ForbiddenAccessException forbidden:
                    return (Create(
                        StatusCodes.Status403Forbidden,
                        "Forbidden.",
                        forbidden.Message), null);

                case ResourceNotFoundException notFound:
                    return (Create(
                        StatusCodes.Status404NotFound,
                        "Resource not found.",
                        notFound.Message), null);

                case ReviewAlreadyExistsException review:
                    return (Create(
                        StatusCodes.Status409Conflict,
                        "Review already exists.",
                        review.Message), null);

                case DuplicateResourceException duplicate:
                    return (Create(
                        StatusCodes.Status409Conflict,
                        "Duplicate resource.",
                        duplicate.Message), null);

                case UserAlreadyExistsException user:
                    return (Create(
                        StatusCodes.Status409Conflict,
                        "User already exists.",
                        user.Message), null);

                case RequestValidationException validation:
                    return (CreateValidation(validation.Errors), null);

                case IdentityValidationException identity:
                    return (CreateValidation(identity.Errors), null);

                case UnauthorizedAccessException unauthorized:
                    return (Create(
                        StatusCodes.Status401Unauthorized,
                        "Unauthorized.",
                        unauthorized.Message), null);

                default:
                    return (Create(
                        StatusCodes.Status500InternalServerError,
                        "An unexpected error occurred.",
                        "An unexpected error occurred."), null);
            }
        }

        private static ProblemDetails Create(int status, string title, string detail)
        {
            return new ProblemDetails
            {
                Type = ProblemTypes.ForStatus(status),
                Title = title,
                Status = status,
                Detail = detail
            };
        }

        private static ProblemDetails CreateValidation(
            IReadOnlyDictionary<string, string[]> errors)
        {
            var problem = new ProblemDetails
            {
                Type = ProblemTypes.Validation,
                Title = "One or more validation errors occurred.",
                Status = StatusCodes.Status400BadRequest,
                Detail = ReasonPhrases.GetReasonPhrase(StatusCodes.Status400BadRequest)
            };

            problem.Extensions["errors"] = errors.ToDictionary(
                pair => pair.Key,
                pair => pair.Value);

            return problem;
        }
    }
}
