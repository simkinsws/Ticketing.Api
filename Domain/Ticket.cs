namespace Ticketing.Api.Domain;

public enum TicketStatus { Open = 0, InProgress = 1, Resolved = 2, Closed = 3 }
public enum TicketPriority { Low = 0, Medium = 1, High = 2, Urgent = 3 }

public class Ticket
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public int TicketNumber { get; set; } // Auto-incrementing: TK-1, TK-2, etc.

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public string Category { get; set; } = "General";
    public TicketStatus Status { get; set; } = TicketStatus.Open;
    public TicketPriority Priority { get; set; } = TicketPriority.Medium;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public string CustomerId { get; set; } = string.Empty;
    public ApplicationUser? Customer { get; set; }

    public string? AssignedAdminId { get; set; }
    public ApplicationUser? AssignedAdmin { get; set; }

    public List<TicketComment> Comments { get; set; } = new();
    
    // Helper method to format ticket number
    public string GetFormattedTicketNumber() => $"#TK-{TicketNumber}";
}
