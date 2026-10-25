using System.ComponentModel.DataAnnotations;

namespace MovieFlix.Api.DTOs.History
{
    /// <summary>
    /// Progress for a whole title. For TV, season/episode are optional; when
    /// present the write is delegated to per-episode tracking.
    /// </summary>
    public class RecordProgressRequest
    {
        [Range(0, int.MaxValue, ErrorMessage = "Progress must be zero or greater.")]
        public int ProgressSeconds { get; set; }

        public bool Completed { get; set; }

        [Range(1, int.MaxValue)]
        public int? SeasonNumber { get; set; }

        [Range(1, int.MaxValue)]
        public int? EpisodeNumber { get; set; }
    }

    public class RecordEpisodeProgressRequest
    {
        [Range(0, int.MaxValue, ErrorMessage = "Progress must be zero or greater.")]
        public int ProgressSeconds { get; set; }

        public bool Completed { get; set; }
    }
}
