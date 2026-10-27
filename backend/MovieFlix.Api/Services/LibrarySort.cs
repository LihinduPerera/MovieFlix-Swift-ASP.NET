using MovieFlix.Api.Exceptions;

namespace MovieFlix.Api.Services
{
    /// <summary>Validated library sort options shared by favorites and watchlist.</summary>
    public static class LibrarySort
    {
        public const string AddedDesc = "added_desc";
        public const string AddedAsc = "added_asc";
        public const string TitleAsc = "title_asc";
        public const string TitleDesc = "title_desc";
        public const string ReleaseDesc = "release_desc";

        public static string Normalize(string? sort)
        {
            var value = string.IsNullOrWhiteSpace(sort)
                ? AddedDesc
                : sort.Trim().ToLowerInvariant();

            return value switch
            {
                AddedDesc or AddedAsc or TitleAsc or TitleDesc or ReleaseDesc => value,
                _ => throw new RequestValidationException(
                    "sort",
                    "sort must be one of added_desc, added_asc, title_asc, title_desc, release_desc.")
            };
        }
    }
}
