using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using MovieFlix.Api.DTOs.Common;
using MovieFlix.Api.DTOs.Media;
using MovieFlix.Api.DTOs.Ratings;
using MovieFlix.Api.DTOs.Reviews;
using MovieFlix.Api.Models;
using MovieFlix.Api.Services;

namespace MovieFlix.Api.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v1/media")]
    public class MediaController : ApiControllerBase
    {
        private readonly IMediaService _mediaService;
        private readonly IRatingService _ratingService;
        private readonly IReviewService _reviewService;

        public MediaController(
            IMediaService mediaService,
            IRatingService ratingService,
            IReviewService reviewService)
        {
            _mediaService = mediaService;
            _ratingService = ratingService;
            _reviewService = reviewService;
        }

        /// <summary>Trending movies and/or TV shows for the day or week.</summary>
        [HttpGet("trending")]
        [OutputCache(PolicyName = "tmdb-trending")]
        public async Task<ActionResult<PagedResponse<MediaSummaryResponse>>> GetTrending(
            [FromQuery] TrendingQueryRequest request,
            CancellationToken cancellationToken)
        {
            return Ok(await _mediaService.GetTrendingAsync(request, cancellationToken));
        }

        /// <summary>Popular titles, optionally filtered to movies or TV.</summary>
        [HttpGet("{mediaType:mediaType}/popular")]
        [OutputCache(PolicyName = "tmdb-feed")]
        public Task<ActionResult<PagedResponse<MediaSummaryResponse>>> GetPopular(
            MediaType mediaType,
            [FromQuery] MediaListQueryRequest request,
            CancellationToken cancellationToken) =>
            GetFeed(mediaType, MediaFeed.Popular, request, cancellationToken);

        /// <summary>Top-rated titles, optionally filtered to movies or TV.</summary>
        [HttpGet("{mediaType:mediaType}/top-rated")]
        [OutputCache(PolicyName = "tmdb-feed")]
        public Task<ActionResult<PagedResponse<MediaSummaryResponse>>> GetTopRated(
            MediaType mediaType,
            [FromQuery] MediaListQueryRequest request,
            CancellationToken cancellationToken) =>
            GetFeed(mediaType, MediaFeed.TopRated, request, cancellationToken);

        /// <summary>Movies currently in theatres.</summary>
        [HttpGet("movie/now-playing")]
        [OutputCache(PolicyName = "tmdb-feed")]
        public Task<ActionResult<PagedResponse<MediaSummaryResponse>>> GetNowPlaying(
            [FromQuery] MediaListQueryRequest request,
            CancellationToken cancellationToken) =>
            GetFeed(MediaType.Movie, MediaFeed.NowPlaying, request, cancellationToken);

        /// <summary>Movies releasing soon.</summary>
        [HttpGet("movie/upcoming")]
        [OutputCache(PolicyName = "tmdb-feed")]
        public Task<ActionResult<PagedResponse<MediaSummaryResponse>>> GetUpcoming(
            [FromQuery] MediaListQueryRequest request,
            CancellationToken cancellationToken) =>
            GetFeed(MediaType.Movie, MediaFeed.Upcoming, request, cancellationToken);

        /// <summary>TV shows currently on the air.</summary>
        [HttpGet("tv/on-the-air")]
        [OutputCache(PolicyName = "tmdb-feed")]
        public Task<ActionResult<PagedResponse<MediaSummaryResponse>>> GetOnTheAir(
            [FromQuery] MediaListQueryRequest request,
            CancellationToken cancellationToken) =>
            GetFeed(MediaType.Tv, MediaFeed.OnTheAir, request, cancellationToken);

        /// <summary>TV episodes airing today.</summary>
        [HttpGet("tv/airing-today")]
        [OutputCache(PolicyName = "tmdb-feed")]
        public Task<ActionResult<PagedResponse<MediaSummaryResponse>>> GetAiringToday(
            [FromQuery] MediaListQueryRequest request,
            CancellationToken cancellationToken) =>
            GetFeed(MediaType.Tv, MediaFeed.AiringToday, request, cancellationToken);

        /// <summary>Discover titles by genre, year, rating and sort order.</summary>
        [HttpGet("{mediaType:mediaType}/discover")]
        [OutputCache(PolicyName = "tmdb-discover")]
        public async Task<ActionResult<PagedResponse<MediaSummaryResponse>>> Discover(
            MediaType mediaType,
            [FromQuery] DiscoverQueryRequest request,
            CancellationToken cancellationToken)
        {
            return Ok(await _mediaService.DiscoverAsync(
                mediaType,
                request,
                cancellationToken));
        }

        /// <summary>Full details for one movie or TV show.</summary>
        [HttpGet("{mediaType:mediaType}/{tmdbId:int}")]
        [OutputCache(PolicyName = "tmdb-details")]
        [ProducesResponseType(typeof(MediaDetailsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<MediaDetailsResponse>> GetDetails(
            MediaType mediaType,
            int tmdbId,
            [FromQuery] LanguageQueryRequest request,
            CancellationToken cancellationToken)
        {
            var details = await _mediaService.GetDetailsAsync(
                mediaType,
                tmdbId,
                request.Language,
                cancellationToken);

            return details is null
                ? NotFoundProblem(
                    $"No {mediaType} with TMDB id {tmdbId} was found.")
                : Ok(details);
        }

        /// <summary>Season summaries for a TV show (specials are excluded).</summary>
        [HttpGet("tv/{tmdbId:int}/seasons")]
        public async Task<ActionResult<List<SeasonSummaryResponse>>> GetSeasons(
            int tmdbId,
            [FromQuery] LanguageQueryRequest request,
            CancellationToken cancellationToken)
        {
            return Ok(await _mediaService.GetSeasonsAsync(
                tmdbId,
                request.Language,
                cancellationToken));
        }

        /// <summary>Episodes in one season, with the caller's progress when signed in.</summary>
        [HttpGet("tv/{tmdbId:int}/seasons/{seasonNumber:int}")]
        public async Task<ActionResult<SeasonDetailsResponse>> GetSeason(
            int tmdbId,
            int seasonNumber,
            [FromQuery] LanguageQueryRequest request,
            CancellationToken cancellationToken)
        {
            var season = await _mediaService.GetSeasonDetailsAsync(
                tmdbId,
                seasonNumber,
                request.Language,
                CurrentUserId,
                cancellationToken);

            return season is null
                ? NotFoundProblem(
                    $"Season {seasonNumber} was not found for TV show {tmdbId}.")
                : Ok(season);
        }

        /// <summary>Membership, rating, review and progress state for one title.</summary>
        [HttpGet("{mediaType:mediaType}/{tmdbId:int}/state")]
        public async Task<ActionResult<MediaStateResponse>> GetState(
            MediaType mediaType,
            int tmdbId,
            CancellationToken cancellationToken)
        {
            return Ok(await _mediaService.GetStateAsync(
                mediaType,
                tmdbId,
                CurrentUserId,
                cancellationToken));
        }

        /// <summary>Community rating aggregate for one title.</summary>
        [HttpGet("{mediaType:mediaType}/{tmdbId:int}/ratings")]
        public async Task<ActionResult<CommunityRatingResponse>> GetCommunityRating(
            MediaType mediaType,
            int tmdbId,
            CancellationToken cancellationToken)
        {
            var rating = await _ratingService.GetCommunityAsync(
                mediaType,
                tmdbId,
                CurrentUserId,
                cancellationToken);

            return rating is null
                ? NotFoundProblem("Rating data is not available for this title.")
                : Ok(rating);
        }

        /// <summary>Paginated public reviews for one title.</summary>
        [HttpGet("{mediaType:mediaType}/{tmdbId:int}/reviews")]
        public async Task<ActionResult<PagedResponse<ReviewResponse>>> GetReviews(
            MediaType mediaType,
            int tmdbId,
            [FromQuery] ReviewListQueryRequest request,
            CancellationToken cancellationToken)
        {
            var reviews = await _reviewService.GetForMediaAsync(
                mediaType,
                tmdbId,
                request.Page,
                request.PageSize,
                CurrentUserId,
                cancellationToken);

            return reviews is null
                ? NotFoundProblem("Reviews are not available for this title.")
                : Ok(reviews);
        }

        /// <summary>Creates the caller's review for one title.</summary>
        [HttpPost("{mediaType:mediaType}/{tmdbId:int}/reviews")]
        [Authorize]
        [ProducesResponseType(typeof(ReviewResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<ReviewResponse>> CreateReview(
            MediaType mediaType,
            int tmdbId,
            [FromBody] CreateReviewRequest request,
            CancellationToken cancellationToken)
        {
            var review = await _reviewService.CreateAsync(
                RequireUserId(),
                mediaType,
                tmdbId,
                request,
                cancellationToken);

            return Created($"/api/v1/reviews/{review.Id}", review);
        }

        private async Task<ActionResult<PagedResponse<MediaSummaryResponse>>> GetFeed(
            MediaType mediaType,
            MediaFeed feed,
            MediaListQueryRequest request,
            CancellationToken cancellationToken)
        {
            return Ok(await _mediaService.GetFeedAsync(
                mediaType,
                feed,
                request,
                cancellationToken));
        }
    }
}
