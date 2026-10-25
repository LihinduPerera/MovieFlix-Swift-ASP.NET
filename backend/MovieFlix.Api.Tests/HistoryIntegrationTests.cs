using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MovieFlix.Api.DTOs.Common;
using MovieFlix.Api.DTOs.Media;
using MovieFlix.Api.DTOs.Profile;
using MovieFlix.Api.Models;

namespace MovieFlix.Api.Tests;

public sealed class HistoryIntegrationTests : IntegrationTestBase
{
    public HistoryIntegrationTests(MovieFlixApiFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task MovieProgress_AppearsInHistoryAndContinueWatchingUntilCompleted()
    {
        var auth = await CreateAuthenticatedClientAsync();
        using var client = auth.Client;
        Factory.Tmdb.AddMovie(8401, "History Film", "/history.jpg");

        var record = await client.PutAsJsonAsync(
            "/api/v1/me/history/movie/8401",
            new { progressSeconds = 120, completed = false });

        Assert.Equal(HttpStatusCode.OK, record.StatusCode);
        var item = await ReadAsync<WatchHistoryItemResponse>(record);
        Assert.Equal(120, item.ProgressSeconds);
        Assert.False(item.Completed);
        Assert.Equal("History Film", item.Media.Title);

        var history = await ReadAsync<PagedResponse<WatchHistoryItemResponse>>(
            await client.GetAsync("/api/v1/me/history"));
        Assert.Equal(1, history.TotalCount);
        Assert.Equal(120, history.Items[0].ProgressSeconds);

        var continueWatching = await ReadAsync<List<WatchHistoryItemResponse>>(
            await client.GetAsync("/api/v1/me/history/continue"));
        Assert.Single(continueWatching);
        Assert.Equal(8401, continueWatching[0].Media.TmdbId);

        var complete = await client.PutAsJsonAsync(
            "/api/v1/me/history/movie/8401",
            new { progressSeconds = 7200, completed = true });

        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        var completed = await ReadAsync<WatchHistoryItemResponse>(complete);
        Assert.True(completed.Completed);

        var continueAfterCompletion = await ReadAsync<List<WatchHistoryItemResponse>>(
            await client.GetAsync("/api/v1/me/history/continue"));
        Assert.Empty(continueAfterCompletion);

        var remove = await client.DeleteAsync("/api/v1/me/history/movie/8401");
        Assert.Equal(HttpStatusCode.NoContent, remove.StatusCode);

        var removeAgain = await client.DeleteAsync("/api/v1/me/history/movie/8401");
        Assert.Equal(HttpStatusCode.NoContent, removeAgain.StatusCode);

        var empty = await ReadAsync<PagedResponse<WatchHistoryItemResponse>>(
            await client.GetAsync("/api/v1/me/history"));
        Assert.Equal(0, empty.TotalCount);
    }

    [Fact]
    public async Task EpisodeProgress_TracksNextEpisode_AndCrossesSeasons()
    {
        var auth = await CreateAuthenticatedClientAsync();
        using var client = auth.Client;

        var first = await client.PutAsJsonAsync(
            "/api/v1/me/history/tv/1399/episodes/1/1",
            new { progressSeconds = 3600, completed = true });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstResult = await ReadAsync<EpisodeProgressResponse>(first);
        Assert.True(firstResult.Episode.Completed);
        Assert.Equal(1, firstResult.Episode.SeasonNumber);
        Assert.Equal(1, firstResult.Episode.EpisodeNumber);
        Assert.NotNull(firstResult.NextEpisode);
        Assert.Equal(2, firstResult.NextEpisode!.EpisodeNumber);
        Assert.Equal(1, firstResult.NextEpisode.SeasonNumber);
        Assert.False(firstResult.ShowCompleted);

        var second = await client.PutAsJsonAsync(
            "/api/v1/me/history/tv/1399/episodes/1/2",
            new { progressSeconds = 3600, completed = true });

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var secondResult = await ReadAsync<EpisodeProgressResponse>(second);
        Assert.NotNull(secondResult.NextEpisode);
        Assert.Equal(2, secondResult.NextEpisode!.SeasonNumber);
        Assert.Equal(1, secondResult.NextEpisode.EpisodeNumber);
        Assert.False(secondResult.ShowCompleted);

        var third = await client.PutAsJsonAsync(
            "/api/v1/me/history/tv/1399/episodes/2/1",
            new { progressSeconds = 3600, completed = true });
        Assert.Equal(HttpStatusCode.OK, third.StatusCode);

        var fourth = await client.PutAsJsonAsync(
            "/api/v1/me/history/tv/1399/episodes/2/2",
            new { progressSeconds = 3600, completed = true });

        Assert.Equal(HttpStatusCode.OK, fourth.StatusCode);
        var finale = await ReadAsync<EpisodeProgressResponse>(fourth);
        Assert.Null(finale.NextEpisode);
        Assert.True(finale.ShowCompleted);

        var tvHistoryResponse = await client.GetAsync(
            "/api/v1/me/history/tv/1399");
        Assert.Equal(HttpStatusCode.OK, tvHistoryResponse.StatusCode);

        var tvHistory = await ReadAsync<TvHistoryResponse>(tvHistoryResponse);
        Assert.Equal(4, tvHistory.Episodes.Count);
        Assert.Null(tvHistory.NextEpisode);
        Assert.True(tvHistory.ShowCompleted);
        Assert.Equal("Game of Thrones", tvHistory.Media.Title);
        Assert.All(
            tvHistory.Episodes,
            state => Assert.True(state.Episode.Completed));

        var mediaState = await ReadAsync<MediaStateResponse>(
            await client.GetAsync("/api/v1/media/tv/1399/state"));
        Assert.True(mediaState.Completed);
        Assert.NotNull(mediaState.LastEpisode);
        Assert.Null(mediaState.NextEpisode);

        var continueWatching = await ReadAsync<List<WatchHistoryItemResponse>>(
            await client.GetAsync("/api/v1/me/history/continue"));
        Assert.Empty(continueWatching);
    }

    [Fact]
    public async Task TvHistory_ExposesTrackedStatesAndNextEpisode()
    {
        var auth = await CreateAuthenticatedClientAsync();
        using var client = auth.Client;

        await client.PutAsJsonAsync(
            "/api/v1/me/history/tv/1399/episodes/1/1",
            new { progressSeconds = 1800, completed = false });

        var response = await client.GetAsync("/api/v1/me/history/tv/1399");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var history = await ReadAsync<TvHistoryResponse>(response);
        Assert.Single(history.Episodes);
        Assert.Equal(1, history.Episodes[0].Episode.SeasonNumber);
        Assert.Equal(1, history.Episodes[0].Episode.EpisodeNumber);
        Assert.Equal(1800, history.Episodes[0].Episode.ProgressSeconds);
        Assert.False(history.Episodes[0].Episode.Completed);

        Assert.NotNull(history.NextEpisode);
        Assert.Equal(1, history.NextEpisode!.SeasonNumber);
        Assert.Equal(1, history.NextEpisode.EpisodeNumber);
        Assert.False(history.ShowCompleted);
    }

    [Fact]
    public async Task MediaLevelTvProgress_DelegatesWhenEpisodeIsSupplied()
    {
        var auth = await CreateAuthenticatedClientAsync();
        using var client = auth.Client;

        var response = await client.PutAsJsonAsync(
            "/api/v1/me/history/tv/1399",
            new
            {
                progressSeconds = 1200,
                completed = false,
                seasonNumber = 1,
                episodeNumber = 1
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var item = await ReadAsync<WatchHistoryItemResponse>(response);
        Assert.Equal(MediaType.Tv, item.Media.MediaType);
        Assert.NotNull(item.LastEpisode);
        Assert.Equal(1, item.LastEpisode!.EpisodeNumber);

        var mediaState = await ReadAsync<MediaStateResponse>(
            await client.GetAsync("/api/v1/media/tv/1399/state"));
        Assert.Equal(1200, mediaState.ProgressSeconds);
        Assert.NotNull(mediaState.LastEpisode);
    }

    [Fact]
    public async Task History_ValidatesEpisodeInputs()
    {
        var auth = await CreateAuthenticatedClientAsync();
        using var client = auth.Client;

        var seasonZero = await client.PutAsJsonAsync(
            "/api/v1/me/history/tv/1399/episodes/0/1",
            new { progressSeconds = 10, completed = true });

        Assert.Equal(HttpStatusCode.BadRequest, seasonZero.StatusCode);
        AssertValidationError(await ReadProblemAsync(seasonZero), "seasonNumber");

        var unknownEpisode = await client.PutAsJsonAsync(
            "/api/v1/me/history/tv/1399/episodes/3/1",
            new { progressSeconds = 10, completed = true });

        Assert.Equal(HttpStatusCode.NotFound, unknownEpisode.StatusCode);

        var movieWithEpisode = await client.PutAsJsonAsync(
            "/api/v1/me/history/movie/550",
            new
            {
                progressSeconds = 10,
                completed = false,
                seasonNumber = 1,
                episodeNumber = 1
            });

        Assert.Equal(HttpStatusCode.BadRequest, movieWithEpisode.StatusCode);

        var onlySeason = await client.PutAsJsonAsync(
            "/api/v1/me/history/tv/1399",
            new
            {
                progressSeconds = 10,
                completed = false,
                seasonNumber = 1
            });

        Assert.Equal(HttpStatusCode.BadRequest, onlySeason.StatusCode);
        AssertValidationError(await ReadProblemAsync(onlySeason), "episodeNumber");
    }

    [Fact]
    public async Task History_ClearRemovesAllTitlesForUserOnly()
    {
        var first = await CreateAuthenticatedClientAsync();
        using var firstClient = first.Client;
        var second = await CreateAuthenticatedClientAsync();
        using var secondClient = second.Client;

        Factory.Tmdb.AddMovie(8402, "First User Film", "/first.jpg");
        Factory.Tmdb.AddMovie(8403, "Second User Film", "/second.jpg");

        await firstClient.PutAsJsonAsync(
            "/api/v1/me/history/movie/8402",
            new { progressSeconds = 100, completed = false });
        await secondClient.PutAsJsonAsync(
            "/api/v1/me/history/movie/8403",
            new { progressSeconds = 100, completed = false });

        var clear = await firstClient.DeleteAsync("/api/v1/me/history");
        Assert.Equal(HttpStatusCode.NoContent, clear.StatusCode);

        var firstHistory = await ReadAsync<PagedResponse<WatchHistoryItemResponse>>(
            await firstClient.GetAsync("/api/v1/me/history"));
        Assert.Equal(0, firstHistory.TotalCount);

        var secondHistory = await ReadAsync<PagedResponse<WatchHistoryItemResponse>>(
            await secondClient.GetAsync("/api/v1/me/history"));
        Assert.Equal(1, secondHistory.TotalCount);
    }

    [Fact]
    public async Task HistoryTimestamps_AreSerializedAsUtc()
    {
        var auth = await CreateAuthenticatedClientAsync();
        using var client = auth.Client;

        Factory.Tmdb.AddMovie(8404, "Timestamp Film", "/timestamp.jpg");

        var response = await client.PutAsJsonAsync(
            "/api/v1/me/history/movie/8404",
            new { progressSeconds = 1, completed = false });

        var json = await ReadJsonAsync(response);
        var lastWatchedAt = json.GetProperty("lastWatchedAt").GetString();

        Assert.NotNull(lastWatchedAt);
        Assert.EndsWith("Z", lastWatchedAt);
        Assert.Equal(
            Factory.Clock.GetUtcNow().UtcDateTime,
            DateTime.Parse(lastWatchedAt, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal));
    }
}
