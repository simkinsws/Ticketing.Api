using System.Security.Claims;

namespace Ticketing.Api.Extensions;

public static class UserContextExtensions
{
    /// <summary>
    /// Gets the current user's ID and DisplayName from claims
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor</param>
    /// <returns>A tuple containing (UserId, DisplayName). Returns ("Unknown", "Unknown") if not found.</returns>
    public static (string UserId, string DisplayName) GetCurrentUserInfo(this IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                     ?? httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value 
                     ?? "Unknown";
        
        var displayName = httpContextAccessor.HttpContext?.User?.FindFirst("display_name")?.Value 
                          ?? "Unknown";

        return (userId, displayName);
    }

    /// <summary>
    /// Gets the current user's ID from claims
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor</param>
    /// <returns>The user ID or "Unknown" if not found</returns>
    public static string GetCurrentUserId(this IHttpContextAccessor httpContextAccessor)
    {
        return httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value 
               ?? httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value 
               ?? "Unknown";
    }

    /// <summary>
    /// Gets the current user's DisplayName from claims
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor</param>
    /// <returns>The display name or "Unknown" if not found</returns>
    public static string GetCurrentUserDisplayName(this IHttpContextAccessor httpContextAccessor)
    {
        return httpContextAccessor.HttpContext?.User?.FindFirst("display_name")?.Value ?? "Unknown";
    }
}
