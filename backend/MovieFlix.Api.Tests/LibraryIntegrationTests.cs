using System.Net;
using System.Net.Http.Json;
using MovieFlix.Api.DTOs.Common;
using MovieFlix.Api.DTOs.Media;
using MovieFlix.Api.Models;

namespace MovieFlix.Api.Tests;

public sealed class LibraryIntegrationTests : IntegrationTestBase
{
    public LibraryIntegrationTests(MovieFlixApiFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task Favorites_AreIdempotent_AndExposeMediaMetadata()
    {
        var auth = await CreateAuthenticatedClientAsync();
        using var client = auth.Client;
        Factory.Tmdb.AddMovie(8101, "Favorite Film", "/favorite.jpg");

        var first = await client.PutAsync(
            "/api/v1/me/favorites/movie/8101",
            null);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var saved = await ReadAsync<SavedMediaResponse>(first);
        Assert.Equal("Favorite Film", saved.Media.Title);
        Assert.Equal("/favorite.jpg", saved.Media.PosterPath);
        Assert.Equal(MediaType.Movie, saved.Media.MediaType);

        var second = await client.PutAsync(
            "/api/v1/me/favorites/movie/8101",
            null);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var listResponse = await client.GetAsync("/api/v1/me/favorites");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var page = await ReadAsync<PagedResponse<SavedMediaResponse>>(listResponse);
        Assert.Equal(1, page.TotalCount);
        var savedItem = Assert.Single(page.Items);
        Assert.Equal(8101, savedItem.Media.TmdbId);
        Assert.Equal("/favorite.jpg", savedItem.Media.PosterPath);
        Assert.NotEqual(default, savedItem.SavedAt);

        var delete = await client.DeleteAsync(
            "/api/v1/me/favorites/movie/8101");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var deleteAgain = await client.DeleteAsync(
            "/api/v1/me/favorites/movie/8101");
        Assert.Equal(HttpStatusCode.NoContent, deleteAgain.StatusCode);

        var empty = await ReadAsync<PagedResponse<SavedMediaResponse>>(
            await client.GetAsync("/api/v1/me/favorites"));
        Assert.Equal(0, empty.TotalCount);
    }

    [Fact]
    public async Task Favorites_FilterByMediaType_AndSortByTitle()
    {
        var auth = await CreateAuthenticatedClientAsync();
        using var client = auth.Client;

        Factory.Tmdb.AddMovie(8102, "Zulu", "/zulu.jpg", "2020-01-01");
        Factory.Tmdb.AddMovie(8103, "Alpha", "/alpha.jpg", "2021-01-01");

        Assert.Equal(
            HttpStatusCode.OK,
            (await client.PutAsync("/api/v1/me/favorites/movie/8102", null)).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.PutAsync("/api/v1/me/favorites/movie/8103", null)).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.PutAsync("/api/v1/me/favorites/tv/1399", null)).StatusCode);

        var tvPage = await ReadAsync<PagedResponse<SavedMediaResponse>>(
            await client.GetAsync("/api/v1/me/favorites?mediaType=tv"));

        Assert.Equal(1, tvPage.TotalCount);
        Assert.All(tvPage.Items, item => Assert.Equal(MediaType.Tv, item.Media.MediaType));

        var sorted = await ReadAsync<PagedResponse<SavedMediaResponse>>(
            await client.GetAsync("/api/v1/me/favorites?sort=title_asc"));

        Assert.Equal(3, sorted.TotalCount);
        Assert.Equal("Alpha", sorted.Items[0].Media.Title);
        Assert.Equal("Game of Thrones", sorted.Items[1].Media.Title);
        Assert.Equal("Zulu", sorted.Items[2].Media.Title);

        var invalidSort = await client.GetAsync(
            "/api/v1/me/favorites?sort=not_a_sort");

        Assert.Equal(HttpStatusCode.BadRequest, invalidSort.StatusCode);
        var problem = await ReadProblemAsync(invalidSort);
        AssertValidationError(problem, "sort");
    }

    [Fact]
    public async Task Watchlist_IsIdempotent_AndIndependentFromFavorites()
    {
        var auth = await CreateAuthenticatedClientAsync();
        using var client = auth.Client;
        Factory.Tmdb.AddMovie(8104, "Watchlist Film", "/watchlist.jpg");

        var add = await client.PutAsync(
            "/api/v1/me/watchlist/movie/8104",
            null);
        Assert.Equal(HttpStatusCode.OK, add.StatusCode);

        var addAgain = await client.PutAsync(
            "/api/v1/me/watchlist/movie/8104",
            null);
        Assert.Equal(HttpStatusCode.OK, addAgain.StatusCode);

        var watchlist = await ReadAsync<PagedResponse<SavedMediaResponse>>(
            await client.GetAsync("/api/v1/me/watchlist"));
        Assert.Equal(1, watchlist.TotalCount);

        var favorites = await ReadAsync<PagedResponse<SavedMediaResponse>>(
            await client.GetAsync("/api/v1/me/favorites"));
        Assert.Equal(0, favorites.TotalCount);

        var remove = await client.DeleteAsync(
            "/api/v1/me/watchlist/movie/8104");
        Assert.Equal(HttpStatusCode.NoContent, remove.StatusCode);

        var removeAgain = await client.DeleteAsync(
            "/api/v1/me/watchlist/movie/8104");
        Assert.Equal(HttpStatusCode.NoContent, removeAgain.StatusCode);
    }

    [Fact]
    public async Task MediaState_ReflectsFavoritesAndWatchlist()
    {
        var auth = await CreateAuthenticatedClientAsync();
        using var client = auth.Client;
        Factory.Tmdb.AddMovie(8105, "State Film", "/state.jpg");

        await client.PutAsync("/api/v1/me/favorites/movie/8105", null);
        await client.PutAsync("/api/v1/me/watchlist/movie/8105", null);

        var stateResponse = await client.GetAsync(
            "/api/v1/media/movie/8105/state");
        Assert.Equal(HttpStatusCode.OK, stateResponse.StatusCode);

        var state = await ReadAsync<MediaStateResponse>(stateResponse);
        Assert.True(state.IsFavorite);
        Assert.True(state.IsInWatchlist);
        Assert.False(state.HasReview);
        Assert.Null(state.UserRating);

        using var anonymous = Factory.CreateClient();
        var anonymousState = await ReadAsync<MediaStateResponse>(
            await anonymous.GetAsync("/api/v1/media/movie/8105/state"));

        Assert.False(anonymousState.IsFavorite);
        Assert.False(anonymousState.IsInWatchlist);
    }

    [Fact]
    public async Task Library_RequiresAuthentication()
    {
        using var client = Factory.CreateClient();

        var response = await client.GetAsync("/api/v1/me/watchlist");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await ReadProblemAsync(response);
    }
}
