using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using MovieFlix.Api.Configuration;
using MovieFlix.Api.Data;
using MovieFlix.Api.Exceptions;
using MovieFlix.Api.Models;
using MovieFlix.Api.Services;
using MovieFlix.Api.Swagger;
using Polly;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext();

    if (context.HostingEnvironment.IsEnvironment("Testing"))
    {
        configuration.MinimumLevel.Warning();
    }
});

builder.Services.AddSingleton(TimeProvider.System);

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    });

builder.Services
    .AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
    })
    .AddMvc();

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

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        ["application/json", "application/problem+json"]);
});

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
                    QueueLimit = 0,
                    AutoReplenishment = true
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
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });

    options.OnRejected = async (context, cancellationToken) =>
    {
        var httpContext = context.HttpContext;

        var retryAfter = TimeSpan.FromSeconds(
            Math.Max(1, rateLimiting.GlobalWindowSeconds));

        if (context.Lease.TryGetMetadata(
            MetadataName.RetryAfter,
            out var leaseRetryAfter))
        {
            retryAfter = leaseRetryAfter;
        }

        httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        httpContext.Response.Headers.RetryAfter =
            Math.Ceiling(retryAfter.TotalSeconds).ToString(
                System.Globalization.CultureInfo.InvariantCulture);

        var logger = httpContext.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("RateLimiting");

        logger.LogWarning(
            "Rate limit exceeded for {Method} {Path} from {RemoteIp}. TraceId: {TraceId}",
            httpContext.Request.Method,
            httpContext.Request.Path,
            httpContext.Connection.RemoteIpAddress,
            httpContext.TraceIdentifier);

        var problem = new ProblemDetails
        {
            Type = ProblemTypes.RateLimited,
            Title = "Too many requests.",
            Status = StatusCodes.Status429TooManyRequests,
            Detail = "The request rate limit was exceeded. Retry later."
        };

        problem.Extensions["traceId"] = httpContext.TraceIdentifier;

        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.WriteAsJsonAsync(
            problem,
            options: null,
            contentType: "application/problem+json",
            cancellationToken);
    };
});

builder.Services.AddHealthChecks()
    .AddDbContextCheck<MovieFlixDbContext>("database");

var databaseProvider = builder.Configuration["Database:Provider"] ?? "Sqlite";

builder.Services.AddDbContext<MovieFlixDbContext>(options =>
{
    if (string.Equals(
        databaseProvider,
        "Postgres",
        StringComparison.OrdinalIgnoreCase))
    {
        var postgresConnection = builder.Configuration
            .GetConnectionString("Postgres")
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

    if (builder.Environment.IsDevelopment())
    {
        options
            .EnableDetailedErrors()
            .EnableSensitiveDataLogging();
    }
});

builder.Services.AddHttpContextAccessor();

builder.Services
    .AddHttpClient<ITmdbService, TmdbService>(client =>
    {
        client.Timeout = Timeout.InfiniteTimeSpan;
    })
    .AddStandardResilienceHandler(options =>
    {
        options.Retry.MaxRetryAttempts = 3;
        options.Retry.Delay = TimeSpan.FromMilliseconds(200);
        options.Retry.BackoffType = DelayBackoffType.Exponential;
        options.Retry.UseJitter = true;

        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(20);

        options.CircuitBreaker.FailureRatio = 0.5;
        options.CircuitBreaker.MinimumThroughput = 5;
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
        options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);
    });

builder.Services.AddOutputCache(options =>
{
    options.AddPolicy("tmdb-search", policy => policy
        .Expire(TimeSpan.FromMinutes(5))
        .SetVaryByQuery("query", "type", "page", "language", "region"));

    options.AddPolicy("tmdb-trending", policy => policy
        .Expire(TimeSpan.FromMinutes(10))
        .SetVaryByQuery("mediaType", "window", "page", "language"));

    options.AddPolicy("tmdb-feed", policy => policy
        .Expire(TimeSpan.FromMinutes(10))
        .SetVaryByQuery("page", "language", "region"));

    options.AddPolicy("tmdb-discover", policy => policy
        .Expire(TimeSpan.FromMinutes(10))
        .SetVaryByQuery(
            "genreId",
            "year",
            "minRating",
            "sortBy",
            "page",
            "language",
            "region"));

    options.AddPolicy("tmdb-details", policy => policy
        .Expire(TimeSpan.FromHours(1))
        .SetVaryByRouteValue("mediaType", "tmdbId")
        .SetVaryByQuery("language"));

    options.AddPolicy("tmdb-season", policy => policy
        .Expire(TimeSpan.FromHours(6))
        .SetVaryByRouteValue("tmdbId")
        .SetVaryByQuery("language"));

    options.AddPolicy("tmdb-genres", policy => policy
        .Expire(TimeSpan.FromHours(24))
        .SetVaryByRouteValue("mediaType")
        .SetVaryByQuery("language"));
});

