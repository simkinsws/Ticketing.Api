using Microsoft.EntityFrameworkCore;
using Ticketing.Api.Data;
using Ticketing.Api.Domain;
using Ticketing.Api.DTOs;

namespace Ticketing.Api.Services;

public interface IUserActivityService
{
    Task<UserActivity> LogActivityAsync(
        string userId,
        ActivityCategory category,
        string title,
        string description,
        ActivityEntityType entityType,
        string? entityId = null,
        string? metadata = null
    );

    Task<ActivityPagedResponse> GetUserActivitiesAsync(string userId, ActivityFilterRequest filter);
    Task<ActivityPagedResponse> GetAllActivitiesAsync(ActivityFilterRequest filter);
}

public class UserActivityService : IUserActivityService
{
    private readonly AppDbContext _db;

    public UserActivityService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<UserActivity> LogActivityAsync(
        string userId,
        ActivityCategory category,
        string title,
        string description,
        ActivityEntityType entityType,
        string? entityId = null,
        string? metadata = null
    )
    {
        var activity = new UserActivity
        {
            UserId = userId,
            Category = category,
            Title = title,
            Description = description,
            EntityType = entityType,
            EntityId = entityId,
            Metadata = metadata
        };

        _db.UserActivities.Add(activity);
        await _db.SaveChangesAsync();

        return activity;
    }

    public async Task<ActivityPagedResponse> GetUserActivitiesAsync(
        string userId,
        ActivityFilterRequest filter
    )
    {
        var query = _db.UserActivities
            .Include(a => a.User)
            .Where(a => a.UserId == userId);

        return await ExecutePagedQueryAsync(query, filter);
    }

    public async Task<ActivityPagedResponse> GetAllActivitiesAsync(ActivityFilterRequest filter)
    {
        var query = _db.UserActivities.Include(a => a.User).AsQueryable();

        if (!string.IsNullOrEmpty(filter.UserId))
        {
            query = query.Where(a => a.UserId == filter.UserId);
        }

        return await ExecutePagedQueryAsync(query, filter);
    }

    private async Task<ActivityPagedResponse> ExecutePagedQueryAsync(
        IQueryable<UserActivity> query,
        ActivityFilterRequest filter
    )
    {
        // Apply category filter
        if (!string.IsNullOrEmpty(filter.Category))
        {
            if (Enum.TryParse<ActivityCategory>(filter.Category, true, out var category))
            {
                query = query.Where(a => a.Category == category);
            }
        }

        // Apply date filters
        if (filter.FromDate.HasValue)
        {
            query = query.Where(a => a.CreatedAt >= filter.FromDate.Value);
        }

        if (filter.ToDate.HasValue)
        {
            query = query.Where(a => a.CreatedAt <= filter.ToDate.Value);
        }

        // Order by most recent first
        query = query.OrderByDescending(a => a.CreatedAt);

        // Get total count before pagination
        var totalCount = await query.CountAsync();

        // Apply pagination
        var activities = await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();

        // Map to DTOs
        var activityDtos = activities.Select(MapToDto).ToList();

        var totalPages = (int)Math.Ceiling(totalCount / (double)filter.PageSize);

        return new ActivityPagedResponse(
            activityDtos,
            totalCount,
            filter.Page,
            filter.PageSize,
            totalPages
        );
    }

    private UserActivityDto MapToDto(UserActivity activity)
    {
        return new UserActivityDto(
            activity.Id,
            activity.UserId,
            activity.User?.UserName ?? "Unknown",
            activity.User?.DisplayName ?? "Unknown User",
            activity.Category.ToString(),
            activity.Title,
            activity.Description,
            activity.EntityType.ToString(),
            activity.EntityId,
            GetRelativeTime(activity.CreatedAt),
            activity.CreatedAt
        );
    }

    private string GetRelativeTime(DateTimeOffset dateTime)
    {
        var timeSpan = DateTimeOffset.UtcNow - dateTime;

        if (timeSpan.TotalSeconds < 60)
            return "just now";

        if (timeSpan.TotalMinutes < 60)
        {
            var minutes = (int)timeSpan.TotalMinutes;
            return $"{minutes} {(minutes == 1 ? "minute" : "minutes")} ago";
        }

        if (timeSpan.TotalHours < 24)
        {
            var hours = (int)timeSpan.TotalHours;
            return $"{hours} {(hours == 1 ? "hour" : "hours")} ago";
        }

        if (timeSpan.TotalDays < 30)
        {
            var days = (int)timeSpan.TotalDays;
            return $"{days} {(days == 1 ? "day" : "days")} ago";
        }

        if (timeSpan.TotalDays < 365)
        {
            var months = (int)(timeSpan.TotalDays / 30);
            return $"{months} {(months == 1 ? "month" : "months")} ago";
        }

        var years = (int)(timeSpan.TotalDays / 365);
        return $"{years} {(years == 1 ? "year" : "years")} ago";
    }
}
