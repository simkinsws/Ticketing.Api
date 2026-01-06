using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Ticketing.Api.Data;
using Ticketing.Api.Domain;

namespace Ticketing.Api.Services;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "";
    public string Audience { get; set; } = "";
    public string Key { get; set; } = "";
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 30;
}

public class TokenService
{
    private readonly JwtOptions _opts;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _db;

    public TokenService(
        IOptions<JwtOptions> options,
        UserManager<ApplicationUser> userManager,
        AppDbContext db
    )
    {
        _opts = options.Value;
        _userManager = userManager;
        _db = db;
    }

    public async Task<(string accessToken, DateTimeOffset expiresAt)> CreateAccessTokenAsync(
        ApplicationUser user
    )
    {
        var roles = await _userManager.GetRolesAsync(user);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? ""),
            new("displayName", user.DisplayName ?? ""),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opts.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expires = DateTimeOffset.UtcNow.AddMinutes(_opts.AccessTokenMinutes);

        var token = new JwtSecurityToken(
            issuer: _opts.Issuer,
            audience: _opts.Audience,
            claims: claims,
            expires: expires.UtcDateTime,
            signingCredentials: creds
        );

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    public (string token, string tokenHash, DateTimeOffset expiresAt) CreateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        var token = Convert.ToBase64String(bytes);
        return (
            token,
            HashRefreshToken(token),
            DateTimeOffset.UtcNow.AddDays(_opts.RefreshTokenDays)
        );
    }

    public string HashRefreshToken(string token)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public async Task StoreRefreshTokenAsync(
        string userId,
        string tokenHash,
        DateTimeOffset expiresAt
    )
    {
        _db.RefreshTokens.Add(
            new RefreshToken
            {
                UserId = userId,
                TokenHash = tokenHash,
                ExpiresAt = expiresAt,
            }
        );
        await _db.SaveChangesAsync();
    }

    public async Task<RefreshToken?> GetActiveRefreshTokenAsync(string userId, string tokenHash)
    {
        return await _db
            .RefreshTokens.Where(r => r.UserId == userId && r.TokenHash == tokenHash)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(r => r.IsActive);
    }

    public async Task RevokeRefreshTokenAsync(RefreshToken token)
    {
        token.RevokedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
    }
}