builder.Services.AddScoped<IMediaCacheService, MediaCacheService>();
builder.Services.AddScoped<IMediaService, MediaService>();
builder.Services.AddScoped<ITvProgressService, TvProgressService>();
builder.Services.AddScoped<IFavoriteService, FavoriteService>();
builder.Services.AddScoped<IWatchlistService, WatchlistService>();
builder.Services.AddScoped<IRatingService, RatingService>();
builder.Services.AddScoped<IWatchHistoryService, WatchHistoryService>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<ICollectionService, CollectionService>();
builder.Services.AddScoped<ISearchHistoryService, SearchHistoryService>();
builder.Services.AddScoped<IPreferencesService, PreferencesService>();
builder.Services.AddScoped<IProfileService, ProfileService>();
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

builder.Services.AddHostedService<MediaCacheWarmupService>();

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
        options.Lockout.AllowedForNewUsers = true;
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
                Encoding.UTF8.GetBytes(jwtSettings["Key"] ?? string.Empty))
        };

        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();

                if (context.Response.HasStarted)
                {
                    return;
                }

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/problem+json";

                var problem = new ProblemDetails
                {
                    Type = ProblemTypes.Unauthorized,
                    Title = "Unauthorized.",
                    Status = StatusCodes.Status401Unauthorized,
                    Detail = "A valid bearer token is required."
                };

                problem.Extensions["traceId"] =
                    context.HttpContext.TraceIdentifier;

                await context.Response.WriteAsJsonAsync(
                    problem,
                    options: null,
                    contentType: "application/problem+json",
                    context.HttpContext.RequestAborted);
            },
            OnForbidden = async context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/problem+json";

                var problem = new ProblemDetails
                {
                    Type = ProblemTypes.Forbidden,
                    Title = "Forbidden.",
                    Status = StatusCodes.Status403Forbidden,
                    Detail = "You do not have access to this resource."
                };

                problem.Extensions["traceId"] =
                    context.HttpContext.TraceIdentifier;

                await context.Response.WriteAsJsonAsync(
                    problem,
                    options: null,
                    contentType: "application/problem+json",
                    context.HttpContext.RequestAborted);
            }
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "MovieFlix API",
        Version = "v1",
        Description =
            "Unified movies and TV platform. Public titles are identified by " +
            "`mediaType` (`movie` or `tv`) and TMDB id."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter the JWT access token."
    });

    options.OperationFilter<AuthorizeCheckOperationFilter>();

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);

    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
    }
});

var forwardedSettings = builder.Configuration
    .GetSection("ForwardedHeaders")
    .Get<ForwardedHeadersSettings>() ?? new ForwardedHeadersSettings();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();

    foreach (var proxy in forwardedSettings.KnownProxies)
    {
        if (IPAddress.TryParse(proxy, out var address))
        {
            options.KnownProxies.Add(address);
        }
    }

    foreach (var network in forwardedSettings.KnownNetworks)
    {
        if (Microsoft.AspNetCore.HttpOverrides.IPNetwork.TryParse(
            network,
            out var parsedNetwork))
        {
            options.KnownNetworks.Add(parsedNetwork);
        }
    }
});

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Type ??=
            ProblemTypes.ForStatus(context.ProblemDetails.Status);

        context.ProblemDetails.Extensions.TryAdd(
            "traceId",
            context.HttpContext.TraceIdentifier);
    };
});

var app = builder.Build();

if (forwardedSettings.Enabled)
{
    app.UseForwardedHeaders();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
}

app.UseExceptionHandler();
app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
app.UseResponseCompression();
app.UseCors("MovieFlixClient");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseOutputCache();

app.MapControllers();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});

app.MapHealthChecks("/health/ready");

app.MapHealthChecks("/health");

await DatabaseSeeder.InitializeAsync(app.Services);

app.Run();

public partial class Program { }
