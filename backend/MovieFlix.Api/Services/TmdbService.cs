using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using MovieFlix.Api.Configuration;
using MovieFlix.Api.DTOs.Tmdb;
using MovieFlix.Api.Exceptions;
using MovieFlix.Api.Models;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace MovieFlix.Api.Services
{
    /// <summary>
    /// Default <see cref="ITmdbService"/>. Maps TMDB status codes and Polly
    /// failures onto the application's distinct exception types.
    /// </summary>
    public class TmdbService : ITmdbService
    {
        private readonly HttpClient _httpClient;
        private readonly TimeProvider _timeProvider;

        public TmdbService(
            HttpClient httpClient,
            IOptions<TmdbSettings> options,
            TimeProvider timeProvider)
        {
            _httpClient = httpClient;
            _timeProvider = timeProvider;

            var settings = options.Value;

            if (!string.IsNullOrWhiteSpace(settings.BaseUrl))
            {
                _httpClient.BaseAddress = new Uri(settings.BaseUrl);
            }

            if (!string.IsNullOrWhiteSpace(settings.AccessToken))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue(
                        "Bearer",
                        settings.AccessToken);
            }

            _httpClient.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue(
                    "application/json"));
        }

        public Task<TmdbSearchResponse> SearchAsync(
            string query,
            SearchType type,
            int page,
            string language,
            string? region,
            CancellationToken cancellationToken = default)
        {
            var path = type switch
            {
                SearchType.Movie => "search/movie",
                SearchType.Tv => "search/tv",
                _ => "search/multi"
            };

            return GetAsync<TmdbSearchResponse>(
                BuildUrl(path,
                    ("query", query),
                    ("page", page.ToString()),
                    ("language", language),
                    ("region", region),
                    ("include_adult", "false")),
                cancellationToken);
        }

        public Task<TmdbMediaDetailsResponse> GetDetailsAsync(
            MediaType mediaType,
            int tmdbId,
            string language,
            CancellationToken cancellationToken = default)
        {
            var path = mediaType == MediaType.Movie
                ? $"movie/{tmdbId}"
                : $"tv/{tmdbId}";

            return GetAsync<TmdbMediaDetailsResponse>(
                BuildUrl(path,
                    ("language", language),
                    ("append_to_response", "videos,credits,similar,images,external_ids")),
                cancellationToken);
        }

        public Task<TmdbSearchResponse> GetTrendingAsync(
            MediaTypeFilter type,
            TrendingWindow window,
            int page,
            string language,
            CancellationToken cancellationToken = default)
        {
            var typeSegment = type switch
            {
                MediaTypeFilter.Movie => "movie",
                MediaTypeFilter.Tv => "tv",
                _ => "all"
            };

            var windowSegment = window == TrendingWindow.Day ? "day" : "week";

            return GetAsync<TmdbSearchResponse>(
                BuildUrl($"trending/{typeSegment}/{windowSegment}",
                    ("page", page.ToString()),
                    ("language", language)),
                cancellationToken);
        }

        public Task<TmdbSearchResponse> GetFeedAsync(
            MediaType mediaType,
            MediaFeed feed,
            int page,
            string language,
            string? region,
            CancellationToken cancellationToken = default)
        {
            var path = (mediaType, feed) switch
            {
                (MediaType.Movie, MediaFeed.Popular) => "movie/popular",
                (MediaType.Movie, MediaFeed.TopRated) => "movie/top_rated",
                (MediaType.Movie, MediaFeed.NowPlaying) => "movie/now_playing",
                (MediaType.Movie, MediaFeed.Upcoming) => "movie/upcoming",
                (MediaType.Tv, MediaFeed.Popular) => "tv/popular",
                (MediaType.Tv, MediaFeed.TopRated) => "tv/top_rated",
                (MediaType.Tv, MediaFeed.OnTheAir) => "tv/on_the_air",
                (MediaType.Tv, MediaFeed.AiringToday) => "tv/airing_today",
                _ => throw new ArgumentOutOfRangeException(
                    nameof(feed),
                    "The requested feed does not exist for this media type.")
            };

            return GetAsync<TmdbSearchResponse>(
                BuildUrl(path,
                    ("page", page.ToString()),
                    ("language", language),
                    ("region", region)),
                cancellationToken);
        }

        public Task<TmdbSeasonDetailsResponse> GetSeasonAsync(
            int tvId,
            int seasonNumber,
            string language,
            CancellationToken cancellationToken = default)
        {
            return GetAsync<TmdbSeasonDetailsResponse>(
                BuildUrl($"tv/{tvId}/season/{seasonNumber}",
                    ("language", language)),
                cancellationToken);
        }

        public async Task<List<TmdbGenre>> GetGenresAsync(
            MediaType mediaType,
            string language,
            CancellationToken cancellationToken = default)
        {
            var path = mediaType == MediaType.Movie
                ? "genre/movie/list"
                : "genre/tv/list";

            var response = await GetAsync<TmdbGenreListResponse>(
                BuildUrl(path, ("language", language)),
                cancellationToken);

            return response.Genres;
        }

        public Task<TmdbSearchResponse> GetDiscoverAsync(
            MediaType mediaType,
            int? genreId,
            int? year,
            double? minRating,
            string? sortBy,
            int page,
            string language,
            string? region,
            CancellationToken cancellationToken = default)
        {
            var path = mediaType == MediaType.Movie
                ? "discover/movie"
                : "discover/tv";

            var yearParameter = mediaType == MediaType.Movie
                ? "primary_release_year"
                : "first_air_date_year";

            return GetAsync<TmdbSearchResponse>(
                BuildUrl(path,
                    ("with_genres", genreId?.ToString()),
                    (yearParameter, year?.ToString()),
                    ("vote_average.gte", minRating?.ToString(
                        System.Globalization.CultureInfo.InvariantCulture)),
                    ("sort_by", sortBy),
                    ("page", page.ToString()),
                    ("language", language),
                    ("region", region),
                    ("include_adult", "false")),
                cancellationToken);
        }

        private async Task<T> GetAsync<T>(
            string url,
            CancellationToken cancellationToken)
        {
            try
            {
                using var response = await _httpClient.GetAsync(
                    url,
                    cancellationToken);

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new TmdbNotFoundException();
                }

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    var retryAfter = 60;

                    if (response.Headers.RetryAfter?.Delta is { } delta)
                    {
                        retryAfter = (int)Math.Ceiling(delta.TotalSeconds);
                    }
                    else if (response.Headers.RetryAfter?.Date is { } date)
                    {
                        retryAfter = (int)Math.Ceiling(
                            (date - _timeProvider.GetUtcNow()).TotalSeconds);
                    }

                    throw new TmdbRateLimitedException(retryAfter);
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new TmdbServiceException();
                }

                var result = await response.Content
                    .ReadFromJsonAsync<T>(cancellationToken: cancellationToken);

                return result ?? throw new TmdbServiceException();
            }
            catch (HttpRequestException ex)
            {
                throw new TmdbUnavailableException(ex);
            }
            catch (OperationCanceledException)
                when (!cancellationToken.IsCancellationRequested)
            {
                throw new TmdbUnavailableException();
            }
            catch (TimeoutRejectedException ex)
            {
                throw new TmdbUnavailableException(ex);
            }
            catch (BrokenCircuitException ex)
            {
                throw new TmdbUnavailableException(ex);
            }
        }

        private static string BuildUrl(
            string path,
            params (string Key, string? Value)[] parameters)
        {
            var query = parameters
                .Where(parameter => !string.IsNullOrWhiteSpace(parameter.Value))
                .Select(parameter =>
                    $"{Uri.EscapeDataString(parameter.Key)}=" +
                    $"{Uri.EscapeDataString(parameter.Value!)}");

            var queryString = string.Join("&", query);

            return string.IsNullOrEmpty(queryString)
                ? path
                : $"{path}?{queryString}";
        }
    }
}
