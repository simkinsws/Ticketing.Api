namespace Ticketing.Api.Domain;

public class UserPreferences
{
    public string UserId { get; set; } = string.Empty; // FK to AspNetUsers (PK)
    public ApplicationUser User { get; set; } = null!;
    
    // Preferences
    public string? Timezone { get; set; } // e.g., "America/New_York", "Europe/London", "Asia/Jerusalem"
    public string? Language { get; set; } // e.g., "EN", "ES", "FR"
    public string? DateFormat { get; set; } // e.g., "MM-dd-yyyy", "dd-MM-yyyy"
    public string? TimeFormat { get; set; } // e.g., "12h", "24h"
    
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
