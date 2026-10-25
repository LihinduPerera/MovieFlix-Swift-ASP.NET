namespace MovieFlix.Api.Exceptions
{
    /// <summary>Stable RFC 7807 problem type URIs used by the API.</summary>
    public static class ProblemTypes
    {
        public const string Validation = "https://movieflix.example/problems/validation";
        public const string Unauthorized = "https://movieflix.example/problems/unauthorized";
        public const string Forbidden = "https://movieflix.example/problems/forbidden";
        public const string NotFound = "https://movieflix.example/problems/not-found";
        public const string Conflict = "https://movieflix.example/problems/conflict";
        public const string LockedOut = "https://movieflix.example/problems/locked-out";
        public const string RateLimited = "https://movieflix.example/problems/rate-limited";
        public const string BadGateway = "https://movieflix.example/problems/bad-gateway";
        public const string Unavailable = "https://movieflix.example/problems/unavailable";
        public const string ServerError = "https://movieflix.example/problems/server-error";

        public static string ForStatus(int? status) => status switch
        {
            400 => Validation,
            401 => Unauthorized,
            403 => Forbidden,
            404 => NotFound,
            409 => Conflict,
            423 => LockedOut,
            429 => RateLimited,
            502 => BadGateway,
            503 => Unavailable,
            500 => ServerError,
            _ => ServerError
        };
    }
}
