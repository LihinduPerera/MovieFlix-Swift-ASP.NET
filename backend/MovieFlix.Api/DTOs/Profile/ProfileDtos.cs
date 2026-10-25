using System.ComponentModel.DataAnnotations;

namespace MovieFlix.Api.DTOs.Profile
{
    public class MeResponse
    {
        public string Id { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public List<string> Roles { get; set; } = [];
    }

    public class UpdateProfileRequest
    {
        [Required]
        [MinLength(1)]
        [MaxLength(100)]
        public string DisplayName { get; set; } = string.Empty;
    }

    public class StatsResponse
    {
        public int Favorites { get; set; }

        public int Watchlist { get; set; }

        public int Ratings { get; set; }

        public int Reviews { get; set; }

        public int Collections { get; set; }

        public int MoviesWatched { get; set; }

        public int ShowsWatched { get; set; }

        public int EpisodesWatched { get; set; }
    }
}
