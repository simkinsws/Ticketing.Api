namespace Ticketing.Api.Domain;

public class Message
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public SenderType SenderType { get; set; }
    public string SenderUserId { get; set; } = null!;
    public string Text { get; set; } = null!;
    public DateTime CreatedAt { get; set; }

    // Navigation
    public Conversation Conversation { get; set; } = null!;
    public ApplicationUser Sender { get; set; } = null!;
}
