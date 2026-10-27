using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using MovieFlix.Api.Models;

namespace MovieFlix.Api.Data
{
    /// <summary>
    /// Forces <see cref="DateTime"/> values to round-trip as UTC. SQLite stores
    /// timestamps without a kind, so without this converter API responses would
    /// lose the trailing "Z" required by the public contract.
    /// </summary>
    public sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
    {
        public UtcDateTimeConverter()
            : base(
                value => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime(),
                value => DateTime.SpecifyKind(value, DateTimeKind.Utc))
        {
        }
    }

    /// <summary>
    /// Persists <see cref="MediaType"/> using the public lowercase wire values
    /// (<c>movie</c>/<c>tv</c>) instead of the CLR enum names.
    /// </summary>
    public sealed class MediaTypeValueConverter : ValueConverter<MediaType, string>
    {
        public MediaTypeValueConverter()
            : base(
                value => value == MediaType.Movie ? "movie" : "tv",
                value => string.Equals(value, "movie", StringComparison.OrdinalIgnoreCase)
                    ? MediaType.Movie
                    : MediaType.Tv)
        {
        }
    }

    /// <summary>Nullable counterpart of <see cref="UtcDateTimeConverter"/>.</summary>
    public sealed class NullableUtcDateTimeConverter : ValueConverter<DateTime?, DateTime?>
    {
        public NullableUtcDateTimeConverter()
            : base(
                value => value.HasValue
                    ? (value.Value.Kind == DateTimeKind.Utc
                        ? value.Value
                        : value.Value.ToUniversalTime())
                    : value,
                value => value.HasValue
                    ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
                    : value)
        {
        }
    }

    /// <summary>
    /// The application's EF Core DbContext. Identity owns the account tables;
    /// MovieFlix adds the unified media cache, TV metadata and user links.
    /// </summary>
    public class MovieFlixDbContext : IdentityDbContext<ApplicationUser>
    {
        public MovieFlixDbContext(DbContextOptions<MovieFlixDbContext> options)
            : base(options)
        {
        }

        public DbSet<MediaItem> MediaItems => Set<MediaItem>();

        public DbSet<Genre> Genres => Set<Genre>();

        public DbSet<MediaItemGenre> MediaItemGenres => Set<MediaItemGenre>();

        public DbSet<TvSeason> TvSeasons => Set<TvSeason>();

        public DbSet<TvEpisode> TvEpisodes => Set<TvEpisode>();

        public DbSet<Favorite> Favorites => Set<Favorite>();

        public DbSet<WatchlistItem> WatchlistItems => Set<WatchlistItem>();

        public DbSet<MediaRating> MediaRatings => Set<MediaRating>();

        public DbSet<Review> Reviews => Set<Review>();

        public DbSet<Collection> Collections => Set<Collection>();

        public DbSet<CollectionItem> CollectionItems => Set<CollectionItem>();

        public DbSet<MediaWatchState> MediaWatchStates => Set<MediaWatchState>();

        public DbSet<EpisodeWatchState> EpisodeWatchStates => Set<EpisodeWatchState>();

        public DbSet<SearchHistoryEntry> SearchHistoryEntries => Set<SearchHistoryEntry>();

        public DbSet<UserPreferences> UserPreferences => Set<UserPreferences>();

        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.Properties<DateTime>()
                .HaveConversion<UtcDateTimeConverter>();

            configurationBuilder.Properties<DateTime?>()
                .HaveConversion<NullableUtcDateTimeConverter>();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<MediaItem>(entity =>
            {
                entity.HasIndex(media => new { media.TmdbId, media.MediaType }).IsUnique();
                entity.HasIndex(media => new { media.MediaType, media.Title });
                entity.HasIndex(media => media.CachedAt);

                // Enum values are persisted as "movie" / "tv".
                entity.Property(media => media.MediaType)
                    .HasConversion<MediaTypeValueConverter>()
                    .HasMaxLength(16);
            });

            modelBuilder.Entity<Genre>(entity =>
            {
                entity.HasIndex(genre => new { genre.TmdbId, genre.MediaType }).IsUnique();

                entity.Property(genre => genre.MediaType)
                    .HasConversion<MediaTypeValueConverter>()
                    .HasMaxLength(16);
            });

