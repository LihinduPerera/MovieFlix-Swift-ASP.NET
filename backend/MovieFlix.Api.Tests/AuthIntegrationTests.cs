using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using MovieFlix.Api.DTOs.Auth;
using MovieFlix.Api.DTOs.Profile;

namespace MovieFlix.Api.Tests;

public sealed class AuthIntegrationTests : IntegrationTestBase
{
    public AuthIntegrationTests(MovieFlixApiFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task Register_ReturnsCreatedUserAndTokens()
    {
        using var client = Factory.CreateClient();
        var email = UniqueEmail();

        var session = await RegisterAsync(client, email, displayName: "Alice");

        Assert.False(string.IsNullOrWhiteSpace(session.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(session.RefreshToken));
        Assert.Equal("Bearer", session.TokenType);
        Assert.Equal(email, session.User.Email);
        Assert.Equal("Alice", session.User.DisplayName);
        Assert.Contains("User", session.User.Roles);
        Assert.True(session.AccessTokenExpiresAt > DateTime.UtcNow);
        Assert.True(session.RefreshTokenExpiresAt > session.AccessTokenExpiresAt);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", session.AccessToken);

        var meResponse = await client.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);

        var me = await ReadAsync<MeResponse>(meResponse);
        Assert.Equal(email, me.Email);
        Assert.Equal("Alice", me.DisplayName);
        Assert.Contains("User", me.Roles);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsConflict()
    {
        using var client = Factory.CreateClient();
        var email = UniqueEmail();
        await RegisterAsync(client, email);

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new
            {
                email,
                displayName = "Duplicate",
                password = "Password1!"
            });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await ReadProblemAsync(response);
        Assert.Equal(409, problem.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Register_InvalidInput_ReturnsValidationProblem()
    {
        using var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new
            {
                email = "not-an-email",
                displayName = "",
                password = "short"
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ReadProblemAsync(response);
        Assert.Equal(400, problem.GetProperty("status").GetInt32());
        AssertValidationError(problem, "Email");
        AssertValidationError(problem, "Password");
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_ReturnsProblemDetails()
    {
        using var client = Factory.CreateClient();

        var response = await client.GetAsync("/api/v1/me/favorites");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await ReadProblemAsync(response);
        Assert.Equal(401, problem.GetProperty("status").GetInt32());
        Assert.True(problem.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task Refresh_RotatesToken_AndReuseRevokesChain()
    {
        using var client = Factory.CreateClient();
        var initial = await RegisterAsync(client);

        var firstRefresh = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new { refreshToken = initial.RefreshToken });

        Assert.Equal(HttpStatusCode.OK, firstRefresh.StatusCode);
        var rotated = await ReadAsync<LoginResponse>(firstRefresh);
        Assert.NotEqual(initial.RefreshToken, rotated.RefreshToken);
        Assert.NotEqual(initial.AccessToken, rotated.AccessToken);

        // Reusing the consumed token must fail...
        var reuse = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new { refreshToken = initial.RefreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);

        // ...and revoke the replacement token as a reuse-detection measure.
        var chainBurned = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new { refreshToken = rotated.RefreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, chainBurned.StatusCode);
    }

    [Fact]
    public async Task Logout_RevokesRefreshTokens()
    {
        var (client, session) = await CreateAuthenticatedClientAsync();
        using var _ = client;

        var newLogin = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email = session.User.Email, password = "Password1!" });

        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
        var login = await ReadAsync<LoginResponse>(newLogin);

        var logout = await client.PostAsync("/api/v1/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var refresh = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new { refreshToken = login.RefreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Login_WrongPassword_LocksAccountAfterFiveFailures()
    {
        using var client = Factory.CreateClient();
        var email = UniqueEmail();
        await RegisterAsync(client, email);

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            var failed = await client.PostAsJsonAsync(
                "/api/v1/auth/login",
                new { email, password = "WrongPassword1!" });

            Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
        }

        var locked = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email, password = "WrongPassword1!" });

        Assert.Equal(HttpStatusCode.Locked, locked.StatusCode);
        var problem = await ReadProblemAsync(locked);
        Assert.Equal(423, problem.GetProperty("status").GetInt32());

        var correctPasswordWhileLocked = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email, password = "Password1!" });

        Assert.Equal(HttpStatusCode.Locked, correctPasswordWhileLocked.StatusCode);
    }

    [Fact]
    public async Task InvalidRefreshToken_ReturnsUnauthorized()
    {
        using var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new { refreshToken = "not-a-real-token" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await ReadProblemAsync(response);
    }
}
