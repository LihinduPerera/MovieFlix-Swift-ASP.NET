using System.ComponentModel.DataAnnotations;
using MovieFlix.Api.Models;

namespace MovieFlix.Api.DTOs.Media
{
    /// <summary>Reusable BCP-47 language validation.</summary>
    public class LanguageQuery
    {
        [RegularExpression(@"^[A-Za-z]{2,3}(-[A-Za-z]{2,4})?$",
            ErrorMessage = "language must be a BCP-47 tag such as 'en-US'.")]
        public string? Language { get; set; }
    }

    public class SearchQueryRequest
    {
        [Required]
        [MinLength(1)]
        [MaxLength(200)]
        public string Query { get; set; } = string.Empty;

        public SearchType Type { get; set; } = SearchType.Multi;

        [Range(1, 500)]
        public int Page { get; set; } = 1;

        [RegularExpression(@"^[A-Za-z]{2,3}(-[A-Za-z]{2,4})?$",
            ErrorMessage = "language must be a BCP-47 tag such as 'en-US'.")]
        public string? Language { get; set; }

        [RegularExpression(@"^[A-Za-z]{2}$",
            ErrorMessage = "region must be a two-letter ISO 3166-1 code such as 'US'.")]
        public string? Region { get; set; }
    }

    public class TrendingQueryRequest
    {
        public MediaTypeFilter MediaType { get; set; } = MediaTypeFilter.All;

        public TrendingWindow Window { get; set; } = TrendingWindow.Week;

        [Range(1, 500)]
        public int Page { get; set; } = 1;

        [RegularExpression(@"^[A-Za-z]{2,3}(-[A-Za-z]{2,4})?$",
            ErrorMessage = "language must be a BCP-47 tag such as 'en-US'.")]
        public string? Language { get; set; }
    }

    public class MediaListQueryRequest
    {
        [Range(1, 500)]
        public int Page { get; set; } = 1;

        [RegularExpression(@"^[A-Za-z]{2,3}(-[A-Za-z]{2,4})?$",
            ErrorMessage = "language must be a BCP-47 tag such as 'en-US'.")]
        public string? Language { get; set; }

        [RegularExpression(@"^[A-Za-z]{2}$",
            ErrorMessage = "region must be a two-letter ISO 3166-1 code such as 'US'.")]
        public string? Region { get; set; }
    }

    public class DiscoverQueryRequest
    {
        [Range(1, int.MaxValue)]
        public int? GenreId { get; set; }

        [Range(1888, 2200)]
        public int? Year { get; set; }

        [Range(0, 10)]
        public double? MinRating { get; set; }

        [RegularExpression(@"^[a-z_]+(\.[a-z_]+)*$",
            ErrorMessage = "sortBy must be a TMDB sort expression such as 'popularity.desc'.")]
        public string? SortBy { get; set; }

        [Range(1, 500)]
        public int Page { get; set; } = 1;

        [RegularExpression(@"^[A-Za-z]{2,3}(-[A-Za-z]{2,4})?$",
            ErrorMessage = "language must be a BCP-47 tag such as 'en-US'.")]
        public string? Language { get; set; }

        [RegularExpression(@"^[A-Za-z]{2}$",
            ErrorMessage = "region must be a two-letter ISO 3166-1 code such as 'US'.")]
        public string? Region { get; set; }
    }

    public class LanguageQueryRequest
    {
        [RegularExpression(@"^[A-Za-z]{2,3}(-[A-Za-z]{2,4})?$",
            ErrorMessage = "language must be a BCP-47 tag such as 'en-US'.")]
        public string? Language { get; set; }
    }

    public class ReviewListQueryRequest
    {
        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;

        [Range(1, 100)]
        public int PageSize { get; set; } = 20;
    }

    public class PagedQueryRequest
    {
        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;

        [Range(1, 100)]
        public int PageSize { get; set; } = 20;
    }

    /// <summary>Paged list request for the user's favorites or watchlist.</summary>
    public class LibraryListQueryRequest
    {
        public MediaType? MediaType { get; set; }

        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;

        [Range(1, 100)]
        public int PageSize { get; set; } = 20;

        public string Sort { get; set; } = "added_desc";
    }

    public class HistoryListQueryRequest
    {
        public MediaType? MediaType { get; set; }

        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;

        [Range(1, 100)]
        public int PageSize { get; set; } = 20;
    }

    public class ContinueWatchingQueryRequest
    {
        [Range(1, 50)]
        public int Limit { get; set; } = 10;
    }
}