            modelBuilder.Entity<MediaItemGenre>(entity =>
            {
                entity.HasKey(link => new { link.MediaItemId, link.GenreId });

                entity.HasOne(link => link.MediaItem)
                    .WithMany(media => media.Genres)
                    .HasForeignKey(link => link.MediaItemId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(link => link.Genre)
                    .WithMany(genre => genre.MediaItems)
                    .HasForeignKey(link => link.GenreId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<TvSeason>(entity =>
            {
                entity.HasIndex(season => new { season.MediaItemId, season.SeasonNumber })
                    .IsUnique();

                entity.HasOne(season => season.MediaItem)
                    .WithMany(media => media.Seasons)
                    .HasForeignKey(season => season.MediaItemId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<TvEpisode>(entity =>
            {
                entity.HasIndex(episode => new
                {
                    episode.MediaItemId,
                    episode.SeasonNumber,
                    episode.EpisodeNumber
                }).IsUnique();

                entity.HasIndex(episode => episode.TmdbId);

                entity.HasOne(episode => episode.MediaItem)
                    .WithMany()
                    .HasForeignKey(episode => episode.MediaItemId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(episode => episode.TvSeason)
                    .WithMany(season => season.Episodes)
                    .HasForeignKey(episode => episode.TvSeasonId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Favorite>(entity =>
            {
                entity.HasOne(favorite => favorite.User)
                    .WithMany()
                    .HasForeignKey(favorite => favorite.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(favorite => favorite.MediaItem)
                    .WithMany()
                    .HasForeignKey(favorite => favorite.MediaItemId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(favorite => new { favorite.UserId, favorite.MediaItemId })
                    .IsUnique();
            });

            modelBuilder.Entity<WatchlistItem>(entity =>
            {
                entity.HasOne(item => item.User)
                    .WithMany()
                    .HasForeignKey(item => item.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(item => item.MediaItem)
                    .WithMany()
                    .HasForeignKey(item => item.MediaItemId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(item => new { item.UserId, item.MediaItemId })
                    .IsUnique();
            });

            modelBuilder.Entity<MediaRating>(entity =>
            {
                entity.HasOne(rating => rating.User)
                    .WithMany()
                    .HasForeignKey(rating => rating.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(rating => rating.MediaItem)
                    .WithMany()
                    .HasForeignKey(rating => rating.MediaItemId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(rating => new { rating.UserId, rating.MediaItemId })
                    .IsUnique();

                entity.HasIndex(rating => rating.MediaItemId);
            });

            modelBuilder.Entity<Review>(entity =>
            {
                entity.HasOne(review => review.User)
                    .WithMany()
                    .HasForeignKey(review => review.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(review => review.MediaItem)
                    .WithMany()
                    .HasForeignKey(review => review.MediaItemId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(review => new { review.UserId, review.MediaItemId })
                    .IsUnique();

                entity.HasIndex(review => new { review.MediaItemId, review.CreatedAt });
            });

            modelBuilder.Entity<Collection>(entity =>
            {
                entity.HasOne(collection => collection.User)
                    .WithMany()
                    .HasForeignKey(collection => collection.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Case-insensitive uniqueness per owner (NormalizedName is upper-cased).
                entity.HasIndex(collection => new { collection.UserId, collection.NormalizedName })
                    .IsUnique();
            });

            modelBuilder.Entity<CollectionItem>(entity =>
            {
                entity.HasOne(item => item.Collection)
                    .WithMany(collection => collection.Items)
                    .HasForeignKey(item => item.CollectionId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(item => item.MediaItem)
                    .WithMany()
                    .HasForeignKey(item => item.MediaItemId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(item => new { item.CollectionId, item.MediaItemId })
                    .IsUnique();
            });

            modelBuilder.Entity<MediaWatchState>(entity =>
            {
                entity.HasOne(state => state.User)
                    .WithMany()
                    .HasForeignKey(state => state.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(state => state.MediaItem)
                    .WithMany()
                    .HasForeignKey(state => state.MediaItemId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(state => state.LastTvEpisode)
                    .WithMany()
                    .HasForeignKey(state => state.LastTvEpisodeId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(state => new { state.UserId, state.MediaItemId })
                    .IsUnique();

                entity.HasIndex(state => new { state.UserId, state.LastWatchedAt });
            });

            modelBuilder.Entity<EpisodeWatchState>(entity =>
            {
                entity.HasOne(state => state.User)
                    .WithMany()
                    .HasForeignKey(state => state.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(state => state.TvEpisode)
                    .WithMany()
                    .HasForeignKey(state => state.TvEpisodeId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(state => new { state.UserId, state.TvEpisodeId })
                    .IsUnique();

                entity.HasIndex(state => new { state.UserId, state.WatchedAt });
            });

            modelBuilder.Entity<SearchHistoryEntry>(entity =>
            {
                entity.HasOne(entry => entry.User)
                    .WithMany()
                    .HasForeignKey(entry => entry.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(entry => new { entry.UserId, entry.NormalizedQuery })
                    .IsUnique();

                entity.HasIndex(entry => new { entry.UserId, entry.SearchedAt });
            });

            modelBuilder.Entity<UserPreferences>(entity =>
            {
                entity.HasOne(preferences => preferences.User)
                    .WithOne()
                    .HasForeignKey<UserPreferences>(preferences => preferences.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(preferences => preferences.UserId).IsUnique();
            });

            modelBuilder.Entity<RefreshToken>(entity =>
            {
                entity.HasOne(token => token.User)
                    .WithMany()
                    .HasForeignKey(token => token.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(token => token.TokenHash).IsUnique();
                entity.HasIndex(token => token.UserId);
            });
        }
    }
}
