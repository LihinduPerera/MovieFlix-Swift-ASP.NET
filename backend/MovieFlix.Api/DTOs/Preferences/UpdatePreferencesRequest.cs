using System.ComponentModel.DataAnnotations;

namespace MovieFlix.Api.DTOs.Preferences
{
    /// <summary>Request body for updating a user's preferences.</summary>
    public class UpdatePreferencesRequest
    {
        [Required]
        [MaxLength(10)]
        [RegularExpression(@"^[A-Za-z]{2}(-[A-Za-z]{2})?$",
            ErrorMessage = "PreferredLanguage must be a BCP-47 tag such as 'en' or 'en-US'.")]
        public string PreferredLanguage { get; set; } = "en-US";

        [Required]
        [MaxLength(5)]
        [RegularExpression(@"^[A-Za-z]{2}$",
            ErrorMessage = "PreferredRegion must be a two-letter ISO 3166-1 code such as 'US'.")]
        public string PreferredRegion { get; set; } = "US";

        [Required]
        [RegularExpression(@"^(system|light|dark)$",
            ErrorMessage = "Theme must be one of: system, light, dark.")]
        public string Theme { get; set; } = "system";

        public bool AutoPlayTrailers { get; set; } = true;

        public bool EmailNotifications { get; set; } = true;

        public bool MatureContentFilter { get; set; } = true;
    }
}
