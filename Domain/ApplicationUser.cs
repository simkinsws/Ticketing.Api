using Microsoft.AspNetCore.Identity;

namespace Ticketing.Api.Domain;

public class ApplicationUser : IdentityUser
{
    public string? DisplayName { get; set; }
    
    public string? Country { get; set; }
    public string? City { get; set; }
    public string? Street { get; set; }
    
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
