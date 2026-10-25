using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MovieFlix.Api.Data;
using MovieFlix.Api.Services;

namespace MovieFlix.Api.Tests;

/// <summary>
/// Boots the real API pipeline against an isolated in-memory SQLite database,
/// a deterministic clock and an in-memory TMDB replacement. No test can reach
/// the network or a developer database.
/// </summary>
public sealed class MovieFlixApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection =
        new("Data Source=:memory:");

    public MovieFlixApiFactory()
    {
        _connection.Open();
    }

    public FakeTmdbService Tmdb { get; } = new();

    // JWT validation still uses the framework clock. Start slightly in the
    // past so tests can advance the test clock without issuing not-yet-valid
    // tokens, while keeping the 60-minute access-token lifetime comfortable.
    public TestTimeProvider Clock { get; } =
        new(DateTimeOffset.UtcNow.AddMinutes(-30));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Use a dedicated environment so app settings aimed at local
        // development (Swagger, EF console logging, request logging) stay out
        // of the test runs. It also loads appsettings.Testing.json, which
        // supplies self-contained JWT/TMDB config and relaxed rate limits so
        // tests never depend on developer user-secrets or the network.
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<MovieFlixDbContext>>();

            services.AddDbContext<MovieFlixDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });

            services.RemoveAll<ITmdbService>();
            services.AddSingleton<ITmdbService>(Tmdb);

            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
