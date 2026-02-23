using Ticketing.Api.Domain;
using Ticketing.Api.Services;

namespace Ticketing.Api.Extensions;

/// <summary>
/// Helper extension methods for logging user activities
/// </summary>
public static class ActivityLoggerExtensions
{
    public static async Task LogTicketCreatedAsync(
        this IUserActivityService activityService,
        string userId,
        Ticket ticket
    )
    {
        await activityService.LogActivityAsync(
            userId,
            ActivityCategory.TicketCreated,
            $"Ticket {ticket.GetFormattedTicketNumber()} was created",
            $"Created a new {ticket.Priority.ToString().ToLower()} priority ticket: {ticket.Title}",
            ActivityEntityType.Ticket,
            ticket.Id.ToString()
        );
    }

    public static async Task LogTicketStatusChangedAsync(
        this IUserActivityService activityService,
        string userId,
        Ticket ticket,
        TicketStatus oldStatus,
        TicketStatus newStatus
    )
    {
        var category = newStatus switch
        {
            TicketStatus.Resolved => ActivityCategory.TicketResolved,
            TicketStatus.Closed => ActivityCategory.TicketClosed,
            TicketStatus.Open when oldStatus != TicketStatus.Open => ActivityCategory.TicketReopened,
            _ => ActivityCategory.TicketStatusChanged
        };

        var description = newStatus switch
        {
            TicketStatus.Resolved => $"Your issue regarding '{ticket.Title}' has been successfully resolved",
            TicketStatus.Closed => $"Ticket '{ticket.Title}' has been closed",
            TicketStatus.InProgress => $"Work has started on ticket '{ticket.Title}'",
            TicketStatus.Open when oldStatus != TicketStatus.Open => $"Ticket '{ticket.Title}' was reopened",
            _ => $"Status changed from {oldStatus} to {newStatus} for ticket '{ticket.Title}'"
        };

        await activityService.LogActivityAsync(
            userId,
            category,
            $"Ticket {ticket.GetFormattedTicketNumber()} status changed to {newStatus}",
            description,
            ActivityEntityType.Ticket,
            ticket.Id.ToString()
        );
    }

    public static async Task LogTicketPriorityChangedAsync(
        this IUserActivityService activityService,
        string userId,
        Ticket ticket,
        TicketPriority oldPriority,
        TicketPriority newPriority
    )
    {
        await activityService.LogActivityAsync(
            userId,
            ActivityCategory.TicketPriorityChanged,
            $"Ticket {ticket.GetFormattedTicketNumber()} priority changed",
            $"Priority changed from {oldPriority} to {newPriority} for ticket '{ticket.Title}'",
            ActivityEntityType.Ticket,
            ticket.Id.ToString()
        );
    }

    public static async Task LogTicketAssignedAsync(
        this IUserActivityService activityService,
        string userId,
        Ticket ticket,
        string adminName
    )
    {
        await activityService.LogActivityAsync(
            userId,
            ActivityCategory.TicketAssigned,
            $"Ticket {ticket.GetFormattedTicketNumber()} was assigned",
            $"Ticket '{ticket.Title}' has been assigned to {adminName}",
            ActivityEntityType.Ticket,
            ticket.Id.ToString()
        );
    }

    public static async Task LogTicketUpdatedAsync(
        this IUserActivityService activityService,
        string userId,
        Ticket ticket
    )
    {
        await activityService.LogActivityAsync(
            userId,
            ActivityCategory.TicketUpdated,
            $"Ticket {ticket.GetFormattedTicketNumber()} was updated",
            $"Updated ticket: {ticket.Title}",
            ActivityEntityType.Ticket,
            ticket.Id.ToString()
        );
    }

    public static async Task LogCommentAddedAsync(
        this IUserActivityService activityService,
        string userId,
        Ticket ticket,
        TicketComment comment,
        string authorName
    )
    {
        var preview = comment.Message.Length > 100 
            ? comment.Message.Substring(0, 100) + "..." 
            : comment.Message;

        await activityService.LogActivityAsync(
            userId,
            ActivityCategory.CommentAdded,
            $"New comment on ticket {ticket.GetFormattedTicketNumber()}",
            $"{authorName} commented: {preview}",
            ActivityEntityType.Comment,
            comment.Id.ToString()
        );
    }
}
