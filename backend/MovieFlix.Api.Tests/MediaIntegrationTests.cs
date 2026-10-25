using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MovieFlix.Api.DTOs.Common;
using MovieFlix.Api.DTOs.Media;
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

    [Fact]
    public async Task InvalidMediaTypeRoute_ReturnsNotFound()
    {
        using var client = Factory.CreateClient();

        var response = await client.GetAsync("/api/v1/media/person/550");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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
