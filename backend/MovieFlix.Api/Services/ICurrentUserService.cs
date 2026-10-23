namespace MovieFlix.Api.Services
{
    public interface ICurrentUserService
    {
        string UserId { get; }

        string Email { get; }

        string DisplayName { get; }
    }
}
