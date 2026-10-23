namespace MovieFlix.Api.DTOs.History
{
    public class WatchHistoryResponse
    {
        public int Id { get; set; }

        public int MovieId { get; set; }

        public int TmdbId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Overview { get; set; } = string.Empty;

        public DateTime WatchedAt { get; set; }

        public int ProgressSeconds { get; set; }

        public bool Completed { get; set; }
    }
}
