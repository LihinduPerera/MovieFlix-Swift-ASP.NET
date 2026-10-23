using System.Security.Claims;

namespace MovieFlix.Api.Services
{
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        private ClaimsPrincipal? Principal =>
            _httpContextAccessor.HttpContext?.User;

        public string UserId
        {
            get
            {
                var userId = Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

                if (string.IsNullOrWhiteSpace(userId))
                {
                    throw new UnauthorizedAccessException("Authenticated user ID is missing.");
                }

                return userId;
            }
        }

        public string Email
        {
            get
            {
                var email = Principal?.FindFirstValue(ClaimTypes.Email);

                if (string.IsNullOrWhiteSpace(email))
                {
                    throw new UnauthorizedAccessException("Authenticated user email is missing.");
                }

                return email;
            }
        }

        public string DisplayName
        {
            get
            {
                var displayName = Principal?.FindFirstValue("display_name");

                if (string.IsNullOrWhiteSpace(displayName))
                {
                    throw new UnauthorizedAccessException("Authenticated user display name is missing.");
                }

                return displayName;
            }
        }
    }
}
