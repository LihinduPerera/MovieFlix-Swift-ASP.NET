using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MovieFlix.Api.Models;

namespace MovieFlix.Api.Data
{
    /// <summary>
    /// The application's EF Core DbContext. Derives from
    /// <see cref="IdentityDbContext{TUser}"/> so ASP.NET Core Identity owns the
    /// identity tables (users, roles, claims, ...) while MovieFlix adds its own
    /// domain tables (movies, favorites, refresh tokens).
    /// </summary>
    public class MovieFlixDbContext : IdentityDbContext<ApplicationUser>
    {
        public MovieFlixDbContext(DbContextOptions<MovieFlixDbContext> options) : base(options)
        {
        }

        public DbSet<Movie> Movies => Set<Movie>();

        public DbSet<Favorite> Favorites => Set<Favorite>();

        public DbSet<WatchlistItem> WatchlistItems => Set<WatchlistItem>();

        public DbSet<MovieRating> MovieRatings => Set<MovieRating>();

        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Required by Identity: creates the AspNet* tables and mappings.
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Movie>()
                .HasIndex(movie => movie.TmdbId)
                .IsUnique(); // Both performance and data integrity

            modelBuilder.Entity<Movie>()
                .HasIndex(movie => movie.Title);

            modelBuilder.Entity<Favorite>()
                .HasOne(favorite => favorite.User)
                .WithMany()
                .HasForeignKey(favorite => favorite.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Favorite>()
                .HasOne(favorite => favorite.Movie)
                .WithMany()
                .HasForeignKey(favorite => favorite.MovieId)
                .OnDelete(DeleteBehavior.Cascade);

            // Composite unique constraint / index.
            modelBuilder.Entity<Favorite>()
                .HasIndex(favorite => new
                {
                    favorite.UserId,
                    favorite.MovieId
                })
                .IsUnique();

            modelBuilder.Entity<WatchlistItem>()
                .HasOne(item => item.User)
                .WithMany()
                .HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<WatchlistItem>()
                .HasOne(item => item.Movie)
                .WithMany()
                .HasForeignKey(item => item.MovieId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<WatchlistItem>()
                .HasIndex(item => new
                {
                    item.UserId,
                    item.MovieId
                })
                .IsUnique();

            modelBuilder.Entity<MovieRating>()
                .HasOne(rating => rating.User)
                .WithMany()
                .HasForeignKey(rating => rating.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<MovieRating>()
                .HasOne(rating => rating.Movie)
                .WithMany()
                .HasForeignKey(rating => rating.MovieId)
                .OnDelete(DeleteBehavior.Cascade);

            // One rating per user per movie.
            modelBuilder.Entity<MovieRating>()
                .HasIndex(rating => new
                {
                    rating.UserId,
                    rating.MovieId
                })
                .IsUnique();

            // Fast community-average lookups per movie.
            modelBuilder.Entity<MovieRating>()
                .HasIndex(rating => rating.MovieId);

            modelBuilder.Entity<RefreshToken>()
                .HasOne(token => token.User)
                .WithMany()
                .HasForeignKey(token => token.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Lookups during refresh must be fast, and tokens are unique.
            modelBuilder.Entity<RefreshToken>()
                .HasIndex(token => token.TokenHash)
                .IsUnique();

            modelBuilder.Entity<RefreshToken>()
                .HasIndex(token => token.UserId);
        }
    }
}
