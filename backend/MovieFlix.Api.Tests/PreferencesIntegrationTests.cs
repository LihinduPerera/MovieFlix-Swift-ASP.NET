using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MovieFlix.Api.Data;
using MovieFlix.Api.DTOs.Auth;
using MovieFlix.Api.DTOs.Preferences;
using MovieFlix.Api.Models;

namespace MovieFlix.Api.Tests;

public class PreferencesIntegrationTests : IDisposable
{
    private readonly MovieFlixApiFactory _factory;
    private readonly HttpClient _client;

    public PreferencesIntegrationTests()
    {
        _factory = new MovieFlixApiFactory();
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private async Task<(string userId, string password)> CreateUserAsync(
        IServiceScope scope, string email)
    {
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        const string password = "TestPassword123!";
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            NormalizedUserName = email.ToUpperInvariant(),
            DisplayName = "Prefs Tester",
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await userManager.CreateAsync(user, password);
        await userManager.AddToRoleAsync(user, "User");
        return (user.Id, password);
    }

    private async Task<LoginResponse> LoginAsync(string email, string password)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/Auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(result);
        return result;
    }

    private void Authorize(LoginResponse login) =>
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

    [Fact]
    public async Task Get_OnFirstAccess_CreatesDefaults()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MovieFlixDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        await CreateUserAsync(scope, "prefs@example.com");

        var login = await LoginAsync("prefs@example.com", "TestPassword123!");
        Authorize(login);

        Assert.Equal(0, await dbContext.UserPreferences.CountAsync());

        var response = await _client.GetAsync("/api/v1/me/preferences");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var prefs = await response.Content.ReadFromJsonAsync<PreferencesResponse>();
        Assert.NotNull(prefs);
        Assert.Equal("en-US", prefs.PreferredLanguage);
        Assert.Equal("system", prefs.Theme);
        Assert.True(prefs.AutoPlayTrailers);

        Assert.Equal(1, await dbContext.UserPreferences.CountAsync());

        // A second call must not create another row.
        await _client.GetAsync("/api/v1/me/preferences");
        Assert.Equal(1, await dbContext.UserPreferences.CountAsync());
    }

    [Fact]
    public async Task Update_ThenGet_ReturnsUpdatedValues()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MovieFlixDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        await CreateUserAsync(scope, "update-prefs@example.com");

        var login = await LoginAsync("update-prefs@example.com", "TestPassword123!");
        Authorize(login);

        var updateResponse = await _client.PutAsJsonAsync(
            "/api/v1/me/preferences",
            new UpdatePreferencesRequest
            {
                PreferredLanguage = "fr-FR",
                PreferredRegion = "FR",
                Theme = "dark",
                AutoPlayTrailers = false,
                EmailNotifications = false,
                MatureContentFilter = false
            });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<PreferencesResponse>();
        Assert.NotNull(updated);
        Assert.Equal("fr-FR", updated.PreferredLanguage);
        Assert.Equal("dark", updated.Theme);
        Assert.False(updated.AutoPlayTrailers);

        var getResponse = await _client.GetAsync("/api/v1/me/preferences");
        var fetched = await getResponse.Content.ReadFromJsonAsync<PreferencesResponse>();
        Assert.NotNull(fetched);
        Assert.Equal("FR", fetched.PreferredRegion);
        Assert.False(fetched.MatureContentFilter);
    }

    [Fact]
    public async Task Update_WithInvalidTheme_Returns400()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MovieFlixDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        await CreateUserAsync(scope, "bad-prefs@example.com");

        var login = await LoginAsync("bad-prefs@example.com", "TestPassword123!");
        Authorize(login);

        var response = await _client.PutAsJsonAsync(
            "/api/v1/me/preferences",
            new { theme = "neon", preferredLanguage = "en-US", preferredRegion = "US" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Preferences_AreIsolatedPerUser()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MovieFlixDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        await CreateUserAsync(scope, "alice-prefs@example.com");
        await CreateUserAsync(scope, "bob-prefs@example.com");

        var aliceLogin = await LoginAsync("alice-prefs@example.com", "TestPassword123!");
        Authorize(aliceLogin);
        await _client.PutAsJsonAsync("/api/v1/me/preferences",
            new UpdatePreferencesRequest
            {
                PreferredLanguage = "de-DE",
                PreferredRegion = "DE",
                Theme = "light",
                AutoPlayTrailers = false,
                EmailNotifications = false,
                MatureContentFilter = false
            });

        var bobLogin = await LoginAsync("bob-prefs@example.com", "TestPassword123!");
        Authorize(bobLogin);

        var response = await _client.GetAsync("/api/v1/me/preferences");
        var bobPrefs = await response.Content.ReadFromJsonAsync<PreferencesResponse>();
        Assert.NotNull(bobPrefs);
        Assert.Equal("en-US", bobPrefs.PreferredLanguage);
        Assert.Equal("system", bobPrefs.Theme);
    }

    [Fact]
    public async Task Preferences_WhenNotAuthenticated_Returns401()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MovieFlixDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        var response = await _client.GetAsync("/api/v1/me/preferences");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
