using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using MovieFlix.Api.DTOs.Common;
using MovieFlix.Api.DTOs.Media;

namespace MovieFlix.Api.Tests;

public sealed class MediaRefreshIntegrationTests : IntegrationTestBase
{
    public MediaRefreshIntegrationTests(MovieFlixApiFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task AdminRefresh_UpdatesMetadata_WithoutLosingWatchProgress()
    {
        const int movieId = 880101;

        Factory.Tmdb.AddMovie(movieId, "Refresh Before");

        var auth = await CreateAuthenticatedClientAsync();
        using var client = auth.Client;

        var progress = await client.PutAsJsonAsync(
            $"/api/v1/me/history/movie/{movieId}",
            new { progressSeconds = 600, completed = false });

        Assert.Equal(HttpStatusCode.OK, progress.StatusCode);

        Factory.Tmdb.GetMovieDetails(movieId).Title = "Refresh After";

        await GrantRoleAsync(auth.Session.User.Id, "Admin");

        var adminSession = await LoginAsync(
            client,
            auth.Session.User.Email);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminSession.AccessToken);

        var refresh = await client.PostAsync(
            $"/api/v1/admin/media/movie/{movieId}/refresh",
            content: null);

        Assert.Equal(HttpStatusCode.NoContent, refresh.StatusCode);

        var history = await ReadAsync<PagedResponse<WatchHistoryItemResponse>>(
            await client.GetAsync("/api/v1/me/history"));

        var item = Assert.Single(history.Items);
        Assert.Equal(movieId, item.Media.TmdbId);
        Assert.Equal("Refresh After", item.Media.Title);
        Assert.Equal(600, item.ProgressSeconds);
        Assert.False(item.Completed);
    }
}
