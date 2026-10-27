using System.Net;
using System.Net.Http.Json;
using MovieFlix.Api.DTOs.Collections;
using MovieFlix.Api.DTOs.Profile;
using MovieFlix.Api.DTOs.SearchHistory;

namespace MovieFlix.Api.Tests;

public sealed class ProfileIntegrationTests : IntegrationTestBase
{
    public ProfileIntegrationTests(MovieFlixApiFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task Profile_CanBeReadAndUpdated()
    {
        var auth = await CreateAuthenticatedClientAsync(
            displayName: "Initial Name");
        using var client = auth.Client;

        var initial = await ReadAsync<MeResponse>(
            await client.GetAsync("/api/v1/me"));
        Assert.Equal("Initial Name", initial.DisplayName);

        var update = await client.PatchAsJsonAsync(
            "/api/v1/me",
            new { displayName = "  Updated Name  " });

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await ReadAsync<MeResponse>(update);
        Assert.Equal("Updated Name", updated.DisplayName);

        var blank = await client.PatchAsJsonAsync(
            "/api/v1/me",
            new { displayName = "" });

        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);
        AssertValidationError(await ReadProblemAsync(blank), "DisplayName");
    }

    [Fact]
    public async Task Stats_AggregateLibraryAndWatchActivity()
    {
        var auth = await CreateAuthenticatedClientAsync();
        using var client = auth.Client;
        Factory.Tmdb.AddMovie(8601, "Stats Film", "/stats.jpg");

        await client.PutAsync("/api/v1/me/favorites/movie/8601", null);
        await client.PutAsync("/api/v1/me/watchlist/movie/8601", null);
        await client.PutAsJsonAsync(
            "/api/v1/me/ratings/movie/8601",
            new { rating = 4 });
        await client.PostAsJsonAsync(
            "/api/v1/media/movie/8601/reviews",
            new { content = "Stats review." });
        await client.PostAsJsonAsync(
            "/api/v1/me/collections",
            new { name = "Stats Collection", description = "" });

        await client.PutAsJsonAsync(
            "/api/v1/me/history/movie/8601",
            new { progressSeconds = 7200, completed = true });

        await client.PutAsJsonAsync(
            "/api/v1/me/history/tv/1396/episodes/1/1",
            new { progressSeconds = 3480, completed = true });
        await client.PutAsJsonAsync(
            "/api/v1/me/history/tv/1396/episodes/1/2",
            new { progressSeconds = 2880, completed = true });

        var statsResponse = await client.GetAsync("/api/v1/me/stats");
        Assert.Equal(HttpStatusCode.OK, statsResponse.StatusCode);

        var stats = await ReadAsync<StatsResponse>(statsResponse);
        Assert.Equal(1, stats.Favorites);
        Assert.Equal(1, stats.Watchlist);
        Assert.Equal(1, stats.Ratings);
        Assert.Equal(1, stats.Reviews);
        Assert.Equal(1, stats.Collections);
        Assert.Equal(1, stats.MoviesWatched);
        Assert.Equal(1, stats.ShowsWatched);
        Assert.Equal(2, stats.EpisodesWatched);
    }
}

