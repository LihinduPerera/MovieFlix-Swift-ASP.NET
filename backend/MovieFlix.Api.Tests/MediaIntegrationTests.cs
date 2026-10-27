using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MovieFlix.Api.Data;
using MovieFlix.Api.DTOs.Common;
using MovieFlix.Api.DTOs.Media;
using MovieFlix.Api.DTOs.Tmdb;
using MovieFlix.Api.Models;

namespace MovieFlix.Api.Tests;

public sealed class MediaIntegrationTests : IntegrationTestBase
{
    public MediaIntegrationTests(MovieFlixApiFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task Search_Multi_ReturnsMoviesAndTvWithoutPersons()
    {
        using var client = Factory.CreateClient();

        var response = await client.GetAsync(
            "/api/v1/search?query=club&type=multi");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await ReadAsync<PagedResponse<MediaSummaryResponse>>(response);
        Assert.Equal(2, page.Items.Count);
        Assert.Contains(page.Items, item => item.MediaType == MediaType.Movie);
        Assert.Contains(page.Items, item => item.MediaType == MediaType.Tv);
        Assert.DoesNotContain(page.Items, item => item.Title == "A Person");

        var movie = Assert.Single(
            page.Items,
            item => item.MediaType == MediaType.Movie);

        Assert.Equal(FakeTmdbService.FightClubId, movie.TmdbId);
        Assert.Equal("Fight Club", movie.Title);
        Assert.Equal("/fight-club.jpg", movie.PosterPath);
        Assert.Equal("/fight-club-backdrop.jpg", movie.BackdropPath);
        Assert.Equal(new DateOnly(1999, 10, 15), movie.ReleaseDate);
        Assert.Equal(8.4, movie.VoteAverage);
        Assert.Equal(26000, movie.VoteCount);

        var tv = Assert.Single(
            page.Items,
            item => item.MediaType == MediaType.Tv);

        Assert.Equal(FakeTmdbService.GameOfThronesId, tv.TmdbId);
        Assert.Equal("Game of Thrones", tv.Title);
        Assert.Equal(new DateOnly(2011, 4, 17), tv.ReleaseDate);
    }

    [Fact]
    public async Task Search_UsesExplicitAndDefaultLanguageRegion()
    {
        using var client = Factory.CreateClient();

        var explicitResponse = await client.GetAsync(
            "/api/v1/search?query=language-check&language=fr-FR&region=GB");
        Assert.Equal(HttpStatusCode.OK, explicitResponse.StatusCode);
        Assert.Equal("fr-FR", Factory.Tmdb.LastLanguage);
        Assert.Equal("GB", Factory.Tmdb.LastRegion);

        var defaultResponse = await client.GetAsync(
            "/api/v1/search?query=default-check");
        Assert.Equal(HttpStatusCode.OK, defaultResponse.StatusCode);
        Assert.Equal("en-US", Factory.Tmdb.LastLanguage);
        Assert.Equal("US", Factory.Tmdb.LastRegion);
    }

    [Fact]
    public async Task Search_InvalidType_ReturnsValidationProblem()
    {
        using var client = Factory.CreateClient();

        var response = await client.GetAsync(
            "/api/v1/search?query=club&type=banana");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ReadProblemAsync(response);
        AssertValidationError(problem, "Type");
    }

    [Fact]
    public async Task Search_MissingQuery_ReturnsValidationProblem()
    {
        using var client = Factory.CreateClient();

        var response = await client.GetAsync("/api/v1/search");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ReadProblemAsync(response);
        AssertValidationError(problem, "Query");
    }

    [Fact]
    public async Task Trending_ReturnsBothTypesUsingCamelCaseEnums()
    {
        using var client = Factory.CreateClient();

        var response = await client.GetAsync(
            "/api/v1/media/trending?mediaType=all&window=day");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await ReadJsonAsync(response);
        var items = json.GetProperty("items");

        Assert.Equal(2, items.GetArrayLength());
        var mediaTypes = items
            .EnumerateArray()
            .Select(item => item.GetProperty("mediaType").GetString())
            .ToHashSet();

        Assert.Contains("movie", mediaTypes);
        Assert.Contains("tv", mediaTypes);
        Assert.Equal(MediaTypeFilter.All, Factory.Tmdb.LastTrendingType);
        Assert.Equal(TrendingWindow.Day, Factory.Tmdb.LastTrendingWindow);
    }

    [Fact]
    public async Task Feed_FiltersToRequestedMediaType()
    {
        using var client = Factory.CreateClient();

        var movieResponse = await client.GetAsync(
            "/api/v1/media/movie/popular?language=de-DE&region=DE");
        Assert.Equal(HttpStatusCode.OK, movieResponse.StatusCode);
        Assert.Equal(MediaFeed.Popular, Factory.Tmdb.LastFeed);
        Assert.Equal("de-DE", Factory.Tmdb.LastLanguage);
        Assert.Equal("DE", Factory.Tmdb.LastRegion);

        var moviePage = await ReadAsync<PagedResponse<MediaSummaryResponse>>(
            movieResponse);
        Assert.All(moviePage.Items, item => Assert.Equal(MediaType.Movie, item.MediaType));

        var tvResponse = await client.GetAsync(
            "/api/v1/media/tv/on-the-air");
        Assert.Equal(HttpStatusCode.OK, tvResponse.StatusCode);
        Assert.Equal(MediaFeed.OnTheAir, Factory.Tmdb.LastFeed);

        var tvPage = await ReadAsync<PagedResponse<MediaSummaryResponse>>(
            tvResponse);
        Assert.All(tvPage.Items, item => Assert.Equal(MediaType.Tv, item.MediaType));
    }

    [Fact]
    public async Task Discover_ForwardsFiltersAndValidatesSort()
    {
        using var client = Factory.CreateClient();

        var response = await client.GetAsync(
            "/api/v1/media/movie/discover?genreId=18&year=2010&minRating=7.5&sortBy=popularity.desc");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(18, Factory.Tmdb.LastDiscoverGenreId);
        Assert.Equal(2010, Factory.Tmdb.LastDiscoverYear);
        Assert.Equal(7.5, Factory.Tmdb.LastDiscoverMinRating);
        Assert.Equal("popularity.desc", Factory.Tmdb.LastDiscoverSortBy);

        var invalid = await client.GetAsync(
            "/api/v1/media/movie/discover?sortBy=NOT VALID");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        await ReadProblemAsync(invalid);
    }

    [Fact]
    public async Task Genres_ReturnTmdbGenres()
    {
        using var client = Factory.CreateClient();

        var response = await client.GetAsync("/api/v1/genres/tv?language=es-ES");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var genres = await ReadAsync<List<GenreResponse>>(response);
        Assert.Contains(genres, genre => genre.Id == 10765);
        Assert.Equal("es-ES", Factory.Tmdb.LastLanguage);
    }

    [Fact]
    public async Task Details_Movie_ReturnsRichMetadataWithoutLocalIds()
    {
        using var client = Factory.CreateClient();

        var response = await client.GetAsync("/api/v1/media/movie/550");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await ReadJsonAsync(response);
        var details = await ReadAsync<MediaDetailsResponse>(response);

        Assert.Equal("Fight Club", details.Title);
        Assert.Equal(MediaType.Movie, details.MediaType);
        Assert.Equal(139, details.RuntimeMinutes);
        Assert.Equal("Mischief. Mayhem. Soap.", details.Tagline);
        Assert.Contains(details.Genres, genre => genre.Name == "Drama");
        Assert.Contains(details.Credits.Cast, cast => cast.Name == "Brad Pitt");
        Assert.Contains(details.Credits.Crew, crew => crew.Job == "Director");
        Assert.Single(details.Videos);
        Assert.Empty(details.Seasons);

        Assert.False(json.TryGetProperty("id", out _));
        Assert.False(json.TryGetProperty("mediaItemId", out _));
        Assert.False(json.TryGetProperty("userId", out _));
        Assert.Equal("movie", json.GetProperty("mediaType").GetString());
        Assert.Equal("1999-10-15", json.GetProperty("releaseDate").GetString());
    }

    [Fact]
    public async Task Details_CapsCreditsVideosAndSimilar_AndStoresLowercaseMediaType()
    {
        const int movieId = 880001;
        Factory.Tmdb.AddMovie(movieId, "Limit Test");
        var source = Factory.Tmdb.GetMovieDetails(movieId);

        source.Credits = new TmdbCredits
        {
            Cast = Enumerable.Range(0, 25)
                .Select(index => new TmdbCastMember
                {
                    Id = 900000 + index,
                    Name = $"Cast {index}",
                    Character = $"Role {index}",
                    Order = 24 - index
                })
                .ToList(),
            Crew =
            [
                new TmdbCrewMember
                {
                    Id = 1,
                    Name = "Director Person",
                    Job = "Director",
                    Department = "Directing"
                },
                new TmdbCrewMember
                {
                    Id = 2,
                    Name = "Writer Person",
                    Job = "Screenplay",
                    Department = "Writing"
                },
                new TmdbCrewMember
                {
                    Id = 3,
                    Name = "Catering Person",
                    Job = "Catering",
                    Department = "Crew"
                }
            ]
        };

        source.Videos = new TmdbVideos
        {
            Results =
            [
                new TmdbVideo
                {
                    Id = "youtube",
                    Key = "yt-key",
                    Name = "Official trailer",
                    Site = "YouTube",
                    Type = "Trailer"
                },
                new TmdbVideo
                {
                    Id = "vimeo",
                    Key = "vimeo-key",
                    Name = "Behind the scenes",
                    Site = "Vimeo",
                    Type = "Featurette"
                }
            ]
        };

        source.Similar = new TmdbSearchResponse
        {
            Results = Enumerable.Range(1, 15)
                .Select(index => new TmdbSearchResult
                {
                    Id = 700000 + index,
                    MediaType = "movie",
                    Title = $"Similar {index}",
                    ReleaseDate = "2021-01-01",
                    VoteAverage = 7,
                    VoteCount = 10,
                    Popularity = 1
                })
                .ToList(),
            TotalResults = 15,
            TotalPages = 1
        };

        using var client = Factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/media/movie/{movieId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var details = await ReadAsync<MediaDetailsResponse>(response);

        Assert.Equal(20, details.Credits.Cast.Count);
        Assert.Equal(
            Enumerable.Range(0, 20),
            details.Credits.Cast.Select(member => member.Order));
        Assert.Equal(2, details.Credits.Crew.Count);
        Assert.DoesNotContain(
            details.Credits.Crew,
            member => member.Job == "Catering");
        Assert.Single(details.Videos);
        Assert.Equal("YouTube", details.Videos[0].Site);
        Assert.Equal(12, details.Similar.Count);

        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MovieFlixDbContext>();

        var storedMediaType = await context.Database
            .SqlQueryRaw<string>(
                "SELECT \"MediaType\" AS \"Value\" FROM \"MediaItems\" WHERE \"TmdbId\" = {0}",
                movieId)
            .SingleAsync();

        Assert.Equal("movie", storedMediaType);

        var storedGenreTypes = await context.Database
            .SqlQueryRaw<string>(
                "SELECT DISTINCT \"MediaType\" AS \"Value\" FROM \"Genres\"")
            .ToListAsync();

        Assert.NotEmpty(storedGenreTypes);
        Assert.All(
            storedGenreTypes,
            value => Assert.Contains(value, new[] { "movie", "tv" }));
    }

    [Fact]
    public async Task Details_Tv_ExcludesSpecialSeason()
    {
        using var client = Factory.CreateClient();

        var response = await client.GetAsync("/api/v1/media/tv/1399");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var details = await ReadAsync<MediaDetailsResponse>(response);

        Assert.Equal("Game of Thrones", details.Title);
        Assert.Equal(2, details.NumberOfSeasons);
        Assert.Equal(2, details.Seasons.Count);
        Assert.DoesNotContain(details.Seasons, season => season.SeasonNumber == 0);
        Assert.All(details.Similar, item => Assert.Equal(MediaType.Tv, item.MediaType));
    }

    [Fact]
    public async Task Details_UnknownTitle_ReturnsNotFoundProblem()
    {
        using var client = Factory.CreateClient();

        var response = await client.GetAsync("/api/v1/media/movie/424242");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await ReadProblemAsync(response);
        Assert.Equal(404, problem.GetProperty("status").GetInt32());
        Assert.True(problem.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task Details_ProviderUnavailable_ReturnsServiceUnavailable()
    {
        using var client = Factory.CreateClient();
        Factory.Tmdb.ThrowUnavailableOnDetails = true;

        try
        {
            var response = await client.GetAsync("/api/v1/media/movie/550?language=it-IT");

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            var problem = await ReadProblemAsync(response);
            Assert.Equal(503, problem.GetProperty("status").GetInt32());
        }
        finally
        {
            Factory.Tmdb.ThrowUnavailableOnDetails = false;
        }
    }

    [Fact]
    public async Task Details_ProviderRateLimited_ReturnsRetryAfter()
    {
        using var client = Factory.CreateClient();
        Factory.Tmdb.ThrowRateLimitedOnDetails = true;

        try
        {
            var response = await client.GetAsync(
                "/api/v1/media/movie/550?language=ja-JP");

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Equal("37", response.Headers.RetryAfter?.ToString());

            var problem = await ReadProblemAsync(response);
            Assert.Equal(503, problem.GetProperty("status").GetInt32());
        }
        finally
        {
            Factory.Tmdb.ThrowRateLimitedOnDetails = false;
        }
    }

    [Fact]
    public async Task Details_ProviderServerError_ReturnsBadGateway()
    {
        using var client = Factory.CreateClient();
        Factory.Tmdb.ThrowBadGatewayOnDetails = true;

        try
        {
            var response = await client.GetAsync(
                "/api/v1/media/movie/550?language=ko-KR");

            Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);

            var problem = await ReadProblemAsync(response);
            Assert.Equal(502, problem.GetProperty("status").GetInt32());
        }
        finally
        {
            Factory.Tmdb.ThrowBadGatewayOnDetails = false;
        }
    }

    [Fact]
    public async Task Search_TypeSpecific_ReturnsOnlyRequestedType()
    {
        using var client = Factory.CreateClient();

        var movieResponse = await client.GetAsync(
            "/api/v1/search?query=club&type=movie");
        Assert.Equal(HttpStatusCode.OK, movieResponse.StatusCode);
        Assert.Equal(SearchType.Movie, Factory.Tmdb.LastSearchType);

        var moviePage = await ReadAsync<PagedResponse<MediaSummaryResponse>>(
            movieResponse);
        Assert.All(
            moviePage.Items,
            item => Assert.Equal(MediaType.Movie, item.MediaType));

        var tvResponse = await client.GetAsync(
            "/api/v1/search?query=club&type=tv");
        Assert.Equal(HttpStatusCode.OK, tvResponse.StatusCode);
        Assert.Equal(SearchType.Tv, Factory.Tmdb.LastSearchType);

        var tvPage = await ReadAsync<PagedResponse<MediaSummaryResponse>>(
            tvResponse);
        Assert.All(tvPage.Items, item => Assert.Equal(MediaType.Tv, item.MediaType));
    }

    [Fact]
    public async Task Seasons_ListExcludesSpecials_AndDetailsIncludeProgressFields()
    {
        using var client = Factory.CreateClient();

        var listResponse = await client.GetAsync(
            "/api/v1/media/tv/1399/seasons");

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var seasons = await ReadAsync<List<SeasonSummaryResponse>>(listResponse);
        Assert.Equal(2, seasons.Count);
        Assert.DoesNotContain(seasons, season => season.SeasonNumber == 0);
        Assert.Equal(2, seasons.Single(season => season.SeasonNumber == 1).EpisodeCount);

        var seasonResponse = await client.GetAsync(
            "/api/v1/media/tv/1399/seasons/1");

        Assert.Equal(HttpStatusCode.OK, seasonResponse.StatusCode);
        var season = await ReadAsync<SeasonDetailsResponse>(seasonResponse);

        Assert.Equal(2, season.Episodes.Count);
        Assert.Equal("Winter Is Coming", season.Episodes[0].Name);
        Assert.Equal(0, season.Episodes[0].ProgressSeconds);
        Assert.False(season.Episodes[0].Completed);
    }

    [Fact]
    public async Task State_ForUncachedTitle_DoesNotCallTmdb()
    {
        using var client = Factory.CreateClient();
        var before = Factory.Tmdb.DetailsCallCount;

        var response = await client.GetAsync(
            "/api/v1/media/movie/777777/state");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(before, Factory.Tmdb.DetailsCallCount);

        var state = await ReadAsync<MediaStateResponse>(response);
        Assert.Equal(777777, state.TmdbId);
        Assert.Equal(MediaType.Movie, state.MediaType);
        Assert.False(state.IsFavorite);
        Assert.False(state.IsInWatchlist);
        Assert.False(state.HasReview);
        Assert.Null(state.UserRating);
    }

    [Theory]
    [InlineData("/api/v1/media/person/550")]
    [InlineData("/api/v1/media/person/popular")]
    public async Task InvalidMediaTypeRoute_ReturnsValidationProblem(string path)
    {
        using var client = Factory.CreateClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ReadProblemAsync(response);
        AssertValidationError(problem, "mediaType");
    }

    [Fact]
    public async Task PageBeyondMaximum_ReturnsValidationProblem()
    {
        using var client = Factory.CreateClient();

        var response = await client.GetAsync(
            "/api/v1/search?query=club&page=501");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ReadProblemAsync(response);
        AssertValidationError(problem, "Page");
    }

    [Fact]
    public async Task UnknownQueryParameter_IsIgnored()
    {
        using var client = Factory.CreateClient();

        var response = await client.GetAsync(
            "/api/v1/search?query=club&unexpected=value");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
