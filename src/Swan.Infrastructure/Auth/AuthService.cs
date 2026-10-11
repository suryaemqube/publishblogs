using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Swan.Domain.Identity;
using Swan.Infrastructure.Persistence;
using Swan.Infrastructure.Security;

namespace Swan.Infrastructure.Auth;

public sealed record AuthOptions
{
    public string Issuer { get; init; } = "swan-api";
    public string Audience { get; init; } = "swan-web";
    public int AccessTokenMinutes { get; init; } = 15;
    public int RefreshTokenDays { get; init; } = 14;
    public int MaxFailedLogins { get; init; } = 5;
    public int LockoutMinutes { get; init; } = 15;
    public required byte[] SigningKey { get; init; }
}

public sealed record TokenPair(string AccessToken, DateTime AccessExpiresUtc, string RefreshToken, DateTime RefreshExpiresUtc);

public enum LoginOutcome { Success, InvalidCredentials, Locked, Inactive }

public sealed record LoginResult(LoginOutcome Outcome, TokenPair? Tokens, DateTime? LockedUntilUtc = null);

public sealed record CurrentUser(Guid Id, string Email, string DisplayName, IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions);

/// <summary>
/// Local-account authentication (PLAN-v2 §11.1). Lockout after repeated failures, refresh-token rotation,
/// and a timing-equalised path for unknown emails so that existence cannot be probed.
/// </summary>
public sealed class AuthService(SwanDbContext db, AuthOptions options, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public async Task<LoginResult> LoginAsync(string email, string password, string? ip, CancellationToken ct = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == normalized, ct);
        var now = _clock.GetUtcNow().UtcDateTime;

        if (user is null)
        {
            PasswordHasher.Verify(password, PasswordHasher.DummyHash); // equalise timing
            await Audit(null, "auth.login.failed", ip, $"unknown account", ct);
            return new LoginResult(LoginOutcome.InvalidCredentials, null);
        }

        if (!user.IsActive)
        {
            await Audit(user.Id, "auth.login.failed", ip, "inactive account", ct);
            return new LoginResult(LoginOutcome.Inactive, null);
        }

        if (user.LockoutUntilUtc is { } until && until > now)
        {
            await Audit(user.Id, "auth.login.locked", ip, null, ct);
            return new LoginResult(LoginOutcome.Locked, null, until);
        }

        if (!PasswordHasher.Verify(password, user.PasswordHash))
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= options.MaxFailedLogins)
            {
                user.LockoutUntilUtc = now.AddMinutes(options.LockoutMinutes);
                user.FailedLoginCount = 0;
            }
            await db.SaveChangesAsync(ct);
            await Audit(user.Id, "auth.login.failed", ip, "bad password", ct);
            return new LoginResult(LoginOutcome.InvalidCredentials, null, user.LockoutUntilUtc);
        }

        user.FailedLoginCount = 0;
        user.LockoutUntilUtc = null;
        user.LastLoginUtc = now;
        var tokens = await IssueAsync(user, ct);
        await Audit(user.Id, "auth.login.succeeded", ip, null, ct);
        return new LoginResult(LoginOutcome.Success, tokens);
    }

    /// <summary>Rotates the refresh token: the presented one is revoked and a new pair is issued.</summary>
    public async Task<TokenPair?> RefreshAsync(string refreshToken, string? ip, CancellationToken ct = default)
    {
        var hash = Hash(refreshToken);
        var now = _clock.GetUtcNow().UtcDateTime;
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (stored is null || stored.ExpiresUtc <= now) return null;

        if (stored.RevokedUtc is not null)
        {
            // Reuse of a rotated token: treat as theft and revoke every session for this user.
            var live = await db.RefreshTokens
                .Where(t => t.UserId == stored.UserId && t.RevokedUtc == null)
                .ToListAsync(ct);
            foreach (var t in live) t.RevokedUtc = now;
            await Audit(stored.UserId, "auth.refresh.reuse_detected", ip, null, ct);
            return null;
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == stored.UserId, ct);
        if (user is null || !user.IsActive) return null;

        stored.RevokedUtc = now;
        var tokens = await IssueAsync(user, ct);
        await db.SaveChangesAsync(ct);
        return tokens;
    }

    public async Task LogoutAsync(string refreshToken, string? ip, CancellationToken ct = default)
    {
        var hash = Hash(refreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is null || stored.RevokedUtc is not null) return;
        stored.RevokedUtc = _clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
        await Audit(stored.UserId, "auth.logout", ip, null, ct);
    }

    public async Task<CurrentUser?> GetCurrentUserAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId && u.IsActive, ct);
        if (user is null) return null;
        var (roles, perms) = await LoadRolesAndPermissionsAsync(userId, ct);
        return new CurrentUser(user.Id, user.Email, user.DisplayName, roles, perms);
    }

    private async Task<TokenPair> IssueAsync(User user, CancellationToken ct)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var (roles, perms) = await LoadRolesAndPermissionsAsync(user.Id, ct);

        var accessExpires = now.AddMinutes(options.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("name", user.DisplayName),
        };
        claims.AddRange(roles.Select(r => new Claim("role", r)));
        claims.AddRange(perms.Select(p => new Claim("perm", p)));

        var creds = new SigningCredentials(new SymmetricSecurityKey(options.SigningKey), SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(options.Issuer, options.Audience, claims, now, accessExpires, creds);
        var access = new JwtSecurityTokenHandler().WriteToken(jwt);

        var refresh = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var refreshExpires = now.AddDays(options.RefreshTokenDays);
        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = Hash(refresh),
            CreatedUtc = now,
            ExpiresUtc = refreshExpires,
        });
        await db.SaveChangesAsync(ct);

        return new TokenPair(access, accessExpires, refresh, refreshExpires);
    }

    private async Task<(List<string> Roles, List<string> Permissions)> LoadRolesAndPermissionsAsync(Guid userId, CancellationToken ct)
    {
        var roles = await (from ur in db.UserRoles.AsNoTracking()
                           join r in db.Roles.AsNoTracking() on ur.RoleId equals r.Id
                           where ur.UserId == userId
                           select r.Name).ToListAsync(ct);

        var perms = await (from ur in db.UserRoles.AsNoTracking()
                           join rp in db.RolePermissions.AsNoTracking() on ur.RoleId equals rp.RoleId
                           join p in db.Permissions.AsNoTracking() on rp.PermissionId equals p.Id
                           where ur.UserId == userId
                           select p.Key).Distinct().OrderBy(k => k).ToListAsync(ct);

        return (roles, perms);
    }

    private async Task Audit(Guid? userId, string action, string? ip, string? details, CancellationToken ct)
    {
        db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = action,
            EntityType = "User",
            EntityId = userId?.ToString(),
            Details = details,
            IpAddress = ip,
            CreatedUtc = _clock.GetUtcNow().UtcDateTime,
        });
        await db.SaveChangesAsync(ct);
    }

    private static string Hash(string token) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
