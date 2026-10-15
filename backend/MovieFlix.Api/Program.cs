using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MovieFlix.Api.Configuration;
using MovieFlix.Api.Data;
using MovieFlix.Api.Exceptions;
using MovieFlix.Api.Models;
using MovieFlix.Api.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddDbContext<MovieFlixDbContext>(options =>
{
    options.UseSqlite("Data Source=MovieFlix.db");

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

builder.Services.AddHttpClient<ITmdbService, TmdbService>(
    client =>
    {
        client.Timeout = TimeSpan.FromSeconds(10);
    });

builder.Services.AddScoped<IMovieService, MovieService>();
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
builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection("Jwt"));

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

app.UseHttpsRedirection();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

await DatabaseSeeder.InitializeAsync(app.Services);

app.Run();

public partial class Program { }
