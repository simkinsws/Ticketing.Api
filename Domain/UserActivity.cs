namespace Ticketing.Api.Domain;

/// <summary>
/// Represents different types of activities that can be logged in the system
/// </summary>
public enum ActivityCategory
{
    // Ticket-related activities
    TicketCreated,
    TicketUpdated,
    TicketStatusChanged,
    TicketPriorityChanged,
    TicketAssigned,
    TicketResolved,
    TicketClosed,
    TicketReopened,
    
    // Comment-related activities
    CommentAdded,
    CommentUpdated,
    CommentDeleted,
    
    // User-related activities
    UserRegistered,
    UserProfileUpdated,
    UserRoleChanged,
    
    // Admin-related activities
    AdminAssignmentChanged
}

/// <summary>
/// Represents the type of entity associated with an activity
/// </summary>
public enum ActivityEntityType
{
    Ticket,
    Comment,
    User,
    System
}

/// <summary>
/// Tracks user activities for the activity feed
/// </summary>
public class UserActivity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// User who performed the activity
    /// </summary>
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }
    
    /// <summary>
    /// Category/type of activity
    /// </summary>
    public ActivityCategory Category { get; set; }
    
    /// <summary>
    /// Short title of the activity (e.g., "Ticket #1234 was resolved")
    /// </summary>
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// Detailed description of the activity
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Type of entity this activity is related to
    /// </summary>
    public ActivityEntityType EntityType { get; set; }
    
    /// <summary>
    /// ID of the related entity (e.g., TicketId, CommentId)
    /// </summary>
    public string? EntityId { get; set; }
    
    /// <summary>
    /// Additional metadata in JSON format (flexible for future needs)
    /// </summary>
    public string? Metadata { get; set; }
    
    /// <summary>
    /// When the activity occurred
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
