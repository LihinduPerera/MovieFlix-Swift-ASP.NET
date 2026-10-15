using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MovieFlix.Api.Configuration;
using MovieFlix.Api.Data;
using MovieFlix.Api.Exceptions;
using MovieFlix.Api.Models;
using MovieFlix.Api.Services;
using Polly;
using Serilog;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Structured logging via Serilog (configured from the "Serilog" config section).
builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext();

    // Keep test output clean: only warnings and above while testing.
    if (context.HostingEnvironment.IsEnvironment("Testing"))
    {
        configuration.MinimumLevel.Warning();
    }
});

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    // 1. Tell Swagger that our API uses JWT Bearer authentication.
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.ParameterLocation.Header,
        Description = "Enter your JWT access token here."
    });

    // 2. Tell Swagger to send the token with API requests.
    options.AddSecurityRequirement(document => new Microsoft.OpenApi.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.OpenApiSecuritySchemeReference(
                "Bearer",
                document),
            new List<string>()
        }
    });
});

builder.Services.AddControllers();

// API versioning: controllers declare [ApiVersion("1.0")] and the API reports
// the supported versions via the api-supported-versions response header. The
// URL contract (/api/v1/...) is kept stable. AddMvc() wires the versioning
// integration into MVC.
builder.Services
    .AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
    })
    .AddMvc();

// CORS: allowed browser origins are configured (empty by default). Native
// clients are unaffected; this is for a future web front-end.
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("MovieFlixClient", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy
                .WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });
});

// Rate limiting: a global per-client limit plus a stricter "auth" policy for
// credential endpoints to slow down brute-force attempts. Limits are
// configurable via the "RateLimiting" section.
var rateLimiting = builder.Configuration
    .GetSection("RateLimiting")
    .Get<RateLimitingSettings>() ?? new RateLimitingSettings();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
        context =>
        {
            var key = context.User.Identity?.IsAuthenticated == true
                ? context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? "authenticated"
                : context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";

            return RateLimitPartition.GetFixedWindowLimiter(
                $"global:{key}",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = rateLimiting.GlobalPermitLimit,
                    Window = TimeSpan.FromSeconds(rateLimiting.GlobalWindowSeconds),
                    QueueLimit = 0
                });
        });

    options.AddPolicy("auth", context =>
    {
        var key = context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";

        return RateLimitPartition.GetFixedWindowLimiter(
            $"auth:{key}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimiting.AuthPermitLimit,
                Window = TimeSpan.FromSeconds(rateLimiting.AuthWindowSeconds),
                QueueLimit = 0
            });
    });
});

builder.Services.AddHealthChecks()
    .AddDbContextCheck<MovieFlixDbContext>("database");

// The database provider is selectable via configuration so the same codebase
// can run on SQLite (default; used locally and in tests) or PostgreSQL in a
// deployed environment. Set Database:Provider to "Postgres" and provide the
// ConnectionStrings:Postgres connection string to switch.
var databaseProvider = builder.Configuration["Database:Provider"] ?? "Sqlite";

builder.Services.AddDbContext<MovieFlixDbContext>(options =>
{
    if (string.Equals(databaseProvider, "Postgres", StringComparison.OrdinalIgnoreCase))
    {
        var postgresConnection = builder.Configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException(
                "Connection string 'Postgres' is required when Database:Provider is 'Postgres'.");

        options.UseNpgsql(postgresConnection);
    }
    else
    {
        options.UseSqlite(
            builder.Configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=MovieFlix.db");
    }

    // For development
    if (builder.Environment.IsDevelopment())
    {
        options
            .EnableDetailedErrors()
            .EnableSensitiveDataLogging()
            .LogTo(Console.WriteLine);
    }
});

builder.Services.AddHttpContextAccessor();

builder.Services
    .AddHttpClient<ITmdbService, TmdbService>(client =>
    {
        // Timeouts are enforced by the resilience pipeline below (attempt +
        // total), so the HttpClient-level timeout is disabled to avoid it
        // preempting the pipeline.
        client.Timeout = Timeout.InfiniteTimeSpan;
    })
    .AddStandardResilienceHandler(options =>
    {
        // Retry transient failures with exponential backoff + jitter.
        options.Retry.MaxRetryAttempts = 3;
        options.Retry.Delay = TimeSpan.FromMilliseconds(200);
        options.Retry.BackoffType = DelayBackoffType.Exponential;
        options.Retry.UseJitter = true;

        // Per-attempt and whole-request timeouts.
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(20);

        // Circuit breaker: stop hammering TMDB while it is unhealthy.
        options.CircuitBreaker.FailureRatio = 0.5;
        options.CircuitBreaker.MinimumThroughput = 5;
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
        options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);
    });

builder.Services.AddOutputCache(options =>
{
    // Cache expensive TMDB-backed GET responses. A distributed cache can be
    // swapped in later (e.g. Redis) without changing these policies.
    options.AddPolicy("tmdb-search", policy => policy
        .Expire(TimeSpan.FromMinutes(5))
        .SetVaryByQuery("query", "page"));

    options.AddPolicy("tmdb-details", policy => policy
        .Expire(TimeSpan.FromHours(1))
        .SetVaryByRouteValue("tmdbId"));
});

builder.Services.AddScoped<IMovieService, MovieService>();
builder.Services.AddScoped<IMovieCacheService, MovieCacheService>();
builder.Services.AddScoped<IFavoriteService, FavoriteService>();
builder.Services.AddScoped<IWatchlistService, WatchlistService>();
builder.Services.AddScoped<IRatingService, RatingService>();
builder.Services.AddScoped<IWatchHistoryService, WatchHistoryService>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<ICollectionService, CollectionService>();
builder.Services.AddScoped<ISearchHistoryService, SearchHistoryService>();
builder.Services.AddScoped<IPreferencesService, PreferencesService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

builder.Services.Configure<TmdbSettings>(
    builder.Configuration.GetSection("Tmdb"));
builder.Services.Configure<TmdbWarmupSettings>(
    builder.Configuration.GetSection("Tmdb:Warmup"));
builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection("Jwt"));

builder.Services.AddHostedService<MovieCacheWarmupService>();

builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.User.RequireUniqueEmail = true;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<MovieFlixDbContext>()
    .AddDefaultTokenProviders();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtSettings = builder.Configuration.GetSection("Jwt");

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromSeconds(30),

            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],

            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(
                    jwtSettings["Key"]!))
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();

app.UseSerilogRequestLogging();

app.UseHttpsRedirection();

app.UseCors("MovieFlixClient");

app.UseAuthentication();

app.UseAuthorization();

app.UseRateLimiter();

app.UseOutputCache();

app.MapControllers();

app.MapHealthChecks("/health");

await DatabaseSeeder.InitializeAsync(app.Services);

app.Run();

public partial class Program { }