public sealed class PreferencesIntegrationTests : IntegrationTestBase
{
    public PreferencesIntegrationTests(MovieFlixApiFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task Preferences_DefaultThenUpdate()
    {
        var auth = await CreateAuthenticatedClientAsync();
        using var client = auth.Client;

        var initialResponse = await client.GetAsync("/api/v1/me/preferences");
        Assert.Equal(HttpStatusCode.OK, initialResponse.StatusCode);

        var initial = await ReadAsync<DTOs.Preferences.PreferencesResponse>(
            initialResponse);
        Assert.Equal("en-US", initial.PreferredLanguage);
        Assert.Equal("US", initial.PreferredRegion);
        Assert.Equal("system", initial.Theme);
        Assert.True(initial.AutoPlayTrailers);
        Assert.True(initial.EmailNotifications);
        Assert.True(initial.MatureContentFilter);

        var update = await client.PutAsJsonAsync(
            "/api/v1/me/preferences",
            new
            {
                preferredLanguage = "fr-FR",
                preferredRegion = "FR",
                theme = "dark",
                autoPlayTrailers = false,
                emailNotifications = false,
                matureContentFilter = false
            });

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await ReadAsync<DTOs.Preferences.PreferencesResponse>(
            update);
        Assert.Equal("fr-FR", updated.PreferredLanguage);
        Assert.Equal("FR", updated.PreferredRegion);
        Assert.Equal("dark", updated.Theme);
        Assert.False(updated.AutoPlayTrailers);
        Assert.False(updated.EmailNotifications);
        Assert.False(updated.MatureContentFilter);

        var fromDatabase = await ReadAsync<DTOs.Preferences.PreferencesResponse>(
            await client.GetAsync("/api/v1/me/preferences"));
        Assert.Equal("fr-FR", fromDatabase.PreferredLanguage);

        var invalid = await client.PutAsJsonAsync(
            "/api/v1/me/preferences",
            new
            {
                preferredLanguage = "xx",
                preferredRegion = "USA",
                theme = "neon",
                autoPlayTrailers = true,
                emailNotifications = true,
                matureContentFilter = true
            });

        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var problem = await ReadProblemAsync(invalid);
        AssertValidationError(problem, "PreferredRegion");
        AssertValidationError(problem, "Theme");
    }

    [Fact]
    public async Task Preferences_RequireAuthentication()
    {
        using var client = Factory.CreateClient();

        var response = await client.GetAsync("/api/v1/me/preferences");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

public sealed class SearchHistoryIntegrationTests : IntegrationTestBase
{
    public SearchHistoryIntegrationTests(MovieFlixApiFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task SearchHistory_DeduplicatesCaseInsensitively()
    {
        var auth = await CreateAuthenticatedClientAsync();
        using var client = auth.Client;

        var first = await RecordAsync(client, "Dune");
        Assert.Equal("Dune", first.Query);

        var second = await RecordAsync(client, "  dune  ");
        Assert.Equal(first.Id, second.Id);
        Assert.Equal("dune", second.Query);

        var history = await ReadAsync<List<SearchHistoryResponse>>(
            await client.GetAsync("/api/v1/me/search-history"));

        var entry = Assert.Single(history);
        Assert.Equal(first.Id, entry.Id);
        Assert.Equal("dune", entry.Query);
    }

    [Fact]
    public async Task SearchHistory_OrdersByMostRecent_AndHonorsLimit()
    {
        var auth = await CreateAuthenticatedClientAsync();
        using var client = auth.Client;

        await RecordAsync(client, "First");
        Factory.Clock.Advance(TimeSpan.FromMinutes(1));
        await RecordAsync(client, "Second");
        Factory.Clock.Advance(TimeSpan.FromMinutes(1));
        await RecordAsync(client, "Third");

        var all = await ReadAsync<List<SearchHistoryResponse>>(
            await client.GetAsync("/api/v1/me/search-history"));

        Assert.Equal(["Third", "Second", "First"], all.Select(item => item.Query));

        var limited = await ReadAsync<List<SearchHistoryResponse>>(
            await client.GetAsync("/api/v1/me/search-history?limit=1"));

        Assert.Single(limited);
        Assert.Equal("Third", limited[0].Query);
    }

    [Fact]
    public async Task SearchHistory_DeleteIsScopedToTheOwner()
    {
        var first = await CreateAuthenticatedClientAsync();
        using var firstClient = first.Client;
        var second = await CreateAuthenticatedClientAsync();
        using var secondClient = second.Client;

        var firstEntry = await RecordAsync(firstClient, "Private search");
        var secondEntry = await RecordAsync(secondClient, "Other search");

        var forbidden = await firstClient.DeleteAsync(
            $"/api/v1/me/search-history/{secondEntry.Id}");
        Assert.Equal(HttpStatusCode.NotFound, forbidden.StatusCode);

        var remove = await firstClient.DeleteAsync(
            $"/api/v1/me/search-history/{firstEntry.Id}");
        Assert.Equal(HttpStatusCode.NoContent, remove.StatusCode);

        var removedAgain = await firstClient.DeleteAsync(
            $"/api/v1/me/search-history/{firstEntry.Id}");
        Assert.Equal(HttpStatusCode.NotFound, removedAgain.StatusCode);

        var secondList = await ReadAsync<List<SearchHistoryResponse>>(
            await secondClient.GetAsync("/api/v1/me/search-history"));
        Assert.Single(secondList);
        Assert.Equal(secondEntry.Id, secondList[0].Id);
    }

    [Fact]
    public async Task SearchHistory_ClearOnlyClearsCurrentUser()
    {
        var first = await CreateAuthenticatedClientAsync();
        using var firstClient = first.Client;
        var second = await CreateAuthenticatedClientAsync();
        using var secondClient = second.Client;

        await RecordAsync(firstClient, "First user query");
        await RecordAsync(secondClient, "Second user query");

        var clear = await firstClient.DeleteAsync("/api/v1/me/search-history");
        Assert.Equal(HttpStatusCode.NoContent, clear.StatusCode);

        var firstHistory = await ReadAsync<List<SearchHistoryResponse>>(
            await firstClient.GetAsync("/api/v1/me/search-history"));
        Assert.Empty(firstHistory);

        var secondHistory = await ReadAsync<List<SearchHistoryResponse>>(
            await secondClient.GetAsync("/api/v1/me/search-history"));
        Assert.Single(secondHistory);
    }

    [Fact]
    public async Task SearchHistory_RejectsEmptyQuery()
    {
        var auth = await CreateAuthenticatedClientAsync();
        using var client = auth.Client;

        var response = await client.PostAsJsonAsync(
            "/api/v1/me/search-history",
            new { query = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertValidationError(await ReadProblemAsync(response), "Query");
    }

    private static async Task<SearchHistoryResponse> RecordAsync(
        HttpClient client,
        string query)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/me/search-history",
            new { query });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await ReadAsync<SearchHistoryResponse>(response);
    }
}
