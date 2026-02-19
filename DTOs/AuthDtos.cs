namespace Ticketing.Api.DTOs;

public record RegisterRequest(string Email, string Password, string? DisplayName, string? PhoneNumber);
public record LoginRequest(string Email, string Password, bool RememberMe = false);
public record RefreshRequest(string AccessToken, string RefreshToken);
public record ConfirmEmailRequest(string UserId, string Token);
public record ForgotPasswordRequest(string Email);
public record ResetPasswordRequest(string UserId, string Token, string NewPassword);

public record AuthResponse(
    string AccessToken,
    int ExpiresInSeconds,
    UserProfile User,
    string RefreshToken
);

public record UserProfile(
    string Id, 
    string Email, 
    string DisplayName, 
    string[] Roles, 
    string? NameIdentifier = null,
    string? Country = null,
    string? City = null,
    string? Street = null,
    DateTimeOffset? CreatedAt = null,
    bool EmailConfirmed = false,
    string? PhoneNumber = null,
    bool PhoneNumberConfirmed = false,
    string? Timezone = null,
    string? Language = null
);

public record UserListItem(
    string Id, 
    string Email, 
    string UserName, 
    string? DisplayName, 
    string? Country = null,
    string? City = null,
    string? Street = null,
    DateTimeOffset? CreatedAt = null
);

public record UpdateUserRequest(
    string? DisplayName, 
    string? PhoneNumber, 
    string? Email, 
    string? Country,
    string? City,
    string? Street,
    string? Timezone,
    string? Language
);
