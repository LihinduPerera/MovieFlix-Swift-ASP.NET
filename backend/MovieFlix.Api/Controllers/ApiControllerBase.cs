using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace MovieFlix.Api.Controllers
{
    /// <summary>Shared helpers for thin controllers.</summary>
    public abstract class ApiControllerBase : ControllerBase
    {
        protected string? CurrentUserId =>
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        protected string RequireUserId() =>
            CurrentUserId
            ?? throw new UnauthorizedAccessException("Authentication is required.");

        protected ObjectResult UnauthorizedProblem(string detail) =>
            StatusCode(
                StatusCodes.Status401Unauthorized,
                new ProblemDetails
                {
                    Type = Exceptions.ProblemTypes.Unauthorized,
                    Title = "Unauthorized.",
                    Status = StatusCodes.Status401Unauthorized,
                    Detail = detail
                });

        protected ObjectResult NotFoundProblem(string detail) =>
            Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Not found.",
                detail: detail,
                type: Exceptions.ProblemTypes.NotFound);
    }
}
