using Microsoft.AspNetCore.Identity;

namespace Ticketing.Api.Domain;

public class ApplicationUser : IdentityUser
{
    public string? DisplayName { get; set; }
}
