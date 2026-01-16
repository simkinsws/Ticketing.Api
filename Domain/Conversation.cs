namespace Ticketing.Api.Domain;

public class Conversation
{
    public Guid Id { get; set; }
    public string CustomerUserId { get; set; } = null!;
    public string CustomerDisplayName { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime LastMessageAt { get; set; }
    public string LastMessagePreview { get; set; } = null!;
    public SenderType LastMessageSender { get; set; }
    public int UnreadForAdminCount { get; set; }
    public int UnreadForCustomerCount { get; set; }
    public bool IsOpen { get; set; }

    // Navigation
    public ApplicationUser Customer { get; set; } = null!;
}
