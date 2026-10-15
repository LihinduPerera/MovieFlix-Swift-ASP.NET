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

        public DbSet<WatchHistoryEntry> WatchHistoryEntries => Set<WatchHistoryEntry>();

        public DbSet<Review> Reviews => Set<Review>();

        public DbSet<Collection> Collections => Set<Collection>();

        public DbSet<CollectionMovie> CollectionMovies => Set<CollectionMovie>();

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

            modelBuilder.Entity<WatchHistoryEntry>()
                .HasOne(entry => entry.User)
                .WithMany()
                .HasForeignKey(entry => entry.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<WatchHistoryEntry>()
                .HasOne(entry => entry.Movie)
                .WithMany()
                .HasForeignKey(entry => entry.MovieId)
                .OnDelete(DeleteBehavior.Cascade);

            // One history row per user per movie (progress is upserted).
            modelBuilder.Entity<WatchHistoryEntry>()
                .HasIndex(entry => new
                {
                    entry.UserId,
                    entry.MovieId
                })
                .IsUnique();

            // "Continue watching" and "recently watched" query by user + time.
            modelBuilder.Entity<WatchHistoryEntry>()
                .HasIndex(entry => new
                {
                    entry.UserId,
                    entry.WatchedAt
                });

            modelBuilder.Entity<Review>()
                .HasOne(review => review.User)
                .WithMany()
                .HasForeignKey(review => review.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Review>()
                .HasOne(review => review.Movie)
                .WithMany()
                .HasForeignKey(review => review.MovieId)
                .OnDelete(DeleteBehavior.Cascade);

            // One review per user per movie.
            modelBuilder.Entity<Review>()
                .HasIndex(review => new
                {
                    review.UserId,
                    review.MovieId
                })
                .IsUnique();

            // Listing a movie's reviews is ordered by time.
            modelBuilder.Entity<Review>()
                .HasIndex(review => new
                {
                    review.MovieId,
                    review.CreatedAt
                });

            modelBuilder.Entity<Collection>()
                .HasOne(collection => collection.User)
                .WithMany()
                .HasForeignKey(collection => collection.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Collection>()
                .HasIndex(collection => collection.UserId);

            modelBuilder.Entity<CollectionMovie>()
                .HasOne(cm => cm.Collection)
                .WithMany(collection => collection.Movies)
                .HasForeignKey(cm => cm.CollectionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CollectionMovie>()
                .HasOne(cm => cm.Movie)
                .WithMany()
                .HasForeignKey(cm => cm.MovieId)
                .OnDelete(DeleteBehavior.Cascade);

            // A movie can appear at most once per collection.
            modelBuilder.Entity<CollectionMovie>()
                .HasIndex(cm => new
                {
                    cm.CollectionId,
                    cm.MovieId
                })
                .IsUnique();

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
