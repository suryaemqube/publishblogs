using Microsoft.EntityFrameworkCore;
using Swan.Domain.Identity;
using Swan.Infrastructure.Persistence;
using Swan.Infrastructure.Security;

namespace Swan.Infrastructure.Auth;

public sealed class UserAdmin(SwanDbContext db, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public const int MinPasswordLength = 12;

    /// <summary>
    /// Creates an active local account with one or more named roles. Fails on duplicate email or unknown role.
    /// <paramref name="actorUserId"/> and <paramref name="ipAddress"/> are recorded on the audit entry when the
    /// account is created from the app; the command line leaves both null.
    /// </summary>
    public async Task<User> CreateUserAsync(string email, string displayName, string password, IEnumerable<string> roleNames,
                                            Guid? actorUserId = null, string? ipAddress = null, CancellationToken ct = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        if (!normalized.Contains('@')) throw new ArgumentException("Email address is not valid.");
        if (password.Length < MinPasswordLength)
            throw new ArgumentException($"Password must be at least {MinPasswordLength} characters.");
        if (await db.Users.AnyAsync(u => u.Email == normalized, ct))
            throw new InvalidOperationException($"An account for '{normalized}' already exists.");

        var roles = await db.Roles.Where(r => roleNames.Contains(r.Name)).ToListAsync(ct);
        var missing = roleNames.Except(roles.Select(r => r.Name)).ToList();
        if (missing.Count > 0)
            throw new ArgumentException($"Unknown role(s): {string.Join(", ", missing)}.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = normalized,
            DisplayName = displayName.Trim(),
            PasswordHash = PasswordHasher.Hash(password),
            IsActive = true,
            CreatedUtc = _clock.GetUtcNow().UtcDateTime,
        };
        db.Users.Add(user);
        db.UserRoles.AddRange(roles.Select(r => new UserRole { UserId = user.Id, RoleId = r.Id }));
        db.AuditLogs.Add(new AuditLog
        {
            UserId = actorUserId,
            Action = "user.created",
            EntityType = "User",
            EntityId = user.Id.ToString(),
            Details = string.Join(",", roles.Select(r => r.Name)),
            IpAddress = ipAddress,
            CreatedUtc = user.CreatedUtc,
        });
        await db.SaveChangesAsync(ct);
        return user;
    }

    /// <summary>
    /// Every account with its role names, for the Team screen. Never returns a password hash, a lockout
    /// counter or a refresh token — only what the screen shows.
    /// </summary>
    public async Task<IReadOnlyList<(User User, IReadOnlyList<string> Roles)>> ListAsync(CancellationToken ct = default)
    {
        var users = await db.Users.AsNoTracking().OrderBy(u => u.DisplayName).ToListAsync(ct);
        var links = await db.UserRoles.AsNoTracking().ToListAsync(ct);
        var roleNames = await db.Roles.AsNoTracking().ToDictionaryAsync(r => r.Id, r => r.Name, ct);

        return users.Select(u => (
            u,
            (IReadOnlyList<string>)links
                .Where(l => l.UserId == u.Id)
                .Select(l => roleNames.GetValueOrDefault(l.RoleId))
                .Where(n => n is not null)
                .Select(n => n!)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList()
        )).ToList();
    }
}
