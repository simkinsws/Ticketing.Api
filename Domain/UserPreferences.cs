namespace Ticketing.Api.Domain;

public class UserPreferences
{
    public string UserId { get; set; } = string.Empty; // FK to AspNetUsers (PK)
    public ApplicationUser User { get; set; } = null!;
    
    // Preferences
    public string? Timezone { get; set; } // e.g., "America/New_York", "Europe/London"
    public string? Language { get; set; } // e.g., "en", "es", "fr" (for future)
    public string? DateFormat { get; set; } // e.g., "MM/DD/YYYY", "DD/MM/YYYY" (for future)
    public string? TimeFormat { get; set; } // e.g., "12h", "24h" (for future)
    
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
