using System.ComponentModel.DataAnnotations;

namespace MovieFlix.Api.DTOs.History
{
    public class RecordProgressRequest
    {
        [Range(0, int.MaxValue, ErrorMessage = "Progress must be zero or greater.")]
        public int ProgressSeconds { get; set; }

        public bool Completed { get; set; }
    }
}
