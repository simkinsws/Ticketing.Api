using Ticketing.Api.Enums;

namespace Ticketing.Api.DTOs;

public record RegisterRequest(string Email, string Password, string? DisplayName);
public record LoginRequest(string Email, string Password);
public record RefreshRequest(string AccessToken);
public record ConfirmEmailRequest(string UserId, string Token);
public record ForgotPasswordRequest(string Email);
public record ResetPasswordRequest(string UserId, string Token, string NewPassword);

public record AuthResponse(
    string AccessToken,
    int ExpiresInSeconds,
    UserProfile User
);

public record UserProfile(string Id, string Email, string DisplayName, string[] Roles, string? nameIdentifier = null);

public record UserListItem(string Id, string Email, string UserName, string? DisplayName);

public record UpdateUserRequest(string? DisplayName, string? PhoneNumber, PreferredLanguage? PreferredLanguage,string? Email);