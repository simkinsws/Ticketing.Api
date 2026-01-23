using Microsoft.AspNetCore.Identity;
using Ticketing.Api.Enums;

namespace Ticketing.Api.Domain;

public class ApplicationUser : IdentityUser
{
    public string? DisplayName { get; set; }
    public bool ReceiveTicketResponse { get; set; } = false;

    public bool ReceiveStatusUpdate { get; set; } = false;
    public bool ReceiveTicketSummary { get; set; } = false;

    public bool ReceiveTipsAndUpdates { get; set; } = false;
    public PreferredLanguage PreferredLanguage { get; set; } = PreferredLanguage.EN;
}
