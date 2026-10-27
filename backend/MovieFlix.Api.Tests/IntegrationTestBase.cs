using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using MovieFlix.Api.DTOs.Auth;
using MovieFlix.Api.Models;

namespace MovieFlix.Api.Tests;

/// <summary>Shared HTTP and serialization helpers for the integration suite.</summary>
public abstract class IntegrationTestBase : IClassFixture<MovieFlixApiFactory>
{
    protected IntegrationTestBase(MovieFlixApiFactory factory)
    {
        Factory = factory;
    }

    protected MovieFlixApiFactory Factory { get; }

    protected static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    protected async Task<LoginResponse> RegisterAsync(
        HttpClient client,
        string? email = null,
        string password = "Password1!",
        string displayName = "Test User")
    {
        email ??= UniqueEmail();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new
            {
                email,
                displayName,
                password
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return await ReadAsync<LoginResponse>(response);
    }

    protected async Task<(HttpClient Client, LoginResponse Session)>
        CreateAuthenticatedClientAsync(
            string? email = null,
            string password = "Password1!",
            string displayName = "Test User")
    {
        var client = Factory.CreateClient();
        var session = await RegisterAsync(
            client,
            email,
            password,
            displayName);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", session.AccessToken);

        return (client, session);
    }

    protected static string UniqueEmail() =>
        $"user-{Guid.NewGuid():N}@example.com";

    protected async Task<LoginResponse> LoginAsync(
        HttpClient client,
        string email,
        string password = "Password1!")
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email, password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await ReadAsync<LoginResponse>(response);
    }

    protected async Task GrantRoleAsync(string userId, string role)
    {
        using var scope = Factory.Services.CreateScope();
        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("Test user was not found.");

        var result = await userManager.AddToRoleAsync(user, role);
        Assert.True(result.Succeeded, string.Join(", ", result.Errors));
    }

    protected static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<T>(json, JsonOptions);

        Assert.NotNull(result);

        return result!;
    }

    protected static async Task<JsonElement> ReadJsonAsync(
        HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        return document.RootElement.Clone();
    }

    protected static async Task<JsonElement> ReadProblemAsync(
        HttpResponseMessage response)
    {
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);

        return await ReadJsonAsync(response);
    }

    protected static void AssertValidationError(
        JsonElement problem,
        string field)
    {
        Assert.True(
            problem.TryGetProperty("errors", out var errors),
            "Problem details did not contain an errors dictionary.");

        Assert.True(
            errors.TryGetProperty(field, out _),
            $"Validation errors did not contain '{field}'. JSON: {problem}");
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

        return options;
    }
}
