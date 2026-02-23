using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ticketing.Api.Domain;
using Ticketing.Api.DTOs;
using Ticketing.Api.Services;

namespace Ticketing.Api.Controllers;

[ApiController]
[Route("api/activities")]
[Authorize]
public class ActivitiesController : ControllerBase
{
    private readonly IUserActivityService _activityService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<ActivitiesController> _logger;

    public ActivitiesController(
        IUserActivityService activityService,
        UserManager<ApplicationUser> userManager,
        ILogger<ActivitiesController> logger
    )
    {
        _activityService = activityService;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// Get activities for the current user
    /// </summary>
    [HttpGet("me")]
    public async Task<ActionResult<ActivityPagedResponse>> GetMyActivities(
        [FromQuery] ActivityFilterRequest filter
    )
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized();
        }

        _logger.LogInformation("User {UserId} requesting their activities", user.Id);

        var activities = await _activityService.GetUserActivitiesAsync(user.Id, filter);
        return Ok(activities);
    }

    /// <summary>
    /// Get activities for a specific user (Admin only)
    /// </summary>
    [HttpGet("users/{userId}")]
    [Authorize(Roles = "Admin,Support")]
    public async Task<ActionResult<ActivityPagedResponse>> GetUserActivities(
        string userId,
        [FromQuery] ActivityFilterRequest filter
    )
    {
        _logger.LogInformation("Admin requesting activities for user {UserId}", userId);

        var targetUser = await _userManager.FindByIdAsync(userId);
        if (targetUser is null)
        {
            return NotFound(new { message = "User not found" });
        }

        var activities = await _activityService.GetUserActivitiesAsync(userId, filter);
        return Ok(activities);
    }

    /// <summary>
    /// Get all activities (Admin only)
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Admin,Support")]
    public async Task<ActionResult<ActivityPagedResponse>> GetAllActivities(
        [FromQuery] ActivityFilterRequest filter
    )
    {
        _logger.LogInformation("Admin requesting all activities");

        var activities = await _activityService.GetAllActivitiesAsync(filter);
        return Ok(activities);
    }
}
